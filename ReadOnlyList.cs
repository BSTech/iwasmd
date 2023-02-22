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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iwasmd
{
	/// <summary>
	/// A true read-only wrapper for the <see cref="List{T}"/> class.
	/// </summary>
	/// <typeparam name="T">The type of the underlying <see cref="List{T}"/> item.</typeparam>
	public class ReadOnlyList<T> : IEnumerable<T>
	{
		private readonly List<T> m_List;

		/// <summary>
		/// Creates the wrapper on a list.
		/// </summary>
		/// <param name="list">The list to be wrapped.</param>
		public ReadOnlyList(List<T> list) => m_List = list;

		public int Count => m_List.Count;

		/// <summary>
		/// Gets the element at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the element to get.</param>
		/// <returns>The element at the specified index.</returns>
		public T this[int index] => m_List[index];

		public static implicit operator ReadOnlyList<T>(List<T> list) => new ReadOnlyList<T>(list);

		public IEnumerator<T> GetEnumerator() => m_List.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => m_List.GetEnumerator();
	}
}
