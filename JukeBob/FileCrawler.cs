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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cave;
using Cave.Collections.Generic;
using Cave.Data;
using Cave.IO;
using Cave.Logging;
using Cave.Media;
using Cave.Media.Audio;
using Cave.Media.Audio.ID3;
using Cave.Media.Audio.ID3.Frames;
using Cave.Media.Audio.MP3;
using Cave.Web;

namespace JukeBob
{
	/// <summary>
	/// Searches for files
	/// </summary>
	public class FileCrawler : BaseCrawler
    {
		#region private variables

		readonly object fileLock = new object();
		readonly MusicDataBase mdb;

		bool exit;
		Task workerTask;
        TaskList taskList;
		int threadCount;
		Dictionary<string, MDBCategory> categories;
        float progressDirectorySearch;
        float progressCleanup;
        float progressNewFileSearch;
        float progressFileScan;
		List<string> rootFolders;

		#endregion

		#region private functions

		void CheckFile(MDBFile mdbFile)
		{
			MDBUpdateType updateType;
			string fullPath;
			try
			{
				fullPath = mdbFile.GetFullPath(mdb);
				//check if root folder is still valid and file exists
				if (rootFolders.Where(r => FileSystem.IsRelative(fullPath, r)).Any() && File.Exists(fullPath))
				{
					var fileInfo = new FileInfo(fullPath);
					if (fileInfo.LastWriteTimeUtc == mdbFile.DateTime && fileInfo.Length == mdbFile.Size)
					{
						updateType = MDBUpdateType.NoChange;
					}
					else
					{
						updateType = MDBUpdateType.Updated;
					}
				}
				else
				{
					updateType = MDBUpdateType.Removed;
					this.LogVerbose("<red>Invalid root <default>{0}", fullPath);
				}
			}
			catch (Exception ex)
			{
				fullPath = null;
				updateType = MDBUpdateType.Removed;
				this.LogVerbose(ex, "<red>Access error <default>{0}", fullPath);
			}

			switch (updateType)
			{
				case MDBUpdateType.Ignored: break;
				case MDBUpdateType.Removed:
				{
					this.LogInfo("Removed file '<red>{0}<default>'", mdbFile);
					RemoveFile(mdbFile.ID);
					break;
				}

				case MDBUpdateType.NoChange:
				{
					if (mdbFile.IsImage)
					{
                        if (!mdb.Images.Exist(nameof(MDBImage.FileID), mdbFile.ID))
                        {
                            updateType = MDBUpdateType.New;
                            goto case MDBUpdateType.New;
                        }
					}
					else
					{
                        if (!mdb.AudioFiles.Exist(nameof(MDBAudioFile.FileID), mdbFile.ID))
                        {
                            updateType = MDBUpdateType.New;
                            goto case MDBUpdateType.New;
                        }
					}
					break;
				}
				case MDBUpdateType.New:
				case MDBUpdateType.Updated:
				{
                    if (mdbFile.IsImage)
                    {
                        try { updateType = CheckImageFile(mdbFile, fullPath); }
                        catch (Exception ex)
                        {
                            this.LogWarning(ex, "Could not update image file {0}.", fullPath);
                            mdb.Images.TryDelete(nameof(MDBImage.FileID), mdbFile.ID);
                        }
                    }
                    else if (mdbFile.FileType == MDBFileType.mp3)
                    {
                        updateType = CheckMusicFile(mdbFile, fullPath);
                    }
                    else
                    {
                        this.LogInfo("Removed file '<red>{0}<default>' reason: unknown file type", mdbFile);
                        RemoveFile(mdbFile.ID);
                    }
					break;
				}
				default: throw new NotImplementedException();
			}
			FileIndexed?.Invoke(this, new MDBFileIndexedEventArgs(updateType, mdbFile));
		}

		void RemoveFile(long id)
		{
			mdb.Files.Delete(id);
			mdb.AudioFiles.TryDelete(nameof(MDBAudioFile.FileID), id);
			mdb.Images.TryDelete(nameof(MDBImage.FileID), id);
		}

		bool UpdateImageDataset(string fullPath, MDBImage mdbImageFile)
		{
			if (mdbImageFile.MusicBrainzGuid == null)
			{
				this.LogWarning("Unknown guid at file: <red>{0}", fullPath);
				mdb.Images.TryDelete(nameof(MDBImage.FileID), mdbImageFile.FileID);
				return false;
			}

			if (mdbImageFile.FileID > 0)
			{
				mdb.Images.Replace(mdbImageFile);
				this.LogInfo("Update image file {0} {1}", mdbImageFile, fullPath);
			}
			else
			{
				mdbImageFile.FileID = mdb.Images.Insert(mdbImageFile);
				this.LogNotice("New image file {0} {1}", mdbImageFile, fullPath);
			}
			return true;
		}

