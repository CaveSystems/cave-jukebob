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

namespace JukeBob
{
    /// <summary>
    /// Event arguments for file indexed events
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class MDBFileIndexedEventArgs : EventArgs
    {
        /// <summary>Gets the file.</summary>
        /// <value>The file.</value>
        public MDBFile File { get; }

        /// <summary>Gets the type of the update.</summary>
        /// <value>The type of the update.</value>
        public MDBUpdateType UpdateType { get; }

        /// <summary>Initializes a new instance of the <see cref="MDBFileIndexedEventArgs"/> class.</summary>
        /// <param name="updateType">Type of the update.</param>
        /// <param name="file">The file.</param>
        public MDBFileIndexedEventArgs(MDBUpdateType updateType, MDBFile file)
        {
            UpdateType = updateType;
            File = file;
        }
    }
}
