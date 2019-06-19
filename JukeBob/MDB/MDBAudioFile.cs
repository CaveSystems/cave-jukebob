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
using System.Text;
using Cave;
using Cave.Collections.Generic;
using Cave.Console;
using Cave.Data;
using Cave.IO;

namespace JukeBob
{
	/// <summary>
	/// Audio File dataset
	/// </summary>
	/// <seealso cref="IXT" />
	[Table("AudioFiles")]
    public struct MDBAudioFile : IXT
    {
        /// <summary>The file identifier</summary>
        [Field(Flags = FieldFlags.ID)]
        public long FileID;

        /// <summary>The category identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long CategoryID;

        /// <summary>The artist identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long AlbumArtistID;

        /// <summary>The artist identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long SongArtistID;

        /// <summary>The album identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long AlbumID;

        /// <summary>The genre identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long GenreID;

        /// <summary>The tag identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long TagID;

        /// <summary>The recording date</summary>
        [Field]
        [DateTimeFormat(DateTimeKind.Local, DateTimeType.Native)]
        public DateTime RecordingDate;

        /// <summary>The genres as semicolon seperated list</summary>
        [Field(Flags = FieldFlags.None)]
        [StringFormat(StringEncoding.UTF8)]
        public Utf8string Genres;

        /// <summary>The tags as semicolon seperated list</summary>
        [Field(Flags = FieldFlags.None)]
        [StringFormat(StringEncoding.UTF8)]
        public Utf8string Tags;

        /// <summary>The title</summary>
        [Field(Flags = FieldFlags.Index, Length = 128)]
        [StringFormat(StringEncoding.UTF8)]
        public Utf8string Title;

        /// <summary>
        /// AcoustID.org Id
        /// </summary>
        [Field(Length = 36)]
        [StringFormat(StringEncoding.ASCII)]
        public Guid AcousticGuid;

        /// <summary>The track number</summary>
        [Field]
        public int TrackNumber;

        /// <summary>The track count</summary>
        [Field]
        public int TrackCount;

        /// <summary>The duration</summary>
        [Field]
        [TimeSpanFormat(DateTimeType.BigIntTicks)]
        public TimeSpan Duration;

        /// <summary>The decoding errors</summary>
        [Field]
        public int Errors;

        /// <summary>The meta errors</summary>
        [Field]
        public MDBMetaErrors MetaErrors;

        /// <summary>Gets or sets the genre names.</summary>
        /// <value>The genre names.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">value;Value may not contain separators!</exception>
        public string[] GenreNames
        {
            get
            {
				if (ReferenceEquals(Genres, null)) return new string[0];
				return Genres.ToString().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
            set
            {
                var items = new Set<string>();
                foreach (string genre in value)
                {
                    if (genre.IndexOfAny(new char[] { ';', ',' }) > -1) throw new ArgumentOutOfRangeException("value", "Value may not contain separators!");
                    string s = genre.Trim();
                    if (s.Length == 0) continue;
                    items.Include(s);
                }
                Genres = ';' + string.Join(";", items.ToArray()) + ';';
            }
        }

        /// <summary>Gets or sets the tag names.</summary>
        /// <value>The tag names.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">value;Value may not contain separators!</exception>
        public string[] TagNames
        {
            get
            {
				if (ReferenceEquals(Tags, null)) return new string[0];
				return Tags.ToString().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
            set
            {
                var items = new Set<string>();
                foreach (string tag in value)
                {
                    if (tag.IndexOfAny(new char[] { ';', ',' }) > -1) throw new ArgumentOutOfRangeException("value", "Value may not contain separators!");
                    string s = tag.Trim();
                    if (s.Length == 0) continue;
                    items.Include(s);
                }
                Tags = ';' + string.Join(";", items.ToArray()) + ';';
            }
        }

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
			return string.Format("AudioFile [{0}] {1}", FileID, Title);
		}

        /// <summary>Provides an eXtended Text string for this object.</summary>
        /// <returns>Returns a new XT instance with the description of this object.</returns>
        public XT ToXT()
        {
			return XT.Format("AudioFile [{0}] <cyan>{1}", FileID, Title);
        }
    }
}