		MDBUpdateType CheckImageFile(MDBFile mdbFile, string fullPath)
		{
			if (mdbFile.ID <= 0) throw new InvalidDataException();

			MDBImage mdbImageFile = new MDBImage();
			mdbImageFile.FileID = mdbFile.ID;

			MDBUpdateType result;
			byte[] data = File.ReadAllBytes(fullPath);
			using (var bmp = Bitmap32.Create(data))
			{
				mdbImageFile.Height = bmp.Height;
				mdbImageFile.Width = bmp.Width;
				mdbImageFile.MimeType = MimeTypes.FromExtension(Path.GetExtension(fullPath));
			}

			string fileNameCheck = mdbFile.Name.ToString().ToLower();
			int index = fileNameCheck.IndexOfAny(new char[] { ' ', '.', '-' });
			if (index > -1) fileNameCheck.Substring(0, index).TryParse(out mdbImageFile.Type);
			//check for specific file names
			if (mdbImageFile.Type == MDBImageType.Undefined)
			{
				switch (fileNameCheck)
				{
					case "front": mdbImageFile.Type = MDBImageType.UserCover; break;
					case "cover": mdbImageFile.Type = MDBImageType.UserCover; break;
				}
			}

			if (mdbImageFile.Type.IsAlbumArt())
			{
				var files = mdb.Files.GetStructs(
					Search.FieldEquals(nameof(MDBFile.FolderID), mdbFile.FolderID) &
					Search.FieldEquals(nameof(MDBFile.FileType), MDBFileType.mp3));

				//new image ?
				MDBImage oldImage;
				if (!mdb.Images.TryGetStruct(nameof(MDBImage.FileID), mdbImageFile.FileID, out oldImage)) { result = MDBUpdateType.New; }
				else { result = MDBUpdateType.Updated; mdbImageFile.FileID = oldImage.FileID; }

				if (files.Count == 0)
				{
					this.LogWarning("Image <red>{0}<default> belongs to <magenta>0<default> albums!", fullPath);
					return MDBUpdateType.Ignored;
				}
				var audioFiles = mdb.AudioFiles.GetStructs(Search.FieldIn(nameof(MDBAudioFile.FileID), files.Select(f => f.ID)));
				var albums = mdb.Albums.GetStructs(new Set<long>(audioFiles.Select(a => a.AlbumID)));
				Set<BinaryGuid> guids = new Set<BinaryGuid>(albums.Where(a => a.MusicBrainzReleaseGroupGuid != null).Select(a => a.MusicBrainzReleaseGroupGuid));
				switch (guids.Count())
				{
					case 0:
						this.LogWarning("Album guid invalid: <red>{0}", Path.GetDirectoryName(fullPath));
						return MDBUpdateType.Ignored;
					case 1:
						mdbImageFile.MusicBrainzGuid = guids.First();
						if (UpdateImageDataset(fullPath, mdbImageFile)) return result; else return MDBUpdateType.Removed;
					default:
						this.LogWarning("Image <red>{0}<default> belongs to <magenta>multiple<default> albums ({1})!", fullPath, guids);
						return MDBUpdateType.Ignored;
				}
			}

			if (mdbImageFile.Type.IsArtistArt())
			{
				//get artist guid from ini // could read this from path too...
				string path = FileSystem.Combine(fullPath, "..", "artist.ini");
				var ini = IniReader.FromFile(path);
				Guid artistGuid = new Guid(ini.ReadSetting("Artist", "Guid"));

				//new image ?
				MDBImage oldImage;
				if (!mdb.Images.TryGetStruct(nameof(MDBImage.FileID), mdbImageFile.FileID, out oldImage)) { result = MDBUpdateType.New; }
				else { result = MDBUpdateType.Updated; }

				mdbImageFile.MusicBrainzGuid = artistGuid;
				UpdateImageDataset(fullPath, mdbImageFile);
				return result;
			}
			this.LogInfo("Ignored image: <red>{0}", fullPath);
			return MDBUpdateType.Ignored;
		}

		string StringForceMaxLength(string text, int maxLength) => text.ForceMaxLength(maxLength, "..");

