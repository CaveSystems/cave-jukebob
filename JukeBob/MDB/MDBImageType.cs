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

namespace JukeBob
{
    /// <summary>
    /// Provides available image types
    /// </summary>
    public enum MDBImageType
    {
        /// <summary>undefined</summary>
        Undefined = 0,

        /// <summary>The artist background images (FanArtTV)</summary>
        ArtistBackground,

        /// <summary>The artist thumb images (FanArtTV)</summary>
        ArtistThumb,

        /// <summary>The artist music logo hd images (FanArtTV)</summary>
        ArtistMusicLogoHD,

        /// <summary>The artist music logo images (FanArtTV)</summary>
        ArtistMusicLogo,

        /// <summary>The artist music banner images (FanArtTV)</summary>
        ArtistMusicBanner,

        /// <summary>The album cover front images (CoverArtArchive)</summary>
        AlbumCoverFront,

        /// <summary>The album cover images (FanArtTV)</summary>
        AlbumCover,

        /// <summary>The album cd art images (FanArtTV)</summary>
        AlbumCDArt,

		/// <summary>
		/// The album cover images (User)
		/// </summary>
		UserCover,
    }
}
