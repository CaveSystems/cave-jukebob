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
using Cave.Console;
using Cave.Data;
using Cave.IO;

namespace JukeBob
{
    /// <summary>
    /// Provides progress information
    /// </summary>
    [Table("Progress")]
    public struct MDBProgress : IXT
    {
        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID)]
        public long ID;

        /// <summary>The source (source component)</summary>
        [Field]
        public Utf8string Source;

        /// <summary>The type (what is done in this work step)</summary>
        [Field]
        public Utf8string Type;

        /// <summary>The progress</summary>
        [Field]
        public double Progress;

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            string progress = Progress.ToString("p");
            return string.Format("[{0}] {1} {2} {3}", ID, Source, Type, progress);
        }

		public XT ToXT()
		{
			string progress = Progress.ToString("p");
			return XT.Format("[{0}] <cyan>{1} <yellow>{2} <green>{3}", ID, Source, Type, progress);
		}
	}
}
