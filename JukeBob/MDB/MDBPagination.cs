﻿#region License AGPL
/*
    This program/library/sourcecode is free software; you can redistribute it
    and/or modify it under the terms of the GNU Affero General Public License
    version 3 as published by the Free Software Foundation subsequent called
    the License.

    You may not use this program/library/sourcecode except in compliance
    with the License. The License is included in the LICENSE.AGPL30 file
    found at the installation directory or the distribution package.

    The above copyright notice and this permission notice shall be included
    in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
    LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
    OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
    WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion License

using Cave;
using Cave.Data;

namespace JukeBob
{
	/// <summary>
	/// Provides pagination for results
	/// </summary>
	[Table("Pagination")]
	public struct MDBPagination
	{
		/// <summary>The identifier</summary>
		[Field(Flags = FieldFlags.ID)]
		public long ID;

		/// <summary>The page number</summary>
		[Field]
		public int Page;

		/// <summary>The page count</summary>
		[Field]
		public int PageCount;

		/// <summary>The rows per page</summary>
		[Field]
		public int RowsPerPage;

		/// <summary>The row count</summary>
		[Field]
		public long RowCount;
	}
}
