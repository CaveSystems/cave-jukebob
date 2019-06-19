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
using System.Linq;
using Cave;
using Cave.Data;

namespace JukeBob
{
	/// <summary>
	/// Provides combined album information
	/// </summary>
	[Table("CombinedAlbums")]
    public struct RPCAlbum
    {
		/// <summary>Loads the specified album.</summary>
		/// <param name="mdb">The MDB.</param>
		/// <param name="album">The album.</param>
		/// <returns></returns>
		public static RPCAlbum Load(MusicDataBase mdb, MDBAlbum album)
        {
            MDBArtist artist = mdb.Artists.GetStruct(album.ArtistID);
            var files = mdb.AudioFiles.GetStructs(nameof(MDBAudioFile.AlbumID), album.ID);
            RPCAlbum result = new RPCAlbum()
            {
                ID = album.ID,
                Name = album.Name,
                ArtistID = artist.ID,
                ArtistName = artist.Name,
                Duration = new TimeSpan(files.Sum(f => f.Duration.Ticks)),
				Tags = files.SelectMany(f => f.TagNames).Distinct().Join(";"),
				Genres = files.SelectMany(f => f.GenreNames).Distinct().Join(";"),
				TrackCount = files.Count,
				Errors = (MDBMetaErrors)files.BinaryOr(f => (long)f.MetaErrors),
            };
			
            return result;      
        }

        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID | FieldFlags.AutoIncrement)]
        public long ID;

        /// <summary>The artist identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long ArtistID;

        /// <summary>The artist name</summary>
        [Field]
        public string ArtistName;

        /// <summary>The album name</summary>
        [Field]
        public string Name;

		/// <summary>Semicolon separated list of tags</summary>
		[Field]
		public string Tags;

		/// <summary>Semicolon separated list of genres</summary>
		[Field]
		public string Genres;

		/// <summary>The track count of the album</summary>
		[Field]
        public long TrackCount;

        /// <summary>The duration of the album</summary>
        [Field]
        public TimeSpan Duration;

		/// <summary>The errors found at the files belonging to this album</summary>
		[Field]
		public MDBMetaErrors Errors;
	}
}
