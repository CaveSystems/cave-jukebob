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

using System;
using System.Linq;
using Cave;
using Cave.Data;

namespace JukeBob
{
	/// <summary>
	/// Provides combined artist information
	/// </summary>
	[Table("CombinedArtists")]
    public struct RPCArtist
    {
		/// <summary>Loads the artist from the specified MusicDataBase tables.</summary>
		/// <param name="mdb">The MusicDataBase.</param>
		/// <param name="artist">The artist.</param>
		/// <returns>Returns a new <see cref="RPCArtist"/> instance</returns>
		public static RPCArtist Load(MusicDataBase mdb, MDBArtist artist)
        {
			var files = mdb.AudioFiles.GetStructs(
				Search.FieldEquals(nameof(MDBAudioFile.SongArtistID), artist.ID) |
				Search.FieldEquals(nameof(MDBAudioFile.AlbumArtistID), artist.ID));

			var result = new RPCArtist()
            {
                ID = artist.ID,
                Name = artist.Name,
				Errors = (MDBMetaErrors)files.BinaryOr(f => (long)f.MetaErrors),
				TitleCount = files.Count,
				Tags = files.SelectMany(f => f.TagNames).Distinct().Join(";"),
				Genres = files.SelectMany(f => f.GenreNames).Distinct().Join(";"),
				Duration = new TimeSpan(files.Sum(f => f.Duration.Ticks)),
			};
            result.AlbumCount = mdb.Albums.Count(nameof(MDBAlbum.ArtistID), artist.ID);
			return result;
        }

        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID)]
        public long ID;

        /// <summary>The name</summary>
        [Field]
        public string Name;

        /// <summary>The album count of this artist</summary>
        [Field]
        public long AlbumCount;

		/// <summary>The title count of this artist</summary>
		[Field]
        public long TitleCount;

		/// <summary>Semicolon separated list of tags</summary>
		[Field]
		public string Tags;

		/// <summary>Semicolon separated list of genres</summary>
		[Field]
		public string Genres;

		/// <summary>The duration for all titles of this artist</summary>
		[Field]
        [TimeSpanFormat(DateTimeType.BigIntTicks)]
        public TimeSpan Duration;

		/// <summary>The errors found at titles of this artist</summary>
		[Field]
		public MDBMetaErrors Errors;
	}
}
