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
using Cave;
using Cave.Data;
using Cave.Web;

namespace JukeBob
{
    /// <summary>
    /// The mdb overview 
    /// </summary>
    [Table("Overview")]
	public struct MDBOverview
	{
		internal static MDBOverview Create(MusicDataBase mdb, WebServer server)
		{
			double fileSize = mdb.Files.Sum(nameof(MDBFile.Size));
			double audioFilesSize = mdb.Files.Sum(nameof(MDBFile.Size), Search.FieldEquals(nameof(MDBFile.FileType), MDBFileType.mp3));
			double imageFilesSize = mdb.Files.Sum(nameof(MDBFile.Size), !Search.FieldEquals(nameof(MDBFile.FileType), MDBFileType.mp3) & !Search.FieldEquals(nameof(MDBFile.FileType), MDBFileType.unknown));
            TimeSpan audiofileDuration = TimeSpan.FromSeconds(Convert.ToDouble(mdb.AudioFiles.Sum(nameof(MDBAudioFile.Duration))));

            return new MDBOverview()
			{
				ID = 1 + Math.Abs(mdb.Files.SequenceNumber),
				LastUpdate = DateTime.Now,
                Version = server.ServerVersionString,
                AlbumCount = mdb.Albums.RowCount,
				ArtistCount = mdb.Artists.RowCount,
				AudioDataSize = (long)audioFilesSize,
				AudioDataSizeString = audioFilesSize.FormatSize(),
				AudioFileCount = mdb.AudioFiles.RowCount,
				Duration = audiofileDuration,
				DurationString = audiofileDuration.FormatTime(),
				FileCount = mdb.Files.RowCount,
				GenreCount = mdb.Genres.RowCount,
				ImageCount = mdb.Images.RowCount,
				ImageDataSize = (long)imageFilesSize,
				ImageDataSizeString = imageFilesSize.FormatSize(),
				TagCount = mdb.Tags.RowCount,
        };
		}

		/// <summary>The identifier</summary>
		[Field(Flags = FieldFlags.ID)]
		public long ID;

		/// <summary>The last update date time</summary>
		[Field]
        [DateTimeFormat(DateTimeKind.Utc, DateTimeType.BigIntTicks)]
        public DateTime LastUpdate;

        [Field]
        public string Version;

		/// <summary>The all files duration</summary>
		[Field]
        [TimeSpanFormat(DateTimeType.BigIntTicks)]
        public TimeSpan Duration;

		/// <summary>The all files duration string</summary>
		[Field]
		public string DurationString;

		/// <summary>The audio data size</summary>
		[Field]
		public long AudioDataSize;

		/// <summary>The audio data size string</summary>
		[Field]
		public string AudioDataSizeString;

		/// <summary>The image data size</summary>
		[Field]
		public long ImageDataSize;

		/// <summary>The image count</summary>
		[Field]
		public long ImageCount;

		/// <summary>The image data size string</summary>
		[Field]
		public string ImageDataSizeString;

		/// <summary>The album count</summary>
		[Field]
		public long AlbumCount;

		/// <summary>The artist count</summary>
		[Field]
		public long ArtistCount;

		/// <summary>The file count</summary>
		[Field]
		public long FileCount;

		/// <summary>The audio file count</summary>
		[Field]
		public long AudioFileCount;

		/// <summary>The genre count</summary>
		[Field]
		public long GenreCount;

		/// <summary>The tag count</summary>
		[Field]
		public long TagCount;

	}
}