		MDBUpdateType CheckMusicFile(MDBFile mdbFile, string fullPath)
		{
			if (mdbFile.ID <= 0) throw new ArgumentOutOfRangeException("MDBFile.ID");

			MDBCategory mdbCategory = GetCategory(Path.GetDirectoryName(fullPath));
			MDBAudioFile audioFile;
			mdb.AudioFiles.TryGetStruct(nameof(MDBAudioFile.FileID), mdbFile.ID, out audioFile);

			string cdg = Path.ChangeExtension(fullPath, ".cdg");
			if (File.Exists(cdg))
			{
				if (!mdb.Categories.TryGetStruct(nameof(MDBCategory.Name), "Karaoke", out mdbCategory))
				{
					mdbCategory.Name = "Karaoke";
					mdb.Categories.UpdateOrInsert(mdbCategory, nameof(MDBCategory.Name));
				}
			}

			MDBArtist albumArtist = new MDBArtist();
			MDBArtist titleArtist = new MDBArtist();
			MDBAlbum album = new MDBAlbum();
			MDBGenre genre = new MDBGenre();
			MDBTag tag = new MDBTag();

			audioFile.FileID = mdbFile.ID;
			audioFile.CategoryID = mdbCategory.ID;
			audioFile.Errors = 0;
			audioFile.MetaErrors = 0;

			Set<string> genres = new Set<string>();
			Set<string> tags = new Set<string>();

			this.LogDebug("Reading data of {0}", mdbFile);
			var data = File.ReadAllBytes(fullPath);
			mdb.Files.Replace(mdbFile);

			using (MemoryStream ms = new MemoryStream(data))
			{
				MP3Reader reader = new MP3Reader(ms);
				reader.Name = mdbFile.ToString();

				TimeSpan duration = TimeSpan.Zero;
				//iterate all frames
				AudioFrame frame = reader.GetNextFrame();

				ID3v2APICFrame id3v2Image = null;

				while (frame != null)
				{
					if (frame.IsAudio)
					{
						duration += frame.Duration;
					}
					else if (frame is ID3v1)
					{
						ID3v1 id3v1 = frame as ID3v1;
						if (!string.IsNullOrEmpty(id3v1.Genre))
						{
							if (string.IsNullOrEmpty(genre.Name)) genre.Name = id3v1.Genre;
							genres.Include(id3v1.Genre.Trim());
						}
						if (string.IsNullOrEmpty(titleArtist.Name)) titleArtist.Name = id3v1.Artist;
						if (string.IsNullOrEmpty(album.Name)) album.Name = id3v1.Album;
						if (string.IsNullOrEmpty(audioFile.Title)) audioFile.Title = id3v1.Title;
						if ((id3v1.TrackNumber > 0) && (audioFile.TrackNumber <= 0)) audioFile.TrackNumber = id3v1.TrackNumber;
						if (audioFile.RecordingDate == DateTime.MinValue)
						{
							int year;
							if (int.TryParse(id3v1.Year, out year) && (year > 1900) && (year <= DateTime.UtcNow.Year))
							{
								audioFile.RecordingDate = new DateTime(year, 1, 1);
							}
						}
					}
					else if (frame is ID3v2)
					{
						ID3v2 id3v2 = frame as ID3v2;

						if (string.IsNullOrEmpty(genre.Name) & !string.IsNullOrEmpty(id3v2.Group))
						{
							genre.Name = id3v2.Group.Trim();
							genres.Include(genre.Name);
						}

						string[] tagGenres = id3v2.ContentTypes;
						foreach (string tagGenre in tagGenres)
						{
							if (string.IsNullOrEmpty(genre.Name)) genre.Name = tagGenre.Trim();
							genres.Include(tagGenre.Trim());
						}

						foreach (string mood in id3v2.Moods)
						{
							if (string.IsNullOrEmpty(tag.Name)) tag.Name = mood.Trim();
							tags.Include(mood.Trim());
						}

						if (string.IsNullOrEmpty(titleArtist.Name)) titleArtist.Name = id3v2.SongArtist.Trim();
						if (string.IsNullOrEmpty(albumArtist.Name)) albumArtist.Name = id3v2.AlbumArtist.Trim();
						if (string.IsNullOrEmpty(album.Name)) album.Name = id3v2.Album.Trim();
						if (string.IsNullOrEmpty(audioFile.Title)) audioFile.Title = id3v2.Title.Trim();

						try { if (audioFile.AcousticGuid == null) audioFile.AcousticGuid = id3v2.AcousticGuid; } catch { }
						try
						{
							if (titleArtist.MusicBrainzArtistGuid == null)
							{
								titleArtist.MusicBrainzArtistGuid = id3v2.MusicBrainzArtistId;
								if (string.IsNullOrEmpty(albumArtist.Name) || titleArtist.Name == albumArtist.Name)
								{
									albumArtist = titleArtist;
								}
							}
						}
						catch { }

						try { if (albumArtist.MusicBrainzArtistGuid == null) albumArtist.MusicBrainzArtistGuid = id3v2.MusicBrainzAlbumArtistId; } catch { }
						try { if (album.MusicBrainzAlbumGuid == null) album.MusicBrainzAlbumGuid = id3v2.MusicBrainzAlbumId; } catch { }
						try { if (album.MusicBrainzReleaseGroupGuid == null) album.MusicBrainzReleaseGroupGuid = id3v2.MusicBrainzReleaseGroupId; } catch { }

						if ((id3v2.TrackNumber > 0) && (audioFile.TrackNumber <= 0)) audioFile.TrackNumber = id3v2.TrackNumber;
						if ((id3v2.TrackCount > 0) && (audioFile.TrackCount <= 0)) audioFile.TrackCount = id3v2.TrackCount;
						if (id3v2.HasDate) audioFile.RecordingDate = id3v2.Date;
						if (id3v2Image == null) id3v2Image = id3v2.GetPictureFrame(ID3v2PictureType.CoverFront);
						if (id3v2.ParserError) audioFile.MetaErrors |= MDBMetaErrors.Unclean;
					}
					else if (!frame.IsValid)
					{
						foreach (byte b in frame.Data)
						{
							if (b != 0)
							{
								audioFile.Errors += frame.Length;
								break;
							}
						}
					}
					frame = reader.GetNextFrame();
				}
				reader.Close();

				audioFile.Duration = duration;

				if (titleArtist.MusicBrainzArtistGuid == null) audioFile.MetaErrors |= MDBMetaErrors.MusicBrainzTitleArtist;
				if (albumArtist.MusicBrainzArtistGuid == null) audioFile.MetaErrors |= MDBMetaErrors.MusicBrainzAlbumArtist;
				if (album.MusicBrainzAlbumGuid == null) audioFile.MetaErrors |= MDBMetaErrors.MusicBrainzAlbum;
				if (album.MusicBrainzReleaseGroupGuid == null) audioFile.MetaErrors |= MDBMetaErrors.MusicBrainzReleaseGroup;

				audioFile.GenreNames = genres.ToArray();
				audioFile.TagNames = tags.ToArray();

				if (string.IsNullOrEmpty(tag.Name)) { tag.Name = "Unset"; audioFile.MetaErrors |= MDBMetaErrors.Tag; }
				tag.Name = StringForceMaxLength(tag.Name, 128);
				tag = mdb.Tags.UpdateOrInsert(tag, nameof(MDBTag.Name));

				if (string.IsNullOrEmpty(genre.Name)) { genre.Name = "Unset"; audioFile.MetaErrors |= MDBMetaErrors.Genre; }
				genre.Name = StringForceMaxLength(genre.Name, 128);
				genre = mdb.Genres.UpdateOrInsert(genre, nameof(MDBGenre.Name));

				if (string.IsNullOrEmpty(audioFile.Title))
				{
					audioFile.MetaErrors |= MDBMetaErrors.Title;
					audioFile.Title = "[untitled]";
				}
				if (audioFile.Title.Length > 128)
				{
					audioFile.MetaErrors |= MDBMetaErrors.Title;
					audioFile.Title = audioFile.Title.ToString().Substring(0, 126) + "..";
				}

				if (string.IsNullOrEmpty(titleArtist.Name))
				{
					titleArtist.Name = "[unknown]";
					audioFile.MetaErrors |= MDBMetaErrors.Artist;
				}
				else
				{
					//artist name too long ?
					if (titleArtist.Name.Length > 128) audioFile.MetaErrors |= MDBMetaErrors.Artist;
					titleArtist.Name = StringForceMaxLength(titleArtist.Name, 128);
				}
				titleArtist = mdb.Artists.UpdateOrInsert(titleArtist, nameof(MDBArtist.MusicBrainzArtistGuid), nameof(MDBArtist.Name));

				if (string.IsNullOrEmpty(albumArtist.Name))
				{
					albumArtist.Name = "[unknown]";
					audioFile.MetaErrors |= MDBMetaErrors.Artist;
				}
				else
				{
					//artist name too long ?
					if (albumArtist.Name.Length > 128) audioFile.MetaErrors |= MDBMetaErrors.Artist;
					albumArtist.Name = StringForceMaxLength(albumArtist.Name, 128);
				}
				albumArtist = mdb.Artists.UpdateOrInsert(albumArtist, nameof(MDBArtist.MusicBrainzArtistGuid), nameof(MDBArtist.Name));

				album.ArtistID = albumArtist.ID;
				if (string.IsNullOrEmpty(album.Name))
				{
					album.Name = "[unnamed]";
					audioFile.MetaErrors |= MDBMetaErrors.Album;
				}
				else
				{
					if (album.Name.Length > 128) audioFile.MetaErrors |= MDBMetaErrors.Album;
					album.Name = StringForceMaxLength(album.Name, 128);
				}
				if (album.MusicBrainzReleaseGroupGuid == null) album.MusicBrainzReleaseGroupGuid = mdb.CreateInvalidGuid(CaveSystemData.CalculateID(album.Name));
				album = mdb.Albums.UpdateOrInsert(album, nameof(MDBAlbum.ArtistID), nameof(MDBAlbum.Name), nameof(MDBAlbum.MusicBrainzReleaseGroupGuid), nameof(MDBAlbum.MusicBrainzAlbumGuid));

				audioFile.TagID = tag.ID;
				audioFile.GenreID = genre.ID;
				audioFile.AlbumID = album.ID;
				audioFile.SongArtistID = titleArtist.ID;
				audioFile.AlbumArtistID = albumArtist.ID;

				if (audioFile.FileID <= 0)
				{
					if (0 == audioFile.MetaErrors) this.LogInfo("New audio file {0}", audioFile);
					else this.LogWarning("New audio file {0} meta errors '<red>{1}<default>'", audioFile, audioFile.MetaErrors);
					audioFile.FileID = mdb.AudioFiles.Insert(audioFile);
					return MDBUpdateType.New;
				}
				else
				{
					if (0 == audioFile.MetaErrors) this.LogInfo("Update audio file {0}", audioFile);
					else this.LogWarning("Update audio file {0} meta errors '<red>{1}<default>'", audioFile, audioFile.MetaErrors);
					mdb.AudioFiles.Replace(audioFile);
					return MDBUpdateType.Updated;
				}
			}
		}

