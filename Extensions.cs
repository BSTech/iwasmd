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
	/// <summary>
	/// A class of extension functions.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Reads a 32-bit limited LEB128 variable length signed integer from the current stream and advances the current position of the stream accordingly.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns>A 32-bit signed integer.</returns>
		public static int ReadLEB128(this BinaryReader reader)
		{
			int val = 0;
			int count = 0;
			byte cur;

			while (true)
			{
				cur = reader.ReadByte();
				val |= (int)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
			}

			if ((count * 7) < 32 && (cur & 64) == 64)
				val |= ~0 << (count * 7);

			return val;
		}
		
		/// <summary>
		/// Reads a 64-bit limited LEB128 variable length signed integer from the current stream and advances the current position of the stream accordingly.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns>A 64-bit signed integer.</returns>
		public static long ReadLEB128_64(this BinaryReader reader)
		{
			long val = 0;
			int count = 0;
			byte cur;

			while (true)
			{
				cur = reader.ReadByte();
				val |= (long)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
			}

			if ((count * 7) < 64 && (cur & 64) == 64)
				val |= (long)(~0 << (count * 7));

			return val;
		}

		/// <summary>
		/// Reads a 32-bit limited LEB128 variable length unsigned integer from the current stream and advances the current position of the stream accordingly.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns>A 32-bit unsigned integer.</returns>
		public static uint ReadULEB128(this BinaryReader reader)
		{
			uint val = 0;
			int count = 0;

			while (true)
			{
				byte cur = reader.ReadByte();
				val |= (uint)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
			}

			return val;
		}
		
		/// <summary>
		/// Reads a 64-bit limited LEB128 variable length unsigned integer from the current stream and advances the current position of the stream accordingly.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns>A 64-bit unsigned integer.</returns>
		public static ulong ReadULEB128_64(this BinaryReader reader)
		{
			ulong val = 0;
			int count = 0;

			while (true)
			{
				byte cur = reader.ReadByte();
				val |= (ulong)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
			}

			return val;
		}
		
		/// <summary>
		/// Forms a 32-bit limited LEB128 variable length signed integer from the array with an offset and adjusts the offset accordingly.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset">The zero-based offset of the LEB128 integer.</param>
		/// <returns>A 32-bit signed integer.</returns>
		public static int ReadLEB128(this byte[] buffer, ref long offset)
		{
			int val = 0;
			int count = 0;
			byte cur;

			while (true)
			{
				cur = buffer[offset++];
				val |= (int)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
				if (offset >= buffer.Length)
					throw new IndexOutOfRangeException("Offset is beyond the array size.");
			}

			if ((count * 7) < 32 && (cur & 64) == 64)
				val |= ~0 << (count * 7);

			return val;
		}
		
		/// <summary>
		/// Forms a 64-bit limited LEB128 variable length signed integer from the array with an offset and adjusts the offset accordingly.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset">The zero-based offset of the LEB128 integer.</param>
		/// <returns>A 64-bit signed integer.</returns>
		public static long ReadLEB128_64(this byte[] buffer, ref long offset)
		{
			long val = 0;
			int count = 0;
			byte cur;

			while (true)
			{
				cur = buffer[offset++];
				val |= (long)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
				if (offset >= buffer.Length)
					throw new IndexOutOfRangeException("Offset is beyond the array size.");
			}

			if ((count * 7) < 64 && (cur & 64) == 64)
				val |= (long)(~0 << (count * 7));

			return val;
		}

		/// <summary>
		/// Forms a 32-bit limited LEB128 variable length unsigned integer from the array with an offset and adjusts the offset accordingly.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset">The zero-based offset of the LEB128 integer.</param>
		/// <returns>A 32-bit unsigned integer.</returns>
		public static uint ReadULEB128(this byte[] buffer, ref long offset)
		{
			uint val = 0;
			int count = 0;

			while (true)
			{
				byte cur = buffer[offset++];
				val |= (uint)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
				if (offset >= buffer.Length)
					throw new IndexOutOfRangeException("Offset is beyond the array size.");
			}

			return val;
		}
		
		/// <summary>
		/// Forms a 64-bit limited LEB128 variable length unsigned integer from the array with an offset and adjusts the offset accordingly.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset">The zero-based offset of the LEB128 integer.</param>
		/// <returns>A 64-bit unsigned integer.</returns>
		public static ulong ReadULEB128_64(this byte[] buffer, ref long offset)
		{
			ulong val = 0;
			int count = 0;

			while (true)
			{
				byte cur = buffer[offset++];
				val |= (ulong)(cur & 127) << (count++ * 7);
				if ((cur & 128) == 0) break;
				if (offset >= buffer.Length)
					throw new IndexOutOfRangeException("Offset is beyond the array size.");
			}

			return val;
		}

		/// <summary>
		/// Reads a C-style null terminated string from the current stream and advances the current position accordingly.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="aligned">When this parameter is <see langword="true"/> the last position after a successful read will be aligned to the first next 32-bit address.</param>
		/// <returns>An ANSI string.</returns>
		/// <exception cref="EndOfStreamException"></exception>
		public static string ReadCString(this BinaryReader reader, bool aligned = false)
		{
			string str = string.Empty;

			for (char ch = reader.ReadChar(); ch != '\0'; ch = reader.ReadChar())
				str += ch;

			if (aligned && (reader.BaseStream.Position % sizeof(int)) != 0)
			{
				long jump = sizeof(int) - (reader.BaseStream.Position % sizeof(int));

				if (reader.BaseStream.Position + jump > reader.BaseStream.Length)
					throw new EndOfStreamException("Cannot read beyond the stream.");

				reader.BaseStream.Position += jump;
			}

			return str;
		}

		/// <summary>
		/// Reads a C-style null terminated wide-char string from the current stream and advances the current position accordingly.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="aligned">When this parameter is <see langword="true"/> the last position after a successful read will be aligned to the first next 32-bit address.</param>
		/// <returns>A unicode wide-char string.</returns>
		/// <exception cref="EndOfStreamException"></exception>
		public static string ReadCWString(this BinaryReader reader, bool aligned = false)
		{
			string str = string.Empty;

			for (short ch = reader.ReadInt16(); ch != 0; ch = reader.ReadInt16())
				str +=  (char)ch;

			if (aligned && (reader.BaseStream.Position % sizeof(int)) != 0)
			{
				long jump = sizeof(int) - (reader.BaseStream.Position % sizeof(int));

				if (reader.BaseStream.Position + jump > reader.BaseStream.Length)
					throw new EndOfStreamException("Cannot read beyond the stream.");

				reader.BaseStream.Position += jump;
			}

			return str;
		}

		/// <summary>
		/// Finds the index of the first occurence of a sequence in the array.
		/// </summary>
		/// <typeparam name="T">The element type of the array.</typeparam>
		/// <param name="array"></param>
		/// <param name="sequence">The sequence of elements to be searched in the array.</param>
		/// <returns>The index of found sequence in the array, or -1 if it is not found.</returns>
		/// <exception cref="ArgumentNullException">if the array or sequence is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">if the sequence is bigger than the array.</exception>
		public static int FindSequence<T>(this T[] array, T[] sequence)
		{
			if (array == null) throw new ArgumentNullException(nameof(array));
			if (sequence == null) throw new ArgumentNullException(nameof(sequence));
			if (array.Length < sequence.Length) throw new ArgumentOutOfRangeException(nameof(sequence), "The sequence to be searched is bigger than the array itself.");
			
			int i = 0;
			for (int n = 0; n < array.Length - sequence.Length; n++)
			{
				if (array[n].Equals(sequence[0]))
				while (array[n + i].Equals(sequence[i++]))
					if (i == sequence.Length) return n;
				i = 0;
			}

			return -1;
		}

		/// <summary>
		/// Tries to find an element in a list based on a condition and gives out the result if it is found.
		/// </summary>
		/// <typeparam name="T">The element type of the list.</typeparam>
		/// <param name="list"></param>
		/// <param name="match"></param>
		/// <param name="result"></param>
		/// <returns><see langword="true"/> if the element is found; <see langword="false"/> otherwise.</returns>
		/// <exception cref="ArgumentNullException">if either the list or the matcher is null.</exception>
		public static bool TryFind<T>(this List<T> list, Predicate<T> match, out T result)
		{
			if (list == null) throw new ArgumentNullException(nameof(list));
			if (match == null) throw new ArgumentNullException(nameof(match));

			int i = list.FindIndex(match);
			result = i >= 0 ? list[i] : default;
			return i >= 0;
		}

		/// <summary>
		/// Removes and returns a number of objects in reverse order at the top of the <see cref="Stack{T}"/>.
		/// </summary>
		/// <param name="stack"></param>
		/// <param name="count">The number of objects that is going to be popped from the <see cref="Stack{T}"/>.</param>
		/// <returns>An array of objects removed from the top of the <see cref="Stack{T}"/>.</returns>
		/// <exception cref="ArgumentOutOfRangeException">if the requested amount is bigger than the amount of available items in the stack.</exception>
		public static object[] PopReverse(this Stack<object> stack, int count)
		{
			if (count > stack.Count)
				throw new ArgumentOutOfRangeException(nameof(count), "Cannot pop the specified amount of elements from the stack.");

			object[] retVal = new object[count];
			while (count-- > 0)
				retVal[count] = stack.Pop();

			return retVal;
		}

	}
}
