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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iwasmd
{
	public class WasmBinaryFile : IDisposable
	{
		readonly BinaryReader reader;
		readonly List<WasmSections.FuncType> types;
		readonly List<WasmSections.Import> imports;
		readonly List<uint> functions;
		readonly List<WasmSections.Table> tables;
		readonly List<ResizableLimits> memories;
		readonly List<WasmSections.Global> globals;
		readonly List<WasmSections.Export> exports;
		readonly List<WasmSections.ElementSegment> elements;
		readonly List<WasmSections.Function> codes;
		readonly List<WasmSections.Data> data;

		uint m_Start;

		internal List<WasmSections.FuncType> Types => types;
		internal List<WasmSections.Import> Imports => imports;
		internal List<uint> Functions => functions;
		internal List<WasmSections.Table> Tables => tables;
		internal List<ResizableLimits> Memories => memories;
		internal List<WasmSections.Global> Globals => globals;
		internal List<WasmSections.Export> Exports => exports;
		internal List<WasmSections.ElementSegment> Elements => elements;
		internal List<WasmSections.Function> Codes => codes;
		internal List<WasmSections.Data> Data => data;

		public WasmBinaryFile(string path)
		{
			reader = new BinaryReader(File.OpenRead(path));
			types = new List<WasmSections.FuncType>();
			imports = new List<WasmSections.Import>();
			functions = new List<uint>();
			tables = new List<WasmSections.Table>();
			memories = new List<ResizableLimits>();
			globals = new List<WasmSections.Global>();
			exports = new List<WasmSections.Export>();
			elements = new List<WasmSections.ElementSegment>();
			codes = new List<WasmSections.Function>();
			data = new List<WasmSections.Data>();
		}

		public void Disassemble()
		{
			using (reader)
			{
				if (reader.ReadUInt32() != 0x6D736100 || reader.ReadUInt32() != 1)
					throw new Exception("Invalid WASM binary file given");

				while (true)
				{
					WasmSectionId id = (WasmSectionId)reader.ReadULEB128();
					uint length = reader.ReadULEB128();

					// "count" will be used as "index" if the current section is "Start" below
					uint count = reader.ReadULEB128();

					switch (id)
					{
						case WasmSectionId.Custom:
							break;
						case WasmSectionId.Type:
							while (count > 0)
							{
								WasmSections.FuncType ft = new WasmSections.FuncType();

								ft.form = (byte)reader.ReadULEB128();
								ft.pCount = reader.ReadULEB128();
								ft.pTypes = new ValueType[ft.pCount];

								for (uint n = 0; n < ft.pCount; n++)
									ft.pTypes[n] = (ValueType)reader.ReadULEB128();

								if (ft.hasReturn = reader.ReadULEB128() == 1)
									ft.rType = (ValueType)reader.ReadULEB128();

								types.Add(ft);
								count--;
							}

							break;
						case WasmSectionId.Import:
							while (count > 0)
							{
								WasmSections.Import imp = new WasmSections.Import();

								imp.mod_Len = reader.ReadULEB128();
								imp.module = new string(reader.ReadChars((int)imp.mod_Len));

								imp.fld_Len = reader.ReadULEB128();
								imp.field = new string(reader.ReadChars((int)imp.fld_Len));

								imp.kind = (ExternalKind)reader.ReadULEB128();

								switch (imp.kind)
								{
									case ExternalKind.Function:
										imp.type_Func = reader.ReadULEB128();
										break;
									case ExternalKind.Table:
										imp.type_Table = _ReadTable();
										break;
									case ExternalKind.Memory:
										imp.type_Memory = _ReadRL();
										break;
									case ExternalKind.Global:
										imp.type_Global = _ReadGlobalType();
										break;
									default:
										throw new Exception($"Invalid import kind \"{imp.kind}\"");
								}

								imports.Add(imp);
								count--;
							}
							break;
						case WasmSectionId.Function:
							while (count > 0)
							{
								functions.Add(reader.ReadULEB128());
								count--;
							}
							break;
						case WasmSectionId.Table:
							while (count > 0)
							{
								tables.Add(_ReadTable());
								count--;
							}
							break;
						case WasmSectionId.Memory:
							while (count > 0)
							{
								memories.Add(_ReadRL());
								count--;
							}
							break;
						case WasmSectionId.Global:
							while (count > 0)
							{
								globals.Add(_ReadGlobal());
								count--;
							}
							break;
						case WasmSectionId.Export:
							while (count > 0)
							{
								WasmSections.Export exp = new WasmSections.Export();

								exp.fld_Len = reader.ReadULEB128();
								exp.field = new string(reader.ReadChars((int)exp.fld_Len));

								exp.kind = (ExternalKind)reader.ReadULEB128();
								exp.index = reader.ReadULEB128();

								exports.Add(exp);
								count--;
							}
							break;
						case WasmSectionId.Start:
							m_Start = count;
							break;
						case WasmSectionId.Element:
							while (count > 0)
							{
								WasmSections.ElementSegment es = new WasmSections.ElementSegment();

								es.index = reader.ReadULEB128();
								es.offset = _ReadInitExpr();
								es.num_elem = reader.ReadULEB128();
								es.elems = new uint[es.num_elem];

								for (uint n = 0; n < es.num_elem; n++)
									es.elems[n] = reader.ReadULEB128();

								elements.Add(es);
								count--;
							}
							break;
						case WasmSectionId.Code:
							while (count > 0)
							{
								WasmSections.Function fn = new WasmSections.Function();

								fn.bodySize = reader.ReadULEB128();

								long pos = reader.BaseStream.Position;

								fn.localCount = reader.ReadULEB128();
								fn.locals = new WasmSections.LocalEntry[fn.localCount];

								for (uint n = 0; n < fn.localCount; n++)
									fn.locals[n] = _ReadLocalEntry();

								int actualBodySize = (int)(fn.bodySize - (reader.BaseStream.Position - pos));

								fn.code = reader.ReadBytes(actualBodySize);

								codes.Add(fn);
								count--;
							}
							break;
						case WasmSectionId.Data:
							while (count > 0)
							{
								WasmSections.Data datum = new WasmSections.Data();

								datum.index = reader.ReadULEB128();
								datum.offset = _ReadInitExpr();
								datum.size = reader.ReadULEB128();
								datum.data = reader.ReadBytes((int)datum.size);

								data.Add(datum);
								count--;
							}
							break;
						default:
							throw new Exception($"Invalid section id \"{id}\"");
					}

					if (reader.BaseStream.Position == reader.BaseStream.Length) break;
				}
			}

			//Disassembler disassembler = new Disassembler(this);
			/*
			List<WasmDefinitions.Function> funs = new List<WasmDefinitions.Function>();
			for (uint i = 0; i < functions.Count; i++)
				funs.Add(disassembler.Disassemble(i));
			
			var fun = funs.First();
			string code = disassembler.DecompileToAssembly(fun);
			*/

			//disassembler.Disassemble();
			//var all = disassembler.DecompileToAssembly().ToList();
		}
	
		private WasmSections.Table _ReadTable()
		{
			WasmSections.Table table = new WasmSections.Table();

			table.element_type = (byte)reader.ReadULEB128();
			table.limits = _ReadRL();

			return table;
		}

		private ResizableLimits _ReadRL()
		{
			ResizableLimits rl = new ResizableLimits();
			rl.flags = reader.ReadULEB128() == 1;
			rl.initial = reader.ReadULEB128();

			if (rl.flags)
				rl.maximum = reader.ReadULEB128();
			return rl;
		}

		private WasmSections.Global _ReadGlobal()
		{
			WasmSections.Global g = new WasmSections.Global();
			g.type = _ReadGlobalType();
			g.init = _ReadInitExpr();

			return g;
		}

		private WasmSections.GlobalType _ReadGlobalType()
		{
			WasmSections.GlobalType gt = new WasmSections.GlobalType();
			gt.content_type = (ValueType)reader.ReadULEB128();
			gt.mutability = reader.ReadULEB128() == 1;
			return gt;
		}

		private WasmSections.LocalEntry _ReadLocalEntry()
		{
			WasmSections.LocalEntry le = new WasmSections.LocalEntry();
			le.count = reader.ReadULEB128();
			le.type = (ValueType)reader.ReadULEB128();
			return le;
		}

		private byte[] _ReadInitExpr()
		{
			List<byte> bytes = new List<byte>();

			while (true)
			{
				byte b = reader.ReadByte();
				bytes.Add(b);
				
				if (b == 0xB) break;
			}
			
			return bytes.ToArray();
		}

		public void Dispose()
		{
			reader.Dispose();
			types.Clear();
			imports.Clear();
			functions.Clear();
			tables.Clear();
			memories.Clear();
			globals.Clear();
			exports.Clear();
			elements.Clear();
			codes.Clear();
			data.Clear();
		}
	}
}
