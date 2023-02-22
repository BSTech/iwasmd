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

namespace iwasmd
{
	public class RTTIParser
	{
		const string type_info = "St9type_info";

		readonly Disassembler disassembler;
		readonly Dictionary<string, int[]> vTables;

		public RTTIParser(Disassembler disassembler)
		{
			this.disassembler = disassembler;
			vTables = new Dictionary<string, int[]>();
		}

		long _FindStringInStream(Stream stream, string what, bool reset = true)
		{
			byte[] str = Encoding.UTF8.GetBytes(what);
			byte[] buf = new byte[1024];
			long pos = 0;
			long orig = stream.Position;

			while (stream.Read(buf, 0, buf.Length) > 0)
			{
				int p = buf.FindSequence(str);

				if (p >= 0)
				{
					stream.Position = reset ? orig : (pos + p);
					return pos + p;
				}

				if (stream.Position == stream.Length) break;

				pos = stream.Seek(-buf.Length / 2, SeekOrigin.Current);
			}

			if (reset) stream.Position = orig;
			return -1;
		}

		void _ReadVTable(BinaryReader reader)
		{
			List<int> ptrs = new List<int>();
			int vtpos = (int)reader.BaseStream.Position;

			string mangled_name = "_ZTV" + reader.ReadCString(true);
			int ptr = reader.ReadInt32();

			while (ptr != 0 && ptr != vtpos)
			{
				ptrs.Add(ptr);
				ptr = reader.ReadInt32();
			}

			vTables.Add(mangled_name, ptrs.ToArray());
		}

		public void Parse()
		{
			using (BinaryReader br = new BinaryReader(disassembler.CreateDataStream()))
			{
				int p = (int)_FindStringInStream(br.BaseStream, type_info, false);
				br.BaseStream.Seek(0, SeekOrigin.Current);

				_ReadVTable(br);
				_ReadVTable(br);
				_ReadVTable(br);
			}
		}
	}
}
