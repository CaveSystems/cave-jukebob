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
using Cave;
using Cave.Data;

namespace JukeBob
{
    /// <summary>
    /// Provides the default stream configuration
    /// </summary>
    [Table("StreamSettings")]
    public struct MDBStreamSetting
    {
        /// <summary>The stream identifier</summary>
        [Field(Flags = FieldFlags.ID)]
        public long StreamID;

        [Field]
        public MDBStreamType StreamType;

        /// <summary>The minimum title count at the playlist</summary>
        [Field]
        public int MinimumTitleCount;

		/// <summary>The maximum title count at the playlist</summary>
		[Field]
		public int MaximumTitleCount;

		/// <summary>The minimum length for a autoselected title</summary>
		[Field]
        [TimeSpanFormat(DateTimeType.BigIntTicks)]
        public TimeSpan MinimumLength;

        /// <summary>The maximum length</summary>
        [Field]
        [TimeSpanFormat(DateTimeType.BigIntTicks)]
        public TimeSpan MaximumLength;
        
            /// <summary>The subset identifier to use</summary>
        [Field]
        public long SubsetID;

		/// <summary>Maximum titles per user (negative values disable this)</summary>
		[Field]
        public int TitlesPerUser;

		/// <summary>The volume in range (0..1)</summary>
		[Field]
		public float Volume;
    }
}