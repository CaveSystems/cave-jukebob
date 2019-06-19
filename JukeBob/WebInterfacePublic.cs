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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Cave;
using Cave.Auth;
using Cave.Collections.Generic;
using Cave.Data;
using Cave.IO;
using Cave.Web;
using Cave.Web.Auth;

namespace JukeBob
{
	partial class WebInterface
    {
		// ---------------------------------------------- /mdb/album

		#region /mdb/album

		/// <summary>Searches for an albums matching the specified criteria.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="albumID">The album identifier.</param>
		/// <remarks>Returns <see cref="RPCAlbum" /></remarks>
		[WebPage(Paths = "/mdb/album")]
		public void GetAlbum(WebData webData, long albumID)
		{
			var result = RPCAlbum.Load(mdb, mdb.Albums.TryGetStruct(albumID));
			webData.Result.AddMessage(webData.Method, "Retrieved Album.");
			webData.Result.AddStruct(result);
		}

		/// <summary>Performs an autocompletion lookup at the album database table for the specified search term.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="term">The term.</param>
		/// <remarks>Returns a json array with album names</remarks>
		[WebPage(Paths = "/mdb/album/autocomplete", AllowAnyParameters = true)]
		public void AutoCompleteAlbum(WebData webData, string term = null)
		{
			ITable table = mdb.Albums;
			string nameField = nameof(MDBAlbum.Name);
			string guidField = nameof(MDBAlbum.MusicBrainzReleaseGroupGuid);
			FilterList(webData, table, nameField, guidField, term);
		}

		/// <summary>Searches for albums matching the specified criteria.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="artistID">The artist identifier.</param>
		/// <param name="filter">The filter applied to the album names. This may contain % as wildcard.</param>
		/// <param name="page">The page.</param>
		/// <param name="categoryID">The category identifier.</param>
		/// <param name="genreID">The genre identifier.</param>
		/// <param name="tagID">The tag identifier.</param>
		/// <param name="genre">The genres to search for.</param>
		/// <param name="tag">The tags to search for.</param>
		/// <remarks>Returns <see cref="RPCAlbum" /> (Albums)</remarks>
		[WebPage(Paths = "/mdb/album/search")]
		public void SearchAlbums(WebData webData, long artistID = 0, string filter = null, int page = 0, long categoryID = 0, long genreID = 0, long tagID = 0, string genre = null, string tag = null)
		{
			ICollection<long> albumIDs = null;

			//select audio files
			if (genreID != 0 || categoryID != 0 || tagID != 0 || genre != null || tag != null)
			{
				Search s = Search.None;
				if (genreID != 0) s &= Search.FieldEquals(nameof(MDBAudioFile.GenreID), genreID);
				if (tagID != 0) s &= Search.FieldEquals(nameof(MDBAudioFile.TagID), tagID);
				if (genre != null) s &= Search.FieldLike(nameof(MDBAudioFile.Genres), MDBSearch.Text("%" + genre + "%"));
				if (tag != null) s &= Search.FieldLike(nameof(MDBAudioFile.Tags), MDBSearch.Text("%" + tag + "%"));
				if (categoryID > 0) s &= GetCategorySearch(categoryID);
				int fieldIndex = mdb.AudioFiles.Layout.GetFieldIndex(nameof(MDBAudioFile.AlbumID));
				albumIDs = mdb.AudioFiles.GetRows(s).Select(r => (long)r.GetValue(fieldIndex)).ToList();
			}

			//select artists            
			IList<MDBAlbum> albums;
			long rowCount;
			{
				Search search = Search.None;
				if (filter != null)
				{
					search &= Search.FieldLike(nameof(MDBAlbum.Name), MDBSearch.Text("%" + filter + "%"));
				}
				if (albumIDs != null)
				{
					search &= Search.FieldIn(nameof(MDBAlbum.ID), albumIDs);
				}
				if (artistID != 0)
				{
					search &= Search.FieldEquals(nameof(MDBAlbum.ArtistID), artistID);
				}
				if (search.Mode == SearchMode.None)
				{
					rowCount = mdb.Albums.RowCount;
				}
				else
				{
					rowCount = mdb.Albums.Count(search);
				}
				albums = mdb.Albums.GetStructs(search, ResultOption.SortAscending(nameof(MDBAlbum.Name)) + ResultOption.Offset(page * RowsPerPage) + ResultOption.Limit(RowsPerPage));
			}

			//join
			var result = albums.Select(i => RPCAlbum.Load(mdb, i));
			//return
			webData.Result.AddMessage(webData.Method, "Retrieved Albums.");
			AddPagination(webData, page, rowCount);
			webData.Result.AddStructs(result);
		}