		MDBCategory GetCategory(string path)
		{
			MDBCategory result = new MDBCategory { Name = "Undefined" };
			if (path.EndsWith("..")) return result;

			lock (mdb.Categories)
			{
				if (categories.TryGetValue(path, out result)) return result;
				string file = FileSystem.Combine(path, "mdb.ini");
				if (File.Exists(file))
				{
					IniReader reader = IniReader.FromFile(file);
					result.Name = reader.ReadSetting("Category", "Name");
					string parentName = reader.ReadSetting("Category", "Parent");
					if (!string.IsNullOrEmpty(parentName))
					{
						result.ParentID = mdb.Categories.FindRow("Name", parentName);
						if (result.ParentID <= 0)
						{
							MDBCategory parent = GetCategory(FileSystem.Combine(path, ".."));
							if (parent.Name != parentName)
							{
								this.LogWarning("Could not set parent of category " + result + "!");
							}
							result.ParentID = parent.ID;
						}
					}
					result = mdb.Categories.UpdateOrInsert(result, nameof(MDBCategory.Name));
					categories[path] = result;
					return result;
				}
			}
			return GetCategory(FileSystem.Combine(path, ".."));
		}

		bool CheckFolderExist(MDBFolder folder)
		{
			bool exists = false;
			try
			{
				exists = Directory.Exists(folder.Name);
			}
			catch (DirectoryNotFoundException ex)
			{
				this.LogWarning(ex, "Folder {0} access/lookup error.", folder);
				exists = false;
			}
			return exists;
		}

