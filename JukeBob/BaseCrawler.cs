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
using Cave.Logging;

namespace JukeBob
{
	/// <summary>
	/// Provides a basic crawler implementation
	/// </summary>
	/// <seealso cref="IMDBCrawler" />
	/// <seealso cref="ILogSource" />
	public abstract class BaseCrawler : IMDBCrawler, ILogSource
    {
		/// <summary>Gets a value indicating whether this <see cref="IMDBCrawler" /> is completed.</summary>
		/// <value><c>true</c> if completed; otherwise, <c>false</c>.</value>
		public abstract bool Completed { get; }

		/// <summary>Gets a value indicating whether this <see cref="IMDBCrawler" /> is error.</summary>
		/// <value><c>true</c> if error; otherwise, <c>false</c>.</value>
		public abstract bool Error { get; }

		/// <summary>Gets the exception.</summary>
		/// <value>The exception.</value>
		public abstract Exception Exception { get; }

		/// <summary>Gets the progress.</summary>
		/// <value>The progress.</value>
		public abstract MDBProgress[] Progress { get; }

		/// <summary>Gets the name of the log source.</summary>
		/// <value>The name of the log source.</value>
		public abstract string LogSourceName { get; }

		/// <summary>Gets a value indicating whether this <see cref="IMDBCrawler" /> is started.</summary>
		/// <value><c>true</c> if started; otherwise, <c>false</c>.</value>
		public abstract bool Started { get; }

		/// <summary>Releases unmanaged and - optionally - managed resources.</summary>
		public abstract void Dispose();

		/// <summary>Starts this instance.</summary>
		public abstract void Start();

		/// <summary>Stops this instance.</summary>
		public abstract void Stop();

		/// <summary>
		/// Provides the name for the crawler instance
		/// </summary>
		public abstract string Name { get; }

		/// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public override string ToString()
        {
            var result = default(MDBProgress);
            foreach(var p in Progress)
            {
                result = p;
                if (p.Progress < 1) break;
            }
            return result.ToString();
        }

		/// <summary>Waits for completion of this instance.</summary>
		public abstract void Wait();
    }
}
