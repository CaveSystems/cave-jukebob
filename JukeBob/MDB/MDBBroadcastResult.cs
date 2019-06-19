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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Cave;
using Cave.Compression;
using Cave.IO;
using Cave.Logging;
using Cave.Net;
using Cave.Web;

namespace JukeBob
{
	/// <summary>
	/// Performs server lookup using udp broadcasts
	/// </summary>
	/// <seealso cref="System.IDisposable" />
	public sealed class MDBBroadcastResult
	{
		/// <summary>Tries to decode the specified packet.</summary>
		/// <param name="e">The <see cref="UdpPacketEventArgs"/> instance containing the event data.</param>
		/// <param name="result">The result.</param>
		/// <returns></returns>
		public static bool TryDecode(UdpPacketEventArgs e, out MDBBroadcastResult result)
		{
			result = null;
			int size = e.Packet.Size;
			byte[] data = e.Packet.Data;
			if (data.Length > size) data = data.GetRange(0, size);

			if (!e.Packet.Data.StartsWith("[MDB]"))
			{
				data = data.Gunzip();
				size = data.Length;
			}
			string content = Encoding.UTF8.GetString(data, 0, size);
			var reader = IniReader.Parse("mdb", content);
			if (reader.ReadSetting("MDB", "Version") != "4.0") return false;
			result = new MDBBroadcastResult()
			{
				LastSeen = DateTime.Now,
				Search = reader.ReadBool("MDB", "Search", false),
				StreamBasePort = reader.ReadInt32("MDB", "Streams", 0),
				Ports = reader.ReadSection("Ports").Select(p => int.Parse(p)).ToList(),
				Addresses = reader.ReadSection("Addresses").Select(ip => IPAddress.Parse(ip)).ToList(),
				Host = reader.ReadSetting("MDB", "Host")
			};
			return true;
		}

		/// <summary>Decodes the specified udp packet.</summary>
		/// <param name="e">The <see cref="UdpPacketEventArgs"/> instance containing the event data.</param>
		/// <returns></returns>
		public static MDBBroadcastResult Decode(UdpPacketEventArgs e)
		{
			if (TryDecode(e, out MDBBroadcastResult result)) { return result; }
			throw new Exception("Invalid package data.");
		}

		List<int> Ports;
		List<IPAddress> Addresses;
		bool isChecking { get; set; }
		public bool Checked { get; set; }

		void Check(IPAddress ip, int port, int streamBasePort)
        {
            try
            {
                this.LogDebug("Test mdb server <cyan>{0}<default> at <cyan>{1}:{2}", Host, ip, port);
                string server = (ip.AddressFamily == AddressFamily.InterNetworkV6) ? $"[{ip}]" : ip.ToString();

				var con = new HttpConnection();
				{
					con.Timeout = TimeSpan.FromSeconds(2);
					Image = con.Download($"http://{server}:{port}/avatar/get?text={Host}");
				}
				var req = new XmlRequest(new Uri($"http://{server}:{port}/mdb/player/state.xml"));
                WebMessage msg = req.Get();
				if (msg.Error == WebError.None)
				{
					lock (this)
					{
						if (Checked) return;
						this.LogNotice("Selected mdb server <green>{0}<default> at <green>{1}:{2}", Host, ip, port);
						Address = ip;
						Port = port;
						StreamBasePort = streamBasePort;
						Checked = true;
						return;
					}
				}
            }
            catch (Exception ex)
            {
                this.LogVerbose(ex, "Could not reach mdb server <red>{0}<default> at <red>{1}:{2}", Host, ip, port);
			}
		}

		/// <summary>
		/// Selects the best address and port
		/// </summary>
		/// <returns></returns>
		public bool SelectBestAddress()
		{
			if (isChecking) return false;
			isChecking = true;
			foreach(IPAddress ip in Addresses)
			{
				if (Checked) break;
				foreach (var port in Ports)
				{
					if (Checked) break;
					Check(ip, port, StreamBasePort);
				}
			}
			isChecking = false;
			return Checked;
		}

		/// <summary>Gets the last seen datetime.</summary>
		/// <value>The last seen datetime.</value>
		public DateTime LastSeen { get; set; }

		/// <summary>
		/// Avatar for this instance
		/// </summary>
		public byte[] Image { get; private set; }

		/// <summary>Gets a value indicating whether this <see cref="MDBBroadcastResult"/> is a search request.</summary>
		/// <value><c>true</c> if package is a search request; otherwise, <c>false</c>.</value>
		public bool Search { get; private set; }

        /// <summary>Gets the address.</summary>
        /// <value>The address.</value>
        public IPAddress Address { get; private set; }

        /// <summary>Gets the port.</summary>
        /// <value>The port.</value>
        public int Port { get; private set; }

		/// <summary>
		/// The Streaming base port
		/// </summary>
		public int StreamBasePort { get; private set; }

		/// <summary>Gets the host name.</summary>
		/// <value>The host name.</value>
		public string Host { get; private set; }

	}
}
