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
	public enum WasmSectionId : byte
	{
		Custom,
		Type,
		Import,
		Function,
		Table,
		Memory,
		Global,
		Export,
		Start,
		Element,
		Code,
		Data
	}

	public enum ExternalKind : byte
	{
		Function,
		Table,
		Memory,
		Global
	}

	public enum ValueType : byte
	{
		F64 = 0x7C,
		F32,
		I64,
		I32,
		AnyFunc = 0x70,
		Func = 0x60,
		EmptyBlock = 0x40,
		Void = 0 // not real
	}

	public struct ResizableLimits
	{
		public bool flags;
		public uint initial;
		public uint maximum;
	}

	public class WasmSections
	{
		public struct FuncType
		{
			public byte form;
			public uint pCount;
			public ValueType[] pTypes;
			public bool hasReturn;
			public ValueType rType;

			public override string ToString() => $"form = {form}, pCount = {pCount}, hasReturn = {hasReturn}";
		}

		public struct Import
		{
			public uint mod_Len;
			public string module;
			public uint fld_Len;
			public string field;
			public ExternalKind kind;
			public uint type_Func;
			public Table type_Table;
			public ResizableLimits type_Memory;
			public GlobalType type_Global;
		}

		public struct Table
		{
			public byte element_type;
			public ResizableLimits limits;
		}

		public struct Global
		{
			public GlobalType type;
			public byte[] init;
		}

		public struct Export
		{
			public uint fld_Len;
			public string field;
			public ExternalKind kind;
			public uint index;
		}

		public struct ElementSegment
		{
			public uint index;
			public byte[] offset;
			public uint num_elem;
			public uint[] elems;
		}

		public struct Function
		{
			public uint bodySize;
			public uint localCount;
			public LocalEntry[] locals;
			public byte[] code;
		}

		public struct Data
		{
			public uint index;
			public byte[] offset;
			public uint size;
			public byte[] data;
		}

		public struct GlobalType
		{
			public ValueType content_type;
			public bool mutability;
		}

		public struct LocalEntry
		{
			public uint count;
			public ValueType type;
		}
	}
}