		bool CheckFileExist(MDBFile file)
		{
			bool exists = false;
			try
			{
				if (mdb.Folders.TryGetStruct(file.FolderID, out var folder) && CheckFolderExist(folder))
				{
					string fullPath = file.GetFullPath(mdb);
					exists = File.Exists(fullPath);
				}
			}
			catch (Exception ex)
			{
				this.LogWarning(ex.Message);
				exists = false;
			}
			return exists;
		}

		void AddNewFiles()
		{
			float count = mdb.Folders.RowCount;
			this.LogInfo("Searching music files at <green>{0}<default> folders", count);
			int i = 0;
			foreach(var folder in mdb.Folders.GetStructs())
			{
				if (exit) break;
                if (!CheckFolderExist(folder))
                {
                    this.LogDebug("{0} Removed folder {1}", progressNewFileSearch.ToString("p"), folder);
                    mdb.Files.TryDelete(nameof(MDBFile.FolderID), folder.ID);
                    mdb.Folders.TryDelete(nameof(MDBFolder.ID), folder.ID);
                    continue;
                }
                progressNewFileSearch = i++ / count;
				this.LogDebug("{0} Scan folder {1}", progressNewFileSearch.ToString("p"), folder);
				if (taskList.MaximumConcurrentThreads <= 1)
				{
					AddNewFilesInDirectory(folder);
				}
				else
				{
					taskList.Wait();
					Task t = Task.Factory.StartNew((s) => { AddNewFilesInDirectory((MDBFolder)s); }, folder);
					taskList.Add(t);
				}
			}
			taskList.WaitAll();
			progressNewFileSearch = 1;
		}

		void AddNewFilesInDirectory(MDBFolder folder)
		{
			string[] files;
			try
			{
				files = Directory.GetFiles(folder.Name);
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Error checking folder <red>{0}", folder);
				return;
			}
			foreach (string fullPath in files)
			{
				try
				{
					string extension = Path.GetExtension(fullPath);

					MDBFileType fileType = mdb.GetFileType(extension);
					if (fileType == MDBFileType.unknown) continue;

					MDBFile file;
					string fileName = Path.GetFileName(fullPath);
					var result = mdb.RegisterFile(folder, fileName, out file);
					this.LogVerbose("<magenta>{0} <cyan>{1}", result, fullPath);
				}
				catch (Exception ex)
				{
					this.LogError(ex, "Error checking file <red>{0}", fullPath);
				}
			}
		}

		void CheckDirectories()
		{
			DateTime start = DateTime.UtcNow;
			EndlessProgress p = new EndlessProgress(mdb.Folders.RowCount + 1000);
			foreach (var rootFolder in rootFolders)
			{
                CheckDirectory(rootFolder, p);
				//Call(CheckDirectory, new C<string, EndlessProgress>(rootFolder, p));
			}
			taskList.WaitAll();

			//clean folders
			var folders = mdb.Folders.GetStructs(Search.FieldSmaller(nameof(MDBFolder.LastChecked), start - TimeSpan.FromSeconds(1)));
			foreach(var folder in folders)
			{
				if (exit) break;
				p.Increment();
				progressDirectorySearch = p.Value;

				this.LogInfo("Removed folder <red>{0}", folder);
				mdb.Folders.TryDelete(folder.ID);
				var fileIDs = mdb.Files.FindRows(nameof(MDBFile.FolderID), folder.ID);
				if (fileIDs.Count > 0)
				{
					mdb.Files.Delete(fileIDs);
					mdb.AudioFiles.TryDelete(fileIDs);
					mdb.Images.TryDelete(fileIDs);
				}
			}
			/*
			List<string> dirs = new List<string>();
			DateTime startTime = DateTime.UtcNow;
			float factor = 1f / rootFolders.Count;
			int i = 0;

			foreach (var rootFolder in rootFolders)
			{
				if (exit) break;
				float start = i++ * factor;
				EndlessProgress p = new EndlessProgress(Math.Max(mdb.Folders.RowCount + 1000, dirs.Count * i / factor));

				string folder = FileSystem.Combine(".", rootFolder);
				dirs.Add(folder);

				var finder = new DirectoryFinder(folder);
				while (true)
				{
					var item = finder.GetNext();
					if (exit || item == null) break;
					p.Increment();
					progressDirectorySearch = p.Value * factor + start;
					dirs.Add(item.FullPath);
				}
			}
			var folderIDs = mdb.Folders.FindRows(Search.FieldSmaller(nameof(MDBFolder.LastChecked), startTime));
			mdb.Folders.Delete(folderIDs);
			mdb.Files.Delete(Search.FieldIn(nameof(MDBFile.FolderID), folderIDs));
			*/
			progressDirectorySearch = 1;
			AddNewFiles();
			Logger.Flush();
			mdb.Save();
		}

