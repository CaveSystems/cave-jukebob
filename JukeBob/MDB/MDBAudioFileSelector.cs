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
using Cave.Data;
using Cave.IO;
using Cave.Logging;

namespace JukeBob
{
	/// <summary>
	/// Provides selection of the next audio file based on configured criteria
	/// </summary>
	/// <seealso cref="Cave.Logging.ILogSource" />
	public class MDBAudioFileSelector : ILogSource
    {
        MusicDataBase mdb;
        Random rnd;
		int sequenceNumber;
		IList<long> audioFileIDs;

		/// <summary>
		/// Initializes a new instance of the <see cref="MDBAudioFileSelector"/> class.
		/// </summary>
		/// <param name="mdb">The MDB.</param>
		public MDBAudioFileSelector(MusicDataBase mdb)
        {
            this.mdb = mdb;
            rnd = new Random();
        }

		/// <summary>Gets the name of the log source.</summary>
		/// <value>The name of the log source.</value>
		public string LogSourceName => "MDBAudioFileSelector";

		/// <summary>Gets the current selection.</summary>
		/// <value>The current selection.</value>
		public MDBFileSelection Selection { get; private set; }

		/// <summary>Selects the next file.</summary>
		/// <param name="streamID">The stream identifier.</param>
		/// <returns>Returns true on success, false otherwise</returns>
		public MDBFileSelection SelectNextFile(long streamID)
        {
			var config = mdb.GetStreamSettings(streamID);
			MDBSubset subset = mdb.Subsets.TryGetStruct(config.SubsetID);
            if (subset.ID == 0) subset.Name = "Undefined";
			int seqNumber = mdb.Subsets.SequenceNumber ^ mdb.SubsetFilters.SequenceNumber ^ mdb.AudioFiles.SequenceNumber;

			if (audioFileIDs == null || seqNumber != sequenceNumber)
			{
				audioFileIDs = mdb.GetSubsetAudioFileIDs(subset.ID, config.MinimumLength, config.MaximumLength);
				if (subset.TitleCount != audioFileIDs.Count)
				{
					subset.TitleCount = audioFileIDs.Count;
					if (subset.ID > 0) mdb.Subsets.Update(subset);
				}
				sequenceNumber = seqNumber;
				this.LogInfo("Reloaded subset {0} at player", subset);
			}

            if (subset.ID > 0 && subset.TitleCount != audioFileIDs.Count)
            {
                subset.TitleCount = audioFileIDs.Count;
                try
                {
                    mdb.Subsets.Update(subset);
                    this.LogInfo("Subset {0} title count updated!", subset);
                }
                catch { }
            }

            if (audioFileIDs.Count == 0)
            {
                this.LogDebug("No subset defined or subset result empty for stream <red>{0}<default> selecting random titles.", streamID);
				audioFileIDs = mdb.AudioFiles.IDs;
            }
            if (audioFileIDs.Count == 0)
            {
                this.LogDebug("No audio files found!");
				Selection = null;
				return null;
            }

            Search listSearch = Search.FieldEquals(nameof(MDBPlayListItem.StreamID), streamID);
            while (true)
            {
                Func<long> getCount = () => mdb.PlayListItems.Count(listSearch);

                //fill playlist
                for (int n = 0; getCount() < config.MinimumTitleCount; n++)
                {
                    //max 4 tries per slot
                    if (n > config.MinimumTitleCount * 4) break;
                    int i = (int)((rnd.Next() * (long)rnd.Next()) % audioFileIDs.Count);
                    long nextID = audioFileIDs[i];
                    //audiofile valid ?
                    MDBAudioFile audioFile;
                    if (!mdb.AudioFiles.TryGetStruct(nextID, out audioFile)) continue;

					//file does not exist
					if (!File.Exists(mdb.Files.TryGetStruct(audioFile.FileID).GetFullPath(mdb)))
					{
						mdb.AudioFiles.TryDelete(audioFile.FileID);
						mdb.Files.TryDelete(audioFile.FileID);
						this.LogError("AudioFile <red>{0}<default> removed (inaccessible).", audioFile);
						continue;
					}

                    //yes, playlist contains id already ?
                    if (mdb.PlayListItems.Exist(listSearch & Search.FieldEquals(nameof(MDBPlayListItem.AudioFileID), nextID))) continue;
                    //no add
                    mdb.PlayListItems.Insert(new MDBPlayListItem()
                    {
                        AudioFileID = audioFile.FileID,
                        StreamID = streamID,
                        SubsetID = subset.ID,
                        Added = DateTime.UtcNow.AddTicks(DefaultRNG.Int8),
                    });
                    this.LogInfo("Added audio file {0} from subset {1} to {2} playlist.", audioFile, subset, streamID);
                }

				mdb.Save();

                //get current entry
                try
                {
					var items = mdb.PlayListItems.GetStructs(
						listSearch & Search.FieldGreater(nameof(MDBPlayListItem.OwnerID), 0), 
						ResultOption.SortAscending(nameof(MDBPlayListItem.Added)) + ResultOption.Limit(1));
					if (items.Count == 0)
					{
						items = mdb.PlayListItems.GetStructs(
							listSearch & Search.FieldEquals(nameof(MDBPlayListItem.OwnerID), 0),
							ResultOption.SortAscending(nameof(MDBPlayListItem.Added)) + ResultOption.Limit(1));
					}
                    var item = items.FirstOrDefault();
                    if (item.ID == 0) continue;

					try
					{
						var result = Selection = MDBFileSelection.Load(mdb, item);
						return result;
					}
					finally
					{
						//always remove playlistitem (even on errors)
						mdb.PlayListItems.TryDelete(item.ID);
					}
                }
                catch (Exception ex)
                {
                    this.LogError(ex, "Cannot start stream {0}!", streamID, ex.Message);
					Selection = null;
					return null;
                }
            }
        }
    }
}
