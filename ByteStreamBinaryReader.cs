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
	/// Represents a binary reader that uses a byte array as a stream.
	/// </summary>
	public class ByteStreamBinaryReader
	{
		private long m_Position = 0;
		private readonly byte[] m_Buffer;

		/// <summary>
		/// Initialize the reader with a byte array as the stream.
		/// </summary>
		/// <param name="bytes">The byte array that is going to be used as the stream.</param>
		/// <exception cref="ArgumentNullException">if the byte array is null.</exception>
		public ByteStreamBinaryReader(byte[] bytes) => m_Buffer = bytes ?? throw new ArgumentNullException(nameof(bytes));

		/// <summary>
		/// Gets or sets whether this reader is interpreting the data in little endian (default) or big endian.
		/// </summary>
		public bool LittleEndian { get; set; } = true;

		/// <summary>
		/// Gets or sets the position of the stream.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">if the new position is out of bounds.</exception>
		public long Position
		{
			get => m_Position;
			set
			{
				if (value < 0 || value > m_Buffer.LongLength)
					throw new ArgumentOutOfRangeException(nameof(value), "Position is beyond the size of the stream.");
				
				m_Position = value;
			}
		}

		/// <summary>
		/// Gets the length of the stream.
		/// </summary>
		public long Length => m_Buffer.Length;

		/// <summary>
		/// Checks if the reader has reached (or is beyond) the end of the stream.
		/// </summary>
		/// <returns></returns>
		public bool IsEOS()
		{
			return m_Position >= m_Buffer.LongLength;
		}

		/// <summary>
		/// Seeks the stream by the amount of given offset from an origin.
		/// </summary>
		/// <param name="offset">A zero-based offset.</param>
		/// <param name="origin">The origin of the offset.</param>
		/// <exception cref="ArgumentOutOfRangeException">if the new position is out of bounds.</exception>
		/// <exception cref="ArgumentException">if the origin is invalid.</exception>
		public void Seek(long offset, BSBRSeekOrigin origin)
		{
			switch (origin)
			{
				case BSBRSeekOrigin.Begin:
					if (offset < 0 || offset > m_Buffer.LongLength)
						throw new ArgumentOutOfRangeException(nameof(offset));

					m_Position = offset;
					break;
				case BSBRSeekOrigin.Current:
					if (m_Position + offset < 0 || m_Position + offset > m_Buffer.LongLength)
						throw new ArgumentOutOfRangeException(nameof(offset));

					m_Position += offset;
					break;
				case BSBRSeekOrigin.End:
					if (offset < 0 || offset > m_Buffer.LongLength)
						throw new ArgumentOutOfRangeException(nameof(offset));

					m_Position = m_Buffer.LongLength - offset;
					break;
				default:
					throw new ArgumentException(nameof(origin));
			}
		}

		/// <summary>
		/// Reads a single unsigned byte from the stream.
		/// </summary>
		/// <returns>The byte read from the stream.</returns>
		/// <exception cref="Exception">when there is no more data to read.</exception>
		public byte ReadByte()
		{
			if (m_Position >= m_Buffer.Length)
				throw new Exception("Cannot read beyond the stream.");

			return m_Buffer[m_Position++];
		}

		/// <summary>
		/// Reads a single signed byte from the stream.
		/// </summary>
		/// <returns>The byte read from the stream.</returns>
		public sbyte ReadSByte() => (sbyte)ReadByte();

		/// <summary>
		/// Reads a single character from the stream.
		/// </summary>
		/// <returns>The character read from the stream.</returns>
		public char ReadChar() => (char)ReadByte();

		/// <summary>
		/// Reads a single signed 16-bit integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public short ReadInt16() => (short)(LittleEndian ? (ReadByte() << 8) | ReadByte()
												 : ReadByte() | (ReadByte() << 8));

		/// <summary>
		/// Reads a single signed 32-bit integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public int ReadInt32() => LittleEndian ? (ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte()
									   : ReadByte() | (ReadByte() << 8) | (ReadByte() << 16) | (ReadByte() << 24);

		/// <summary>
		/// Reads a single signed 64-bit integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public long ReadInt64() => LittleEndian ? (ReadByte() << 56) | (ReadByte() << 48) | (ReadByte() << 40) | (ReadByte() << 32) | (ReadByte() << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte()
									    : ReadByte() | (ReadByte() << 8) | (ReadByte() << 16) | (ReadByte() << 24) | (ReadByte() << 32) | (ReadByte() << 40) | (ReadByte() << 48) | (ReadByte() << 56);

		/// <summary>
		/// Reads a single signed 32-bit limited LEB128-encoded variable length integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public int ReadVarint() => m_Buffer.ReadLEB128(ref m_Position);

		/// <summary>
		/// Reads a single signed 64-bit limited LEB128-encoded variable length integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public long ReadVarlong() => m_Buffer.ReadLEB128_64(ref m_Position);

		/// <summary>
		/// Reads a single unsigned 32-bit limited LEB128-encoded variable length integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public uint ReadUVarint() => m_Buffer.ReadULEB128(ref m_Position);

		/// <summary>
		/// Reads a single unsigned 64-bit limited LEB128-encoded variable length integer from the stream.
		/// </summary>
		/// <returns>The integer read from the stream.</returns>
		public ulong ReadUVarlong() => m_Buffer.ReadULEB128_64(ref m_Position);

		/// <summary>
		/// Reads a C-style null terminated string from the stream.
		/// </summary>
		/// <param name="aligned">When this parameter is <see langword="true"/> the last position after a successful read will be aligned to the first next 32-bit address.</param>
		/// <returns>An ANSI string read from the stream.</returns>
		/// <exception cref="Exception">when there is no more data to read.</exception>
		public string ReadCString(bool aligned = false)
		{
			string str = string.Empty;

			for (char ch = ReadChar(); ch != '\0'; ch = ReadChar())
				str += ch;

			if (aligned && (m_Position % sizeof(int)) != 0)
			{
				long jump = sizeof(int) - (m_Position % sizeof(int));

				if (m_Position + jump > m_Buffer.LongLength)
					throw new Exception("Cannot read beyond the stream.");

				m_Position += jump;
			}

			return str;
		}

		/// <summary>
		/// Reads a C-style null terminated wide-char string from the stream.
		/// </summary>
		/// <param name="aligned">When this parameter is <see langword="true"/> the last position after a successful read will be aligned to the first next 32-bit address.</param>
		/// <returns>A unicode wide-char string read from the stream.</returns>
		/// <exception cref="Exception">when there is no more data to read.</exception>
		public string ReadCWString(bool aligned = false)
		{
			string str = string.Empty;

			for (short ch = ReadInt16(); ch != 0; ch = ReadInt16())
				str +=  (char)ch;

			if (aligned && (m_Position % sizeof(int)) != 0)
			{
				long jump = sizeof(int) - (m_Position % sizeof(int));

				if (m_Position + jump > m_Buffer.LongLength)
					throw new Exception("Cannot read beyond the stream.");

				m_Position += jump;
			}

			return str;
		}

		/// <summary>
		/// Reads a single 32-bit single precision floating point number from the stream. This function is marked as unsafe.
		/// </summary>
		/// <returns>The number read from the stream.</returns>
		public unsafe float ReadFloat()
		{
			int val = ReadInt32();
			return *(float*)&val;
		}
		
		/// <summary>
		/// Reads a single 64-bit double precision floating point number from the stream. This function is marked as unsafe.
		/// </summary>
		/// <returns>The number read from the stream.</returns>
		public unsafe double ReadDouble()
		{
			long val = ReadInt64();
			return *(double*)&val;
		}
	}

	public enum BSBRSeekOrigin { Begin, Current, End }
}
