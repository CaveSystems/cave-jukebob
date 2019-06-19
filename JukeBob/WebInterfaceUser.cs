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
using System.Net;
using Cave;
using Cave.Data;
using Cave.IO;
using Cave.Web;

namespace JukeBob
{
	partial class WebInterface
	{
		#region /mdb/playlist

		/// <summary>Adds a new title to the playlist.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="audioFileID">The audio file identifier.</param>
		/// <param name="streamID">The stream identifier.</param>
		/// <exception cref="WebException">Song was already added to playlist!
		/// or</exception>
		[WebPage(Paths = "/mdb/playlist/add", AuthType = WebServerAuthType.Session)]
		public void AddToPlaylist(WebData webData, long audioFileID, long streamID = 0)
		{
			if (streamID == 0) streamID = (long)MDBStreamType.JukeBob;
			lock (mdb.PlayListItems)
			{
				var settings = mdb.GetStreamSettings(streamID);
				var presentCount = mdb.PlayListItems.Count(nameof(MDBNowPlaying.StreamID), MDBStreamType.JukeBob);
				if (settings.MaximumTitleCount > 0 && presentCount > settings.MaximumTitleCount)
				{
					throw new WebServerException(WebError.DatasetAlreadyPresent, "Already {0} titles present at playlist!", presentCount);
				}

				bool exist =
					//if is in nowplaying (file, JukeBob)
					mdb.NowPlaying.Exist(
						Search.FieldEquals(nameof(MDBNowPlaying.AudioFileID), audioFileID) &
						Search.FieldEquals(nameof(MDBNowPlaying.StreamID), MDBStreamType.JukeBob)
					) ||
					//or is in playlist (file, JukeBob)
					mdb.PlayListItems.Exist(
						Search.FieldEquals(nameof(MDBPlayListItem.AudioFileID), audioFileID) &
						Search.FieldEquals(nameof(MDBPlayListItem.StreamID), MDBStreamType.JukeBob)
					);
				if (exist) throw new WebServerException(WebError.DatasetAlreadyPresent, "Title is already present at playlist!");

				var item = new MDBPlayListItem()
				{
					AudioFileID = audioFileID,
					StreamID = streamID,
					Added = DateTime.UtcNow.AddTicks(DefaultRNG.Int8),
				};

				item.OwnerID = webData.Session.GetUser().ID;
				long titleCount = mdb.PlayListItems.Count(Search.FieldEquals(nameof(MDBPlayListItem.OwnerID), item.OwnerID));
				
				if (settings.TitlesPerUser > 0 && titleCount + 1 > settings.TitlesPerUser)
				{
					throw new WebServerException(WebError.MissingRights, 0, string.Format("You already have {0} titles at the playlist.", titleCount));
				}				
				mdb.PlayListItems.Insert(item);
			}
		}

		/// <summary>Removes the specified title from the playlist if the title was added by the system or the calling user.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="playlistItemID">The playlist item identifier.</param>
		[WebPage(Paths = "/mdb/playlist/remove", AuthType = WebServerAuthType.Session)]
		public void RemoveFromPlaylist(WebData webData, long playlistItemID)
		{
			var userLevel = (MDBUserLevel)webData.Session.GetUser().AuthLevel;
			long ownerID = CaveSystemData.CalculateID(webData.Request.SourceAddress);
			if (ownerID > 0) ownerID = -ownerID;

			Search search =
				Search.FieldEquals(nameof(MDBPlayListItem.ID), playlistItemID) &
				Search.FieldEquals(nameof(MDBPlayListItem.StreamID), MDBStreamType.JukeBob);

			lock (mdb.PlayListItems)
			{
				foreach (var item in mdb.PlayListItems.GetStructs(search))
				{
					//admin mode or auto added subset title or added by myself
					if (item.OwnerID >= 0 || item.OwnerID == ownerID || userLevel.HasFlag(MDBUserLevel.Admin))
					{
						mdb.PlayListItems.Delete(item.ID);
					}
					else
					{
						throw new WebServerException(WebError.MissingRights, 0, "You can only delete your own titles!");
					}
				}
			}
		}

		#endregion

		#region /mdb/player

		/// <summary>Skips a title at the specified stream.</summary>
		/// <param name="webData">The web data.</param>
		[WebPage(Paths = "/mdb/player/skip", AuthType = WebServerAuthType.Session)]
		public void SkipTitleAtPlayerUser(WebData webData)
		{
			if (player == null) return;
			var user = webData.Session.GetUser();
			if (((MDBUserLevel)user.AuthLevel).HasFlag(MDBUserLevel.Admin) || player.CurrentPlayListItem.PlayListItem.OwnerID <= 0 || player.CurrentPlayListItem.PlayListItem.OwnerID == user.ID)
			{
				webData.Result.AddMessage(webData.Method, "Skipped current title...");
				player.Skip();
			}
			else
			{
				webData.Result.AddMessage(webData.Method,WebError.InvalidOperation, "Title belongs to another user. Only Admins may skip this title!");
			}
		}

		#endregion

		#region /mdb/user/account/password/update

		/// <summary>Updates the current user record with a new password.</summary>
		/// <param name="webData">The web data.</param>
		/// <param name="newPassword">The new password.</param>
		[WebPage(Paths = "/mdb/user/account/password/update", AuthType = WebServerAuthType.Session)]
		public void UpdateUserPassword(WebData webData, string newPassword)
		{
			var user = webData.Session.GetUser();
			user.SetRandomSalt();
			user.SetPassword(newPassword);
			authTables.Users.Update(user);
			webData.Result.AddMessage(webData.Method, "Password of user {0} updated...", user);
		}

		#endregion

		#region /mdb/user/account/delete

		/// <summary>Deletes the current user record.</summary>
		/// <param name="webData">The web data.</param>
		[WebPage(Paths = "/mdb/user/account/delete", AuthType = WebServerAuthType.Session)]
		public void DeleteCurrentUser(WebData webData)
		{
			var user = webData.Session.GetUser();
			authTables.Users.TryDelete(user.ID);
			webData.Session.Expire();
			webData.Result.AddMessage(webData.Method, "User {0} deleted...", user);
		}

		#endregion
	}
}
