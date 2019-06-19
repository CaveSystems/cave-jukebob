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
using Cave.Collections.Generic;
using Cave.Data;
using Cave;
using Cave.IO;
using Cave.Logging;
using Cave.Web;
using Cave.Media;
using Cave.Web.Auth;
using System.Text;
using Cave.Auth;
using Cave.Console;

namespace JukeBob
{
	partial class WebInterface
    {
		static object ThumbCreateSyncRoot = new object();

		public LogCollector LogCollector { get; } = new LogCollector() { Level = LogLevel.Information, MaximumItemCount = 200, ExceptionMode = LogExceptionMode.None, };

		MusicDataBase mdb;
		IPlayer player;
		IMDBCrawler crawler;
		AuthTables authTables;
        MDBOverview cachedOverview;


        string xtToHtml(XT content)
		{
			var sb = new StringBuilder();
			XTColor color = XTColor.Default;
			XTStyle style = XTStyle.Default;
			foreach (XTItem item in content.Items)
			{
				if (item.Color != color)
				{
					if (color != XTColor.Default) sb.Append("</span>");
					color = item.Color;
					if (color != XTColor.Default)
					{
						sb.Append("<span class=\"jb-log-color-");
						sb.Append(color.ToString().ToLower());
						sb.Append("\">");
					}
				}
				if (item.Style != style)
				{
					if (style != XTStyle.Default) sb.Append("</span>");
					style = item.Style;
					if (style != XTStyle.Default)
					{
						sb.Append("<span class=\"jb-log-style-");
						sb.Append(style.ToString().ToLower());
						sb.Append("\">");
					}
				}
				if (item.Text.Contains("\n"))
				{
					sb.Append("<br/>");
				}
				else
				{
					sb.Append(item.Text);
				}
			}
			if (color != XTColor.Default)
			{
				sb.Append("</span>");
			}
			if (style != XTStyle.Default)
			{
				sb.Append("</span>");
			}
			return sb.ToString();
		}

		void AddPagination(WebData webData, int page, long rowCount)
		{
			webData.Result.AddStruct(new MDBPagination()
			{
				Page = page,
				PageCount = (int)((rowCount + RowsPerPage - 1) / RowsPerPage),
				RowsPerPage = RowsPerPage,
				RowCount = rowCount,
			});
		}

		void FilterSimple(WebData webData, ITable table, string fieldName, string filter, int page)
		{
			if (table == null) throw new ArgumentNullException(nameof(table));
			if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
			Search search;
			long rowCount;
			if (filter == null)
			{
				search = Search.None;
				rowCount = table.RowCount;
			}
			else
			{
				search = Search.FieldLike(fieldName, MDBSearch.Text(filter));
				rowCount = table.Count(search);
			}
			ResultOption options = ResultOption.Limit(RowsPerPage) + ResultOption.SortAscending(fieldName);
			if (page > 0) options += ResultOption.Offset(page * RowsPerPage);
			var rows = table.GetRows(search, options);
			webData.Result.AddMessage(webData.Method, "Retrieved {0}.", table);
			AddPagination(webData, page, rowCount);
			webData.Result.AddRows(rows, table.Layout);
		}

		void FilterList(WebData webData, ITable table, string nameField, string guidField, string text)
		{
			var ids = new Set<long>();
			if (text == null)
			{
				ids.IncludeRange(table.FindRows(Search.None, ResultOption.Limit(20)));
			}
			else
			{
				ids.AddRange(table.FindRows(Search.FieldLike(nameField, MDBSearch.Text(text + "%")) & Search.FieldNotEquals(guidField, null), ResultOption.SortAscending(nameField) + ResultOption.Group(nameField) + ResultOption.Group(guidField) + ResultOption.Limit(20)));
				if (ids.Count < 20)
				{
					ids.IncludeRange(table.FindRows(Search.FieldLike(nameField, MDBSearch.Text("% " + text + "%")) & Search.FieldNotEquals(guidField, null), ResultOption.SortAscending(nameField) + ResultOption.Group(nameField) + ResultOption.Group(guidField) + ResultOption.Limit(20)));
				}
				if (ids.Count < 20)
				{
					ids.IncludeRange(table.FindRows(Search.FieldLike(nameField, MDBSearch.Text(text + "%")) & Search.FieldEquals(guidField, null), ResultOption.SortAscending(nameField) + ResultOption.Group(nameField) + ResultOption.Limit(20 - ids.Count)));
				}
				if (ids.Count < 20)
				{
					ids.IncludeRange(table.FindRows(Search.FieldLike(nameField, MDBSearch.Text("% " + text + "%")) & Search.FieldEquals(guidField, null), ResultOption.SortAscending(nameField) + ResultOption.Group(nameField) + ResultOption.Limit(20 - ids.Count)));
				}
			}
			var json = new JsonWriter();
			json.BeginArray("results");
			if (ids.Count > 0)
			{
				//get items
				var values = table.GetValues<string>(nameField, false, ids.SubRange(0, Math.Min(20, ids.Count)));
				foreach (var value in values)
				{
					json.BeginObject();
					json.String("id", value);
					json.String("text", value);
					json.EndObject();
				}
			}
			json.EndArray();
			var message = WebMessage.Create(webData.Method, $"Filter {nameField} {text}");
			webData.Answer = WebAnswer.Json(webData.Request, message, json.ToString());
		}

