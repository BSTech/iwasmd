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
	public class WasmDefinitions
	{
		public readonly struct Global
		{
			public string Name { get; }
			public string Value { get; }
			public string Type { get; }
			public bool IsConst { get; }
			public Global(string name, string value, string type, bool isConst)
			{
				Name = name;
				Value = value;
				Type = type;
				IsConst = isConst;
			}

			public override string ToString() => $"{(IsConst ? "const " : string.Empty)}{Type} {Name} = {Value}";
		}

		public readonly struct Type
		{
			public bool HasReturn { get; }
			public ValueType ReturnType { get; }
			public ValueType[] ParametersType { get; }

			public Type(bool hasReturn, ValueType rType, ValueType[] pTypes)
			{
				HasReturn = hasReturn;
				ReturnType = rType;
				ParametersType = pTypes;
			}

			public override string ToString()
			{
				string s = string.Empty;
				if (HasReturn) s = $"{Disassembler.GetTypeStr(ReturnType)} ";
				s += '(';
				if (ParametersType != null)
					s += string.Join(", ", ParametersType.Select(x => Disassembler.GetTypeStr(x)));
				return s + ')';
			}
		}

		public readonly struct Function
		{
			// true if this function is actually a placeholder for initializing the globals
			// note that this is not in the specification
			public bool IsGlobal { get; }
			
			public string Name { get; }
			public ValueType ReturnType { get; }
			public uint Index { get; }
			public List<Local> Locals { get; }
			public List<Local> Parameters => Locals.Where(x => x.IsParameter).ToList();
			public byte[] Bytecode { get; }
			public string ExportedName { get; }
			public List<CrossReference> XRefs { get; }
			public string Signature => IsGlobal ? Name : $"{(Bytecode == null ? "extern " : string.Empty)}{Disassembler.GetTypeStr(ReturnType)} {Name}({string.Join(", ", Parameters)})";

			public Function(string name, ValueType rtype, uint index, List<Local> locals, byte[] code, string expname, bool is_global = false)
			{
				Name = is_global ? "<global>" : name;
				ReturnType = rtype;
				Index = index;
				Locals = locals;
				Bytecode = code;
				ExportedName = expname;
				IsGlobal = is_global;
				XRefs = new List<CrossReference>();
			}

			public override string ToString() => Signature;

			public readonly struct Local
			{
				public string Name { get; }
				public ValueType Type { get; }
				public bool IsParameter { get; }

				public Local(string name, ValueType type, bool par)
				{
					Name = name;
					Type = type;
					IsParameter = par;
				}

				public override string ToString() => $"{Disassembler.GetTypeStr(Type)} {Name}";
			}
		}
	}
}