		#endregion

		// ---------------------------------------------- /mdb/artist

		#region /mdb/artist

		/// <summary>Searches for artists matching the specified criteria.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="artistID">The artist identifier.</param>
		/// <remarks>Returns <see cref="RPCArtist" /> (Artists)</remarks>
		[WebPage(Paths = "/mdb/artist")]
		public void GetArtist(WebData webData, long artistID)
		{
			var result = RPCArtist.Load(mdb, mdb.Artists.TryGetStruct(artistID));
			webData.Result.AddMessage(webData.Method, "Retrieved Artist.");
			webData.Result.AddStruct(result);
		}

		/// <summary>Performs an autocompletion lookup at the album database table for the specified search term.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="term">The term.</param>
		/// <remarks>Returns a json array with artist names</remarks>
		[WebPage(Paths = "/mdb/artist/autocomplete", AllowAnyParameters = true)]
		public void AutoCompleteArtist(WebData webData, string term = null)
		{
			ITable table = mdb.Artists;
			string nameField = nameof(MDBArtist.Name);
			string guidField = nameof(MDBArtist.MusicBrainzArtistGuid);
			FilterList(webData, table, nameField, guidField, term);
		}

		/// <summary>Searches for artists matching the specified criteria.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="filter">The filter applied to the artist names. This may contain % as wildcard.</param>
		/// <param name="page">The page.</param>
		/// <param name="categoryID">The category identifier.</param>
		/// <param name="genreID">The genre identifier.</param>
		/// <param name="tagID">The tag identifier.</param>
		/// <param name="genre">The genres to search for.</param>
		/// <param name="tag">The tags to search for.</param>
		/// <remarks>Returns <see cref="RPCArtist" /> (Artists)</remarks>
		[WebPage(Paths = "/mdb/artist/search")]
		public void SearchArtists(WebData webData, string filter = null, int page = 0, long categoryID = 0, long genreID = 0, long tagID = 0, string genre = null, string tag = null)
		{
			ICollection<long> artistIDs = null;

			//select audio files
			if (genreID != 0 || categoryID != 0 || tagID != 0 || genre != null || tag != null)
			{
				Search s = Search.None;
				if (genreID != 0) s &= Search.FieldEquals(nameof(MDBAudioFile.GenreID), genreID);
				if (tagID != 0) s &= Search.FieldEquals(nameof(MDBAudioFile.TagID), tagID);
				if (genre != null) s &= Search.FieldLike(nameof(MDBAudioFile.Genres), MDBSearch.Text("%" + genre + "%"));
				if (tag != null) s &= Search.FieldLike(nameof(MDBAudioFile.Tags), MDBSearch.Text("%" + tag + "%"));
				if (categoryID > 0) s &= GetCategorySearch(categoryID);
				int fieldIndex = mdb.AudioFiles.Layout.GetFieldIndex(nameof(MDBAudioFile.SongArtistID));
				artistIDs = mdb.AudioFiles.GetRows(s).Select(r => (long)r.GetValue(fieldIndex)).ToList();
			}

			//select artists            
			IList<MDBArtist> artists;
			long rowCount;
			{
				Search search = Search.None;
				if (filter != null)
				{
					search &= Search.FieldLike(nameof(MDBArtist.Name), MDBSearch.Text("%" + filter + "%"));
				}
				if (artistIDs != null)
				{
					search &= Search.FieldIn(nameof(MDBArtist.ID), artistIDs);
				}
				if (search.Mode == SearchMode.None)
				{
					rowCount = mdb.Artists.RowCount;
				}
				else
				{
					rowCount = mdb.Artists.Count(search);
				}
				artists = mdb.Artists.GetStructs(search, ResultOption.SortAscending(nameof(MDBArtist.Name)) + ResultOption.Offset(page * RowsPerPage) + ResultOption.Limit(RowsPerPage));
			}

			var result = artists.Select(i => RPCArtist.Load(mdb, i));
			webData.Result.AddMessage(webData.Method, "Retrieved Artists with filter.");
			AddPagination(webData, page, rowCount);
			webData.Result.AddStructs(result);
		}

