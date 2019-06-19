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
using System.IO;
using System.Linq;
using Cave;
using Cave.Auth;
using Cave.IO;
using Cave.Logging;
using Cave.Web.Auth;

namespace JukeBob
{
	/// <summary>
	/// Provides the web interface for all functions
	/// </summary>
	/// <seealso cref="Cave.Logging.ILogSource" />
	public sealed partial class WebInterface : ILogSource
	{
		/// <summary>Initializes a new instance of the <see cref="WebInterface" /> class.</summary>
		/// <param name="mdb">The MusicDataBase instance.</param>
		/// <param name="authTables">The authentication tables.</param>
		/// <param name="player">The player.</param>
		/// <exception cref="System.ArgumentNullException">
		/// mdb
		/// or
		/// authTables
		/// </exception>
		/// <exception cref="ArgumentNullException">mdb
		/// or
		/// player</exception>
		public WebInterface(MusicDataBase mdb, AuthTables authTables, IPlayer player)
		{
			FileCrawler = new FileCrawler(mdb);
			ArtCrawler = new ArtCrawler(mdb);
			
			this.mdb = mdb ?? throw new ArgumentNullException(nameof(mdb));
			this.authTables = authTables ?? throw new ArgumentNullException(nameof(authTables));
			this.player = player ?? throw new ArgumentNullException(nameof(player));

			EmptyImage = WebImage.FromFile(FileSystem.Combine(mdb.WebFolder, "images", "empty.png"), mdb.CacheFolder);
			this.LogInfo("Loading album replacement images...");
			ReplaceAlbumImages = Directory.GetFiles(FileSystem.Combine(mdb.WebFolder, "images"), "cd-??.png").Select(f => WebImage.FromFile(f, mdb.CacheFolder)).ToArray();
			if (ReplaceAlbumImages.Length == 0)
			{
				ReplaceAlbumImages = new WebImage[] { WebImage.FromFile(FileSystem.Combine(mdb.WebFolder, "images", "no-image.png"), mdb.CacheFolder) };
			}

			this.LogInfo("Loading artist replacement images...");
			ReplaceArtistImages = Directory.GetFiles(FileSystem.Combine(mdb.WebFolder, "images"), "artist-??.png").Select(f => WebImage.FromFile(f, mdb.CacheFolder)).ToArray();
			if (ReplaceArtistImages.Length == 0)
			{
				ReplaceArtistImages = new WebImage[] { WebImage.FromFile(FileSystem.Combine(mdb.WebFolder, "images", "no-image.png"), mdb.CacheFolder) };
			}
		}

		/// <summary>Gets or sets the rows per page.</summary>
		/// <value>The rows per page.</value>
		public int RowsPerPage { get; set; } = 30;

		/// <summary>Gets or sets the replace album images.</summary>
		/// <value>The replace album images.</value>
		public WebImage[] ReplaceAlbumImages { get; set; }

		/// <summary>Gets or sets the not available image.</summary>
		/// <value>The not available image.</value>
		public WebImage[] ReplaceArtistImages { get; set; }

		/// <summary>Gets or sets the empty image.</summary>
		/// <value>The empty image.</value>
		public WebImage EmptyImage { get; set; }

		public IMDBCrawler FileCrawler { get; set; } 
		public IMDBCrawler ArtCrawler { get; set; } 

		/// <summary>Gets the name of the log source.</summary>
		/// <value>The name of the log source.</value>
		public string LogSourceName => "WebInterface";
	}
}