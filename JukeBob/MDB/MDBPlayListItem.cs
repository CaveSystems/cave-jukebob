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

using System;
using Cave;
using Cave.Data;

namespace JukeBob
{
    /// <summary>
    /// Provides playlist items
    /// </summary>
    [Table("PlayListItems")]
    public struct MDBPlayListItem
    {
        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID | FieldFlags.AutoIncrement)]
        public long ID;

        /// <summary>The playlist type (1 = JukeBob, 2 = Karaoke)</summary>
        [Field(Flags = FieldFlags.Index)]
        public long StreamID;

		/// <summary>The date time this file was added to the list</summary>
		[Field(Flags = FieldFlags.Index)]
		[DateTimeFormat(DateTimeKind.Utc, DateTimeType.BigIntTicks)]
		public DateTime Added;

		/// <summary>The owner identifier</summary>
		[Field]
        public long OwnerID;

		/// <summary>The subset identifier</summary>
		[Field]
		public long SubsetID;

		/// <summary>The audio file identifier</summary>
		[Field(Flags = FieldFlags.Index)]
        public long AudioFileID;
	}
}
