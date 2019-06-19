#region License AGPL
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
using Cave.Console;
using Cave.Data;
using Cave.IO;
using System;

namespace JukeBob
{
	/// <summary>
	/// Provides a file dataset
	/// </summary>
	[Table("Files")]
	public struct MDBFile : IXT
	{
		/// <summary>The identifier</summary>
		[Field(Flags = FieldFlags.ID | FieldFlags.AutoIncrement)]
		public long ID;

		/// <summary>The folder identifier</summary>
		[Field(Flags = FieldFlags.Index)]
		public long FolderID;

		/// <summary>The file type</summary>
		[Field]
		public MDBFileType FileType;

		/// <summary>The name</summary>
		[Field(Flags = FieldFlags.Index)]
		[StringFormat(StringEncoding.UTF8)]
		public Utf8string Name;

		/// <summary>The extension</summary>
		[Field]
		[StringFormat(StringEncoding.UTF8)]
		public Utf8string Extension;

		/// <summary>The size</summary>
		[Field]
		public long Size;

		/// <summary>The last change date time</summary>
		[Field]
		[DateTimeFormat(DateTimeKind.Utc, DateTimeType.BigIntTicks)]
		public DateTime DateTime;

		/// <summary>Gets a value indicating whether this instance is an image.</summary>
		/// <value><c>true</c> if this instance is an image; otherwise, <c>false</c>.</value>
		public bool IsImage
		{
			get
			{
				switch (FileType)
				{
					case MDBFileType.bmp:
					case MDBFileType.jpeg:
					case MDBFileType.png: return true;
					default: return false;
				}
			}
		}

		/// <summary>
		/// Gets the full path of the file
		/// </summary>
		/// <param name="mdb">The mdb instance</param>
		/// <returns></returns>
		public string GetFullPath(MusicDataBase mdb)
		{
			if (FolderID == 0)
			{
				if (ReferenceEquals(Extension, null)) return Name;
				return Name + Extension;
			}
			return mdb.Folders.GetStruct(FolderID).GetFullPath(mdb, Name + Extension);
		}

		/// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
		/// <returns>A <see cref="string" /> that represents this instance.</returns>
		public override string ToString()
		{
			return string.Format("File [{0}] {1}{2}", ID, Name, Extension);
		}

		public XT ToXT()
		{
			return XT.Format("File [{0}] <cyan>{1}{2}", ID, Name, Extension);
		}
	}
}
