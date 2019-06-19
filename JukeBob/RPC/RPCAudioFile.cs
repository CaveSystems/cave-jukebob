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
using Cave.Data;
using System;

namespace JukeBob
{
	/// <summary>
	/// Provides combined audio file information
	/// </summary>
	[Table("CombinedAudioFiles")]
    public struct RPCAudioFile
    {
        /// <summary>Loads the dataset using the specified MDB instance.</summary>
        /// <param name="mdb">The MDB.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public static RPCAudioFile Load(MusicDataBase mdb, MDBAudioFile file)
        {
            MDBArtist albumArtist, titleArtist;
            MDBAlbum album;
            MDBCategory category;
            MDBGenre genre;
            MDBTag tag;
            mdb.Artists.TryGetStruct(file.AlbumArtistID, out albumArtist);
            mdb.Artists.TryGetStruct(file.SongArtistID, out titleArtist);
            mdb.Albums.TryGetStruct(file.AlbumID, out album);
            mdb.Categories.TryGetStruct(file.CategoryID, out category);
            mdb.Genres.TryGetStruct(file.GenreID, out genre);
            mdb.Tags.TryGetStruct(file.TagID, out tag);
            return new RPCAudioFile()
            {
                AlbumID = album.ID,
                AlbumName = album.Name,
                AlbumArtistID = albumArtist.ID,
                AlbumArtistName = albumArtist.Name,
                TitleArtistID = titleArtist.ID,
                TitleArtistName = titleArtist.Name,
                AudioFileID = file.FileID,
                Category = category.Name,
                Duration = file.Duration,
                Errors = file.Errors,
                Genre = genre.Name,
                Genres = file.Genres,
                MetaErrors = file.MetaErrors,
                RecordingDate = file.RecordingDate,
                Tag = tag.Name,
                Tags = file.Tags,
                TrackCount = file.TrackCount,
                TrackNumber = file.TrackNumber,
                Title = file.Title,
            };
        }

        /// <summary>Loads the dataset using the specified MDB instance.</summary>
        /// <param name="mdb">The MDB.</param>
        /// <param name="audioFileID">The audio file identifier.</param>
        /// <returns></returns>
        public static RPCAudioFile Load(MusicDataBase mdb, long audioFileID)
        {
            return Load(mdb, mdb.AudioFiles[audioFileID]);
        }

        /// <summary>The audio file identifier</summary>
        [Field(Flags = FieldFlags.ID)]
        public long AudioFileID;

        /// <summary>The title</summary>
        [Field]
        public string Title;

        /// <summary>The album artist identifier</summary>
        [Field]
        public long AlbumArtistID;

        /// <summary>The album artist name</summary>
        [Field]
        public string AlbumArtistName;

		/// <summary>The title artist identifier</summary>
		[Field]
        public long TitleArtistID;

		/// <summary>The title artist name</summary>
		[Field]
        public string TitleArtistName;

        /// <summary>The album identifier</summary>
        [Field]
        public long AlbumID;

        /// <summary>The album name</summary>
        [Field]
        public string AlbumName;

        /// <summary>The genre</summary>
        [Field]
        public string Genre;

        /// <summary>The category</summary>
        [Field]
        public string Category;

        /// <summary>The tag</summary>
        [Field]
        public string Tag;

        /// <summary>The genres</summary>
        [Field]
        public string Genres;

        /// <summary>The tags</summary>
        [Field]
        public string Tags;

        /// <summary>The recording date</summary>
        [Field]
        [DateTimeFormat(DateTimeKind.Local, DateTimeType.Native)]
        public DateTime RecordingDate;

        /// <summary>The track number</summary>
        [Field]
        public int TrackNumber;

        /// <summary>The track count</summary>
        [Field]
        public int TrackCount;

        /// <summary>The duration</summary>
        [Field]
        public TimeSpan Duration;

        /// <summary>The decoding errors</summary>
        [Field]
        public int Errors;

        /// <summary>The meta errors</summary>
        [Field]
        public MDBMetaErrors MetaErrors;
    }
}