        //private void CheckDirectory(C<string, EndlessProgress> obj)
        private void CheckDirectory(string path, EndlessProgress progress)
        {
			string baseFolder = FileSystem.Combine(".", path);
			{
				Call(UpdateFolder, baseFolder);
			}
			var finder = new DirectoryFinder(baseFolder);
			while (true)
			{
				var item = finder.GetNext();
				if (exit || item == null) break;
				progress.Increment();
				progressDirectorySearch = progress.Value;
				Call(UpdateFolder, item.FullPath);
			}
		}

		private void UpdateFolder(string fullPath)
		{
			var folder = mdb.Folders.TryGetStruct(nameof(MDBFolder.Name), fullPath);
			folder.LastChecked = DateTime.UtcNow;
			if (folder.ID > 0)
			{
				mdb.Folders.Replace(folder);
				this.LogVerbose("<green>Update {0}", folder);
			}
			else
			{
				folder.Name = fullPath;
                folder.ID = mdb.Folders.Insert(folder);
				this.LogInfo("<green>New {0}", folder);
			}
		}

		void Cleanup(ref float progress)
		{
			float allRowsCount = mdb.Tags.RowCount + mdb.Genres.RowCount + mdb.Artists.RowCount + mdb.Albums.RowCount + mdb.AudioFiles.RowCount + mdb.Images.RowCount;
			long value = 0;

            #region cleanup no longer present audiofiles 
            this.LogInfo("Cleanup '<yellow>{0}<default>' AudioFiles...", mdb.AudioFiles.RowCount);
			foreach (var audioFile in mdb.AudioFiles.GetStructs())
			{
				if (exit) return;
				progress = value++ / allRowsCount;
				Call(CleanupAudioFile, audioFile);
			}
			#endregion

			#region cleanup no longer present images 
			this.LogInfo("Cleanup '<yellow>{0}<default>' Images...", mdb.Images.RowCount);
			foreach (var image in mdb.Images.GetStructs())
			{
				if (exit) return;
				progress = value++ / allRowsCount;
				Call(CleanupImageFile, image);
			}
			#endregion

			#region cleanup unused tags
			this.LogInfo("Cleanup '<yellow>{0}<default>' Tags...", mdb.Tags.RowCount);
			foreach (MDBTag tag in mdb.Tags.GetStructs())
			{
				if (exit) return;
				progress = value++ / allRowsCount;

				if (!mdb.AudioFiles.Exist(Search.FieldEquals(nameof(MDBAudioFile.TagID), tag.ID)))
				{
					this.LogInfo("Removed tag '<red>{0}<default>'", tag.Name);
					mdb.Tags.Delete(tag.ID);
				}
			}
			#endregion

			#region cleanup unused genres
			this.LogInfo("Cleanup '<yellow>{0}<default>' Genres...", mdb.Genres.RowCount);
			foreach (MDBGenre genre in mdb.Genres.GetStructs())
			{
				if (exit) return;
				progress = value++ / allRowsCount;

				if (!mdb.AudioFiles.Exist(Search.FieldEquals(nameof(MDBAudioFile.GenreID), genre.ID)))
				{
					this.LogInfo("Removed genre '<red>{0}<default>'", genre.Name);
					mdb.Genres.Delete(genre.ID);
				}
			}
			#endregion

			#region cleanup unused artists
			this.LogInfo("Cleanup '<yellow>{0}<default>' Artists...", mdb.Artists.RowCount);
			foreach (var artist in mdb.Artists.GetStructs())
			{
				if (exit) return;
				progress = value++ / allRowsCount;

				if (!mdb.AudioFiles.Exist(
					Search.FieldEquals(nameof(MDBAudioFile.AlbumArtistID), artist.ID) |
					Search.FieldEquals(nameof(MDBAudioFile.SongArtistID), artist.ID)))
				{
					this.LogInfo("Removed artist '<red>{0}<default>'", artist.Name);
					mdb.Artists.Delete(artist.ID);
				}
			}
			#endregion

			#region cleanup unused albums
			this.LogInfo("Cleanup '<yellow>{0}<default>' Albums...", mdb.Albums.RowCount);
			foreach (var album in mdb.Albums.GetStructs())
			{
				if (exit) return;
				progress = value++ / allRowsCount;

				if (!mdb.AudioFiles.Exist(Search.FieldEquals(nameof(MDBAudioFile.AlbumID), album.ID)))
				{
					this.LogInfo("Removed album '<red>{0}<default>'", album.Name);
					mdb.Albums.Delete(album.ID);
				}
			}
			#endregion

			Logger.Flush();
			progress = 1;
			mdb.Save();
		}

