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

using Cave;
using Cave.Console;
using Cave.Data;
using Cave.IO;

namespace JukeBob
{
	/// <summary>
	/// A subset filter defintion
	/// </summary>
	[Table("SubsetFilters")]
    public struct MDBSubsetFilter : IXT
    {
        /// <summary>The identifier</summary>
        [Field(Flags = FieldFlags.ID | FieldFlags.AutoIncrement)]
        public long ID;

        /// <summary>The subset identifier</summary>
        [Field(Flags = FieldFlags.Index)]
        public long SubsetID;

        /// <summary>The mode</summary>
        [Field]
        public MDBSubsetFilterMode Mode;

        /// <summary>The type</summary>
        [Field]
        public MDBSubsetFilterType Type;

        /// <summary>The target</summary>
        [Field(Length = 40)]
        [StringFormat(StringEncoding.UTF8)]
        public Utf8string Text;

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
			return string.Format("SubsetFilter [{0}] {1} {2} {3}", ID, Mode, Type, Text);
        }

		public XT ToXT()
		{
			switch (Mode)
			{
				case MDBSubsetFilterMode.Whitelist:
				{
					return XT.Format("SubsetFilter [{0}] <green>{1} <cyan>{2} <magenta>{3}", ID, Mode, Type, Text);
				}
				default:
				{
					return XT.Format("SubsetFilter [{0}] <red>{1} <cyan>{2} <magenta>{3}", ID, Mode, Type, Text);
				}
			}

		}
	}
}
