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
	public class JSDecompiler : IDecompiler
	{
		readonly StringBuilder m_Output = new StringBuilder();
		int m_Rfn_offset = 0;

		public void Prepare(Disassembler disassembler, List<AddressRange> dataRanges, int realFunctionOffset)
		{
			foreach (WasmDefinitions.Global g in disassembler.Globals)
				m_Output.AppendLine($"{(g.IsConst ? "const" : "let")} {g.Name} = {g.Value}; /* type: {g.Type} */");
			m_Rfn_offset = realFunctionOffset;
		}

		public void Decompile(Disassembler disassembler, WasmDefinitions.Function function)
		{
			Stack<object> stack = new Stack<object>();
			BranchList branchList = new BranchList();
			int _ = 1;

			// yes, this looks ugly but i love packing things together
			m_Output.AppendLine($"function {function.Name}({string.Join(", ", function.Parameters.Select(x => $"{x.Name} /* {x.Type} */"))}){(function.ExportedName != null ? $" /* export: \"{function.ExportedName}\" */" : string.Empty)}").AppendLine("{");

			foreach (var local in function.Locals)
			{
				if (local.IsParameter) continue;
				string localInitialValue = string.Empty;

				switch (local.Type)
				{
					case ValueType.F64:
					case ValueType.F32: localInitialValue = "0.0F"; break;
					case ValueType.I64:
					case ValueType.I32: localInitialValue = "0"; break;
					/*case ValueType.AnyFunc:
					case ValueType.Func:
					case ValueType.EmptyBlock:
						break;*/
					default:
						throw new Exception($"Unexpected local type \"{local.Type}\"");
				}
				
				_AppendLine($"var {local.Name} = {localInitialValue};", _);
			}

			m_Output.AppendLine();

			if (function.Bytecode != null)
			{
				ByteStreamBinaryReader reader = new ByteStreamBinaryReader(function.Bytecode);
				BuildBody(disassembler, function, reader, stack, branchList, ref _);
			}

			m_Output.AppendLine("}");
		}

		private void BuildBody(Disassembler disassembler, WasmDefinitions.Function fn, ByteStreamBinaryReader reader, Stack<object> stack, BranchList branches, ref int counter, int depth = 0, bool inside_if = false)
		{
			while (!reader.IsEOS())
			{
				Instructions instruction = (Instructions)reader.ReadByte();
				if (instruction == Instructions.ixx_trunc_sat_fyy_p)
				{
					
					continue;
				}

				/*if ((int)instruction >= 40 && (int)instruction <= 62)
				{
					uint o = reader.ReadUVarint();
					uint a = reader.ReadUVarint();
					continue;
				}*/

				switch (instruction)
				{
					case Instructions.trap:
						break;
					case Instructions.nop:
						break;
					case Instructions.block:
						{
							ValueType type = (ValueType)reader.ReadByte();
							string branch = branches.Generate(counter, type, false);
							
							_AppendLine($"function __lambda_{branch}() {{", depth);
							BuildBody(disassembler, fn, reader, stack, branches, ref counter, depth + 1, true);
							_AppendLine("}", depth);
							
							Expression expr_end = new Expression($"__lambda_{branch}", ExprType.FunctionCall);
							
							if (type == ValueType.Void || type == ValueType.EmptyBlock)
								_AppendLine($"{expr_end};", depth);
							else
							{
								var temp_local = new WasmDefinitions.Function.Local("__dyn_local_l", type, false);
								_AppendLine($"{new Expression("=", ExprType.Binary, temp_local, expr_end)};", depth);
								stack.Push(temp_local);
								stack.Push(temp_local);
							}

							branches.Remove(branch);
							break;
						}
					case Instructions.loop:
						{
							ValueType type = (ValueType)reader.ReadByte();
							string branch = branches.Generate(counter, type, true);
							
							if (type != ValueType.EmptyBlock)
								_AppendLine($"function __lambda_{branch}() {{", ++depth);

							_AppendLine($"while (true) {{", depth);
							//branches.Add(null, ValueType.EmptyBlock, true);
							BuildBody(disassembler, fn, reader, stack, branches, ref counter, depth + 1, true);
							_AppendLine("break;", depth + 1);
							_AppendLine("}", depth);

							if (type != ValueType.EmptyBlock)
							{
								Expression expr_end = new Expression($"__lambda_{branch}", ExprType.FunctionCall);
								var temp_local = new WasmDefinitions.Function.Local("__dyn_local_l", type, false);
								_AppendLine($"{new Expression("=", ExprType.Binary, temp_local, expr_end)};", --depth);
								stack.Push(temp_local);
								stack.Push(temp_local);
							}

							branches.Remove(branch);
							break;
						}
					case Instructions.@if:
						{
							ValueType type = (ValueType)reader.ReadByte();
							string branch = null;
								branch = branches.Generate(counter, type, false);
							
							if (type != ValueType.EmptyBlock)
							{
								_AppendLine($"function __lambda_{branch}() {{", ++depth);
							}

							_AppendLine($"if ({_OperandToString(stack.Pop())}) {{", depth);
							BuildBody(disassembler, fn, reader, stack, branches, ref counter, depth + 1, true);
							_AppendLine("}", depth);

							if (type != ValueType.EmptyBlock)
							{
								Expression expr_end = new Expression($"__lambda_{branch}", ExprType.FunctionCall);
								var temp_local = new WasmDefinitions.Function.Local("__dyn_local_l", type, false);
								_AppendLine($"{new Expression("=", ExprType.Binary, temp_local, expr_end)};", --depth);
								stack.Push(temp_local);
								stack.Push(temp_local);
							}
							
							branches.Remove(branch);
							break;
						}
					case Instructions.@else:
						if (inside_if) return;

						_AppendLine($"if ({_OperandToString(stack.Pop())}) {{", depth);
						BuildBody(disassembler, fn, reader, stack, branches, ref counter, depth, true);
						_AppendLine("}", depth);
						break;
					case Instructions.end:
						if (stack.Count == 0) return;
						if (branches.Count > 0 && branches.GetCurrent().Item2 == ValueType.EmptyBlock) return;
						
						_AppendLine($"return {_OperandToString(stack.Pop())};", depth);
						return;
					case Instructions.br:
						{
							uint rel_depth = reader.ReadUVarint();
							(string current_branch, ValueType current_type, bool is_in_loop) = branches.GetCurrent();
							string retStr = current_type == ValueType.EmptyBlock ? "return;" : $"return {_OperandToString(stack.Pop())};";

							if (branches.Count == 1)
							{
								_AppendLine(retStr, depth);
								break;
							}

							(string target_branch, ValueType target_type, bool target_is_loop) = branches.Get((int)rel_depth, is_in_loop);
							retStr = target_type == ValueType.EmptyBlock ? "return;" : $"return {_OperandToString(stack.Pop())};";

							//if (current_branch == target_branch)
								_AppendLine(is_in_loop ? "continue;" : retStr/*$"break {target_branch};"*/, depth);
							//else
							//	_AppendLine(is_in_loop ? "continue;" : $"break {target_branch};", depth);

							break;
						}
					case Instructions.br_if:
						{
							uint rel_depth = reader.ReadUVarint();
							(string current_branch, ValueType current_type, bool is_in_loop) = branches.GetCurrent();
							string retStr = current_type == ValueType.EmptyBlock ? "return;" : $"return {_OperandToString(stack.Pop())};";

							_AppendLine($"if ({_OperandToString(stack.Pop())})", depth);

							if (branches.Count == 1)
							{
								_AppendLine(retStr, depth + 1);
								break;
							}

							(string target_branch, ValueType target_type, bool target_is_loop) = branches.Get((int)rel_depth, is_in_loop);
							retStr = target_type == ValueType.EmptyBlock ? "return;" : $"return {_OperandToString(stack.Pop())};";
							
							//if (current_branch == target_branch)
								_AppendLine(is_in_loop ? "continue;" : retStr/*$"break {target_branch};"*/, depth + 1);
							//else
							//	_AppendLine(is_in_loop ? "continue;" : $"break {target_branch};", depth + 1);

							break;
						}
					case Instructions.br_table:
						{
							uint tCount = reader.ReadUVarint();
							uint[] tTargets = new uint[tCount];
							for (uint n = 0; n < tCount; n++)
								tTargets[n] = reader.ReadUVarint();
							uint defTarget = reader.ReadUVarint();
							
							break;
						}
					case Instructions.@return:
							// i like exploring new methods for such unnecessary details
							// like creating a string that is either "return;" (with no space) or "return X;"
							_AppendLine($"{string.Join(" ", "return", _OperandToString(_PopStackMatchType(stack, fn.ReturnType)))};", depth);
							break;
					case Instructions.call:
						{
							WasmDefinitions.Function callee = disassembler.Functions[(int)reader.ReadUVarint()];
							Expression e = new Expression(callee.Name, ExprType.FunctionCall, callee.Parameters.Select(x => _PopStackMatchType(stack, x.Type)).ToArray());
							if (callee.ReturnType == ValueType.Void)
								_AppendLine($"{e};", depth);
							else stack.Push(e);
							break;
						}
					case Instructions.call_indirect:
						{
							WasmDefinitions.Type callee_type = disassembler.Types[(int)reader.ReadUVarint()];
							//Expression e = new Expression(callee.Name, ExprType.FunctionCall, callee.Parameters.Select(x => _PopStackMatchType(stack, x.Type)).ToArray());
							Expression e = new Expression(_OperandToString(stack.Pop()), ExprType.FunctionCall, callee_type.ParametersType.Select(x => _PopStackMatchType(stack, x)).ToArray());
							
							if (callee_type.ReturnType == ValueType.Void)
								_AppendLine($"{e};", depth);
							else stack.Push(e);

							break;
						}
					case Instructions.drop:
						if (stack.Count > 0 && stack.Pop() is Expression expr && expr.ExprType == ExprType.FunctionCall)
							_AppendLine($"{expr};", depth);
						break;
					case Instructions.select:
						{
							/*
							    yes, either the stack system or my last brain cell is broken
							    but there is something wrong with stack order, this should
							    also explain why does the PopReverse() function exist
								see it for yourself:
								
								i32.const 123 <- "condition == true" value
								i32.const 456 <- "condition == false" value
								i32.const condition
								select
								
								this should translate to:
								
								condition ? 123 : 456
								
								yet in real life:
								
								condition ? 456 : 123
							        ^        ^     ^
							        |        |     L__stack[top - 2]
							        |        L________stack[top - 1]
							        L_________________stack[top]
								
								condition is in the correct position but expression values are reversed
								if you think this is my mistake, please take a look at Expression.ToString()
								because I rushed it and the source of trouble can be there instead
							*/

							object cond = stack.Pop();
							object false_val = stack.Pop();
							object true_val = stack.Pop();

							stack.Push(new Expression(null, ExprType.Ternary, cond, true_val, false_val));
							break;
						}
					case Instructions.getlocal:
						stack.Push(fn.Locals[(int)reader.ReadUVarint()]);
						break;
					case Instructions.setlocal:
					case Instructions.teelocal:
						{
							var local = fn.Locals[(int)reader.ReadUVarint()];
							//if (_PopStackWithExpectedType(stack, out Expression e))
							//{
							//	stack.Push(new Expression("=", ExprType.Binary, local, e));
							//}

							//stack.Push(new Expression("=", ExprType.Binary, local, stack.Pop()));
							_AppendLine($"{new Expression("=", ExprType.Binary, local, stack.Pop())};", depth);
							if (instruction == Instructions.teelocal)
								stack.Push(local);
							break;
						}
					case Instructions.getglobal:
						stack.Push(disassembler.Globals[(int)reader.ReadUVarint()]);
						break;
					case Instructions.setglobal:
						//stack.Push(new Expression("=", ExprType.Binary, disassembler.Globals[(int)reader.ReadUVarint()], stack.Pop()));
						_AppendLine($"{new Expression("=", ExprType.Binary, disassembler.Globals[(int)reader.ReadUVarint()], stack.Pop())};", depth);
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
						{
							/*
								let me clear some confusion if you are like me:
								
								getlocal var0
								ixx_load offset [align]
								setlocal var1

								this code above turns into:
								
								var1 = mem_get_value(var0 + offset, [align]);
								
								since "+ 0" is redundant, we will filter it
							*/

							uint align = reader.ReadUVarint();
							uint offset = reader.ReadUVarint();
							stack.Push(new Expression("mem_get_value", ExprType.FunctionCall, offset == 0 ? stack.Pop() : new Expression("+", ExprType.Binary, stack.Pop(), offset), align));
							break;
						}
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
							/*
								like ixx_load above, it works in the same way:
								
								getlocal var0
								getlocal var1
								ixx_store offset [align]
								
								this code above turns into:
								
								mem_set_value(var0 + offset, var1, [align])
							*/
							uint align = reader.ReadUVarint();
							uint offset = reader.ReadUVarint();
							object val = stack.Pop();
							object addr = stack.Pop();

							if (offset != 0) addr = new Expression("+", ExprType.Binary, addr, offset);

							_AppendLine($"{new Expression("mem_set_value", ExprType.FunctionCall, addr, val, align)};", depth);
							break;
						}
					case Instructions.current_memory:
						stack.Push(new Expression("__getmemsize", ExprType.FunctionCall, reader.ReadByte()));
						break;
					case Instructions.grow_memory:
						stack.Push(new Expression("__growmemsize", ExprType.FunctionCall, reader.ReadByte()));
						break;
					case Instructions.i32_const:
						stack.Push(reader.ReadVarint());
						break;
					case Instructions.i64_const:
						stack.Push(reader.ReadVarlong());
						break;
					case Instructions.f32_const:
						stack.Push(reader.ReadFloat());
						break;
					case Instructions.f64_const:
						stack.Push(reader.ReadDouble());
						break;
					case Instructions.i32_eqz:
					case Instructions.i64_eqz:
						stack.Push(new Expression("==", ExprType.Binary, stack.Pop(), 0));
						break;
					case Instructions.i32_eq:
					case Instructions.i64_eq:
					case Instructions.f32_eq:
					case Instructions.f64_eq:
						stack.Push(new Expression("==", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_ne:
					case Instructions.i64_ne:
					case Instructions.f32_ne:
					case Instructions.f64_ne:
						stack.Push(new Expression("!=", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_lt_s:
					case Instructions.i32_lt_u:
					case Instructions.i64_lt_s:
					case Instructions.i64_lt_u:
					case Instructions.f32_lt:
					case Instructions.f64_lt:
						stack.Push(new Expression("<", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_gt_s:
					case Instructions.i32_gt_u:
					case Instructions.i64_gt_s:
					case Instructions.i64_gt_u:
					case Instructions.f32_gt:
					case Instructions.f64_gt:
						stack.Push(new Expression(">", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_le_s:
					case Instructions.i32_le_u:
					case Instructions.i64_le_s:
					case Instructions.i64_le_u:
					case Instructions.f32_le:
					case Instructions.f64_le:
						stack.Push(new Expression("<=", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_ge_s:
					case Instructions.i32_ge_u:
					case Instructions.i64_ge_s:
					case Instructions.i64_ge_u:
					case Instructions.f32_ge:
					case Instructions.f64_ge:
						stack.Push(new Expression(">=", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_clz:
					case Instructions.i64_clz:
						stack.Push(new Expression("int32_count_leading_zero_bits", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.i32_ctz:
					case Instructions.i64_ctz:
						stack.Push(new Expression("int32_count_trailing_zero_bits", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.i32_popcnt:
					case Instructions.i64_popcnt:
						stack.Push(new Expression("int32_count_one_bits", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.i32_add:
					case Instructions.i64_add:
					case Instructions.f32_add:
					case Instructions.f64_add:
						stack.Push(new Expression("+", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_sub:
					case Instructions.i64_sub:
					case Instructions.f32_sub:
					case Instructions.f64_sub:
						stack.Push(new Expression("-", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_mul:
					case Instructions.i64_mul:
					case Instructions.f32_mul:
					case Instructions.f64_mul:
						stack.Push(new Expression("*", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_div_s:
					case Instructions.i64_div_s:
					case Instructions.i32_div_u:
					case Instructions.i64_div_u:
					case Instructions.f32_div:
					case Instructions.f64_div:
						stack.Push(new Expression("/", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_rem_s:
					case Instructions.i32_rem_u:
					case Instructions.i64_rem_s:
					case Instructions.i64_rem_u:
						stack.Push(new Expression("%", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_and:
					case Instructions.i64_and:
						stack.Push(new Expression("&", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_or:
					case Instructions.i64_or:
						stack.Push(new Expression("|", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_xor:
					case Instructions.i64_xor:
						stack.Push(new Expression("^", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_shl:
					case Instructions.i64_shl:
						stack.Push(new Expression("<<", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_shr_s:
					case Instructions.i64_shr_s:
					case Instructions.i32_shr_u:
					case Instructions.i64_shr_u:
						stack.Push(new Expression(">>", ExprType.Binary, stack.PopReverse(2)));
						break;
					case Instructions.i32_rotl:
					case Instructions.i64_rotl:
						stack.Push(new Expression("int_rotl", ExprType.FunctionCall, stack.PopReverse(2)));
						break;
					case Instructions.i32_rotr:
					case Instructions.i64_rotr:
						stack.Push(new Expression("int_rotr", ExprType.FunctionCall, stack.PopReverse(2)));
						break;
					case Instructions.f32_abs:
					case Instructions.f64_abs:
						stack.Push(new Expression("__abs", ExprType.FunctionCall, stack.PopReverse(2)));
						break;
					case Instructions.f32_neg:
					case Instructions.f64_neg:
						stack.Push(new Expression("-", ExprType.Unary, stack.Pop()));
						break;
					case Instructions.f32_ceil:
					case Instructions.f64_ceil:
						stack.Push(new Expression("__ceil", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.f32_floor:
					case Instructions.f64_floor:
						stack.Push(new Expression("__floor", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.f32_trunc:
					case Instructions.f64_trunc:
						stack.Push(new Expression("__trunc", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.f32_nearest:
					case Instructions.f64_nearest:
						stack.Push(new Expression("__nearest", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.f32_sqrt:
					case Instructions.f64_sqrt:
						stack.Push(new Expression("__sqrt", ExprType.FunctionCall, stack.Pop()));
						break;
					case Instructions.f32_min:
					case Instructions.f64_min:
						stack.Push(new Expression("__min", ExprType.FunctionCall, stack.PopReverse(2)));
						break;
					case Instructions.f32_max:
					case Instructions.f64_max:
						stack.Push(new Expression("__max", ExprType.FunctionCall, stack.PopReverse(2)));
						break;
					case Instructions.f32_copysign:
					case Instructions.f64_copysign:
						stack.Push(new Expression("__copysign", ExprType.FunctionCall, stack.Pop()));
						break;
					#region future instructions to implement
					case Instructions.i32_wrap_i64:
						break;
					case Instructions.i32_trunc_s_f32:
						break;
					case Instructions.i32_trunc_u_f32:
						break;
					case Instructions.i32_trunc_s_f64:
						break;
					case Instructions.i32_trunc_u_f64:
						break;
					case Instructions.i64_extends_s_i32:
						break;
					case Instructions.i64_extends_u_i32:
						break;
					case Instructions.i64_trunc_s_f32:
						break;
					case Instructions.i64_trunc_u_f32:
						break;
					case Instructions.i64_trunc_s_f64:
						break;
					case Instructions.i64_trunc_u_f64:
						break;
					case Instructions.f32_convert_s_i32:
						break;
					case Instructions.f32_convert_u_i32:
						break;
					case Instructions.f32_convert_s_i64:
						break;
					case Instructions.f32_convert_u_i64:
						break;
					case Instructions.f32_demote_f64:
						break;
					case Instructions.f64_convert_s_i32:
						break;
					case Instructions.f64_convert_u_i32:
						break;
					case Instructions.f64_convert_s_i64:
						break;
					case Instructions.f64_convert_u_i64:
						break;
					case Instructions.f64_demote_f32:
						break;
					case Instructions.i32_reinterpret_f32:
						break;
					case Instructions.i64_reinterpret_f64:
						break;
					case Instructions.f32_reinterpret_i32:
						break;
					case Instructions.f64_reinterpret_i64:
						break;
					#endregion
					default:
						break;
				}

				counter++;
			}
		}

		private object _PopStackMatchType(Stack<object> stack, ValueType type)
		{
			switch (type)
			{
				case ValueType.F64:
				case ValueType.F32:
				case ValueType.I64:
				case ValueType.I32:
					{
						while (stack.Count > 0)
						{
							object top = stack.Pop();
							if ((top is WasmDefinitions.Function.Local local && local.Type == type))
								return local;
							if (top is Expression || top.GetType() == Disassembler.GetTypeEquivalent(type))
								return top;
						}

						break;
					}
				case ValueType.Void: return typeof(void);
				case ValueType.AnyFunc:
				case ValueType.Func:
				case ValueType.EmptyBlock:
				default:
					throw new NotImplementedException();
			}
			return null;
		}

		void _AppendLine(string line, int depth) => m_Output.Append(new string(' ', depth * 4)).AppendLine(line);

		public string BuildOutput() => m_Output.ToString();


		bool _PopStackWithExpectedType<T>(Stack<object> stack, out T value)
		{
			value = default;

			if (stack.Peek() is T)
			{
				value = (T)stack.Pop();
				return true;
			}
			
			return false;
		}
		
		bool _ExpectStackValueType(Stack<object> stack, ValueType type)
		{
			object peek = stack.Peek();
			
			if (peek is WasmDefinitions.Function.Local)
				return ((WasmDefinitions.Function.Local)peek).Type == type;
			if (peek is WasmDefinitions.Global)
				return ((WasmDefinitions.Global)peek).Type == Disassembler.GetTypeStr(type);

			switch (type)
			{
				case ValueType.F64: return peek is double;
				case ValueType.F32: return peek is float;
				case ValueType.I64: return peek is long;
				case ValueType.I32: return peek is int;
				case ValueType.AnyFunc:
				case ValueType.Func:
				case ValueType.EmptyBlock:
				default: return false;
			}
		}

		static string _OperandToString(object operand)
		{
			if (operand.GetType() == typeof(void))
				return string.Empty;

			switch (operand)
			{
				case WasmDefinitions.Function.Local local: return local.Name;
				case WasmDefinitions.Global g: return g.Name;
				case Expression expr: return expr.ToString();
				case int int32val: return int32val.ToString();
				case uint uint32val: return uint32val.ToString();
				case long int64val: return int64val.ToString();
				case ulong uint64val: return uint64val.ToString();
				case float floatval: return floatval.ToString();
				case double doubleval: return doubleval.ToString();
				default: return null;
			}
		}

		private struct State
		{
			public BranchList branches;
			public int counter, depth;
			public bool inside_if;

			public static State Create() => new State
			{
				branches = new BranchList(),
				counter = 0,
				depth = 0,
				inside_if = false
			};
		}

		private class BranchList
		{
			readonly List<(string, ValueType, bool)> m_Branches = new List<(string, ValueType, bool)>();
			int m_Current = 0;

			//public (string, ValueType, bool) this[int index] => m_Branches[m_Current - index];

			public int Count => m_Branches.Count;

			public void Add(string name, ValueType block_type, bool is_loop)
			{
				m_Branches.Add((name, block_type, is_loop));
				m_Current = m_Branches.Count - 1;
			}
			
			public string Generate(int id, ValueType block_type, bool is_loop)
			{
				string name = $"{(is_loop ? "loop_" : "block_")}{id}";
				Add(name, block_type, is_loop);
				return name;
			}

			public void Clear()
			{
				m_Branches.Clear();
				m_Current = 0;
			}

			public (string, ValueType, bool) Get(int depth, bool is_in_loop) => m_Branches[is_in_loop ? m_Current - depth : m_Current - depth];
			public (string, ValueType, bool) GetCurrent() => m_Branches[m_Current];

			public int GetIndex(string name) => m_Branches.FindIndex(x => name.Equals(x.Item1));
			
			public void Remove(string name)
			{
				int index = GetIndex(name);
				if (index >= 0) m_Branches.RemoveAt(index);
				m_Current = m_Branches.Count - 1;
			}
		}

		// operand N is left-hand side, operand 0 is right-hand side
		// so we need to reverse the list
		private readonly struct Expression
		{
			public string Expr { get; }
			public ExprType ExprType { get; }
			public IEnumerable<object> Operands { get; }

			public Expression(string expr, ExprType type, params object[] operands)
			{
				Expr = expr;
				ExprType = type;
				
				// allow list of operands as a single parameter too
				Operands = (operands.Length > 0 && operands[0] is IEnumerable<object> enumerable) ? enumerable : operands;
			}

			public override string ToString()
			{
				object[] operands = Operands.Select(_OperandToString).ToArray();

				switch (ExprType)
				{
					case ExprType.Unary: return $"{Expr}{operands[0]}";
					case ExprType.Binary: return $"{operands[0]} {Expr} {operands[1]}";
					// the only ternary expression is "x ? a : b" so we don't need an expression for this one
					case ExprType.Ternary: return $"{operands[0]} ? {operands[1]} : {operands[2]}";
					case ExprType.FunctionCall: return $"{Expr}({string.Join(", ", operands)})";
					default: return null;
				}
			}
		}

		private enum ExprType
		{
			Unary, Binary, Ternary, FunctionCall
		}
	}
}
