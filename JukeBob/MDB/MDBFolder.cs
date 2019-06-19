#region CopyRight 2017
/*
    Copyright (c) 2003-2017 Andreas Rohleder (andreas@rohleder.cc)
    All rights reserved
*/
#endregion
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
#region Authors & Contributors
/*
   Author:
     Andreas Rohleder <andreas@rohleder.cc>

   Contributors:
 */
#endregion Authors & Contributors

using Cave;
using Cave.Console;
using Cave.Data;
using Cave.IO;
using System;
using System.IO;

namespace JukeBob
{
    /// <summary>
    /// Provides a folder dataset
    /// </summary>
    [Table("Folders")]
    public struct MDBFolder : IXT
    {
        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID | FieldFlags.AutoIncrement)]
        public long ID;

		[Field]
		[DateTimeFormat(DateTimeKind.Utc, DateTimeType.Native)]
		public DateTime LastChecked;

        /// <summary>The name</summary>
        [Field(Flags = FieldFlags.Index)]
        [StringFormat(StringEncoding.UTF8)]
        public Utf8string Name;

        /// <summary>Gets the full path.</summary>
        /// <param name="mdb">The MDB.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.DirectoryNotFoundException">Parent Folder missing!</exception>
        public string GetFullPath(MusicDataBase mdb, string fileName)
        {
			if (fileName == null) throw new ArgumentNullException("fileName");
			return FileSystem.Combine(Name, fileName);
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return string.Format("Folder [{0}] {1}", ID, Name);
        }

		public XT ToXT()
		{
			return XT.Format("Folder [{0}] <cyan>{1}", ID, Name);
		}
	}
}