		void Call<T>(Action<T> action, T para) 
		{
			if (taskList.MaximumConcurrentThreads > 1)
			{
				taskList.Wait();
				Task t = Task.Factory.StartNew((p) => action((T)p), para);
				taskList.Add(t);
			}
			else
			{
				action(para);
			}
		}

		void CheckAllFiles()
		{
			//check all files
			this.LogInfo("Checking '<yellow>{0}<default>' files, using up to <cyan>{1}<default> threads...", mdb.Files.RowCount, taskList.MaximumConcurrentThreads);
			int count = 0;
			var folderIDs = mdb.Folders.IDs.Shuffle();
			float allRowsCount = 2 * folderIDs.Count;

			for (int i = 0; i < 2; i++)
			{
				//in each folder, scan mp3 first then everything else
				//we shuffle the ids for better progress display on interrupted crawls
				foreach (var folderID in folderIDs)
				{
					progressFileScan = count++ / allRowsCount;
					if (exit) break;

					IList<long> ids;
					switch (i)
					{
						case 0:
						ids = mdb.Files.FindRows(
							Search.FieldEquals(nameof(MDBFile.FileType), MDBFileType.mp3) & 
							Search.FieldEquals(nameof(MDBFile.FolderID), folderID), 
							ResultOption.SortAscending(nameof(MDBFile.FileType)));
						break;
						case 1:
						ids = mdb.Files.FindRows(
							Search.FieldNotEquals(nameof(MDBFile.FileType), MDBFileType.mp3) &
							Search.FieldEquals(nameof(MDBFile.FolderID), folderID),
							ResultOption.SortAscending(nameof(MDBFile.FileType)));
						break;
						default: continue;
					}
					ScanFilesInFolder(ids);
				}
			}

			taskList.WaitAll();
			Logger.Flush();
			progressFileScan = 1;
			mdb.Save();
		}

		void ScanFilesInFolder(IList<long> ids)
		{
			foreach (var fileID in ids)
			{
				if (exit) break;
				var file = mdb.Files.TryGetStruct(fileID);
				if (file.ID != fileID) continue;
				Call(ScanFile, file);
			}
		}

		void ScanFile(MDBFile mdbFile)
		{
			int tryCount = 0;
			while (true)
			{
				try
				{
					CheckFile(mdbFile);
					break;
				}
				catch (Exception ex)
				{
					if (++tryCount < 3)
					{
						this.LogDebug(ex, "Error checking file {0}. <yellow>Retrying...", mdbFile);
						Thread.Sleep(2000);
					}
					else
					{
						this.LogError(ex, "Error checking file {0}. <red>Giving up...\nFullPath: {1}", mdbFile, mdbFile.GetFullPath(mdb));
						mdb.Files.Delete(mdbFile.ID);
						break;
					}
				}
			}
		}

		void CleanupImageFile(MDBImage image)
		{
			if (!mdb.Files.TryGetStruct(image.FileID, out MDBFile file) || !CheckFileExist(file))
            {
				this.LogInfo("<red>Removed<default> image file '{0}'", image);
				mdb.Images.TryDelete(nameof(MDBAudioFile.FileID), image.FileID);
				mdb.Files.TryDelete(nameof(MDBFile.ID), image.FileID);
				return;
			}
		}

		void CleanupAudioFile(MDBAudioFile audioFile)
		{
            if (!mdb.Files.TryGetStruct(audioFile.FileID, out MDBFile file) || !CheckFileExist(file))
			{
				this.LogInfo("<red>Removed<default> audio file '{0}'", audioFile);
				mdb.AudioFiles.TryDelete(nameof(MDBAudioFile.FileID), audioFile.FileID);
				mdb.Files.TryDelete(nameof(MDBFile.ID), audioFile.FileID);
				return;
			}

			bool update = false;
			if (!mdb.Tags.Exist(audioFile.TagID)) { audioFile.MetaErrors |= MDBMetaErrors.Tag; audioFile.TagID = 0; update = true; }
			if (!mdb.Genres.Exist(audioFile.GenreID)) { audioFile.MetaErrors |= MDBMetaErrors.Genre; audioFile.GenreID = 0; update = true; }
			if (!mdb.Albums.Exist(audioFile.AlbumID)) { audioFile.MetaErrors |= MDBMetaErrors.Album; audioFile.AlbumID = 0; update = true; }
			if (!mdb.Artists.Exist(audioFile.AlbumArtistID)) { audioFile.MetaErrors |= MDBMetaErrors.Artist; audioFile.AlbumArtistID = 0; update = true; }
			if (!mdb.Artists.Exist(audioFile.SongArtistID)) { audioFile.MetaErrors |= MDBMetaErrors.Artist; audioFile.SongArtistID = 0; update = true; }
			if (update)
			{
				this.LogDebug("Audio file {0} meta errors: <red>{1}", audioFile, audioFile.MetaErrors);
				mdb.AudioFiles.Update(audioFile);
			}
		}

