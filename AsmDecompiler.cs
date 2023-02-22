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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iwasmd
{
	public class AsmDecompiler : IDecompiler
	{
		StringBuilder m_Output = new StringBuilder();
		StringBuilder m_Builder = new StringBuilder();

		List<AddressRange> m_DataRanges;
		ulong m_progCnt = 0;

		public void Prepare(Disassembler disassembler, List<AddressRange> dataRanges, int realFunctionOffset)
		{
			m_DataRanges = dataRanges;
			m_Output.AppendLine(string.Join(Environment.NewLine, disassembler.Globals.Select(g => $"static {g.Name} = {g.Value};")));

			foreach (WasmSections.Export export in disassembler.InnerFile.Exports)
			{
				switch (export.kind)
				{
					case ExternalKind.Function: continue;
					case ExternalKind.Table:
						{
							WasmSections.Table table = disassembler.InnerFile.Tables[(int)export.index];
							m_Output.AppendLine($"export table {export.field} = {{.type = {table.element_type}, .initial = {table.limits.initial}{(table.limits.flags ? $", .maximum = {table.limits.maximum}" : string.Empty)}}}");
							break;
						}
					case ExternalKind.Memory:
						{
							ResizableLimits limits = disassembler.InnerFile.Memories[(int)export.index];
							m_Output.AppendLine($"export memory {export.field} = {{.initial = {limits.initial}{(limits.flags ? $", .maximum = {limits.maximum}" : string.Empty)}}}");
							break;
						}
					case ExternalKind.Global:
						{
							WasmDefinitions.Global g = disassembler.Globals[(int)export.index];
							m_Output.AppendLine($"export global {(g.IsConst ? "const " : string.Empty)}{g.Type} {g.Name}");
							break;
						}
					default: throw new Exception($"Invalid export kind \"{export.kind}\"");
				}
			}

		}

		// TODO use ByteStreamBinaryReader in the future
		public void Decompile(Disassembler disassembler, WasmDefinitions.Function function)
		{
			long pos = 0;
			byte[] code = function.Bytecode;

			if (code != null)
				m_Builder.Append($"{m_progCnt:X8}\t");

			m_Builder.Append(function);

			// lets check if the function is exported
			//if (disassembler.InnerFile.Exports.TryFind(x => x.kind == ExternalKind.Function && x.index == function.Index, out WasmSections.Export export))
			if (function.ExportedName != null)
				m_Builder.Append($" export {function.ExportedName}");

			if (code == null)
			{
				m_Builder.Append(';').ToString();
				return;
			}

			m_Builder.AppendLine().AppendLine("{");

			foreach (WasmDefinitions.Function.Local local in function.Locals)
				if (!local.IsParameter)
					m_Builder.AppendLine($"\t\t{local};");

			if (function.Locals.Count > 0)
				m_Builder.AppendLine();

			while (pos < code.Length)
			{
				Instructions i = (Instructions)code[pos++];
				
				if (!Enum.IsDefined(typeof(Instructions), i))
					throw new Exception($"Encountered invalid opcode \"{i}\" during decompilation");

				if (i == Instructions.end && pos == code.Length) break;
				else if (i == Instructions.ixx_trunc_sat_fyy_p)
				{
					byte inner = code[pos++];
					switch (inner)
					{
						case 0: m_Builder.Append("\ti32_trunc_sat_f32_s "); break;
						case 1: m_Builder.Append("\ti32_trunc_sat_f32_u "); break;
						case 2: m_Builder.Append("\ti32_trunc_sat_f64_s "); break;
						case 3: m_Builder.Append("\ti32_trunc_sat_f64_u "); break;
						case 4: m_Builder.Append("\ti64_trunc_sat_f32_s "); break;
						case 5: m_Builder.Append("\ti64_trunc_sat_f32_u "); break;
						case 6: m_Builder.Append("\ti64_trunc_sat_f64_s "); break;
						case 7: m_Builder.Append("\ti64_trunc_sat_f64_u "); break;

						default:
							throw new Exception($"Encountered invalid opcode \"0xFC{inner:X2}\" during decompilation");
					}
				}
				else if (i == Instructions.block) m_Builder.Append($"\t\tlabel_{pos - 1:X8}: ");
				else m_Builder.Append($"{m_progCnt + (ulong)pos:X8}\t{i} ");

				switch (i)
				{
					case Instructions.block:
					case Instructions.loop:
					case Instructions.@if:
						{
							ValueType type = (ValueType)code[pos++];
							m_Builder.AppendLine((type != ValueType.EmptyBlock) ? $"type={type}" : string.Empty);
							break;
						}
					case Instructions.br:
					case Instructions.br_if:
						m_Builder.AppendLine($"{code.ReadULEB128(ref pos):X8}h");
						break;
					case Instructions.br_table:
						{
							uint tcnt = code.ReadULEB128(ref pos);
							m_Builder.Append($"{tcnt} ");

							if (tcnt > 0)
							{
								m_Builder.Append($"{{ {code.ReadULEB128(ref pos):X8}h");

								for (uint n = 1; n < tcnt; n++)
									m_Builder.Append($", {code.ReadULEB128(ref pos):X8}h");

								m_Builder.Append(" }");
							}

							m_Builder.AppendLine($" -> {code.ReadULEB128(ref pos):X8}h");
							break;
						}
					case Instructions.call:
						{
							uint offset = (uint)pos;
							uint index = code.ReadULEB128(ref pos);
							disassembler.FindRefsFromCallFnIndex(function, index, offset);
							m_Builder.AppendLine(disassembler.Functions[(int)index].Name);
							break;
						}
					case Instructions.call_indirect:
						{
							uint offset = (uint)pos;
							uint index = code.ReadULEB128(ref pos);
							//disassembler.FindRefsFromCallTypeIndex(function, index, offset);
							m_Builder.AppendLine($"{disassembler.Types[(int)index]} (type index: {index})");
							pos++; // skip reserved varint1
							break;
						}
					case Instructions.getlocal:
					case Instructions.setlocal:
					case Instructions.teelocal:
						m_Builder.AppendLine(function.Locals[(int)code.ReadULEB128(ref pos)].Name);
						break;
					case Instructions.getglobal:
					case Instructions.setglobal:
						m_Builder.AppendLine(disassembler.Globals[(int)code.ReadULEB128(ref pos)].Name);
						break;
					case Instructions.i32_load:
					case Instructions.i64_load:
					case Instructions.f32_load:
					case Instructions.f64_load:
					case Instructions.i32_load8_s:
					case Instructions.i32_load8_u:
					case Instructions.i32_load16_s:
					case Instructions.i32_load16_u:
					case Instructions.i64_load8_s:
					case Instructions.i64_load8_u:
					case Instructions.i64_load16_s:
					case Instructions.i64_load16_u:
					case Instructions.i64_load32_s:
					case Instructions.i64_load32_u:
					case Instructions.i32_store:
					case Instructions.i64_store:
					case Instructions.f32_store:
					case Instructions.f64_store:
					case Instructions.i32_store8:
					case Instructions.i32_store16:
					case Instructions.i64_store8:
					case Instructions.i64_store16:
					case Instructions.i64_store32:
						{
							uint flags = code.ReadULEB128(ref pos);
							uint offset = code.ReadULEB128(ref pos);
							//uint align = 0;

							// crude log2 implementation
							/*
							while (flags > 1)
							{
								flags >>= 1;
								align++;
							}
							*/
							// crude log2 implementation was too crude, delay it for test purposes now
							//align = flags;

							string text = m_DataRanges.Any(x => x.Start <= offset && offset <= x.End) ? $"\"{disassembler.MakeReadableDataInfo(offset)}\"" : string.Empty;

							m_Builder.AppendLine($"{offset:X8}h{(flags != 0 ? $" [align={flags}]" : " ")}{text}");
							break;
						}
					case Instructions.current_memory:
					case Instructions.grow_memory:
						pos++; // skip reserved varint1
						break;
					case Instructions.i32_const:
						m_Builder.AppendLine(code.ReadULEB128(ref pos).ToString());
						break;
					case Instructions.i64_const:
						m_Builder.AppendLine(code.ReadULEB128_64(ref pos).ToString());
						break;
					case Instructions.f32_const:
						m_Builder.AppendLine(BitConverter.ToInt32(code, (int)pos).ToString());
						pos += 4;
						break;
					case Instructions.f64_const:
						m_Builder.AppendLine(BitConverter.ToInt64(code, (int)pos).ToString());
						pos += 8;
						break;
					default:
						m_Builder.AppendLine();
						break;
				}
			}
			m_progCnt += (ulong)code.Length;
			m_Builder.AppendLine("}");
		}
	
		public string BuildOutput()
		{
			string result = m_Output.ToString() + m_Builder.ToString();
			
			m_Output.Clear();
			m_Builder.Clear();

			return result;
		}
	}
}
