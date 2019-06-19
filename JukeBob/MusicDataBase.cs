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
using Cave.Console;
using Cave.Data;
using Cave.IO;
using Cave.Logging;
using Cave.Media;
using Cave.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JukeBob
{
    /// <summary>Provides the music database</summary>
    /// <seealso cref="Cave.Logging.ILogSource" />
    public class MusicDataBase : ILogSource
    {
        static object UpdateLock { get; } = new object();

        /// <summary>The tables -> sequence numbers lookup</summary>
        Dictionary<ITable, long> m_Tables = new Dictionary<ITable, long>();

        bool SaveImage(byte[] data, MDBFolder mdbFolder, string name, ref MDBImage image, object obj)
        {
            string fullPath;
            var file = new MDBFile()
            {
                FolderID = mdbFolder.ID,
                Name = name,
            };

            ImageType imgType;
            switch (image.Type)
            {
                case MDBImageType.ArtistMusicBanner:
                case MDBImageType.ArtistMusicLogo:
                case MDBImageType.ArtistMusicLogoHD:
                case MDBImageType.AlbumCDArt:
                    fullPath = mdbFolder.GetFullPath(this, name + ".png");
                    file.Extension = ".png";
                    file.FileType = MDBFileType.png;
                    imgType = ImageType.Png;
                    image.MimeType = "image/png";
                    break;
                default:
                    fullPath = mdbFolder.GetFullPath(this, name + ".jpg");
                    file.Extension = ".jpg";
                    imgType = ImageType.Jpeg;
                    file.FileType = MDBFileType.jpeg;
                    image.MimeType = "image/jpg";
                    break;
            }

            int width, height;
            using (var img = Bitmap32.Create(data))
            {
                width = img.Width; height = img.Height;
                //save if not present at disk
                if (!File.Exists(fullPath))
                {
                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, imgType, 99);
                        data = ms.ToArray();
                    }
                }
            }

            bool writeFile = true;
            //find old dataset (check for replace)
            {
                if (TryGetFile(fullPath, false, out MDBFile mdbFile))
                {
                    file.ID = mdbFile.ID;
                    if (mdbFile.GetFullPath(this) == fullPath)
                    {
                        writeFile = false;
                    }
                    else
                    {
                        string oldPath = mdbFile.GetFullPath(this);
                        File.Delete(oldPath);
                    }
                }
            }

            //save image data
            if (writeFile)
            {
                foreach (string oldFile in Directory.GetFiles(Path.GetDirectoryName(fullPath), Path.GetFileNameWithoutExtension(fullPath) + ".*"))
                {
                    File.Delete(oldFile);
                    if (TryGetFile(oldFile, false, out MDBFile mdbFile))
                    {
                        Files.Delete(mdbFile.ID);
                    }

                    this.LogInfo("Deleted old file {0}", oldFile);
                }
                File.WriteAllBytes(fullPath, data);
                this.LogInfo("Saved new image {0}", fullPath);
            }
            //get fileinfo
            var fileInfo = new FileInfo(fullPath);
            //create file dataset
            file.DateTime = fileInfo.LastWriteTimeUtc;
            file.Size = fileInfo.Length;
            if (file.ID > 0)
            {
                Files.Replace(file);
            }
            else
            {
                file.ID = Files.Insert(file);
            }
            //update image dataset 
            image.Width = width;
            image.Height = height;
            image.FileID = file.ID;
            if (Images.Exist(file.ID))
            {
                Images.Replace(image);
                this.LogNotice("<cyan>Update {0} image<default> dataset for <yellow>{1} <default>{2}", image, obj, mdbFolder);
                return false;
            }
            else
            {
                Images.Insert(image);
                this.LogNotice("<green>New {0} image<default> dataset for <yellow>{1} <default>{2}", image, obj, mdbFolder);
                return true;
            }
        }

        /// <summary>Initializes a new instance of the <see cref="MusicDataBase"/> class.</summary>
        /// <param name="rootFolder">The root folder.</param>
        /// <exception cref="FileNotFoundException">WebFolder missing!</exception>
        /// <exception cref="Exception"></exception>
        public MusicDataBase(string rootFolder)
        {
            this.LogVerbose("Initializing MusicDataBase");

            RootFolder = rootFolder;
            ConfigFileName = FileSystem.Combine(rootFolder, "JukeBob.ini");
            this.LogVerbose("Loading configuration {0}", ConfigFileName);
            Config = IniReader.FromFile(ConfigFileName);
            if (!Config.HasSection("MusicDataBase"))
            {
                throw new FileNotFoundException($"Missing configuration {ConfigFileName}!", ConfigFileName);
            }

            foreach (string folder in Config.ReadSection("MusicFolders", true))
            {
                string f = Path.GetFullPath(folder);
                this.LogVerbose("MusicFolder {0} => {1}", folder, f);
                if (!Directory.Exists(f))
                {
                    throw new DirectoryNotFoundException(string.Format("MusicFolder {0} not found!", folder));
                }

                MusicFolders.Include(f);
            }

            DataBasePath = GetFolderConfig("MusicDataBase", "DatabaseFolder", "database");
            this.LogVerbose("Using Database path {0}", DataBasePath);
            try
            {
                ArtistArtFolder = GetFolderConfig("MusicDataBase", "ArtFolder", "art");
                this.LogVerbose("ArtistArtFolder {0}", ArtistArtFolder);
                Directory.CreateDirectory(ArtistArtFolder);

                CacheFolder = GetFolderConfig("MusicDataBase", "CacheFolder", "cache");
                this.LogVerbose("CacheFolder {0}", CacheFolder);
                Directory.CreateDirectory(CacheFolder);

                WebFolder = GetFolderConfig("MusicDataBase", "WebFolder", "web");
                this.LogVerbose("WebFolder {0}", WebFolder);
                if (!File.Exists(FileSystem.Combine(WebFolder, "index.cwt")))
                {
                    throw new FileNotFoundException("WebFolder missing!");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Could not load configuration from {0}", ConfigFileName), ex);
            }
        }

        public MDBHostInformation Host;

        public Guid CreateInvalidGuid(long id)
        {
            byte[] data = new byte[16];
            BitConverterBE.Instance.GetBytes(id).CopyTo(data, 8);
            return new Guid(data);
        }

        public string GetFolderConfig(string section, string name, string defaultValue = null)
        {
            string p = Config.ReadString(section, name, defaultValue);
            if (p.StartsWith("~"))
            {
                return Path.GetFullPath(FileSystem.Combine(FileSystem.ProgramDirectory, "./" + p.Substring(1)));
            }
            else
            {
                return Path.GetFullPath(FileSystem.Combine(RootFolder, p));
            }
        }

        public void SetHostConfiguration(string deviceName, int webPort, int ftpPort, IEnumerable<IPAddress> list)
        {
            Host = new MDBHostInformation()
            {
                ID = 1,
                Name = deviceName,
                WebPort = webPort,
                FtpPort = ftpPort,
                IPAddresses = list.Join(";"),
            };
        }

        /// <summary>Gets the music folders.</summary>
        /// <value>The music folders.</value>
        public Set<string> MusicFolders { get; } = new Set<string>();

        /// <summary>Gets the database.</summary>
        /// <value>The database.</value>
        public IDatabase Database { get; private set; }

        /// <summary>Gets the tables.</summary>
        /// <value>The tables.</value>
        public List<ITable> Tables
        {
            get
            {
                lock (m_Tables)
                {
                    return m_Tables.Keys.ToList();
                }
            }
        }

        /// <summary>Saves the specified table.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table">The table.</param>
        public void SaveTable<T>(ITable<T> table) where T : struct
        {
            if (Database != null)
            {
                return;
            }

            var memoryTable = (IMemoryTable<T>)table;
            lock (m_Tables)
            {
                //table was updated ? no -> exit
                if (m_Tables[memoryTable] == memoryTable.SequenceNumber)
                {
                    return;
                }
                //yes, save
                string fullPath = FileSystem.Combine(DataBasePath, memoryTable.Name + ".dat");
                lock (memoryTable)
                {
                    DatWriter.WriteTable(memoryTable, fullPath);
                    m_Tables[memoryTable] = memoryTable.SequenceNumber;
                }
                this.LogInfo("Saved table {0}", memoryTable);
            }
            return;
        }

        /// <summary>Loads the specified table.</summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ITable<T> LoadTable<T>() where T : struct
        {
            if (Database != null)
            {
                ITable<T> table = Database.GetTable<T>(TableFlags.AllowCreate);
                this.LogInfo("Loaded {0} from {1}", table, Database);
                return table;
            }

            IMemoryTable<T> memoryTable = new ConcurrentMemoryTable<T>();
            string fullPath = FileSystem.Combine(DataBasePath, memoryTable.Name + ".dat");
            lock (memoryTable)
            {
                memoryTable.Clear();
                lock (m_Tables)
                {
                    m_Tables[memoryTable] = 0;
                }

                if (File.Exists(fullPath))
                {
                    try
                    {
                        DatReader.ReadTable(memoryTable, fullPath);
                        this.LogInfo("Loaded {0} from file.", memoryTable);
                        lock (m_Tables)
                        {
                            m_Tables[memoryTable] = memoryTable.SequenceNumber;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.LogWarning(ex, "Could not load {0}.", memoryTable);
                    }
                }
            }
            return memoryTable;
        }

        public MDBFolder GetArtistArtFolder(MDBArtist artist)
        {
            string guidData = Base32.Safe.Encode(artist.MusicBrainzArtistGuid.ToArray());

            var sb = new StringBuilder();
            int i = 0;
            foreach (char c in guidData)
            {
                if (++i == 2)
                {
                    sb.Append('/');
                    i = 0;
                }
                sb.Append(c);
            }
            string folder = FileSystem.Combine(ArtistArtFolder, sb.ToString());
            Directory.CreateDirectory(folder);
            return GetOrCreateFolder(folder);
        }

        /// <summary>Gets the stream settings.</summary>
        /// <value>The stream settings.</value>
        public ITable<MDBStreamSetting> StreamSettings { get; private set; }

        /// <summary>Gets the albums.</summary>
        /// <value>The albums.</value>
        public ITable<MDBAlbum> Albums { get; private set; }

        /// <summary>Gets the artists.</summary>
        /// <value>The artists.</value>
        public ITable<MDBArtist> Artists { get; private set; }

        /// <summary>Gets the audio files.</summary>
        /// <value>The audio files.</value>
        public ITable<MDBAudioFile> AudioFiles { get; private set; }

        /// <summary>Gets the categories.</summary>
        /// <value>The categories.</value>
        public ITable<MDBCategory> Categories { get; private set; }

        /// <summary>Gets the files.</summary>
        /// <value>The files.</value>
        public ITable<MDBFile> Files { get; private set; }

        /// <summary>Gets the folders.</summary>
        /// <value>The folders.</value>
        public ITable<MDBFolder> Folders { get; private set; }

        /// <summary>Gets the genres.</summary>
        /// <value>The genres.</value>
        public ITable<MDBGenre> Genres { get; private set; }

        /// <summary>Gets the images.</summary>
        /// <value>The images.</value>
        public ITable<MDBImage> Images { get; private set; }

        /// <summary>Gets the play list items.</summary>
        /// <value>The play list items.</value>
        public ITable<MDBPlayListItem> PlayListItems { get; private set; }

        /// <summary>Gets the now playing.</summary>
        /// <value>The now playing.</value>
        public ITable<MDBNowPlaying> NowPlaying { get; private set; } = new SynchronizedMemoryTable<MDBNowPlaying>();

        /// <summary>Gets the subset filters.</summary>
        /// <value>The subset filters.</value>
        public ITable<MDBSubsetFilter> SubsetFilters { get; private set; }

        /// <summary>Gets the subsets.</summary>
        /// <value>The subsets.</value>
        public ITable<MDBSubset> Subsets { get; private set; }

        /// <summary>Gets the tags.</summary>
        /// <value>The tags.</value>
        public ITable<MDBTag> Tags { get; private set; }

        /// <summary>Gets the crawler black list.</summary>
        /// <value>The crawler black list.</value>
        public ITable<MDBCrawlerBlackListItem> CrawlerBlackList { get; private set; }


        /// <summary>Gets the name of the configuration file.</summary>
        /// <value>The name of the configuration file.</value>
        public string ConfigFileName { get; }

        /// <summary>Gets the root folder for all paths.</summary>
        /// <value>The root folder for all paths.</value>
        public string RootFolder { get; private set; }

        /// <summary>Gets the data base path.</summary>
        /// <value>The data base path.</value>
        public string DataBasePath { get; }

        /// <summary>Gets the configuration.</summary>
        /// <value>The configuration.</value>
        public IniReader Config { get; }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "MDBTables";

        /// <summary>Gets the artist art folder.</summary>
        /// <value>The artist art folder.</value>
        public string ArtistArtFolder { get; }

        /// <summary>Gets the cache folder.</summary>
        /// <value>The cache folder.</value>
        public string CacheFolder { get; }

        /// <summary>Gets the play list sequence number.</summary>
        /// <value>The play list sequence number.</value>
        public int PlayListSequenceNumber
        {
            get
            {
                var table = PlayListItems as IMemoryTable<MDBPlayListItem>;
                if (table != null)
                {
                    return table.SequenceNumber;
                }

                long result = PlayListItems.RowCount;
                foreach (long id in PlayListItems.IDs)
                {
                    result ^= id;
                }

                return result.GetHashCode();
            }
        }


        /// <summary>Gets the web folder.</summary>
        /// <value>The web folder.</value>
        public string WebFolder { get; }

        /// <summary>Saves this instance.</summary>
        public void Save()
        {
            if (Database == null)
            {
                Directory.CreateDirectory(DataBasePath);
            }

            SaveTable(Albums);
            SaveTable(Artists);
            SaveTable(AudioFiles);
            SaveTable(Categories);
            SaveTable(Files);
            SaveTable(Folders);
            SaveTable(Genres);
            SaveTable(Images);
            SaveTable(SubsetFilters);
            SaveTable(Subsets);
            SaveTable(Tags);
            SaveTable(PlayListItems);
            SaveTable(StreamSettings);
            CleanBlackList();
            SaveTable(CrawlerBlackList);
        }

        /// <summary>Loads this instance.</summary>
        public void Load()
        {
            if (Database == null)
            {
                //todo check why we need this catch block
                ConnectionString.TryParse(Config.ReadString("MusicDataBase", "Database", ""), out ConnectionString connection);
                switch (connection.Protocol)
                {
                    case null: case "": case "memory": break;
#if NETSTANDARD20
#else
                    default:
                        this.LogVerbose("Connecting to {0}", connection);
                        DbConnectionOptions options = DbConnectionOptions.AllowCreate | DbConnectionOptions.AllowUnsafeConnections;
                        options |= Arguments.FromEnvironment().IsOptionPresent("dbverbose") ? DbConnectionOptions.VerboseLogging : DbConnectionOptions.None;
                        Database = Connector.ConnectDatabase(connection, options);
                        this.LogInfo("Using database connection {0}", connection);
                        break;
#endif
                }
            }
            Albums = LoadTable<MDBAlbum>();
            Artists = LoadTable<MDBArtist>();
            AudioFiles = LoadTable<MDBAudioFile>();
            Categories = LoadTable<MDBCategory>();
            Files = LoadTable<MDBFile>();
            Folders = LoadTable<MDBFolder>();
            Genres = LoadTable<MDBGenre>();
            Images = LoadTable<MDBImage>();
            SubsetFilters = LoadTable<MDBSubsetFilter>();
            Subsets = LoadTable<MDBSubset>();
            Tags = LoadTable<MDBTag>();
            PlayListItems = LoadTable<MDBPlayListItem>();
            StreamSettings = LoadTable<MDBStreamSetting>();
            CrawlerBlackList = LoadTable<MDBCrawlerBlackListItem>();
            CleanBlackList();
        }

        /// <summary>Gets the best image from the specified list using the specified types with descending priority.</summary>
        /// <param name="randomKey">The random key.</param>
        /// <param name="images">The images.</param>
        /// <param name="types">The types (priority by index).</param>
        /// <returns></returns>
        public MDBImage GetBestImage(int randomKey, IEnumerable<MDBImage> images, params MDBImageType[] types)
        {
            try
            {
                if (randomKey < 0)
                {
                    randomKey = -randomKey;
                }

                var result = new List<MDBImage>();
                foreach (MDBImageType type in types)
                {
                    foreach (MDBImage img in images)
                    {
                        try
                        {
                            if (img.Type != type)
                            {
                                continue;
                            }

                            if (!Files.TryGetStruct(img.FileID, out MDBFile file))
                            {
                                continue;
                            }

                            string fullPath = file.GetFullPath(this);
                            if (!File.Exists(fullPath))
                            {
                                continue;
                            }

                            result.Add(img);
                        }
                        catch (Exception ex)
                        {
                            this.LogWarning(ex, "Cannot access image file {0}", img);
                            Images.TryDelete(img.FileID);
                            Files.TryDelete(img.FileID);
                        }
                    }
                    if (result.Count > 0)
                    {
                        result.Sort();
                        return result[randomKey % result.Count];
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogWarning(ex, "Error looking up best image for {0}.", types);
            }
            return default(MDBImage);
        }

        /// <summary>Gets or creates a folder dataset.</summary>
        /// <param name="fullPath">The full path.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Invalid root!</exception>
        public MDBFolder GetOrCreateFolder(string fullPath)
        {
            MDBFolder folder;
            bool newFolder = false;
            lock (Folders)
            {
                if (!Folders.TryGetStruct(Search.FieldEquals(nameof(MDBFolder.Name), fullPath), out folder))
                {
                    folder = new MDBFolder() { Name = fullPath, };
                    folder.ID = Folders.Insert(folder);
                    newFolder = true;
                }
            }
            if (newFolder)
            {
                this.LogInfo("New folder <green>{0}", folder);
            }

            return folder;
        }

        /// <summary>Removes the file.</summary>
        /// <param name="mdbFile">The MDB file.</param>
        public void RemoveFile(MDBFile mdbFile)
        {
            if (Files.Exist(mdbFile.ID))
            {
                Files.Delete(mdbFile.ID);
            }

            this.LogInfo("Removed file '<red>{0}<default>'", mdbFile.Name);
            if (mdbFile.IsImage)
            {
                foreach (MDBImage imageFile in Images.GetStructs(nameof(MDBImage.FileID), mdbFile.ID))
                {
                    Images.Delete(imageFile.FileID);
                    this.LogInfo("Removed image file <red>{0}", imageFile);
                }
            }
            else
            {
                foreach (MDBAudioFile audioFile in AudioFiles.GetStructs(nameof(MDBAudioFile.FileID), mdbFile.ID))
                {
                    AudioFiles.Delete(audioFile.FileID);
                    this.LogInfo("Removed audio file <red>{0}", audioFile);
                }
            }
        }

        /// <summary>Gets the type of the file.</summary>
        /// <param name="extension">The extension.</param>
        /// <returns></returns>
        public MDBFileType GetFileType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".mp3": return MDBFileType.mp3;
                case ".jpg": case ".jpeg": return MDBFileType.jpeg;
                case ".png": return MDBFileType.png;
                case ".bmp": return MDBFileType.bmp;
                default: return MDBFileType.unknown;
            }
        }

        /// <summary>Checks the black list and returns whether the item is new or not.</summary>
        /// <param name="itemType">Type of the item.</param>
        /// <param name="itemGuid">The item unique identifier.</param>
        /// <returns>Returns true if the item was not blacklisted, false otherwise</returns>
        public bool CheckBlackList(MDBCrawlerBlackListItemType itemType, BinaryGuid itemGuid)
        {
            lock (CrawlerBlackList)
            {
                MDBCrawlerBlackListItem item = CrawlerBlackList.TryGetStruct(
                    Search.FieldEquals(nameof(MDBCrawlerBlackListItem.Type), itemType) &
                    Search.FieldEquals(nameof(MDBCrawlerBlackListItem.Guid), itemGuid));
                //item already present?
                if (item.ID > 0)
                {
                    //yes, is expired -> no = false
                    if (DateTime.UtcNow < item.Expire)
                    {
                        return false;
                    }
                    //yes = true;
                }
                else
                {
                    //new blacklist item
                    CrawlerBlackList.Insert(new MDBCrawlerBlackListItem()
                    {
                        Guid = itemGuid,
                        Type = itemType,
                        Expire = DateTime.UtcNow.AddDays(14),
                    });
                }
                return true;
            }
        }

        /// <summary>Removes all expired items from the blacklist.</summary>
        public void CleanBlackList()
        {
            CrawlerBlackList?.TryDelete(Search.FieldSmaller(nameof(MDBCrawlerBlackListItem.Expire), DateTime.UtcNow));
        }

        /// <summary>Registers the file.</summary>
        /// <param name="mdbFolder">The folder.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="mdbFile">The MDB file.</param>
        /// <returns></returns>
        public MDBUpdateType RegisterFile(MDBFolder mdbFolder, string fileName, out MDBFile mdbFile)
        {
            mdbFile = default(MDBFile);

            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            MDBFileType fileType = GetFileType(extension);
            if (fileType == MDBFileType.unknown)
            {
                return MDBUpdateType.Ignored;
            }

            foreach (MDBFile f in Files.GetStructs(
                Search.FieldEquals(nameof(MDBFile.Name), name) &
                Search.FieldEquals(nameof(MDBFile.Extension), extension) &
                Search.FieldEquals(nameof(MDBFile.FileType), fileType) &
                Search.FieldEquals(nameof(MDBFile.FolderID), mdbFolder.ID)))
            {
                if (mdbFile.ID == 0)
                {
                    mdbFile = f;
                }
                else
                {
                    this.LogWarning("Delete duplicate file {0}", f);
                    Files.Delete(f.ID);
                }
            }

            string fullPath = mdbFolder.GetFullPath(this, fileName);
            var fileInfo = new FileInfo(fullPath);
            long fileSize = fileInfo.Length;
            DateTime fileLastWriteTime = fileInfo.LastWriteTimeUtc;

            bool replaceDataset = false;

            if (mdbFile.Name != name) { replaceDataset = true; this.LogDebug("File new {0}", name); }
            else if (mdbFile.Extension != extension) { replaceDataset = true; this.LogDebug("File new {0}", name); }
            else if (mdbFile.DateTime.ToUniversalTime().Ticks != fileLastWriteTime.ToUniversalTime().Ticks) { replaceDataset = true; this.LogDebug("File datetime changed {0}", name); }
            else if (mdbFile.Size != fileSize) { replaceDataset = true; this.LogDebug("File size changed {0}", name); }
            else if (mdbFile.FileType != fileType) { replaceDataset = true; this.LogDebug("File type changed {0}", name); }
            else if (mdbFile.FolderID != mdbFolder.ID) { replaceDataset = true; this.LogDebug("File FolderID changed {0}", name); }

            if (replaceDataset)
            {
                if (mdbFile.ID > 0)
                {
                    this.LogDebug("File changed? Scanning whole file again: {0}", fullPath);
                }

                mdbFile.FileType = fileType;
                mdbFile.FolderID = mdbFolder.ID;
                mdbFile.DateTime = fileLastWriteTime;
                mdbFile.Size = fileSize;
                mdbFile.Name = name;
                mdbFile.Extension = extension;
            }

            if (mdbFile.ID <= 0)
            {
                this.LogInfo("New file <cyan>{0}", fullPath);
                mdbFile.ID = Files.Insert(mdbFile);
                return MDBUpdateType.New;
            }
            else if (replaceDataset)
            {
                this.LogInfo("Update file <cyan>{0}", fullPath);
                Files.Replace(mdbFile);
                return MDBUpdateType.Updated;
            }
            return MDBUpdateType.NoChange;
        }

        /// <summary>Checks whether the download should be retried.</summary>
        /// <param name="wex">The WebException.</param>
        /// <returns></returns>
        public bool CheckDownloadRetry(WebException wex)
        {
            HttpStatusCode? status = (wex.Response as HttpWebResponse)?.StatusCode;
            switch (status)
            {
                case HttpStatusCode.NotFound: return false;
                case (HttpStatusCode)520:
                case HttpStatusCode.ServiceUnavailable: return true;
            }
            switch (wex.Status)
            {
                case WebExceptionStatus.ProtocolError:
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.Timeout:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Tries to get file with the specified fullpath.</summary>
        /// <param name="fullPath">The full path.</param>
        /// <param name="checkExtension">if set to <c>true</c> check if the file exists with another extension.</param>
        /// <param name="file">The file.</param>
        /// <returns>Returns true on success, false otherwise</returns>
        public bool TryGetFile(string fullPath, bool checkExtension, out MDBFile file)
        {
            MDBFolder folder = Folders.TryGetStruct(nameof(MDBFolder.Name), Path.GetDirectoryName(fullPath));
            if (folder.ID > 0)
            {
                Search search =
                    Search.FieldEquals(nameof(MDBFile.FolderID), folder.ID) &
                    Search.FieldEquals(nameof(MDBFile.Name), Path.GetFileNameWithoutExtension(fullPath));
                if (checkExtension)
                {
                    search &= Search.FieldEquals(nameof(MDBFile.Extension), Path.GetExtension(fullPath));
                }

                var files = Files.GetStructs(search);
                for (int i = 1; i < files.Count; i++)
                {
                    this.LogWarning("Removing duplicate file registration. <red>{0}", files[i]);
                    Files.Delete(files[i].ID);
                }
                if (files.Count > 0) { file = files.First(); return true; }
            }
            file = default(MDBFile);
            return false;
        }

        /// <summary>Gets the full name of the image file.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="fileName">Name of the file without extension.</param>
        /// <returns></returns>
        public string GetImageFileNameWithExtension(MDBFolder folder, string fileName)
        {
            string[] files = Directory.GetFiles(folder.Name, fileName + ".*");
            if (!files.Any())
            {
                return null;
            }

            return files.First();
        }

        /// <summary>Saves the artist image.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="name">The name.</param>
        /// <param name="imageType">Type of the image.</param>
        /// <param name="artist">The artist.</param>
        /// <param name="data">The data.</param>
        /// <exception cref="Exception">Invalid image type!</exception>
        public void SaveArtistImage(MDBFolder folder, string name, MDBImageType imageType, MDBArtist artist, byte[] data)
        {
            var image = new MDBImage()
            {
                MusicBrainzGuid = artist.MusicBrainzArtistGuid,
                Type = imageType,
            };
            if (!image.IsArtistArt)
            {
                throw new Exception("Invalid image type!");
            }

            SaveImage(data, folder, name, ref image, artist);
            WriteArtistIni(folder, artist);
        }

        /// <summary>Writes the artist ini.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="artist">The artist.</param>
        /// <exception cref="ArgumentNullException">MusicBrainzArtistGuid</exception>
        public void WriteArtistIni(MDBFolder folder, MDBArtist artist)
        {
            if (artist.MusicBrainzArtistGuid == null)
            {
                throw new ArgumentNullException("MusicBrainzArtistGuid");
            }

            string artistIniFile = folder.GetFullPath(this, "artist.ini");
            FileSystem.TouchFile(artistIniFile);
            var ini = IniReader.FromFile(artistIniFile);

            var writer = new IniWriter(ini);
            writer.WriteSetting("Artist", "Guid", artist.MusicBrainzArtistGuid.ToString());
            var artists = new Set<string>(ini.ReadSection("Artists"));
            artists.Include(artist.Name);
            writer.WriteSection("Artists", artists);
            writer.Save();
        }

        /// <summary>Saves the album image.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="name">The name.</param>
        /// <param name="imageType">Type of the image.</param>
        /// <param name="album">The album.</param>
        /// <param name="data">The data.</param>
        /// <exception cref="Exception">Invalid image type!</exception>
        public void SaveAlbumImage(MDBFolder folder, string name, MDBImageType imageType, MDBAlbum album, byte[] data)
        {
            var image = new MDBImage()
            {
                MusicBrainzGuid = album.MusicBrainzReleaseGroupGuid,
                Type = imageType,
            };
            if (!image.IsAlbumArt)
            {
                throw new Exception("Invalid image type!");
            }

            SaveImage(data, folder, name, ref image, album);
        }

        /// <summary>Tries to load the image data.</summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">data</exception>
        public byte[] TryLoadImageData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                using (var img = Bitmap32.Create(data))
                {
                    return data;
                }
            }
            catch { return null; }
        }

        /// <summary>Tries to load the image data.</summary>
        /// <param name="fullPath">The full path.</param>
        /// <returns></returns>
        public byte[] TryLoadImageData(string fullPath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(fullPath);
                return TryLoadImageData(data);
            }
            catch { }
            return null;
        }

        /// <summary>Tries to download the image data.</summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public byte[] TryDownloadImageData(string url)
        {
            byte[] imageData = null;
            int i = 0;
            while (imageData == null && i++ < 10)
            {
                try
                {
                    var http = new HttpConnection
                    {
                        Timeout = TimeSpan.FromSeconds(60)
                    };
                    imageData = http.Download(url);
                    break;
                }
                catch (WebException ex)
                {
                    if (CheckDownloadRetry(ex))
                    {
                        Thread.Sleep(1000);
                        this.LogDebug(ex, "Retry: {0} <red>{1}", url, ex.Message);
                        continue;
                    }
                    this.LogDebug(ex, "Could not download <red>{0}", url);
                    break;
                }
                catch (Exception ex)
                {
                    this.LogDebug(ex, "Could not download <red>{0}", url);
                    break;
                }
            }
            if (imageData != null)
            {
                imageData = TryLoadImageData(imageData);
            }

            return imageData;
        }

        /// <summary>Gets the subset audio file IDs.</summary>
        /// <param name="subsetID">The subset identifier.</param>
        /// <param name="minDuration">The minimum duration.</param>
        /// <param name="maxDuration">The maximum duration.</param>
        /// <returns></returns>
        public IList<long> GetSubsetAudioFileIDs(long subsetID, TimeSpan minDuration, TimeSpan maxDuration)
        {
            Search search = Search.None;
            #region whitelist
            {
                var whitelist = SubsetFilters.GetStructs(Search.FieldEquals(nameof(MDBSubsetFilter.SubsetID), subsetID) & Search.FieldEquals(nameof(MDBSubsetFilter.Mode), MDBSubsetFilterMode.Whitelist));
                foreach (MDBSubsetFilter filter in whitelist)
                {
                    switch (filter.Type)
                    {
                        case MDBSubsetFilterType.Album:
                            foreach (long albumID in Albums.FindRows(Search.FieldLike(nameof(MDBAlbum.Name), MDBSearch.Text(filter.Text))))
                            {
                                search |= Search.FieldEquals(nameof(MDBAudioFile.AlbumID), albumID);
                            }
                            break;
                        case MDBSubsetFilterType.Artist:
                            foreach (long artistID in Artists.FindRows(Search.FieldLike(nameof(MDBArtist.Name), MDBSearch.Text(filter.Text))))
                            {
                                search |= Search.FieldEquals(nameof(MDBAudioFile.SongArtistID), artistID);
                            }
                            break;
                        case MDBSubsetFilterType.Category:
                        {
                            var ids = Categories.FindRows(nameof(MDBCategory.Name), MDBSearch.Text(filter.Text));
                            if (ids.Count != 1) { this.LogError(string.Format("Error at SubsetFilter {0}", filter)); }
                            foreach (long id in ids)
                            {
                                search |= Search.FieldEquals(nameof(MDBAudioFile.CategoryID), id);
                            }
                            break;
                        }
                        case MDBSubsetFilterType.Tag:
                        {
                            search |= Search.FieldLike(nameof(MDBAudioFile.Tags), MDBSearch.Text(filter.Text));
                            break;
                        }
                        case MDBSubsetFilterType.Genre:
                        {
                            search |= Search.FieldLike(nameof(MDBAudioFile.Genres), MDBSearch.Text(filter.Text));
                            break;
                        }
                        case MDBSubsetFilterType.Title:
                        {
                            search |= Search.FieldLike("Title", MDBSearch.Text(filter.Text));
                            break;
                        }
                        default: this.LogError(string.Format("Unknown SubsetType at SubsetFilter {0}", filter)); break;
                    }
                }
            }
            #endregion
            #region blacklist
            {
                var blacklist = SubsetFilters.GetStructs(Search.FieldEquals(nameof(MDBSubsetFilter.SubsetID), subsetID) & Search.FieldEquals(nameof(MDBSubsetFilter.Mode), MDBSubsetFilterMode.Blacklist));
                foreach (MDBSubsetFilter filter in blacklist)
                {
                    switch (filter.Type)
                    {
                        case MDBSubsetFilterType.Album:
                            foreach (long albumID in Albums.FindRows(Search.FieldLike(nameof(MDBAlbum.Name), MDBSearch.Text(filter.Text))))
                            {
                                search &= !Search.FieldEquals(nameof(MDBAudioFile.AlbumID), albumID);
                            }
                            break;
                        case MDBSubsetFilterType.Artist:
                            foreach (long artistID in Artists.FindRows(Search.FieldLike(nameof(MDBArtist.Name), MDBSearch.Text(filter.Text))))
                            {
                                search &= !Search.FieldEquals(nameof(MDBAudioFile.SongArtistID), artistID);
                            }
                            break;
                        case MDBSubsetFilterType.Category:
                        {
                            var ids = Categories.FindRows(nameof(MDBCategory.Name), MDBSearch.Text(filter.Text));
                            if (ids.Count != 1) { this.LogError(string.Format("Error at SubsetFilter {0}", filter)); }
                            foreach (long id in ids)
                            {
                                search &= !Search.FieldEquals(nameof(MDBAudioFile.CategoryID), id);

                            }
                            break;
                        }
                        case MDBSubsetFilterType.Tag:
                        {
                            search &= !Search.FieldLike(nameof(MDBAudioFile.Tags), MDBSearch.Text(filter.Text));
                            break;
                        }
                        case MDBSubsetFilterType.Genre:
                        {
                            search &= !Search.FieldLike(nameof(MDBAudioFile.Genres), MDBSearch.Text(filter.Text));
                            break;
                        }
                        case MDBSubsetFilterType.Title:
                        {
                            search &= !Search.FieldLike("Title", MDBSearch.Text(filter.Text));
                            break;
                        }
                        default: this.LogError(string.Format("Unknown SubsetType at SubsetFilter {0}", filter)); break;
                    }
                }
            }
            #endregion
            if (minDuration > TimeSpan.Zero)
            {
                search &= Search.FieldGreaterOrEqual(nameof(MDBAudioFile.Duration), minDuration);
            }

            if (maxDuration > minDuration)
            {
                search &= Search.FieldSmallerOrEqual(nameof(MDBAudioFile.Duration), maxDuration);
            }

            return AudioFiles.FindRows(search);
        }

        /// <summary>Checks the stream identifier against the specified stream type and sets defaults.</summary>
        public MDBStreamSetting GetStreamSettings(long streamID)
        {
            MDBStreamSetting result = StreamSettings.TryGetStruct(streamID);
            bool update = false;
            if (result.StreamID != streamID)
            {
                result.StreamID = streamID;
                if (result.Volume == 0)
                {
                    result.Volume = 1;
                }
                update = true;
            }
            switch (result.StreamType)
            {
                case MDBStreamType.Silence: break;
                case MDBStreamType.JukeBob: if (result.MinimumTitleCount < 1) { update = true; result.MinimumTitleCount = 5; } break;
                default: throw new NotImplementedException();
            }
            if (update)
            {
                StreamSettings.Replace(result);
            }
            return result;
        }

        /// <summary>
        /// Gets a list of local ip addresses.
        /// </summary>
        /// <returns></returns>
        public List<IPAddress> GetLocalAddresses()
        {
            var ips = NetTools.GetLocalAddresses().Where(i => i.IsDnsEligible).Select(i => i.Address).Where(ip => !IPAddress.IsLoopback(ip) && !ip.IsIPv6LinkLocal && !ip.IsIPv6Multicast && !ip.IsIPv6Teredo && !ip.IsIPv6SiteLocal).ToList();
            ips.Sort(new Comparison<IPAddress>((a, b) =>
                Comparer<string>.Default.Compare(
                    (a.AddressFamily == AddressFamily.InterNetwork ? " " : "") + a.ToString(),
                    (b.AddressFamily == AddressFamily.InterNetwork ? " " : "") + b.ToString()
                 )
            ));
            return ips;
        }
    }
}