		void Worker()
		{
			try
			{
				if (!exit)
				{
					this.LogNotice("Start new file search.");
					CheckDirectories();
				}
				if (!exit)
				{
					this.LogNotice("Start file scanning.");
					CheckAllFiles();
				}
				if (!exit)
				{
					this.LogNotice("Start cleanup.");
					Cleanup(ref progressCleanup);
				}
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Unhandled Error during crawl!");
			}
			finally
			{
				this.LogNotice("FileCrawler exit.");
				mdb.Save();

				taskList = null;
				threadCount = 0;
				categories = null;
				progressDirectorySearch = 0;
				progressCleanup = 0;
				progressNewFileSearch = 0;
				progressFileScan = 0;
				rootFolders = null;
			}
		}

		#endregion

		/// <summary>
		/// Creates the file crawler
		/// </summary>
		/// <param name="mdb"></param>
		public FileCrawler(MusicDataBase mdb)
		{
			this.mdb = mdb;
		}

		#region public functions

		/// <summary>Initializes a new instance of the <see cref="FileCrawler"/> class.</summary>
		public override void Start()
        {
			if (Started && !Completed)
			{
				throw new Exception("Already running!");
			}
			
			mdb.Config.GetValue("Crawler", "ThreadCount", ref threadCount);
            if (threadCount < 1) threadCount = 1;
            this.LogDebug("Crawler using {0} threads.", threadCount);

			categories = new Dictionary<string, MDBCategory>();
			taskList = new TaskList();
            taskList.MaximumConcurrentThreads = threadCount;
			exit = false;
			rootFolders = new List<string>();
			rootFolders.Add(mdb.ArtistArtFolder);
            foreach (string folder in mdb.MusicFolders)
            {
				var dir = FileSystem.Combine(mdb.RootFolder, folder);
                if (Directory.Exists(dir)) rootFolders.Add(dir);
            }
			workerTask?.Dispose();
			workerTask = Task.Factory.StartNew(Worker);
		}

		/// <summary>Stops this instance.</summary>
		/// <exception cref="Exception">
		/// Not jet started
		/// or
		/// Already stopping
		/// </exception>
		public override void Stop()
		{
			if (!Started) throw new Exception("Not jet started");
			if (exit) throw new Exception("Already stopping");
			exit = true;
			workerTask.Wait();
		}

		/// <summary>Waits for completion of this instance.</summary>
		public override void Wait()
		{
			workerTask.Wait();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public override void Dispose()
		{
			exit = true;
			workerTask?.Dispose();
			workerTask = null;
		}

		#endregion

		#region public properties

        /// <summary>
        /// Name of the Crawler
        /// </summary>
		public override string Name => "FileCrawler";

		/// <summary>Gets the name of the log source.</summary>
		/// <value>The name of the log source.</value>
		public override string LogSourceName { get { return "mdb-file-crawler"; } }

#pragma warning disable 0649
        /// <summary>Provides an event for each indexed file</summary>
        public EventHandler<MDBFileIndexedEventArgs> FileIndexed;
#pragma warning restore 0649

		/// <summary>Gets the state.</summary>
		/// <value>The state.</value>
		public string State
        {
            get
            {
                if (Error) return "Error";
                else if (Completed) return "Completed";
                else if (exit) return "Exit Requested";
                else if (Started) return "Started";
                else return "Waiting";
            }
        }

        /// <summary>Gets the progress.</summary>
        /// <value>The progress.</value>
        public override MDBProgress[] Progress
        {
            get
            {
                return new MDBProgress[]
                {
					new MDBProgress() { ID = 1, Progress = progressDirectorySearch, Type = "Directory search", Source = "FileCrawler" },
                    new MDBProgress() { ID = 2, Progress = progressNewFileSearch, Type = "New file search", Source = "FileCrawler" },
					new MDBProgress() { ID = 3, Progress = progressFileScan, Type = "File scanning", Source = "FileCrawler" },
					new MDBProgress() { ID = 4, Progress = progressCleanup, Type = "Cleanup", Source = "FileCrawler" },
				};
            }
        }

		/// <summary>Gets a value indicating whether this <see cref="FileCrawler"/> is started.</summary>
		/// <value><c>true</c> if started; otherwise, <c>false</c>.</value>
		public override bool Started { get { return workerTask != null; } }

        /// <summary>Gets a value indicating whether this <see cref="T:Cave.MDB.IMDBCrawler" /> is completed.</summary>
        /// <value><c>true</c> if completed; otherwise, <c>false</c>.</value>
        public override bool Completed { get { return Started && workerTask.IsCompleted; } }

        /// <summary>Gets a value indicating whether this <see cref="T:Cave.MDB.IMDBCrawler" /> encountered an error.</summary>
        /// <value><c>true</c> if an error occured; otherwise, <c>false</c>.</value>
        public override bool Error { get { return Completed && workerTask.IsFaulted; } }

        /// <summary>Gets the exception.</summary>
        /// <value>The exception.</value>
        public override Exception Exception { get { return workerTask.Exception; } }

        /// <summary>Gets the thread count.</summary>
        /// <value>The thread count.</value>
        public int ThreadCount { get { return threadCount; } }

		#endregion
    }
}