		bool TryGetThumb(WebData webData, MDBImage img, string fileName)
		{
			try
			{
				//try to load thumb
				var hash = Base32.Safe.Encode(Hash.FromString(Hash.Type.SHA256, fileName));
				string thumbFileName = FileSystem.Combine(mdb.CacheFolder, "Thumbs", hash + ".jpg");
				var mime = MimeTypes.FromExtension(".jpg");
				if (File.Exists(thumbFileName))
				{
					try
					{
						webData.Answer = WebAnswer.Raw(webData.Request, WebMessage.Create(webData.Method, thumbFileName), File.ReadAllBytes(thumbFileName), mime);
						webData.Answer.SetCacheTime(TimeSpan.FromDays(1));
						return true;
					}
					catch { /*file access error, writing in progress ?, wait for lock and retry*/ }
				}
				//wait until last thumb generation is finished
				byte[] data;
				lock (ThumbCreateSyncRoot)
				{
					//do a second check after lock is released...
					if (File.Exists(thumbFileName))
					{
						try
						{
							webData.Answer = WebAnswer.Raw(webData.Request, WebMessage.Create(webData.Method, thumbFileName), File.ReadAllBytes(thumbFileName), mime);
							webData.Answer.SetCacheTime(TimeSpan.FromDays(1));
							return true;
						}
						catch { /*file access error, recreate thumb*/ }
					}
					//generate thumb
					using (var bmp = Bitmap32.FromFile(fileName))
					{
						data = WebImage.RenderThumb(bmp, thumbFileName);
					}
				}
				webData.Answer = WebAnswer.Raw(webData.Request, WebMessage.Create(webData.Method, thumbFileName), data, mime);
				webData.Answer.AllowCompression = false;
				webData.Answer.SetCacheTime(TimeSpan.FromDays(1));
				return true;
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Could not load / create thumb for {0}", fileName);
			}
			return false;
		}

		IList<MDBImage> FindArtistImagesByArtistID(MDBImageType[] types, long artistID)
		{
			var artist = mdb.Artists.TryGetStruct(artistID);
			if (artist.ID != 0 && artist.MusicBrainzArtistGuid != null)
			{
				return mdb.Images.GetStructs(Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), artist.MusicBrainzArtistGuid) & Search.FieldIn(nameof(MDBImage.Type), types));
			}
			return new MDBImage[0];
		}

		IList<MDBImage> FindArtistImages(out MDBImageType[] types, long audioFileID = 0, long artistID = 0, bool thumb = false, bool background = false)
		{
			if (background)
			{
				types = new MDBImageType[] { MDBImageType.ArtistBackground };
			}
			else
			{
				types = new MDBImageType[] { MDBImageType.ArtistThumb, MDBImageType.ArtistMusicLogoHD, MDBImageType.ArtistMusicLogo, MDBImageType.ArtistBackground };
			}

			MDBAudioFile audioFile = mdb.AudioFiles.TryGetStruct(audioFileID);
			var albums = new Set<long>();
			if (audioFileID > 0)
			{
				//lookup SongArtistID
				var images = FindArtistImagesByArtistID(types, audioFile.SongArtistID);
				if (images.Count > 0) return images;
				//lookup AlbumArtistID
				images = FindArtistImagesByArtistID(types, audioFile.AlbumArtistID);
				if (images.Count > 0) return images;
				//will use these for album lookup later
				if (audioFile.SongArtistID > 0) albums.IncludeRange(mdb.Albums.FindRows(nameof(MDBAlbum.ArtistID), audioFile.SongArtistID));
				if (audioFile.AlbumArtistID > 0) albums.IncludeRange(mdb.Albums.FindRows(nameof(MDBAlbum.ArtistID), audioFile.AlbumArtistID));
			}
			if (artistID > 0)
			{
				//lookup artistID
				var images = FindArtistImagesByArtistID(types, artistID);
				if (images.Count > 0) return images;
				//will use these for album lookup later
				albums.IncludeRange(mdb.Albums.FindRows(nameof(MDBAlbum.ArtistID), artistID));
			}

			//no lookup album of artist
			types = new MDBImageType[] { MDBImageType.AlbumCoverFront, MDBImageType.AlbumCover, MDBImageType.UserCover, MDBImageType.AlbumCDArt };
			if (audioFile.AlbumID > 0)
			{
				var album = mdb.Albums.TryGetStruct(audioFile.AlbumID);
				var images = mdb.Images.GetStructs(Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), album.MusicBrainzReleaseGroupGuid) & Search.FieldIn(nameof(MDBImage.Type), types));
				if (images.Count > 0) return images;
			}
			//no lookup any album of artist
			foreach (var album in mdb.Albums.GetStructs(albums))
			{
				if (album.MusicBrainzReleaseGroupGuid != null)
				{
					var images = mdb.Images.GetStructs(Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), album.MusicBrainzReleaseGroupGuid) & Search.FieldIn(nameof(MDBImage.Type), types));
					if (images.Count > 0) return images;
				}
			}

			return new MDBImage[0];
		}

		Search GetCategorySearch(long categoryID)
		{
			var categoryIDs = new Set<long>();
			categoryIDs.Add(categoryID);
			//get flat list of all child categories
			var category = mdb.Categories.TryGetStruct(categoryID);
			var children = mdb.Categories.GetStructs(nameof(MDBCategory.ParentID), category.ID);
			for (int i = 0; children.Count > 0 && i < 10; i++)
			{
				var ids = new Set<long>(children.Select(c => c.ID));
				categoryIDs.IncludeRange(ids);
				children = mdb.Categories.GetStructs(Search.FieldIn(nameof(MDBCategory.ParentID), ids));
			}
			return Search.FieldIn(nameof(MDBAudioFile.CategoryID), categoryIDs);
		}

        MDBOverview GetOverview(MusicDataBase mdb, WebServer server)
        {
            if (cachedOverview.ID != Math.Abs(mdb.Files.SequenceNumber) + 1)
            {
                cachedOverview = MDBOverview.Create(mdb, server);
            }
            return cachedOverview;
        }
    }
}
