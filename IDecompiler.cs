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
	/// <summary>
	/// Represents a decompiler interface that can be used by the <see cref="Disassembler"/> class.
	/// </summary>
	public interface IDecompiler
	{
		void Prepare(Disassembler disassembler, List<AddressRange> dataRanges, int realFunctionOffset);
		void Decompile(Disassembler disassembler, WasmDefinitions.Function function);
	}
}
