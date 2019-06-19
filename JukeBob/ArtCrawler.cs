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
using Cave.Collections.Generic;
using Cave.Data;
using Cave.IO;
using Cave.Logging;
using Cave.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JukeBob
{
    /// <summary>
    /// Searches for album and artist art
    /// </summary>
    /// <seealso cref="IMDBCrawler" />
    class ArtCrawler : BaseCrawler
    {
        /// <summary>The fan art tv apikey</summary>
        public const string FanArtTV_APIKEY = "9a5b09b9231d81ec18c5851e791a5507";

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public override string LogSourceName { get { return "mdb-art-crawler"; } }

        #region private implementation

        MusicDataBase mdb;
        bool m_Exit;
        Task m_Task;
        float progressFanArtTV;
        float progressMusicBrainz;
        float progressCleanup;

		#region common functions        
		#region GetAlbumFolder
		/// <summary>Gets the album folder.</summary>
		/// <param name="album">The album.</param>
		/// <returns></returns>
		MDBFolder GetAlbumFolder(MDBAlbum album)
		{
			var paths = new Set<MDBFolder>();
			foreach (var audioFile in mdb.AudioFiles.GetStructs(
				Search.FieldEquals(nameof(MDBAudioFile.AlbumID), album.ID) &
				Search.FieldEquals(nameof(MDBAudioFile.AlbumArtistID), album.ArtistID)))
			{
				if (audioFile.FileID <= 0) continue;
				MDBFile file;
				if (!mdb.Files.TryGetStruct(audioFile.FileID, out file)) continue;
				MDBFolder folder;
				if (!mdb.Folders.TryGetStruct(file.FolderID, out folder)) continue;
				paths.Include(folder);
			}
			if (paths.Count > 1)
			{
				this.LogWarning("Multiple folders for album {0}", album);
			}
			if (paths.Count > 0)
			{
				return paths.First();
			}
			return new MDBFolder() { ID = 0 };
        }
        #endregion

        #region FindAlbumImage  
        /// <summary>
        /// Iterates all albums searching for the specified fileNameWithoutExtension.
        /// If the image is found it is returned via imageData. 
        /// All albums within the album list not containing the image are returned via albumsWithMissingImages.
        /// </summary>
        /// <param name="albums">The albums.</param>
        /// <param name="fileNameWithoutExtension">The file name without extension.</param>
        /// <param name="imageData">The image data.</param>
        /// <param name="albumsWithMissingImages">The albums with missing images.</param>
        void FindAlbumImage(IEnumerable<MDBAlbum> albums, string fileNameWithoutExtension, out byte[] imageData, out List<MDBAlbum> albumsWithMissingImages)
        {
            albumsWithMissingImages = new List<MDBAlbum>();
            imageData = null;
            foreach (var album in albums)
            {
                var albumFolder = GetAlbumFolder(album);
				if (albumFolder.ID == 0) continue;
                string fullPath = mdb.GetImageFileNameWithExtension(albumFolder, fileNameWithoutExtension);
                if (fullPath == null)
                {
                    //file is missing
                    albumsWithMissingImages.Add(album);
                    continue;
                }

                //file exists
                if (imageData == null)
                {
                    imageData = mdb.TryLoadImageData(fullPath);
                    if (imageData == null)
                    {
                        //image cannot be read
                        albumsWithMissingImages.Add(album);
                        continue;
                    }
                }
				string fileName = Path.GetFileName(fullPath);
                MDBFile mdbFile;
                mdb.RegisterFile(albumFolder, fileName, out mdbFile);
                if (!mdb.Images.Exist(nameof(MDBImage.FileID), mdbFile.ID))
                {
                    //image dataset is missing
                    albumsWithMissingImages.Add(album);
                    continue;
                }
            }
        }
        #endregion

        #region Cleanup
        /// <summary>Performs a cleanup removing all images no longer present at the file system</summary>
        void Cleanup()
        {
            if (m_Exit) return;
            float datasets = mdb.Images.RowCount;
            int n = 0;
            foreach (MDBImage i in mdb.Images.GetStructs())
            {
                progressCleanup = n++ / datasets;
                MDBFile file;
                if (!mdb.Files.TryGetStruct(i.FileID, out file) || !File.Exists(file.GetFullPath(mdb)))
                {
                    if (file.ID > 0) mdb.Files.Delete(file.ID);
                    mdb.Images.Delete(i.FileID);
					this.LogInfo("<red>Removed <default>{0}", i);
                }
                if (m_Exit) return;
            }
            progressCleanup = 1f;
        }
        #endregion

        void Worker()
        {
			try
			{
				FanArtTV_CrawlArtists(); if (m_Exit) return;
				MusicBrainz_CrawlAlbums(); if (m_Exit) return;
				Cleanup();
				mdb.Save();
			}
			finally
			{
				progressFanArtTV = 0;
				progressMusicBrainz = 0;
				progressCleanup = 0;
			}
		}
        #endregion

        #region FanArtTV
        void FanArtTV_CheckAlbumImage(MDBImageType imageType, MDBArtist artist, IEnumerable<MDBAlbum> albums, JsonNode node)
        {
            if (!albums.Any()) return;
            foreach (JsonNode v in node.Values)
            {
                try
                {
                    string source = v["url"].Value.ToString();
                    string fileNameWithoutExtension = imageType.ToString() + " " + v["id"].Value.ToString();
                    //check if each album has this image, autoload if we already have it at any album
                    byte[] imageData = null;
                    var albumsWithMissingImages = new List<MDBAlbum>();
                    FindAlbumImage(albums, fileNameWithoutExtension, out imageData, out albumsWithMissingImages);
                    if (albumsWithMissingImages.Count == 0) continue;
                    //try load from files table
                    if (imageData == null)
                    {
                        foreach (var file in mdb.Files.GetStructs(nameof(MDBFile.Name), fileNameWithoutExtension))
                        {
                            string fullPath = file.GetFullPath(mdb);
                            imageData = mdb.TryLoadImageData(fullPath);
                            if (imageData == null)
                            {
								File.Delete(fullPath);
                                mdb.Files.Delete(file.ID);
                            }
                        }
                    }
                    //if not found, load from web
                    if (imageData == null)
                    {
                        this.LogInfo("Download FanArtTV Image <blue>{0}", source);
                        imageData = mdb.TryDownloadImageData(source);
                        if (imageData == null) throw new Exception("Could not load image data!");
                    }
					//save to all albums
					foreach (MDBAlbum album in albumsWithMissingImages)
					{
						var albumFolder = GetAlbumFolder(album);
						if (albumFolder.ID == 0) continue;
						mdb.SaveAlbumImage(albumFolder, fileNameWithoutExtension, imageType, album, imageData);
					}
                }
                catch (Exception ex)
                {
                    this.LogWarning(ex, "Error saving {0} image {1} for artist {2}.", imageType, v, artist);
                }
            }
        }

        void FanArtTV_CheckArtistImage(MDBImageType imageType, MDBArtist artist, JsonNode node)
        {
            foreach (JsonNode v in node.Values)
            {
                try
                {
                    string source = v["url"].Value.ToString();

					MDBFolder folder = mdb.GetArtistArtFolder(artist);
                    string fileNameWithoutExtension = imageType.ToString() + " " + v["id"].Value.ToString();

                    byte[] imageData = null;
                    //download only if not exists
                    string fullPath = mdb.GetImageFileNameWithExtension(folder, fileNameWithoutExtension);
                    if (fullPath != null)
                    {
                        //file exists
                        MDBFile mdbFile;
                        mdb.RegisterFile(folder, fullPath, out mdbFile);
                        if (mdb.Images.Exist(nameof(MDBImage.FileID), mdbFile.ID)) continue;
                        //register image (image dataset missing)
                        imageData = mdb.TryLoadImageData(fullPath);
                    }           
                    //download
                    if (imageData == null)
                    {
                        this.LogInfo("Download FanArtTV Image <cyan>{0}", source);
                        imageData = mdb.TryDownloadImageData(source);
                        if (imageData == null)
                        {
                            this.LogInfo("<yellow>Invalid FanArtTV Artist image <default>for artist <red>{0}", artist.Name);
                            continue;
                        }
                    }
                    mdb.SaveArtistImage(folder, fileNameWithoutExtension, imageType, artist, imageData);
                }
                catch (Exception ex)
                {
                    this.LogWarning(ex, "Error saving artist image {0}.", artist);
                }
            }
        }

        void FanArtTV_ParseNode(MDBArtist artist, IEnumerable<MDBAlbum> albums, JsonNode node)
        {
            MDBImageType imageType = MDBImageType.Undefined;

            switch (node.Name)
            {
                case "mbid_id":
                case "name":
                    return;
                case "hdmusiclogo": imageType = MDBImageType.ArtistMusicLogoHD; break;
                case "musiclogo": imageType = MDBImageType.ArtistMusicLogo; break;
                case "albums": FanArtTV_ParseGuidRoot(artist, node); break;
                case "cdart": imageType = MDBImageType.AlbumCDArt; break;
                case "albumcover": imageType = MDBImageType.AlbumCover; break;
                case "artistthumb": imageType = MDBImageType.ArtistThumb; break;
                case "artistbackground": imageType = MDBImageType.ArtistBackground; break;
                case "musicbanner": imageType = MDBImageType.ArtistMusicBanner; break;
                default: throw new NotImplementedException(string.Format("Unknown FanArtTV node {0}", node.Name));
            }
            if (imageType == MDBImageType.Undefined) return;
            if (imageType.IsAlbumArt())
            {
                FanArtTV_CheckAlbumImage(imageType, artist, albums, node);
                return;
            }
            FanArtTV_CheckArtistImage(imageType, artist, node);
        }

        void FanArtTV_ParseGuidRoot(MDBArtist artist, JsonNode node)
        {
            foreach (JsonNode v in node.SubNodes)
            {
                if (string.IsNullOrEmpty(v.Name)) continue;
                var guid = new Guid(v.Name);
                var albums = mdb.Albums.GetStructs(
                    Search.FieldEquals(nameof(MDBAlbum.MusicBrainzAlbumGuid), guid) |
                    Search.FieldEquals(nameof(MDBAlbum.MusicBrainzReleaseGroupGuid), guid));

                foreach (JsonNode n in v.SubNodes)
                {
                    FanArtTV_ParseNode(artist, albums, n);
                }
            }
        }

        void FanArtTV_DownloadArtist(MDBArtist artist)
        {
            byte[] data = null;
            JsonReader reader = null;
            {
				this.LogDebug("Starting FanArtTV lookup of {0}", artist);
				string fileName = FileSystem.Combine(mdb.CacheFolder, "FanArtTV", artist.MusicBrainzArtistGuid.ToString() + ".json");
                if (File.Exists(fileName) && (DateTime.UtcNow - FileSystem.GetLastWriteTimeUtc(fileName) < TimeSpan.FromDays(30)))
                {
					this.LogDebug("Load from cache {0}", fileName);
                    try { reader = new JsonReader(File.ReadAllBytes(fileName)); }
                    catch { try { File.Delete(fileName); } catch { } }
                }
                if (reader == null)
                {
					//download json
					if (!mdb.CheckBlackList(MDBCrawlerBlackListItemType.FanArtTV, artist.MusicBrainzArtistGuid)) return;

					for(int retry = 0; ;retry++)
                    {
						if (m_Exit) return;
                        try
                        {
                            this.LogDebug("Download FanArtTV Artist <cyan>" + artist.Name + " <default>" + artist.MusicBrainzArtistGuid);
							var http = new HttpConnection();
							http.Timeout = TimeSpan.FromSeconds(30);
                            data = http.Download("http://webservice.fanart.tv/v3/music/" + artist.MusicBrainzArtistGuid + "?api_key=" + FanArtTV_APIKEY);
                            reader = new JsonReader(data);
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
							File.WriteAllBytes(fileName, data);
                            this.LogNotice("<green>New fanart.tv dataset <default>for <cyan>" + artist.Name + " <default>" + artist.MusicBrainzArtistGuid);
                            break;
                        }
                        catch (WebException wex)
                        {
							if (retry < 10 && mdb.CheckDownloadRetry(wex))
							{
								Thread.Sleep(1000);
								this.LogDebug(wex, "Retry {0}: {1} <red>{2}", retry, artist, wex.Message);
								continue;
							}
							this.LogWarning(wex, "<yellow>No fanart.tv dataset <default>for {0} - <red>{1}", artist, wex.Message);
							return;
						}
                        catch (Exception ex)
                        {
							this.LogWarning(ex, "<yellow>No fanart.tv dataset <default>for {0} - <red>{1}", artist, ex.Message);
                            return;
                        }
                    }
                }
            }
            foreach (var node in reader.Root.SubNodes)
            {
                try
                {
                    FanArtTV_ParseNode(artist, null, node);
                }
                catch (Exception ex)
                {
                    this.LogDebug(ex, "Error while parsing FanArtTV node");
                    return;
                }
            }

            this.LogVerbose(string.Format("<cyan>{0} <default>{1} <green>ok", artist.Name, artist.MusicBrainzArtistGuid));
        }

        void FanArtTV_CrawlArtists()
        {
            if (m_Exit) return;
            float max = mdb.Artists.RowCount;
            long counter = 0;

			//do not stress fanart.tv with multithreaded downloads...
			foreach (MDBArtist artist in mdb.Artists.GetStructs(Search.None, ResultOption.SortAscending(nameof(MDBArtist.Name))))
            {
                if (artist.MusicBrainzArtistGuid == null) continue;

				try
				{
					FanArtTV_DownloadArtist(artist);
				}
				catch (Exception ex)
				{
					this.LogWarning(ex, "Error while crawling artist {0}", artist);
					Thread.Sleep(1000);
				}

                progressFanArtTV = counter++ / max;
                if (m_Exit) return;
            };
            progressFanArtTV = 1;
        }
        #endregion

        #region MusicBrainz
        JsonReader MusicBrainz_Get(string uri, string path, Guid mbid)
        {
            bool writeFile = false;
            string mbJsonFile = FileSystem.Combine(path, mbid + ".json");
            string mbJsonData = null;
            if (File.Exists(mbJsonFile))
            {
                mbJsonData = File.ReadAllText(mbJsonFile);
            }
            if (mbJsonData == null)
            {
                bool ok = false;
                for (int i = 0; i < 1000; i++)
                {
                    try
                    {
                        if (i == 0) this.LogDebug("Download MusicBrainz " + uri);
                        if (m_Exit) return null;
                        string mbPageData = HttpConnection.GetString(uri);
                        mbJsonData = StringExtensions.GetString(mbPageData, -1, "<script type=\"application/ld+json\">", "</script>");
                        ok = true;
                        break;
                    }
                    catch (WebException ex)
                    {
						if (mdb.CheckDownloadRetry(ex))
						{
							Thread.Sleep(1000);
							this.LogDebug(ex, "Retry: {0} <red>{1}", uri, ex.Message);
							continue;
						}
                        this.LogDebug(ex, "<yellow>No musicbrainz dataset<default> for {0} <red>{1}", uri, ex.Message);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        this.LogDebug(ex, "<yellow>No musicbrainz dataset<default> for {0} <red>{1}", uri, ex.Message);
                        return null;
                    }
                }
                if (!ok)
                {
                    this.LogError("<red>Error:<default> Could not retrieve <red>" + uri);
                    return null;
                }
                writeFile = true;
            }
            var reader = new JsonReader(Encoding.UTF8.GetBytes(mbJsonData));
            if (writeFile) File.WriteAllText(mbJsonFile, mbJsonData);
            return reader;
        }

        List<Guid> MusicBrainz_GetReleaseGroups(Guid musicBrainzGuid)
        {
            var validGuids = new List<Guid>();

            string uri = "http://musicbrainz.org/release-group/" + musicBrainzGuid;
            string path = FileSystem.Combine(mdb.CacheFolder, "MusicBrainz", "Release");
            JsonReader reader = MusicBrainz_Get(uri, path, musicBrainzGuid);
            if (reader == null) return validGuids;

            string id = reader.Root["@id"].Value.ToString(); validGuids.Add(new Guid(id.Substring(id.LastIndexOf('/') + 1)));
            string name = reader.Root["name"].Value.ToString();

            foreach (JsonNode node in reader.Root.SubNodes)
            {
                if (node.Name != "sameAs") continue;
                switch (node.Type)
                {
                    case JsonNodeType.Value:
                        {
                            string[] parts = node.Value.ToString().Split('/');
                            if (parts.Length < 2) continue;
                            id = parts[parts.Length - 1];
                            if (parts[parts.Length - 2] != "release-group") continue;
                            try { validGuids.Add(new Guid(id)); }
                            catch { }
                        }
                        break;
                    case JsonNodeType.Array:
                        foreach (object value in reader.Root["sameAs"].Values)
                        {
                            string[] parts = value.ToString().Split('/');
                            if (parts.Length < 2) continue;
                            id = parts[parts.Length - 1];
                            if (parts[parts.Length - 2] != "release-group") continue;
                            try { validGuids.Add(new Guid(id)); }
                            catch { }
                        }
                        break;
                }
            }

            return validGuids;
        }

        void MusicBrainz_CrawlAlbums()
        {
            if (m_Exit) return;

            long counter = 0;
            var guids = mdb.Albums.GetValues<BinaryGuid>(nameof(MDBAlbum.MusicBrainzReleaseGroupGuid));
            float max = guids.Count;

            foreach (var releaseGroup in guids)
            {
				progressMusicBrainz = counter++ / max;
				if (m_Exit) return;

				var altReleaseGroups = MusicBrainz_GetReleaseGroups(releaseGroup);
                var albums = mdb.Albums.GetStructs(Search.FieldIn(nameof(MDBAlbum.MusicBrainzReleaseGroupGuid), altReleaseGroups));

                foreach (Guid altReleaseGroup in altReleaseGroups)
                {
                    try
                    {
                        string uri = "http://coverartarchive.org/release-group/" + altReleaseGroup + "/front.jpg";
                        string name = Base32.Safe.Encode(releaseGroup.ToArray());

                        string fileNameWithoutExtension = MDBImageType.AlbumCoverFront + " " + name;
                        //check if each album has this image, autoload if we already have it at any album
                        byte[] imageData;
                        List<MDBAlbum> albumsWithMissingImages;
                        FindAlbumImage(albums, fileNameWithoutExtension, out imageData, out albumsWithMissingImages);

                        if (albumsWithMissingImages.Count == 0) continue;
                        //try load from files table
                        if (imageData == null)
                        {
                            foreach (var file in mdb.Files.GetStructs(nameof(MDBFile.Name), fileNameWithoutExtension))
                            {
                                string fullPath = file.GetFullPath(mdb);
                                imageData = mdb.TryLoadImageData(fullPath);
                                if (imageData == null)
                                {
									File.Delete(fullPath);
                                    mdb.Files.Delete(file.ID);
                                }
                            }
                        }
                        //if not found, load from web
                        if (imageData == null)
                        {
							if (!mdb.CheckBlackList(MDBCrawlerBlackListItemType.CoverArtArchive, releaseGroup)) continue;

							this.LogInfo("Download MusicBrainz Image <blue>{0}", uri);
                            imageData = mdb.TryDownloadImageData(uri);
                            if (imageData == null)
                            {
                                this.LogInfo("<yellow>No MusicBrainz Cover Art dataset <default>for release group <red>{0}", altReleaseGroup);
                                continue;
                            }
                        }
                        //save to all albums
                        foreach (MDBAlbum album in albumsWithMissingImages)
                        {
                            var artist = mdb.Artists[album.ArtistID];
                            var albumFolder = GetAlbumFolder(album);
                            mdb.SaveAlbumImage(albumFolder, fileNameWithoutExtension, MDBImageType.AlbumCoverFront, album, imageData);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.LogWarning(ex, "Error saving {0} image {1} for album {2}.", MDBImageType.AlbumCoverFront, altReleaseGroup, albums);
                    }
                }
            };
            progressMusicBrainz = 1;
        }

		#endregion

		#endregion

		public ArtCrawler(MusicDataBase mdb)
		{
			this.mdb = mdb;
		}

		/// <summary>Gets a value indicating whether this <see cref="T:Cave.MDB.IMDBCrawler" /> is completed.</summary>
		/// <value><c>true</c> if completed; otherwise, <c>false</c>.</value>
		public override bool Completed { get { return m_Task?.IsCompleted != false; } }

        /// <summary>Gets a value indicating whether this <see cref="T:Cave.MDB.IMDBCrawler" /> is error.</summary>
        /// <value><c>true</c> if error; otherwise, <c>false</c>.</value>
        public override bool Error { get { return m_Task?.IsFaulted != false; } }

        /// <summary>Gets the exception.</summary>
        /// <value>The exception.</value>
        public override Exception Exception { get { return m_Task.Exception; } }

        /// <summary>Gets the progress.</summary>
        /// <value>The progress.</value>
        public override MDBProgress[] Progress
        {
            get
            {
                return new MDBProgress[]
                {
                    new MDBProgress() { ID = 1, Progress = progressFanArtTV, Type = "FanArtTV Artist scanner", Source = "ArtCrawler", },
                    new MDBProgress() { ID = 2, Progress = progressMusicBrainz, Type = "Music Brainz Cover Art Archive scanner", Source = "ArtCrawler", },
                    new MDBProgress() { ID = 3, Progress = progressCleanup, Type = "Cleanup", Source = "ArtCrawler", },
                };
            }
        }

		public override string Name => "ArtCrawler";

		/// <summary>Gets a value indicating whether this <see cref="ArtCrawler"/> is started.</summary>
		/// <value><c>true</c> if started; otherwise, <c>false</c>.</value>
		public override bool Started { get { return m_Task != null; } }

        /// <summary>Starts this instance.</summary>
        /// <exception cref="Exception">Already running!</exception>
        public override void Start()
		{
			if (m_Task != null)
			{
				throw new Exception("Already running!");
			}
			this.LogInfo("Arts.Artists folder: {0}", mdb.ArtistArtFolder);
            Directory.CreateDirectory(mdb.ArtistArtFolder);
			this.LogInfo("Arts.Cache folder: {0}", mdb.CacheFolder);
			Directory.CreateDirectory(mdb.CacheFolder);
            Directory.CreateDirectory(FileSystem.Combine(mdb.CacheFolder, "FanArtTV"));
            Directory.CreateDirectory(FileSystem.Combine(mdb.CacheFolder, "MusicBrainz", "Artist"));
            Directory.CreateDirectory(FileSystem.Combine(mdb.CacheFolder, "MusicBrainz", "Release"));
            m_Exit = false;
            m_Task = Task.Factory.StartNew(Worker);
        }

        /// <summary>Stops this instance.</summary>
        public override void Stop()
        {            
            m_Exit = true;
            Task t = m_Task;
			m_Task = null;
            if (t != null)
            {
                m_Task = null;
                t.Wait();
                t.Dispose();
            }
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public override void Dispose()
        {
            Stop();
        }

        public override void Wait()
        {
            m_Task?.Wait();
        }
    }
}