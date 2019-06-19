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
using System.Linq;
using System.Text;
using Cave;
using Cave.Compression;
using Cave.IO;
using Cave.Logging;
using Cave.Net;

namespace JukeBob
{
	/// <summary>
	/// Sends notification broadcasts for this mdb instance.
	/// </summary>
	/// <seealso cref="System.IDisposable" />
	public sealed class MDBBroadcaster : IDisposable
	{
		/// <summary>The default broadcast port</summary>
		public const int BroadcastPort = 0x11db;

		static byte[] searchPacket;

		/// <summary>Gets the search packet.</summary>
		/// <value>The search packet.</value>
		public static byte[] SearchPacket
		{
			get
			{
				if (searchPacket == null)
				{
					var writer = new IniWriter();
					writer.WriteSetting("MDB", "Version", "4.0");
					writer.WriteSetting("MDB", "Search", true.ToString());
					searchPacket = Encoding.UTF8.GetBytes(writer.ToString());
				}
				return searchPacket;
			}
		}

		MusicDataBase mdb;
		MDBBroadcastSocket sock;
		byte[] config;
		IniWriter writer = new IniWriter();
		DateTime nextUpdate;
        DateTime nextSend;

        void SendAnswer(object sender, UdpPacketEventArgs e)
		{
			lock (this)
			{
                if (DateTime.Now > nextSend)
                {
                    if (e.Packet.Data.StartsWith("[MDB]"))
                    {
                        var reader = IniReader.Parse("request", e.Packet.Data);
                        bool isSearch = reader.ReadBool("MDB", "Search", false);
                        if (isSearch)
                        {
                            this.LogDebug("Answering search of {0}", e.Packet.RemoteEndPoint);
                            try
                            {
                                if (DateTime.Now > nextUpdate)
                                {
                                    UpdateConfig();
                                    nextUpdate = DateTime.Now.AddSeconds(10);
                                }
                                sock.Send(config);
                                nextSend = DateTime.Now.AddSeconds(1);
                            }
                            catch (Exception ex)
                            {
                                this.LogError(ex, "Could not send broadcast answer to {0}", e.Packet.RemoteEndPoint);
                            }
                        }
                    }
                }
			}
		}

		/// <summary>Initializes a new instance of the <see cref="MDBBroadcaster"/> class.</summary>
		/// <param name="mdb">The MDB.</param>
		public MDBBroadcaster(MusicDataBase mdb)
		{
			this.mdb = mdb;
		}

		/// <summary>Gets the port.</summary>
		/// <value>The port.</value>
		public int Port { get { return sock == null ? 0 : sock.Port; } }

		/// <summary>Gets a value indicating whether this <see cref="MDBBroadcaster"/> is disposed.</summary>
		/// <value><c>true</c> if disposed; otherwise, <c>false</c>.</value>
		public bool Disposed { get; private set; }

		public void UpdateConfig()
		{
			var ips = mdb.GetLocalAddresses();
			writer.WriteSection("Addresses", ips);
			var newString = ips.Join(";");
			if (newString != mdb.Host.IPAddresses)
			{
				mdb.Host.IPAddresses = newString;
				this.LogDebug("System Addresses: {0}", newString);
			}

			config = Encoding.UTF8.GetBytes(writer.ToString());
			byte[] gz = config.Gzip();
			if (gz.Length < config.Length) { config = gz; }
			if (config.Length > 2400)
			{
				throw new InvalidDataException("Configuration data exceeds limits. Please reduce the number of ip/port combinations we listen at or disable ipv6 support.");
			}
		}

		/// <summary>Starts the specified server end points.</summary>
		/// <exception cref="Exception">Already started!</exception>
		/// <exception cref="ObjectDisposedException">Broadcaster</exception>
		/// <exception cref="ArgumentOutOfRangeException">serverEndPoints - Please define ServerEndpoints!</exception>
		/// <exception cref="InvalidDataException">Configuration data exceeds limits. Please reduce the number of ip/port combinations we listen at or disable ipv6 support.</exception>
		public void Start()
		{
			if (config != null) { throw new Exception("Already started!"); }
			if (Disposed) { throw new ObjectDisposedException("Broadcaster"); }

			writer.WriteSetting("MDB", "Version", "4.0");
			writer.WriteSetting("MDB", "Host", mdb.Host.Name);
			writer.WriteSetting("MDB", "Streams", 7500);
			writer.WriteSection("Ports", mdb.Host.WebPort.ToString());
			writer.WriteSetting("MDB", "WebPort", mdb.Host.WebPort);
			writer.WriteSetting("MDB", "FtpPort", mdb.Host.FtpPort);
			UpdateConfig();
			
			sock = new MDBBroadcastSocket(BroadcastPort, true);
			sock.Received += SendAnswer;
			this.LogInfo("Answering broadcasts at port {0}", BroadcastPort);
		}

		#region IDisposable Support

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Disposed = true;
			sock?.Dispose();
			sock = null;
		}
		#endregion
	}
}