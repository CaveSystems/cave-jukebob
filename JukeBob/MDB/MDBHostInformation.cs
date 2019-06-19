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

using Cave;
using Cave.Data;

namespace JukeBob
{
	/// <summary>
	/// Host information
	/// </summary>
	[Table("HostInformations")]
    public struct MDBHostInformation
    {
		/// <summary>
		/// Identifier
		/// </summary>
		[Field(Flags = FieldFlags.ID)]
		public long ID;

		/// <summary>
		/// Device or hostname
		/// </summary>
		[Field]
		public string Name;

		/// <summary>
		/// Semicolon separated list of ip addresses.
		/// </summary>
		[Field]
		public string IPAddresses;

		/// <summary>
		/// Port for webserver
		/// </summary>
		[Field]
		public int WebPort;

		/// <summary>
		/// Port for ftp server
		/// </summary>
		[Field]
		public int FtpPort;

		/// <summary>
		/// Port for streaming
		/// </summary>
		[Field]
		public int StreamPort;
	}
}