		#endregion

		// ---------------------------------------------- /mdb/list

		#region /mdb/list

		/// <summary>Retrieves the database overview.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="displayStrings">if set to <c>true</c> return strings instead of long values.</param>
		/// <remarks>Returns <see cref="MDBOverview" /> (Overview) with long or string vales</remarks>
		[WebPage(Paths = "/mdb/list/overview")]
		public void GetOverviewList(WebData webData, bool displayStrings = false)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved Overview.");
			webData.Result.AddStruct(GetOverview(mdb, webData.Server));
		}

        /// <summary>Retrieves all categories, tags and genres available.</summary>
        /// <param name="webData">The web data.</param>
        /// <remarks>Returns <see cref="MDBCategory"/>, <see cref="MDBTag"/> and <see cref="MDBGenre"/></remarks>
        [WebPage(Paths = "/mdb/list/selectors")]
		public void GetSelectorsList(WebData webData)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved categories, Tags and Genres.");
			webData.Result.AddStructs(mdb.Categories.GetStructs(resultOption: ResultOption.SortAscending(nameof(MDBCategory.Name))));
			webData.Result.AddStructs(mdb.Tags.GetStructs(resultOption: ResultOption.SortAscending(nameof(MDBTag.Name))));
			webData.Result.AddStructs(mdb.Genres.GetStructs(resultOption: ResultOption.SortAscending(nameof(MDBGenre.Name))));
		}

		/// <summary>Gets the categories matching a name filter.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="filter">The filter applied to the category names. This may contain % as wildcard.</param>
		/// <param name="page">The page.</param>
		/// <remarks>Returns <see cref="MDBCategory" /> (Categories)</remarks>
		[WebPage(Paths = "/mdb/list/categories")]
		public void GetCategoriesList(WebData webData, string filter = null, int page = 0)
		{
			FilterSimple(webData, mdb.Categories, nameof(MDBCategory.Name), filter, page);
		}

		/// <summary>Gets genres matching a name filter.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="filter">The filter applied to the genre names. This may contain % as wildcard.</param>
		/// <param name="page">The page.</param>
		/// <remarks>Returns <see cref="MDBGenre" /> (Genres)</remarks>
		[WebPage(Paths = "/mdb/list/genres")]
		public void GetGenresList(WebData webData, string filter = null, int page = 0)
		{
			FilterSimple(webData, mdb.Genres, nameof(MDBGenre.Name), filter, page);
		}

		/// <summary>Gets tags matching a name filter.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="filter">The filter applied to the tag names. This may contain % as wildcard.</param>
		/// <param name="page">The page.</param>
		/// <remarks>Returns <see cref="MDBTag" /> (Tags)</remarks>
		[WebPage(Paths = "/mdb/list/tags")]
		public void GetTagsList(WebData webData, string filter = null, int page = 0)
		{
			FilterSimple(webData, mdb.Tags, nameof(MDBTag.Name), filter, page);
		}

		#endregion

		// ---------------------------------------------- /mdb/image

		#region /mdb/image

		/// <summary>Gets a specified image or a replacement if the image cannot be found.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="imageID">The image identifier.</param>
		/// <param name="thumb">if set to <c>true</c> [return thumbnail].</param>
		/// <remarks>Returns an image of mimetype jpeg or png</remarks>
		[WebPage(Paths = "/mdb/image/get")]
		public void GetImage(WebData webData, long imageID, bool thumb = false)
		{
			try
			{
				MDBImage img = mdb.Images.TryGetStruct(imageID);
				MDBFile file = mdb.Files.TryGetStruct(img.FileID);
				if (file.ID > 0)
				{
					string fullPath = file.GetFullPath(mdb);
					if (thumb)
					{
						if (TryGetThumb(webData, img, fullPath)) return;
					}
					webData.Answer = WebAnswer.Raw(webData.Request, WebMessage.Create(fullPath, "/mdb/image/get"), File.ReadAllBytes(fullPath), img.MimeType);
					webData.Answer.AllowCompression = false;
					webData.Answer.SetCacheTime(TimeSpan.FromHours(1));
					return;
				}
			}
			catch { }
			webData.Result.AddMessage(webData.Method, WebError.NotFound, "Image {0} cannot be found!", imageID);
		}

		/// <summary>Retrieves a list of all images matching the specified criteria.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="albumID">The album identifier.</param>
		/// <param name="artistID">The artist identifier.</param>
		/// <exception cref="WebException">Please specify albumID or artistID!</exception>
		/// <remarks>Returns <see cref="MDBImage"/> (Images)</remarks>
		[WebPage(Paths = "/mdb/image/search")]
		public void SearchImage(WebData webData, long albumID = 0, long artistID = 0)
		{
			IList<MDBImage> images;
			if (albumID != 0)
			{
				webData.Result.AddMessage(webData.Method, "Retrieved album images.");
				var album = mdb.Albums.TryGetStruct(albumID);
				images = mdb.Images.GetStructs(
					Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), album.MusicBrainzReleaseGroupGuid) &
					Search.FieldIn(nameof(MDBImage.Type), MDBImageType.AlbumCDArt, MDBImageType.AlbumCover, MDBImageType.AlbumCoverFront, MDBImageType.UserCover));
			}
			else if (artistID != 0)
			{
				var artist = mdb.Artists.TryGetStruct(artistID);
				images = mdb.Images.GetStructs(
					Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), artist.MusicBrainzArtistGuid) &
					Search.FieldIn(nameof(MDBImage.Type), MDBImageType.ArtistBackground, MDBImageType.ArtistMusicBanner,
						MDBImageType.ArtistMusicLogo, MDBImageType.ArtistMusicLogoHD, MDBImageType.ArtistThumb));
				webData.Result.AddMessage(webData.Method, "Retrieved artist images.");
			}
			else
			{
				throw new WebServerException(WebError.InvalidParameters, 0, "Please specify albumID or artistID!");
			}
			webData.Result.AddStructs(images);
		}

		/// <summary>Gets an artist background image.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="audioFileID">The audio file identifier.</param>
		/// <param name="artistID">The artist identifier.</param>
		/// <param name="thumb">if set to <c>true</c> [return thumbnail].</param>
		/// <param name="background">if set to <c>true</c> [background].</param>
		/// <remarks>Returns an image of mimetype jpeg or png</remarks>
		[WebPage(Paths = "/mdb/image/artist/get")]
		public void GetArtistImage(WebData webData, long audioFileID = 0, long artistID = 0, bool thumb = false, bool background = false)
		{
			var images = FindArtistImages(out MDBImageType[] types, audioFileID, artistID, thumb, background);
			if (images.Count > 0)
			{
				int rnd = (artistID ^ audioFileID).GetHashCode();
				var image = mdb.GetBestImage(rnd, images, types);
				GetImage(webData, image.FileID, thumb);
				return;
			}

			//no image found
			if (background)
			{
				webData.Result.AddMessage(webData.Method, WebError.NotFound, "No image available!");
				return;
			}
			long id = artistID != 0 ? artistID : audioFileID;
			string url = $"/mdb/image/artist/replacement?id={id % ReplaceArtistImages.Length}&thumb={thumb}";
			webData.Result.AddMessage(webData.Method, WebError.Redirect, $"Image not available!\nPlease use <a href=\"{url}\">this link</a>.");
			webData.Result.Headers["Location"] = url;
			webData.Result.Type = WebResultType.Html;
		}

		/// <summary>Gets the artist not available image.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="id">The identifier.</param>
		/// <param name="thumb">if set to <c>true</c> [return thumbnail].</param>
		/// <exception cref="ArgumentOutOfRangeException">id</exception>
		/// <remarks>Returns an image of mimetype jpeg or png</remarks>
		[WebPage(Paths = "/mdb/image/artist/replacement")]
		public void GetArtistReplacementImage(WebData webData, long id, bool thumb)
		{
			if (id < 0 || id >= ReplaceArtistImages.Length) throw new ArgumentOutOfRangeException(nameof(id));
			var replacementImage = ReplaceArtistImages[id];

			if (thumb)
			{
				webData.Answer = WebAnswer.Raw(
					webData.Request,
					WebMessage.Create(webData.Method, replacementImage.FileName),
					replacementImage.ThumbData,
					replacementImage.MimeType);
			}
			else
			{
				webData.Answer = WebAnswer.Raw(
					webData.Request,
					WebMessage.Create(webData.Method, replacementImage.FileName),
					replacementImage.Data,
					replacementImage.MimeType);
			}
			
			webData.Answer.Headers.Add("MDBReplacementImage", "NotAvailable");
			webData.Answer.AllowCompression = false;
			webData.Answer.SetCacheTime(TimeSpan.FromDays(1));
		}

		/// <summary>Gets the album image.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="audioFileID">The audio file identifier.</param>
		/// <param name="albumID">The album identifier.</param>
		/// <param name="thumb">if set to <c>true</c> [return thumbnail].</param>
		/// <remarks>Returns an image of mimetype jpeg or png</remarks>
		[WebPage(Paths = "/mdb/image/album/get")]
		public void GetAlbumImage(WebData webData, long audioFileID = 0, long albumID = 0, bool thumb = false)
		{
			var pseudoRandom = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute).GetHashCode();

			MDBAlbum album;
			var audioFile = default(MDBAudioFile);
			IList<MDBImage> images = null;

			if (albumID == 0)
			{
				audioFile = mdb.AudioFiles.TryGetStruct(audioFileID);
				albumID = audioFile.AlbumID;
			}
			album = mdb.Albums.TryGetStruct(albumID);

			#region get album image (direct)
			if (album.MusicBrainzReleaseGroupGuid != null)
			{
				images = mdb.Images.GetStructs(Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), album.MusicBrainzReleaseGroupGuid));
			}
			#endregion
			#region get artist image by audiofile.SongArtistID
			if (images == null || images.Count == 0)
			{
				if (audioFile.SongArtistID > 0)
				{
					images = FindArtistImages(out MDBImageType[] types, audioFileID, audioFile.SongArtistID, thumb, false);
				}
			}
			#endregion
			#region get artist image by album.ArtistID
			if (images == null || images.Count == 0)
			{
				if (album.ArtistID > 0)
				{
					images = FindArtistImages(out MDBImageType[] types, audioFileID, album.ArtistID, thumb, false);
				}
			}
			#endregion
			if (images != null && images.Count > 0)
			{
				var image = mdb.GetBestImage(pseudoRandom, images, MDBImageType.AlbumCover, MDBImageType.AlbumCoverFront, MDBImageType.UserCover, MDBImageType.AlbumCDArt, MDBImageType.ArtistThumb, MDBImageType.ArtistMusicLogoHD, MDBImageType.ArtistMusicLogo, MDBImageType.ArtistBackground);
				if (image.FileID > 0)
				{
					GetImage(webData, image.FileID, thumb);
					return;
				}
			}

			long id = albumID != 0 ? albumID : audioFileID;
			string url = $"/mdb/image/album/replacement?id={id % ReplaceAlbumImages.Length}&thumb={thumb}";
			webData.Result.AddMessage(webData.Method, WebError.Redirect, $"Image not available!\nPlease use <a href=\"{url}\">this link</a>.");
			webData.Result.Headers["Location"] = url;
			webData.Result.Type = WebResultType.Html;
		}

		/// <summary>Gets the album not available image.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="id">The identifier.</param>
		/// <param name="thumb">if set to <c>true</c> [return thumbnail].</param>
		/// <exception cref="ArgumentOutOfRangeException">id</exception>
		/// <remarks>Returns an image of mimetype jpeg or png</remarks>
		[WebPage(Paths = "/mdb/image/album/replacement")]
		public void GetAlbumReplacementImage(WebData webData, long id, bool thumb)
		{
			if (id < 0 || id >= ReplaceAlbumImages.Length) throw new ArgumentOutOfRangeException(nameof(id));
			var replacementImage = ReplaceAlbumImages[id];

			if (thumb)
			{
				webData.Answer = WebAnswer.Raw(
					webData.Request,
					WebMessage.Create(webData.Method, replacementImage.FileName),
					replacementImage.ThumbData,
					replacementImage.MimeType);
			}
			else
			{
				webData.Answer = WebAnswer.Raw(
					webData.Request,
					WebMessage.Create(webData.Method, replacementImage.FileName),
					replacementImage.Data,
					replacementImage.MimeType);
			}

			webData.Answer.AllowCompression = false;
			webData.Answer.Headers.Add("MDBReplacementImage", "NotAvailable");
			webData.Answer.SetCacheTime(TimeSpan.FromDays(1));
		}
		#endregion

		// ---------------------------------------------- /mdb/audiofiles

		#region /mdb/audiofile
		/// <summary>Searches for audio files matching the specified criteria.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="page">The page.</param>
		/// <param name="artistID">The artist identifier.</param>
		/// <param name="albumID">The album identifier.</param>
		/// <param name="categoryID">The category identifier.</param>
		/// <param name="genreID">The genre identifier.</param>
		/// <param name="tagID">The tag identifier.</param>
		/// <param name="filter">The filter.</param>
		/// <param name="title">The title.</param>
		/// <param name="album">The album.</param>
		/// <param name="artist">The artist.</param>
		/// <param name="genre">The genre.</param>
		/// <param name="tag">The tag.</param>
		/// <exception cref="WebException">
		/// Please use title or filter, not both!
		/// or
		/// Cannot use artist search and artistID at the same time!
		/// or
		/// Cannot use album search and albumID at the same time!
		/// </exception>
		/// <remarks>Returns <see cref="RPCAudioFile"/> (AudioFiles)</remarks>
		[WebPage(Paths = "/mdb/audiofile/search")]
		public void SearchAudioFiles(WebData webData, int page = 0, long artistID = 0, long albumID = 0, long categoryID = 0, long genreID = 0, long tagID = 0,
			string filter = null, string title = null, string album = null, string artist = null, string genre = null, string tag = null)
		{
			if (title == null) title = filter;

			Search search = Search.None;
			if (artistID > 0)
			{
				if (artist != null) throw new WebServerException(WebError.InvalidParameters, 0, "Cannot use artist search and artistID at the same time!");
				search &=
					Search.FieldEquals(nameof(MDBAudioFile.AlbumArtistID), artistID) |
					Search.FieldEquals(nameof(MDBAudioFile.SongArtistID), artistID);
			}
			else if (artist != null)
			{
				var ids = mdb.Artists.FindRows(Search.FieldLike(nameof(MDBArtist.Name), artist));
				search &=
					Search.FieldIn(nameof(MDBAudioFile.AlbumArtistID), ids) |
					Search.FieldIn(nameof(MDBAudioFile.SongArtistID), ids);
			}

			if (albumID > 0)
			{
				search &= Search.FieldEquals(nameof(MDBAudioFile.AlbumID), albumID);
				if (album != null) throw new WebServerException(WebError.InvalidParameters, 0, "Cannot use album search and albumID at the same time!");
			}
			else if (album != null)
			{
				var ids = mdb.Albums.FindRows(Search.FieldLike(nameof(MDBArtist.Name), album));
				search &= Search.FieldIn(nameof(MDBAudioFile.AlbumID), ids);
			}

			if (categoryID > 0) search &= GetCategorySearch(categoryID);
			if (genreID > 0) search &= Search.FieldEquals(nameof(MDBAudioFile.GenreID), genreID);
			if (tagID > 0) search &= Search.FieldEquals(nameof(MDBAudioFile.TagID), tagID);
			if (genre != null) search &= Search.FieldLike(nameof(MDBAudioFile.Genres), MDBSearch.Text("%" + genre + "%"));
			if (tag != null) search &= Search.FieldLike(nameof(MDBAudioFile.Tags), MDBSearch.Text("%" + tag + "%"));
			if (title != null) search &= Search.FieldLike(nameof(MDBAudioFile.Title), MDBSearch.Text("%" + title + "%"));

			{
				long rowCount = mdb.AudioFiles.Count(search);
				var ids = mdb.AudioFiles.FindRows(search, ResultOption.Limit(RowsPerPage) + ResultOption.Offset(page * RowsPerPage) + ResultOption.SortAscending(nameof(MDBAudioFile.Title)));
				webData.Result.AddMessage(webData.Method, "Found {0} matching AudioFiles.", rowCount);
				AddPagination(webData, page, rowCount);
				var files = ids.Select(i => RPCAudioFile.Load(mdb, i));
				webData.Result.AddStructs(files);
			}
		}

		#endregion

		#region /mdb/player
		/// <summary>Retrieves the now playing information and the playlist of the JukeBob stream</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="hash">The hash. This function blocks as long as the hash matches.</param>
		/// <param name="streamID">The stream identifier.</param>
		/// <exception cref="NotImplementedException"></exception>
		/// <remarks>
		/// Returns <see cref="MDBNowPlaying" />, <see cref="MDBPlayListItem" />, <see cref="MDBAlbum" /> Albums, <see cref="MDBArtist" /> Artists,
		/// <see cref="MDBAudioFile" />, <see cref="MDBSubset" />, <see cref="MDBPlayerState" />
		/// </remarks>
		[WebPage(Paths = "/mdb/player/state")]
		public void GetPlayerState(WebData webData, int hash = 0, long streamID = 0)
		{
			//default is JukeBob
			if (streamID == 0) streamID = (long)MDBStreamType.JukeBob;

			MDBNowPlaying nowPlaying = mdb.NowPlaying.TryGetStruct(1);
			var myHash = nowPlaying.GetHashCode() ^ mdb.PlayListSequenceNumber ^ -1;
			DateTime timeout = DateTime.UtcNow.AddSeconds(30 + DefaultRNG.UInt8 % 30);
			while (hash == myHash && DateTime.UtcNow < timeout)
			{
				Thread.Sleep(200);
				nowPlaying = mdb.NowPlaying.TryGetStruct(1);
				myHash = nowPlaying.GetHashCode() ^ mdb.PlayListSequenceNumber ^ -1;
			}

			var playList = new List<MDBPlayListItem>();
			playList.AddRange(mdb.PlayListItems.GetStructs(
				Search.FieldEquals(nameof(MDBPlayListItem.StreamID), streamID) & Search.FieldGreater(nameof(MDBPlayListItem.OwnerID), 0), 
				ResultOption.SortAscending(nameof(MDBPlayListItem.Added))));
			playList.AddRange(mdb.PlayListItems.GetStructs(
				Search.FieldEquals(nameof(MDBPlayListItem.StreamID), streamID) & Search.FieldEquals(nameof(MDBPlayListItem.OwnerID), 0), 
				ResultOption.SortAscending(nameof(MDBPlayListItem.Added))));

			var audioFileIDs = new Set<long>(nowPlaying.AudioFileID, playList.Select(i => i.AudioFileID));
			var files = mdb.AudioFiles.GetStructs(audioFileIDs);
			var albums = mdb.Albums.GetStructs(files.Select(f => f.AlbumID));
			var artistIDs = new Set<long>(files.Select(f => f.AlbumArtistID), files.Select(f => f.SongArtistID));
			var artists = mdb.Artists.GetStructs(artistIDs);
			var users = authTables.Users.GetStructs(playList.Select(i => i.OwnerID).Where(i => i > 0)).Select(u => u.ClearPrivateFields());

			var subsetIDs = playList.Where(i => i.SubsetID > 0).Select(i => i.SubsetID).ToSet();
			if (nowPlaying.SubsetID > 0) subsetIDs.Include(nowPlaying.SubsetID);
			var subsets = mdb.Subsets.GetStructs(subsetIDs);

			long ownerID;
			if (webData.Session.IsAuthenticated())
			{
				ownerID = webData.Session.GetUser().ID;
			}
			else
			{
				ownerID = -webData.Session.ID;
			}			

			webData.Result.AddMessage(webData.Method, "Retrieved JukeBob NowPlaying");
			webData.Result.AddStructs(playList);
			webData.Result.AddStructs(files);
			webData.Result.AddStructs(albums);
			webData.Result.AddStructs(artists);
			webData.Result.AddStructs(subsets);
			webData.Result.AddStructs(users);
			webData.Result.AddStruct(new MDBPlayerState() { ID = ownerID, Hash = myHash, StreamType = MDBStreamType.JukeBob });
			nowPlaying.UpdateDateTime = DateTime.UtcNow;
			webData.Result.AddStruct(nowPlaying);
		}
		#endregion

		#region /mdb/login

		/// <summary>Creates a useraccount and logs in</summary>
		/// <param name="data">The web data.</param>
		[WebPage(Paths = "/mdb/login")]
		public void CreateUserAndLogin(WebData data, string credentials)
		{
			var parts = Base64.UrlChars.DecodeUtf8(credentials).Split(':');
			if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
			{
				throw new WebServerException(WebError.InvalidParameters, "Invalid credentials.");
			}
			while (true)
			{
				User user;
				EmailAddress email;
				if (authTables.Login(parts[0], parts[1], out user, out email))
				{
					data.Session.SetAuthentication(user, 0);
					data.Result.AddMessage(data.Method, WebError.Redirect, $"Image not available!\nPlease use <a href=\"/\">this link</a>.");
					data.Result.Headers["Location"] = "/";
					data.Result.Type = WebResultType.Html;
					return;
				}
				authTables.CreateUser(parts[0], null, parts[1], UserState.Confirmed, 0, out user, out email);
			}
		}

		#endregion

		#region /mdb/device

		/// <param name="data">The web data.</param>
		[WebPage(Paths = "/mdb/configuration/host")]
		public void GetHostConfiguration(WebData data)
		{
			data.Result.AddStruct(mdb.Host);
			data.Result.AddMessage(data.Method, "Data retrieved.");
		}

		#endregion
	}
}
