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

namespace JukeBob
{
    /// <summary>
    /// provides meta errors
    /// </summary>
    [Flags]
    public enum MDBMetaErrors
    {
        /// <summary>No errors</summary>
        None = 0,

        /// <summary>The title is missing or invalid</summary>
        Title = 1 << 0,

        /// <summary>The artist is missing or invalid</summary>
        Artist = 1 << 1,

        /// <summary>The album is missing or invalid</summary>
        Album = 1 << 2,

        /// <summary>The genre is missing or invalid</summary>
        Genre = 1 << 3,

        /// <summary>The tag is missing or invalid</summary>
        Tag = 1 << 4,

        /// <summary>The music brainz title artist is missing or invalid</summary>
        MusicBrainzTitleArtist = 1 << 5,

        /// <summary>The music brainz album artist is missing or invalid</summary>
        MusicBrainzAlbumArtist = 1 << 6,

        /// <summary>The music brainz release group is missing or invalid</summary>
        MusicBrainzReleaseGroup = 1 << 7,

        /// <summary>The music brainz album is missing or invalid</summary>
        MusicBrainzAlbum = 1 << 8,

		/// <summary>The data is unclean, at least one parser error occured</summary>
		Unclean = 1 << 31,
    }
}