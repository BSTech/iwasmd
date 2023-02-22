/*
    iwasmd - Interactive WebAssembly Decompiler
    Copyright (C) 2022  bstech_
    
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
    
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace iwasmd
{
	/// <summary>
	/// A disassembler for WebAssembly binary.
	/// </summary>
	public class Disassembler
	{
		// note to myself: block_type is varint7, which holds a ValueType to indicate a signature with a single result (ValueType::EmptyBlock indicates a signature with 0 results)

		readonly WasmBinaryFile file;
		readonly List<WasmDefinitions.Function> functions;
		readonly List<WasmDefinitions.Global> globals;
		readonly List<uint> table_elements;
		readonly List<WasmDefinitions.Type> types;

		bool m_IsDisassembled = false;
		int m_RealFunctionOffset;
		MemoryStream dataStream;

		/************************************************************************************************************************************/
		// lets expose some private members as separate properties but keep using the original private members
		// this behavior is intended because we want noone to interfere with any of them, so we will not only
		// make them read only, but will also make the lists read only.

		/// <summary>
		/// The binary file this disassembler is working on.
		/// </summary>
		public WasmBinaryFile InnerFile => file;

		/// <summary>
		/// The list of disassembled functions retrieved from the internal list. See <seealso cref="Disassemble"/> for more information.
		/// </summary>
		public ReadOnlyList<WasmDefinitions.Function> Functions => functions;

		/// <summary>
		/// The list of global members retrieved from the internal list. See <seealso cref="Disassemble"/> for more information.
		/// </summary>
		public ReadOnlyList<WasmDefinitions.Global> Globals => globals;

		/// <summary>
		/// The list of elements in the main table retrieved from the internal list. See <seealso cref="Disassemble"/> for more information.
		/// </summary>
		public ReadOnlyList<uint> TableElements => table_elements;

		/// <summary>
		/// The list of function types retrieved from the internal list. See <seealso cref="Disassemble"/> for more information.
		/// </summary>
		public ReadOnlyList<WasmDefinitions.Type> Types => types;

		/************************************************************************************************************************************/

		/// <summary>
		/// Gets the standard name of a value type.
		/// </summary>
		/// <param name="type">The value type to be named.</param>
		/// <returns>The standard name of the value type.</returns>
		/// <exception cref="Exception"></exception>
		public static string GetTypeStr(ValueType type)
		{
			switch (type)
			{
				case ValueType.F64: return "double";
				case ValueType.F32: return "float";
				case ValueType.I64: return "long long";
				case ValueType.I32: return "int";
				case ValueType.Void: return "void";
				default: throw new Exception($"\"{type}\" cannot be a valid value type here");
			}
		}

		/// <summary>
		/// Gets the equivalent language type of a value type.
		/// </summary>
		/// <param name="type">The value type to be named.</param>
		/// <returns>The standard name of the value type.</returns>
		/// <exception cref="Exception"></exception>
		public static Type GetTypeEquivalent(ValueType type)
		{
			switch (type)
			{
				case ValueType.F64: return typeof(double);
				case ValueType.F32: return typeof(float);
				case ValueType.I64: return typeof(long);
				case ValueType.I32: return typeof(int);
				default: throw new Exception($"\"{type}\" cannot be a valid value type here");
			}
		}

		/// <summary>
		/// Initializes the disassembler based on a binary file and prepares some information immediately.
		/// </summary>
		/// <param name="file">The binary file that is going to be worked on.</param>
		/// <exception cref="Exception">if there goes something wrong during preparation.</exception>
		public Disassembler(WasmBinaryFile file)
		{
			this.file = file;
			functions = new List<WasmDefinitions.Function>();
			globals = new List<WasmDefinitions.Global>();

			m_RealFunctionOffset = _ProcessImportedFunctions();
			
			uint counter = 0;
			foreach (WasmSections.Global g in file.Globals)
			{
				// quick value decoding since global initializers are always a single xxx.const instruction
				// to skip the opcode, skip the first byte of the initializer
				// and for the same purpose set "dummy" to 1 for ReadULEB128* functions
				long dummy = 1;

				string val;
				switch (g.type.content_type)
				{
					case ValueType.F64: val = BitConverter.ToInt64(g.init, 1).ToString();  break;
					case ValueType.F32: val = BitConverter.ToInt32(g.init, 1).ToString();  break;
					case ValueType.I64: val = g.init.ReadULEB128_64(ref dummy).ToString(); break;
					case ValueType.I32: val = g.init.ReadULEB128(ref dummy).ToString();    break;
					default: throw new Exception($"\"{g.type.content_type}\" cannot be a valid value type here");
				}

				globals.Add(new WasmDefinitions.Global($"global_{counter++}", val, GetTypeStr(g.type.content_type), !g.type.mutability));
			}

			// support only one table
			WasmSections.ElementSegment elements = file.Elements.First();
			table_elements = new List<uint>(elements.elems);

			types = file.Types.Select(x => new WasmDefinitions.Type(x.hasReturn, x.rType, x.pTypes)).ToList();
		}

		int _ProcessImportedFunctions()
		{
			uint counter = 0;
			foreach (WasmSections.Import import in file.Imports)
				if (import.kind == ExternalKind.Function)
				{
					string fnName = $"$imp_{import.module}.{import.field}";
					//fnNames.Add(fnName);
					WasmSections.FuncType type = file.Types[(int)import.type_Func];
					List<WasmDefinitions.Function.Local> locals = new List<WasmDefinitions.Function.Local>();

					for (uint i = 0; i < type.pCount; i++)
						locals.Add(new WasmDefinitions.Function.Local($"par{i}", type.pTypes[i], true));

					functions.Add(new WasmDefinitions.Function(fnName, type.hasReturn ? type.rType : ValueType.Void, counter++, locals, null, null));
				}
			return (int)counter;
		}

		/// <summary>
		/// Disassembles all functions by calling <see cref="Disassemble(uint)"/> and stores them in an internal list.
		/// This function ensures everything is disassembled, which is required by the <see cref="Decompile(IDecompiler)" /> function to work.
		/// </summary>
		public void Disassemble()
		{
			// clear the function list if it contains anything old and add imports again
			functions.Clear();
			m_RealFunctionOffset = _ProcessImportedFunctions();

			for (uint n = 0; n < file.Functions.Count; n++)
				functions.Add(Disassemble((uint)(n + file.Imports.Count)));

			// add the fake global initializer at the end
			//if (globals.Count > 0)
				//functions.Add(new WasmDefinitions.Function(null, null, ~0U, null, null, true));

			m_IsDisassembled = true;
		}

		/// <summary>
		/// Disassembles a single function specified by its index. This function never ensures a complete disassembly, see <seealso cref="Disassemble"/> and <seealso cref="Decompile(IDecompiler)"/> for more information.
		/// </summary>
		/// <param name="fnIndex">The index of the function.</param>
		/// <returns>A <see cref="WasmDefinitions.Function">Function</see> instance which holds more information about the function.</returns>
		public WasmDefinitions.Function Disassemble(uint fnIndex)
		{
			string fnName = $"fun_{fnIndex:X8}";
			fnIndex -= (uint)file.Imports.Count;

			int ftypeIndex = (int)file.Functions[(int)fnIndex];
			WasmSections.Function function = file.Codes[(int)fnIndex];
			WasmSections.FuncType type = file.Types[ftypeIndex];
			
			List<WasmDefinitions.Function.Local> locals = new List<WasmDefinitions.Function.Local>();

			for (uint n = 0; n < type.pCount; n++)
				locals.Add(new WasmDefinitions.Function.Local($"par{n}", type.pTypes[n], true));

			int counter = 0;
			foreach (WasmSections.LocalEntry entry in function.locals)
				for (uint n = 0; n < entry.count; n++)
					locals.Add(new WasmDefinitions.Function.Local($"local{counter++}", entry.type, false));

			string expName = null;
			if (file.Exports.TryFind(x => x.kind == ExternalKind.Function && x.index == fnIndex, out WasmSections.Export export))
				expName = export.field;

			return new WasmDefinitions.Function(fnName, type.hasReturn ? type.rType : ValueType.Void, (uint)(fnIndex + file.Imports.Count), locals, function.code, expName);
		}

		/// <summary>
		/// Decompiles all disassembled functions stored in an internal list. <see cref="Disassemble"/> is called automatically if no disassembly has been done before.
		/// </summary>
		/// <param name="decompiler">The decompiler to be utilized. An example decompiler is the legacy <see cref="AsmDecompiler"/>.</param>
		public void Decompile(IDecompiler decompiler)
		{
			if (!m_IsDisassembled)
				Disassemble();
				//throw new InvalidOperationException("Disassembly has not been done, call Disassemble() first. Note that partial disassembly is not counted.");
			
			List<AddressRange> dataRanges = new List<AddressRange>();
			dataStream = CreateDataStream(dataRanges);

			decompiler.Prepare(this, dataRanges, m_RealFunctionOffset);
			
			// iterate through all of the functions and skip our fake global initializer
			foreach (WasmDefinitions.Function function in functions)
				if (!function.IsGlobal)
					decompiler.Decompile(this, function);

			dataStream.Dispose();
		}

		/// <summary>
		/// Dumps the data section to a file. See also <seealso cref="CreateDataStream(List{AddressRange})"/>.
		/// </summary>
		/// <param name="path"></param>
		public void DumpEntireData(string path)
		{
			using (FileStream fs = File.OpenWrite(path))
				using (MemoryStream ms = CreateDataStream())
					ms.CopyTo(fs);
		}

		/// <summary>
		/// Creates a continuous stream of the data contained in the binary file with required adjustments.
		/// </summary>
		/// <param name="ranges">An optional list of <see cref="AddressRange"/>s to hold the actual start and end addresses of chunks.</param>
		/// <returns>A <see cref="MemoryStream"/> that holds the data.</returns>
		public MemoryStream CreateDataStream(List<AddressRange> ranges = null)
		{
			MemoryStream ms = new MemoryStream();
			
			foreach (WasmSections.Data d in file.Data)
			{
				// see global parser in the constructor for more info
				long leb_offset = 1;
				uint offset = d.offset.ReadULEB128(ref leb_offset);

				ms.Seek(offset, SeekOrigin.Begin);
				ms.Write(d.data, 0, d.data.Length);

				ranges?.Add(new AddressRange(offset, offset + (uint)d.data.Length));
			}
			
			// reset stream position to make the stream ready to use
			ms.Position = 0;
			return ms;
		}

		/// <summary>
		/// Reads the data with an offset and tries to interpret data type. Everything that is not a readable string is
		/// interpreted as a 32-bit integer.<br /><b>Note:</b> This function can only be used during decompilation (hence by a decompiler).
		/// </summary>
		/// <param name="offset">The offset of the data.</param>
		/// <returns>A readable string of an integer or a C-style ANSI/wide-char string.</returns>
		public string MakeReadableDataInfo(uint offset)
		{
			if (dataStream == null)
				throw new InvalidOperationException("This function cannot be used outside the decompilation state.");
			
			string result = string.Empty;
			bool validString = true;

			// hold original stream position and go to the requested offset
			long orig = dataStream.Position;
			dataStream.Position = offset;

			// read the stream byte-by-byte until it is NULL or EOF to test if there is a C-style null terminated string
			// support only ANSI characters
			for (int b = dataStream.ReadByte(); b > 0; b = dataStream.ReadByte())
			{
				// check if the char is printable
				if (b < 32 || b > 126)
				{
					// if it is not readable, mark it as a binary integer for further processing and exit from the loop
					validString = false;
					break;
				}
				
				// if it is printable, add it at the end of string
				result += (char)b;
			}

			// couldn't find an ANSI string, now try for wide-char string
			if (!validString)
			{
				// go back where were we at the beginning
				dataStream.Position = offset;
				validString = true;

				// support only wide-char characters (inlined reading of a 16-bit integer is used)
				for (int b = (dataStream.ReadByte() << 8) | dataStream.ReadByte(); b > 0; b = (dataStream.ReadByte() << 8) | dataStream.ReadByte())
				{
					// check if the char is printable
					if (b < 32 || b > 126)
					{
						// if it is not readable, mark it as a binary integer for further processing and exit from the loop
						validString = false;
						break;
					}

					// if it is printable, add it at the end of string
					result += (char)b;
				}
			}

			// if it is still not a string, go back to requested position and interpret
			// the data as an integer, print it in hex format and leave it as it is
			if (!validString)
			{
				dataStream.Position = orig;
				byte[] int32buf = new byte[sizeof(int)];
				dataStream.Read(int32buf, 0, sizeof(int));
				result = BitConverter.ToInt32(int32buf, 0).ToString("X") + 'h'; // ...h
			}

			// go back to original position
			dataStream.Position = orig;
			return result;
		}

		/// <summary>
		/// Not implemented yet. Do not use.
		/// </summary>
		/// <param name="function"></param>
		/// <param name="index"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public TwoWayCrossReference FindRefsFromCallTypeIndex(WasmDefinitions.Function function, uint index, uint offset)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Finds a cross reference linked with a function based on the target index and source offset.
		/// </summary>
		/// <param name="function">The function to be searched.</param>
		/// <param name="index">The target index. This should be the index operand of <c>call</c> instruction.</param>
		/// <param name="offset">The source offset. This should be the offset of <c>call</c> instruction.</param>
		/// <returns>A two way cross reference information. <see cref="TwoWayCrossReference.From">From</see> contains the references
		/// the source function has and <see cref="TwoWayCrossReference.To">To</see> contains the ones the target function has.</returns>
		public TwoWayCrossReference FindRefsFromCallFnIndex(WasmDefinitions.Function function, uint index, uint offset)
		{
			WasmDefinitions.Function fn = functions[(int)index];

			return new TwoWayCrossReference(new CrossReference(index > function.Index, true, 0, fn), new CrossReference(function.Index > index, false, offset, function));

			//fn.XRefs?.Add(xref_to);

			//function.XRefs.Add(xref_from);
		}
	}

	public enum Instructions
	{
		trap, // or unreachable
		nop,
		block,
		loop,
		@if,
		@else,
		end = 0xB,
		br,
		br_if,
		br_table,
		@return,

		call,
		call_indirect,

		drop = 0x1A,
		select,

		getlocal = 0x20,
		setlocal,
		teelocal,
		getglobal,
		setglobal,

		i32_load = 0x28,
		i64_load,
		f32_load,
		f64_load,
		i32_load8_s,
		i32_load8_u,
		i32_load16_s,
		i32_load16_u,
		i64_load8_s,
		i64_load8_u,
		i64_load16_s,
		i64_load16_u,
		i64_load32_s,
		i64_load32_u,
		i32_store,
		i64_store,
		f32_store,
		f64_store,
		i32_store8,
		i32_store16,
		i64_store8,
		i64_store16,
		i64_store32,
		current_memory,
		grow_memory,

		i32_const,
		i64_const,
		f32_const,
		f64_const,

		i32_eqz,
		i32_eq,
		i32_ne,
		i32_lt_s,
		i32_lt_u,
		i32_gt_s,
		i32_gt_u,
		i32_le_s,
		i32_le_u,
		i32_ge_s,
		i32_ge_u,
		i64_eqz,
		i64_eq,
		i64_ne,
		i64_lt_s,
		i64_lt_u,
		i64_gt_s,
		i64_gt_u,
		i64_le_s,
		i64_le_u,
		i64_ge_s,
		i64_ge_u,
		f32_eq,
		f32_ne,
		f32_lt,
		f32_gt,
		f32_le,
		f32_ge,
		f64_eq,
		f64_ne,
		f64_lt,
		f64_gt,
		f64_le,
		f64_ge,

		i32_clz,
		i32_ctz,
		i32_popcnt,
		i32_add,
		i32_sub,
		i32_mul,
		i32_div_s,
		i32_div_u,
		i32_rem_s,
		i32_rem_u,
		i32_and,
		i32_or,
		i32_xor,
		i32_shl,
		i32_shr_s,
		i32_shr_u,
		i32_rotl,
		i32_rotr,
		i64_clz,
		i64_ctz,
		i64_popcnt,
		i64_add,
		i64_sub,
		i64_mul,
		i64_div_s,
		i64_div_u,
		i64_rem_s,
		i64_rem_u,
		i64_and,
		i64_or,
		i64_xor,
		i64_shl,
		i64_shr_s,
		i64_shr_u,
		i64_rotl,
		i64_rotr,

		f32_abs,
		f32_neg,
		f32_ceil,
		f32_floor,
		f32_trunc,
		f32_nearest,
		f32_sqrt,
		f32_add,
		f32_sub,
		f32_mul,
		f32_div,
		f32_min,
		f32_max,
		f32_copysign,
		f64_abs,
		f64_neg,
		f64_ceil,
		f64_floor,
		f64_trunc,
		f64_nearest,
		f64_sqrt,
		f64_add,
		f64_sub,
		f64_mul,
		f64_div,
		f64_min,
		f64_max,
		f64_copysign,

		i32_wrap_i64,
		i32_trunc_s_f32,
		i32_trunc_u_f32,
		i32_trunc_s_f64,
		i32_trunc_u_f64,
		i64_extends_s_i32,
		i64_extends_u_i32,
		i64_trunc_s_f32,
		i64_trunc_u_f32,
		i64_trunc_s_f64,
		i64_trunc_u_f64,
		f32_convert_s_i32,
		f32_convert_u_i32,
		f32_convert_s_i64,
		f32_convert_u_i64,
		f32_demote_f64,
		f64_convert_s_i32,
		f64_convert_u_i32,
		f64_convert_s_i64,
		f64_convert_u_i64,
		f64_demote_f32,

		i32_reinterpret_f32,
		i64_reinterpret_f64,
		f32_reinterpret_i32,
		f64_reinterpret_i64,

		ixx_trunc_sat_fyy_p = 0xfc
	}

	public readonly struct AddressRange
	{
		public uint Start { get; }
		public uint End { get; }

		public AddressRange(uint start, uint end)
		{
			Start = start;
			End = end;
		}
	}

	public readonly struct CrossReference
	{
		// true if the reference direction points down and false if it points up
		public bool DirectionDown { get; }
		// true if the reference points to a destination and false if it points to a source
		public bool Destination { get; }
		public uint Offset { get; }
		public WasmDefinitions.Function What { get; }

		public CrossReference(bool down, bool dest, uint offset, WasmDefinitions.Function refr)
		{
			DirectionDown = down;
			Destination = dest;
			Offset = offset;
			What = refr;
		}

		public override string ToString() => $"{(Destination ? 'd' : 's')}{(DirectionDown ? '\u2193' : '\u2191')}{What.Name}+{Offset:X8}h";
	}

	public readonly struct TwoWayCrossReference
	{
		public CrossReference From { get; }
		public CrossReference To { get; }

		public TwoWayCrossReference(CrossReference from, CrossReference to)
		{
			From = from;
			To = to;
		}
	}
}
