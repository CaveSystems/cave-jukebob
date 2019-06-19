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
using System.Collections.Generic;
using System.Linq;
using Cave;
using Cave.Console;
using Cave.Data;
using Cave.Logging;
using Cave.Web;

namespace JukeBob
{
	partial class WebInterface
    {
		#region /admin/crawler

		/// <summary>Starts the file crawler.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="crawlerType">Type of the crawler.</param>
		/// <remarks>
		/// Returns <see cref="MDBState" /> (State) and <see cref="MDBProgress" /> (Progress) if <see cref="MDBComponentState" /> == Running
		/// </remarks>
		[WebPage(Paths = "/admin/crawler/start", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void StartCrawler(WebData webData, string crawlerType)
		{
			lock (mdb)
			{
				if (crawler != null && crawler.Completed) { crawler?.Dispose(); crawler = null; }
				if (crawler == null)
				{
					switch (crawlerType)
					{
						case "file": crawler = FileCrawler; break;
						case "art": crawler = ArtCrawler; break;
					}
					crawler?.Start();
				}
			}
			if (webData != null) GetCrawlerState(webData);
		}

		/// <summary>Stops the crawler.</summary>
		/// <param name="webData">The web data.</param>
		[WebPage(Paths = "/admin/crawler/stop", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void StopCrawler(WebData webData)
		{
			string message = "Crawler stopped";
			lock (mdb)
			{
				if (crawler != null)
				{
					if (crawler.Completed)
					{
						crawler = null;
					}
					else
					{
						message = "Crawler stopping...";
						crawler.Stop();
					}
				}
			}
			webData.Result.AddMessage(webData.Method, message);
		}

		/// <summary>Gets the crawler progress.</summary>
		/// <param name="webData">The web data.</param>
		/// <remarks>Returns <see cref="MDBState"/> (State) and <see cref="MDBProgress"/> (Progress) if <see cref="MDBComponentState"/> == Running</remarks>
		[WebPage(Paths = "/admin/crawler/state", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetCrawlerState(WebData webData)
		{
			var crawlers = new List<MDBCrawler>();
			if (FileCrawler != null) crawlers.Add(new MDBCrawler()
			{
				ID = 1,
				Name = FileCrawler.Name,
			});
			if (ArtCrawler != null) crawlers.Add(new MDBCrawler()
			{
				ID = 2,
				Name = ArtCrawler.Name,
			});
			webData.Result.AddStructs(crawlers);

			if (crawler == null || crawler.Completed)
			{
				if (crawler != null)
				{
					crawler?.Dispose();
					crawler = null;
				}
				string message = "Crawler stopped";
				webData.Result.AddMessage(webData.Method, message);
				webData.Result.AddStruct(new MDBState() { ID = 1, State = MDBComponentState.Stopped });
			}
			else if (crawler.Error)
			{
				string message = "Crawler error";
				webData.Result.AddMessage(webData.Method, message);
				webData.Result.AddStruct(new MDBState() { ID = 1, State = MDBComponentState.Error, Message = crawler.Exception.ToText(), Source = crawler.Name });
				mdb.Save();
			}
			else
			{
				string message = "Crawler running...";
				webData.Result.AddMessage(webData.Method, message);
				webData.Result.AddStruct(new MDBState() { ID = 1, State = MDBComponentState.Running, Source = crawler.Name });
				webData.Result.AddStructs(crawler.Progress);
			}
		}

		#endregion /admin/crawler

		#region /admin/audiofile
		/// <summary>Gets the audio file information.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="audioFileID">The audio file identifier.</param>
		/// <remarks>Returns <see cref="RPCAudioFile"/> (FullAudioFileInfo), <see cref="MDBAudioFile"/> (AudioFiles), <see cref="MDBFile"/> (Files)</remarks>
		[WebPage(Paths = "/admin/audiofile", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetAudioFile(WebData webData, long audioFileID)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved AudioFile information.");
			var webAudioFile = RPCAudioFile.Load(mdb, audioFileID);
			var audioFile = mdb.AudioFiles.TryGetStruct(audioFileID);
			var file = mdb.Files.TryGetStruct(audioFile.FileID);
			webData.Result.AddStruct(webAudioFile);
			webData.Result.AddStruct(audioFile);
			webData.Result.AddStruct(file);
		}
		#endregion

		#region /admin/list

		/// <summary>Lists all artists without images of a specified type.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="imageType">Type of the image.</param>
		/// <remarks>Returns <see cref="RPCArtist" /></remarks>
		[WebPage(Paths = "/admin/list/artists-without-image", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetArtistsWithoutImage(WebData webData, MDBImageType imageType = 0)
		{
			var artists = mdb.Artists.GetStructs();
			List<RPCArtist> missing = new List<RPCArtist>();
			foreach (var artist in artists)
			{
				var search = Search.FieldEquals(nameof(MDBImage.MusicBrainzGuid), artist.MusicBrainzArtistGuid);
				if (imageType != 0) search &= Search.FieldEquals(nameof(MDBImage.Type), imageType);
				if (!mdb.Images.Exist(search))
				{
					missing.Add(RPCArtist.Load(mdb, artist));
				}
			}
			webData.Result.AddMessage(webData.Method, "Retrieved artist datasets with missing imagetype {0}...", imageType);
			webData.Result.AddStructs(missing);
		}

		/// <summary>Lists all albums with errors.</summary>
		/// <param name="webData">The web data.</param>
		/// <remarks>Returns <see cref="RPCAlbum"/>, <see cref="RPCArtist"/></remarks>
		[WebPage(Paths = "/admin/list/albums-with-errors", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetAlbumsWithErrorsList(WebData webData)
		{
			var audioFiles = mdb.AudioFiles.GetStructs(Search.FieldNotEquals(nameof(MDBAudioFile.MetaErrors), 0));
			var albumErrors = new Dictionary<long, MDBMetaErrors>();
			foreach (var audioFile in audioFiles)
			{
				albumErrors.TryGetValue(audioFile.AlbumID, out MDBMetaErrors errors);
				errors |= audioFile.MetaErrors;
				albumErrors[audioFile.AlbumID] = errors;
			}
			var albums = mdb.Albums.GetStructs(
				Search.FieldIn(nameof(MDBAlbum.ID), albumErrors.Keys) |
				Search.FieldEquals(nameof(MDBAlbum.MusicBrainzAlbumGuid), null) |
				Search.FieldEquals(nameof(MDBAlbum.MusicBrainzReleaseGroupGuid), null)
				);

			foreach (var album in albums)
			{
				albumErrors.TryGetValue(album.ID, out MDBMetaErrors errors);
				if (album.MusicBrainzAlbumGuid == null) errors |= MDBMetaErrors.MusicBrainzAlbum;
				if (album.MusicBrainzReleaseGroupGuid == null) errors |= MDBMetaErrors.MusicBrainzReleaseGroup;
				albumErrors[album.ID] = errors;
			}
			var artists = mdb.Artists.GetStructs(albums.Select(a => a.ArtistID));

			webData.Result.AddMessage(webData.Method, "Retrieved albums with errors datasets...");
			webData.Result.AddStructs(albums.Select(a => RPCAlbum.Load(mdb, a)));
			webData.Result.AddStructs(artists.Select(a => RPCArtist.Load(mdb, a)));
		}

		/// <summary>Gets the audio files with meta errors.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="page">The page.</param>
		/// <remarks>Returns <see cref="RPCAudioFile"/> (FullAudioFileInfo)</remarks>
		[WebPage(Paths = "/admin/list/files-with-errors", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetFilesWithErrorsList(WebData webData, int page = 0)
		{
			Search search = Search.FieldNotEquals(nameof(MDBAudioFile.MetaErrors), MDBMetaErrors.None);
			long rowCount = mdb.AudioFiles.Count(search);
			var ids = mdb.AudioFiles.FindRows(search, ResultOption.Limit(RowsPerPage) + ResultOption.Offset(page * RowsPerPage));
			var files = ids.Select(i => RPCAudioFile.Load(mdb, i));

			webData.Result.AddMessage(webData.Method, "Retrieved AudioFiles with MetaErrors.");
			AddPagination(webData, page, rowCount);
			webData.Result.AddStructs(files);
		}

		#endregion

		#region /admin/player

		/// <summary>Skips a title at the specified stream.</summary>
		/// <param name="webData">The web data.</param>
		[WebPage(Paths = "/admin/player/skip", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void SkipTitleAtPlayerAdmin(WebData webData)
		{
			webData.Result.AddMessage(webData.Method, "Skipping current title...");
			player?.Skip();
		}

		#endregion

		#region /admin/stream/settings
		/// <summary>Gets all stream settings.</summary>
		/// <param name="webData">The web data.</param>
		/// <remarks>Returns <see cref="MDBStreamSetting"/></remarks>
		[WebPage(Paths = "/admin/stream/settings", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetStreamSettings(WebData webData)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved subset datasets...");
			var settings = new MDBStreamSetting[]
			{
				mdb.GetStreamSettings((long)MDBStreamType.JukeBob)
			};
			webData.Result.AddStructs(settings);
		}

		/// <summary>Sets/replaces a stream setting.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="streamID">The stream identifier.</param>
		/// <param name="subsetID">The subset identifier.</param>
		/// <param name="titlesPerUser">The titles per user (&lt; 0: unlimited, == 0: only admin).</param>
		/// <param name="minimumTitleCount">The minimum title count always present at the list (filled with random titles from subset).</param>
		/// <param name="maximumTitleCount">The maximum title count (&lt;= 0 allow any number of titles).</param>
		/// <param name="minimumLength">The minimum length.</param>
		/// <param name="maximumLength">The maximum length.</param>
		/// <param name="volume">The volume (0..1)</param>
		/// <remarks>Returns <see cref="MDBStreamSetting" /></remarks>
		[WebPage(Paths = "/admin/stream/setting/set", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void SetStreamSetting(WebData webData, 
			long streamID, long? subsetID = null, int? titlesPerUser = null, int? minimumTitleCount = null, int? maximumTitleCount = null, 
			TimeSpan? minimumLength = null, TimeSpan? maximumLength = null, float? volume = null)
		{
			if (streamID != (long)MDBStreamType.JukeBob) throw new ArgumentException("Invalid streamID");
			lock (mdb.StreamSettings)
			{
				var settings = mdb.StreamSettings.TryGetStruct(streamID);
				if (maximumLength.HasValue) settings.MaximumLength = maximumLength.Value;
				if (minimumLength.HasValue) settings.MinimumLength = minimumLength.Value;
				if (maximumTitleCount.HasValue) settings.MaximumTitleCount = maximumTitleCount.Value;
				if (minimumTitleCount.HasValue) settings.MinimumTitleCount = minimumTitleCount.Value;
				if (titlesPerUser.HasValue) settings.TitlesPerUser = titlesPerUser.Value;
				if (subsetID.HasValue) settings.SubsetID = subsetID.Value;
				if (volume.HasValue) settings.Volume = Math.Max(0, Math.Min(1, volume.Value));
				mdb.StreamSettings.Replace(settings);
				mdb.Save();
			}
			GetStreamSetting(webData, streamID);
		}

        /// <summary>Gets a specified stream setting.</summary>
        /// <param name="webData">The web data.</param>
        /// <param name="streamID">The stream identifier.</param>
        /// <exception cref="WebServerException">Invalid streamID!</exception>
        /// <remarks>Returns <see cref="MDBStreamSetting" /></remarks>
        [WebPage(Paths = "/admin/stream/setting/get", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetStreamSetting(WebData webData, long streamID)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved subset datasets...");
			var setting = mdb.GetStreamSettings(streamID);
			webData.Result.AddStruct(setting);
		}
		#endregion

		#region /admin/subset

		/// <summary>Gets the subsets.</summary>
		/// <param name="webData">The web data.</param>
		/// <remarks>Returns <see cref="MDBSubset"/> (Subsets)</remarks>
		[WebPage(Paths = "/admin/subset/list", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetSubsetList(WebData webData)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved subset datasets...");
			var subsets = mdb.Subsets.GetStructs();
			webData.Result.AddStructs(subsets);
		}

        /// <summary>Updates or creates a subset. If subsetID &lt;= 0 a new subset will be created.</summary>
        /// <param name="webData">The web data.</param>
        /// <param name="subsetID">The subset identifier.</param>
        /// <param name="name">The name.</param>
        /// <exception cref="WebServerException">Subset does not exist!</exception>
        /// <remarks>Returns <see cref="MDBSubset"/> (Subsets)</remarks>
        [WebPage(Paths = "/admin/subset/update", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void UpdateSubset(WebData webData, long subsetID = 0, string name = null)
		{
			MDBSubset subset;
			if (subsetID <= 0)
			{
				subset = new MDBSubset() { ID = CaveSystemData.CalculateID(name), Name = name, };
				subset.ID = mdb.Subsets.Insert(subset);
			}
			else
			{
				subset = mdb.Subsets.TryGetStruct(subsetID);
				if (subset.ID == 0) throw new WebServerException(WebError.DatasetMissing, 0, "Subset does not exist!");
				if (name != null) subset.Name = name;
				mdb.Subsets.Replace(subset);
			}
			mdb.Save();
			GetSubsetList(webData);
			GetSubsetFilterList(webData, subset.ID);
		}

        /// <summary>Updates or creates a subset. If subsetID &lt;= 0 a new subset will be created.</summary>
        /// <param name="webData">The web data.</param>
        /// <param name="subsetID">The subset identifier.</param>
        /// <exception cref="WebServerException">Subset does not exist!</exception>
        /// <remarks>Returns <see cref="MDBSubset" /> (Subsets)</remarks>
        [WebPage(Paths = "/admin/subset/delete", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void DeleteSubset(WebData webData, long subsetID = 0)
		{
			mdb.Subsets.TryDelete(nameof(MDBSubset.ID), subsetID);
			mdb.Save();
			GetSubsetList(webData);
		}

		/// <summary>Gets all filters defined at a subset.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="subsetID">The subset identifier.</param>
		/// <remarks>Returns <see cref="MDBSubset"/> (Subsets) and <see cref="MDBSubsetFilter"/> (SubsetFilters)</remarks>
		[WebPage(Paths = "/admin/subsetfilter/list", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetSubsetFilterList(WebData webData, long subsetID = 0)
		{
			webData.Result.AddMessage(webData.Method, "Retrieved subset filter datasets...");
			var subset = mdb.Subsets.TryGetStruct(subsetID);
			var filters = mdb.SubsetFilters.GetStructs(nameof(MDBSubsetFilter.SubsetID), subset.ID);
			webData.Result.AddStruct(subset);
			webData.Result.AddStructs(filters);
		}

        /// <summary>Deletes a subset filter.</summary>
        /// <param name="webData">The web data.</param>
        /// <param name="subsetFilterID">The subset filter identifier.</param>
        /// <exception cref="WebServerException"><see cref="WebError.DatasetMissing"/> SubsetFilterID now found!</exception>
        /// <remarks>Returns <see cref="MDBSubset"/> (Subsets) and <see cref="MDBSubsetFilter"/> (SubsetFilters)</remarks>
        [WebPage(Paths = "/admin/subsetfilter/delete", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void DeleteSubsetFilter(WebData webData, long subsetFilterID)
		{
			var filter = mdb.SubsetFilters.TryGetStruct(subsetFilterID);
			if (filter.ID != subsetFilterID) throw new WebServerException(WebError.DatasetMissing, 0, "SubsetFilterID not found!");
			mdb.SubsetFilters.TryDelete(nameof(MDBSubsetFilter.ID), subsetFilterID);
			mdb.Save();
			GetSubsetFilterList(webData, filter.SubsetID);
		}

        /// <summary>Updates or creates a subset filter. Creating new filters require at least parameter subsetID to be set. Updating existing items require at least subsetFilterID to be set.</summary>
        /// <param name="webData">The web data.</param>
        /// <param name="subsetID">The subset identifier.</param>
        /// <param name="subsetFilterID">The subset filter identifier.</param>
        /// <param name="mode">The mode.</param>
        /// <param name="type">The type.</param>
        /// <param name="text">The text.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.InvalidParameters"/> SubsetID has to be specified for new filters!
        /// or
        /// <see cref="WebError.DatasetMissing"/> SubsetFilterID {0} is not present!
        /// </exception>
        [WebPage(Paths = "/admin/subsetfilter/update", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void UpdateSubsetFilter(WebData webData, long subsetID = 0, long subsetFilterID = 0, MDBSubsetFilterMode mode = 0, MDBSubsetFilterType type = 0, string text = null)
		{
			MDBSubsetFilter filter;
			if (subsetFilterID == 0)
			{
				if (subsetID == 0) throw new WebServerException(WebError.InvalidParameters, 0, "SubsetID has to be specified for new filters!");
				filter = new MDBSubsetFilter()
				{
					Text = text,
					Mode = mode,
					SubsetID = subsetID,
					Type = type
				};
				filter.ID = mdb.SubsetFilters.Insert(filter);
			}
			else
			{
				filter = mdb.SubsetFilters.TryGetStruct(subsetFilterID);
				if (filter.ID != subsetFilterID) throw new WebServerException(WebError.DatasetMissing, 0, string.Format("SubsetFilterID {0} is not present!", subsetFilterID));
				if (mode != 0) filter.Mode = mode;
				if (type != 0) filter.Type = type;
				if (text != null) filter.Text = text;
				mdb.SubsetFilters.Replace(filter);
			}
			mdb.Save();
			GetSubsetFilterList(webData, filter.SubsetID);
		}

		#endregion /admin/subset		

		#region /admin/log

		/// <summary>Gets the last log log entries.</summary>
		/// <param name="webData">The web data.</param>
		/// <remarks>Returns <see cref="LogEntry"/></remarks>
		[WebPage(Paths = "/admin/log", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void GetLog(WebData webData, LogLevel? minLevel = null)
		{
			LogLevel level = minLevel ?? LogLevel.Verbose;
			webData.Result.AddMessage(webData.Method, "Retrieved logging datasets...");
			int i = (int)(DateTime.Now.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond);
			List<LogEntry> items = new List<LogEntry>();
			foreach (var msg in LogCollector.ToArray().Reverse())
			{
				if (msg.Level > level) continue;

				if (msg.Exception == null || 0 == LogCollector.ExceptionMode)
				{
					items.Add(new LogEntry()
					{
						ID = i--,
						Level = msg.Level,
						Content = msg.Content,
						Source = msg.Source,
						DateTime = msg.DateTime,
					});
					continue;
				}

				//log stacktrace
				bool stackTrace = (0 != (LogCollector.ExceptionMode & LogExceptionMode.StackTrace));
				XT exceptionMessage = msg.Exception.ToXT(stackTrace);
				//with same level ?
				if (0 != (LogCollector.ExceptionMode & LogExceptionMode.SameLevel))
				{
					items.Add(new LogEntry()
					{
						ID = i--,
						Level = msg.Level,
						Content = msg.Content + new XT("\n") + exceptionMessage,
						Source = msg.Source,
						DateTime = msg.DateTime,
					});
				}
				else
				{
					//two different messages
					items.Add(new LogEntry()
					{
						ID = i--,
						Level = msg.Level,
						Content = msg.Content,
						Source = msg.Source,
						DateTime = msg.DateTime,
					});
					if (level >= LogLevel.Verbose)
					{
						items.Add(new LogEntry()
						{
							ID = i--,
							Level = LogLevel.Verbose,
							Content = exceptionMessage,
							Source = msg.Source,
							DateTime = msg.DateTime,
						});
					}
				}
			}
			webData.Result.AddStructs(items);
		}

		#endregion
	}
}
