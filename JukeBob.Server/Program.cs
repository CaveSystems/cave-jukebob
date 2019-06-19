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
using System.IO;
using System.Reflection;
using Cave;
using Cave.Auth;
using Cave.Data;
using Cave.IO;
using Cave.Logging;
using Cave.Net.Ftp;
using Cave.Service;
using Cave.Web;
using Cave.Web.Auth;
using Cave.Web.Avatar;

namespace JukeBob
{
    class Server : ServiceProgram
    {
		AuthTables authTables;

        Server()
        {
			//Allow multiple instances
            //ServiceName += "_" + Base32.Safe.Encode(CaveSystem.ProgramID);
			if (Platform.IsMicrosoft)
			{
				this.LogInfo("<green>Enabling <default>microsoft media timing.");
				//TODO WindowsMediaTimer.Begin();
			}
            if (Logger.DebugReceiver != null) Logger.DebugReceiver.Mode = LogReceiverMode.Opportune;
            Assembly.LoadWithPartialName("MySql.Data");
        }

        void SetFtpMusicFolders(MusicDataBase mdb, FtpServer ftpServer)
		{
			lock (ftpServer.RootFolders)
			{
				ftpServer.RootFolders.Clear();
				foreach (var dir in mdb.MusicFolders)
				{
					try
					{
						FileSystem.GetFileSystemEntries(dir);
						string name = Path.GetFileNameWithoutExtension(dir);
						int i = 0;
						string selectedName = name;
						while (!ftpServer.RootFolders.TryAdd(selectedName, dir))
						{
							selectedName = name + (++i);
						}
						this.LogDebug("Added root <cyan>{0}<default> {1}", selectedName, dir);
					}
					catch (Exception ex)
					{
						this.LogWarning(ex, "Cannot access folder {0}", dir);
					}
				}
				ftpServer.DisconnectAllClients();
			}
		}

