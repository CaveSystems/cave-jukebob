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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cave.Logging;
using Cave.Net;

namespace JukeBob
{
	/// <summary>
	/// Provides a broadcast socket for package transmission
	/// </summary>
	/// <seealso cref="System.IDisposable" />
	public sealed class MDBBroadcastSocket : IDisposable, ILogSource
    {
        Socket sock;
        bool disposed;
		Dictionary<IPAddress, int> errors = new Dictionary<IPAddress, int>();
		Thread thread;
		bool useNetworkInterfaces;

		/// <summary>Gets the port.</summary>
		/// <value>The port.</value>
		public int Port { get; }

		/// <summary>Gets the name of the log source.</summary>
		/// <value>The name of the log source.</value>
		public string LogSourceName => "MDBBroadcastSocket";

		/// <summary>Initializes a new instance of the <see cref="MDBBroadcastSocket" /> class.</summary>
		/// <param name="port">The port.</param>
		/// <param name="enableReceiver">if set to <c>true</c> [enable receiver].</param>
		public MDBBroadcastSocket(int port, bool enableReceiver)
		{
			Port = port;
			try
			{
				useNetworkInterfaces = (NetworkInterface.GetAllNetworkInterfaces().Length > 0);
				sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				this.LogVerbose("Start listening at port <cyan>{0}<default>.", port);
				sock.EnableBroadcast = true;
				sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
				sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				sock.Bind(new IPEndPoint(IPAddress.Any, port));
				thread = new Thread(Read)
				{
					Name = LogSourceName + " UDP:" + sock.LocalEndPoint
				};
				thread.Start();
			}

			catch (Exception ex)
			{
				this.LogWarning(ex, "Could not enable ipv4 broadcaster!");
			}
		}

        private void Read()
        {
            Thread.CurrentThread.IsBackground = true;
			IPAddress addr = IPAddress.Any;
            while (!disposed)
            {
                try
                {
                    byte[] buf = new byte[2500];
                    EndPoint ep = new IPEndPoint(addr, Port);
					if (sock == null) return;
					int i = sock.ReceiveFrom(buf, ref ep);
                    Task.Factory.StartNew((packet) => OnReceived((UdpPacket)packet),
                    new UdpPacket((IPEndPoint)sock.LocalEndPoint, (IPEndPoint)ep, buf, 0, (ushort)i));
                }
                catch (Exception ex)
                {
                    this.LogDebug(ex, "Error during udp receive at {0}:{1}", addr, Port);
                }
            }
        }

		int GetError(IPAddress address)
		{
			return errors.ContainsKey(address) ? errors[address] : 0;
		}

		void IncError(IPAddress address)
		{
			int i = errors[address] = GetError(address) + 1;
			if (i >= 5)
			{
				this.LogAlert("Broadcast at {0} disabled...", address);
			}
		}

		/// <summary>Sends the specified packet.</summary>
		/// <param name="packet">The packet.</param>
		public void Send(byte[] packet)
		{
			if (sock == null) return;
			try
			{
				SendClassicBroadcast(packet);
				if (useNetworkInterfaces) SendInterfaceBroadcast(packet);
			}
			catch (Exception ex)
			{
				this.LogWarning(ex, "Error while preparing broadcast...");
				this.LogAlert("Broadcast disabled...");
				Close();
			}
		}

		private void SendInterfaceBroadcast(byte[] packet)
		{
			foreach (NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces())
			{
				foreach (UnicastIPAddressInformation info in i.GetIPProperties().UnicastAddresses)
				{
					SendInterfaceBroadcast(info, packet);
				}
			}
		}

		private void SendInterfaceBroadcast(UnicastIPAddressInformation info, byte[] packet)
		{
			switch (info.Address.AddressFamily)
			{
				case AddressFamily.InterNetwork:
				{
					IPAddress addr = info.GetBroadcastAddress();
					if (GetError(addr) >= 5) return;
					try
					{
						sock?.SendTo(packet, new IPEndPoint(addr, Port));
					}
					catch (Exception ex)
					{
						this.LogWarning(ex, "Error while sending broadcast at {0}:{1}", addr, Port);
						IncError(addr);
					}
					break;
				}
			}
		}

		private void SendClassicBroadcast(byte[] packet)
		{
			IPAddress addr = IPAddress.Broadcast;
			if (GetError(addr) >= 5) return;
			try
			{
				sock.SendTo(packet, new IPEndPoint(addr, Port));
			}
			catch (Exception ex)
			{
				this.LogWarning(ex, "Error while sending broadcast at {0}:{1}", addr, Port);
				IncError(addr);
			}
		}

		private void OnReceived(UdpPacket packet)
        {
            Received?.Invoke(this, new UdpPacketEventArgs(packet));
        }

        /// <summary>The received event</summary>
        public EventHandler<UdpPacketEventArgs> Received;
		

		/// <summary>Closes this instance.</summary>
		public void Close()
        {
            if (!disposed)
            {
				var s = sock;
				sock = null;
				s?.Close();
                disposed = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
			Close();
        }
    }
}
