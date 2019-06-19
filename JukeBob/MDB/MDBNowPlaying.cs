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

namespace JukeBob
{
    /// <summary>
    /// Provides a now playing dataset
    /// </summary>
    [Table("NowPlaying")]
    public struct MDBNowPlaying : IXT
    {
		/// <summary>Creates the specified MDB.</summary>
		/// <param name="mdb">The MDB.</param>
		/// <param name="streamID">The stream identifier.</param>
		/// <param name="ownerID">The owner identifier (+user / -session / +subset).</param>
		/// <param name="subsetID">The subset identifier.</param>
		/// <param name="startTime">The start time.</param>
		/// <param name="audioFile">The audio file.</param>
		/// <returns></returns>
		public static MDBNowPlaying Create(MusicDataBase mdb, long streamID, long ownerID, long subsetID, DateTime startTime, MDBAudioFile audioFile)
        {
            var albumArtist = mdb.Artists.TryGetStruct(audioFile.AlbumArtistID);
            var artist = mdb.Artists.TryGetStruct(audioFile.SongArtistID);
            var album = mdb.Albums.TryGetStruct(audioFile.AlbumID);
            var genre = mdb.Genres.TryGetStruct(audioFile.GenreID);
            var tag = mdb.Tags.TryGetStruct(audioFile.TagID);

            return new MDBNowPlaying()
            {
                StreamID = streamID,
                OwnerID = ownerID,
				SubsetID = subsetID,
                AudioFileID = audioFile.FileID,
                Duration = audioFile.Duration,
                StartDateTime = startTime,
                Title = audioFile.Title,
                ArtistName = artist.Name,
                AlbumArtistName = albumArtist.Name,
                AlbumName = album.Name,
                Genre = genre.Name,
                Genres = audioFile.Genres,
                Tag = tag.Name,
                Tags = audioFile.Tags,
            };
        }

        /// <summary>The playlist identifier</summary>
        [Field(Flags = FieldFlags.ID)]
        public long StreamID;

        /// <summary>The owner identifier</summary>
        [Field]
        public long OwnerID;

		/// <summary>The subset identifier</summary>
		[Field]
		public long SubsetID;

		/// <summary>The audio file identifier</summary>
		[Field]
        public long AudioFileID;

        /// <summary>The start date time</summary>
        [Field]
        [DateTimeFormat(DateTimeKind.Utc, DateTimeType.BigIntTicks)]
        public DateTime StartDateTime;

		/// <summary>The update date time</summary>
		[Field]
		[DateTimeFormat(DateTimeKind.Utc, DateTimeType.BigIntTicks)]
		public DateTime UpdateDateTime;

		/// <summary>The duration</summary>
		[Field]
        public TimeSpan Duration;

        /// <summary>The title</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string Title;

        /// <summary>The artist name</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string ArtistName;

        /// <summary>The album artist name</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string AlbumArtistName;

        /// <summary>The album name</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string AlbumName;

        /// <summary>The main genre</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string Genre;

        /// <summary>The genre list this title may belong to</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string Genres;

        /// <summary>The main tag</summary>
        [Field]
        [StringFormat(StringEncoding.UTF8)]
        public string Tag;

		/// <summary>The tags this title may belong to</summary>
		[Field]
        [StringFormat(StringEncoding.UTF8)]
        public string Tags;

        /// <summary>The position</summary>
        public TimeSpan Position { get { return DateTime.UtcNow - StartDateTime; } }

        /// <summary>Gets a value indicating whether this instance is playing in karaoke mode.</summary>
        /// <value><c>true</c> if this instance is playing in karaoke mode; otherwise, <c>false</c>.</value>
        public bool IsKaraoke { get { return Genre == "Karaoke"; } }

        /// <summary>Returns a hash code for this instance.</summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return StartDateTime.GetHashCode() ^ StreamID.GetHashCode() ^ AudioFileID.GetHashCode();
        }

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            return string.Format("NowPlaying [{0}]", Position.FormatTimeSpan(1));
        }

        /// <summary>Provides an eXtended Text string for this object.</summary>
        /// <returns>Returns a new XT instance with the description of this object.</returns>
        public XT ToXT()
        {
            return XT.Format("NowPlaying [{0}]", Position.FormatTimeSpan(1));
        }
    }
}