		protected override void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Escape: ServiceParameters.CommitShutdown(); break;
            }
        }

		protected override void Worker()
		{
			if (LogConsole != null)
			{
				LogConsole.ExceptionMode = LogExceptionMode.Full;
				LogConsole.Flags |= LogConsoleFlags.DisplayTimeStamp;
                LogConsole.Mode = LogReceiverMode.Opportune;
			}

            Logger.DebugReceiver?.Close();

			//init the async mysql connection class we want to use.
			//new MySql.Data.MySqlClient.MySqlConnection().Dispose();

			WebServer webServer = null;
			FtpServer ftpServer = null;
			MusicDataBase mdb = null;
			MDBBroadcaster broadcaster = null;
			DesktopPlayer player = null;

			try
			{
				var rootFolder = FileSystem.ProgramDirectory;
				webServer = new WebServer();
				mdb = new MusicDataBase(rootFolder);
				if (LogSystem != null)
				{
                    //allow user to override loglevel by commandline
					if (LogSystem.Level == LogLevel.Information)
					{
						LogSystem.Level = mdb.Config.ReadEnum<LogLevel>("MusicDataBase", "LogLevel", LogLevel.Information);
					}
				}

				this.LogInfo("Loading Database...");
				mdb.Load();

				MemoryStorage.Default.LogVerboseMessages = LogSystem?.Level == LogLevel.Verbose;
				if (null != mdb.Database) mdb.Database.Storage.LogVerboseMessages = MemoryStorage.Default.LogVerboseMessages;

				if (mdb.Config.ReadBool("MusicDataBase", "ClearDatabase", false))
				{
					foreach (ITable t in mdb.Tables) t.Clear();
				}

				mdb.Save();

				this.LogInfo("Loading auth tables...");
				if (mdb.Database != null)
				{
					webServer.AuthTables.Connect(TableConnectorMode.Direct, mdb.Database);
				}
				else
				{
					try
					{
						webServer.AuthTables.Load(mdb.DataBasePath);
					}
					catch (Exception ex)
					{
						this.LogWarning(ex, "Load auth tables failed. Recreating...");
					}
				}
				authTables = webServer.AuthTables;

				var authInterface = new AuthInterface<MDBUserLevel>(webServer);
				{
					//auth interface
					User admin = webServer.AuthTables.CheckAdminPresent();
					if (mdb.Config.ReadBool("MusicDataBase", "LocalhostIsAdmin", false))
					{
						authInterface.DefaultLocalhostUser = admin;
					}
				}

				this.LogInfo("Initializing FtpServer...");
				ftpServer = new FtpServer();
				ftpServer.CheckLogin += this.FtpServerCheckLogin;
				//add music dirs to ftp server
				SetFtpMusicFolders(mdb, ftpServer);
				
				int ftpPort = mdb.Config.ReadInt32("FtpServer", "Port", 8021);
				ftpServer.Listen(ftpPort);

				player = new DesktopPlayer(mdb, (long)MDBStreamType.JukeBob);
				player.Start();

				this.LogInfo("Initializing WebServer...");
				webServer.SessionMode = WebServerSessionMode.Cookie;
				webServer.SessionTimeout = TimeSpan.FromDays(1);
				webServer.PerformanceChecks = mdb.Config.ReadBool("WebServer", "PerformanceChecks", false);
				webServer.EnableExplain = mdb.Config.ReadBool("WebServer", "Explain", false);
				webServer.TransmitLayout = false;
				webServer.EnableTemplates = true;
				webServer.CheckAccess += WebServerCheckAccess;
				webServer.StaticFilesPath = mdb.WebFolder;

				//prepare rpc
				var avatarFolder = mdb.GetFolderConfig("MusicDataBase", "AvatarFolder", "avatar");
				webServer.Register(new AvatarInterface(avatarFolder));
				webServer.Register(authInterface);
				var webInterface = new WebInterface(mdb, webServer.AuthTables, player);

				webInterface.LogCollector.Level = LogLevel.Debug;
				webInterface.LogCollector.MaximumItemCount = 1000;
				webInterface.LogCollector.ExceptionMode = LogExceptionMode.Full;

				webServer.Register(webInterface);
				webServer.Register(new HostInterface(this));

				//start server
				int webPort = mdb.Config.ReadInt32("WebServer", "Port", 8080);
				webServer.Listen(webPort);

				mdb.SetHostConfiguration(Environment.UserName + "." + Environment.MachineName, webPort, ftpPort, mdb.GetLocalAddresses());

				//using (Streamer streamer = new Streamer(mdb, MDBStreamType.JukeBox))
				broadcaster = new MDBBroadcaster(mdb);
				broadcaster.Start();

				if (mdb.Config.ReadBool("Crawler", "RunAtStartup", false))
				{
					webInterface.FileCrawler.Start();
				}

				//main loop
				this.LogNotice("Entering main loop...");
				while (!ServiceParameters.Shutdown)
				{
					ServiceParameters.WaitForShutdown(1000);
					//SetConsoleTitle();                
				}
			}
			finally
			{
				//cleanup
				broadcaster?.Dispose();
				ftpServer?.Close();
				player?.Stop();
				player?.Dispose();
				authTables?.Save();
				authTables = null;
				mdb?.Save();
				webServer?.Close();
			}
			this.LogInfo("Shutdown completed.");
			Logger.Flush();
		}

		private void FtpServerCheckLogin(object sender, FtpLoginEventArgs e)
		{
			bool valid = false;
			if (authTables.Login(e.UserName, e.Password, out var user, out var emailAddress))
			{
				valid = (((MDBUserLevel)user.AuthLevel).HasFlag(MDBUserLevel.Admin));					
			}
			e.Denied = !valid;
		}

		private void WebServerCheckAccess(object sender, WebAccessEventArgs e)
		{
			switch (e.Data.Method?.PageAttribute.AuthType)
			{
				case null:
				case WebServerAuthType.None: break;

				case WebServerAuthType.Basic:
					if (!e.Data.Session.IsAuthenticated()) e.Denied = true;
					break;

				case WebServerAuthType.Session:
					var requiredLevel = e.Data.Method.PageAttribute.AuthData.Parse<MDBUserLevel>();
					var level = (MDBUserLevel)e.Data.Session.GetUser().AuthLevel;
					if (!level.HasFlag(requiredLevel)) e.Denied = true;
					break;
			}
		}

        [STAThread]
        static void Main()
        {
			new Server().Run();
        }
    }
}
