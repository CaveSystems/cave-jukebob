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
using Cave.Data;

namespace JukeBob
{
	/// <summary>
	/// Provides a selection
	/// </summary>
	public class MDBFileSelection
    {
		/// <summary>Loads the specified <see cref="MDBPlayListItem"/>.</summary>
		/// <param name="mdb">The <see cref="MusicDataBase"/> instance.</param>
		/// <param name="item">The <see cref="MDBPlayListItem"/>.</param>
		/// <returns></returns>
		public static MDBFileSelection Load(MusicDataBase mdb, MDBPlayListItem item)
		{
			var result = new MDBFileSelection();
			result.AudioFile = mdb.AudioFiles.TryGetStruct(item.AudioFileID);
			result.File = mdb.Files.TryGetStruct(result.AudioFile.FileID);
			result.NowPlaying = MDBNowPlaying.Create(mdb, item.StreamID, item.OwnerID, item.SubsetID, DateTime.MinValue, result.AudioFile);
			result.PlayListItem = item;
			return result;
		}

		/// <summary>Gets the currently selected audio file.</summary>
		/// <value>The currently selected audio file.</value>
		public MDBAudioFile AudioFile { get; private set; }

		/// <summary>Gets the currently selected file.</summary>
		/// <value>The currently selected file.</value>
		public MDBFile File { get; private set; }

		/// <summary>Gets the now playing dataset.</summary>
		/// <value>The now playing dataset.</value>
		public MDBNowPlaying NowPlaying { get; private set; }

		/// <summary>Gets the currently selected play list item.</summary>
		/// <value>The currently selected play list item.</value>
		public MDBPlayListItem PlayListItem { get; private set; }
	}
}
