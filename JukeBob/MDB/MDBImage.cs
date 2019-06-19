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
using System.Drawing;

namespace JukeBob
{
    /// <summary>
    /// Provides an image dataset
    /// </summary>
    /// <seealso cref="IXT" />
    /// <seealso cref="IComparable{MDBImage}" />
    [Table("Images")]
    public struct MDBImage : IXT, IComparable<MDBImage>
    {
        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID | FieldFlags.AutoIncrement)]
        public long FileID;

        /// <summary>The album/artist unique identifier</summary>
        [Field(Flags = FieldFlags.Index, Length = 36)]
        [StringFormat(StringEncoding.ASCII)]
        public BinaryGuid MusicBrainzGuid;

        /// <summary>The type</summary>
        [Field]
        public MDBImageType Type;

        /// <summary>The MIME type</summary>
        [Field(Length = 34)]
        [StringFormat(StringEncoding.ASCII)]
        public Utf8string MimeType;

        /// <summary>The width</summary>
        [Field]
        public int Width;

        /// <summary>The height</summary>
        [Field]
        public int Height;

        /// <summary>Gets a value indicating whether this instance is album art.</summary>
        /// <value><c>true</c> if this instance is album art; otherwise, <c>false</c>.</value>
        public bool IsAlbumArt
        {
            get
            {
                switch (Type)
                {
					case MDBImageType.UserCover:
                    case MDBImageType.AlbumCover:
                    case MDBImageType.AlbumCDArt:
                    case MDBImageType.AlbumCoverFront:
                        return true;
                    default: return false;
                }
            }
        }

        /// <summary>Gets a value indicating whether this instance is artist art.</summary>
        /// <value><c>true</c> if this instance is artist art; otherwise, <c>false</c>.</value>
        public bool IsArtistArt
        {
            get
            {
                switch (Type)
                {
                    case MDBImageType.ArtistBackground:
                    case MDBImageType.ArtistMusicBanner:
                    case MDBImageType.ArtistMusicLogo:
                    case MDBImageType.ArtistMusicLogoHD:
                    case MDBImageType.ArtistThumb:
                        return true;
                    default: return false;
                }
            }
        }

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
			return string.Format("Image [{0}] {1} {2}x{3}", FileID, Type, Width, Height);
		}

		/// <summary>Provides an eXtended Text string for this object.</summary>
		/// <returns>Returns a new XT instance with the description of this object.</returns>
		public XT ToXT()
        {
			return XT.Format("Image [{0}] <cyan>{1} <magenta>{2}<default>x<magenta>{3}", FileID, Type, Width, Height);
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="other">An object to compare with this instance.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="other" /> in the sort order.  Zero This instance occurs in the same position in the sort order as <paramref name="other" />. Greater than zero This instance follows <paramref name="other" /> in the sort order.
        /// </returns>
        public int CompareTo(MDBImage other)
        {
            return Type.CompareTo(other.Type) * 100 + (Width * Height).CompareTo(other.Width * other.Height);
        }
    }
}
