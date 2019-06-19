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
    /// Provides the interface for crawlers
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public interface IMDBCrawler : IDisposable
    {
        /// <summary>Gets a value indicating whether this <see cref="IMDBCrawler"/> is completed.</summary>
        /// <value><c>true</c> if completed; otherwise, <c>false</c>.</value>
        bool Completed { get; }

        /// <summary>Gets a value indicating whether this <see cref="IMDBCrawler"/> is error.</summary>
        /// <value><c>true</c> if error; otherwise, <c>false</c>.</value>
        bool Error { get; }

        /// <summary>Gets the exception.</summary>
        /// <value>The exception.</value>
        Exception Exception { get; }

        /// <summary>Gets the progress.</summary>
        /// <value>The progress.</value>
        MDBProgress[] Progress { get; }

		/// <summary>
		/// Retrieves the name of the crawler instance
		/// </summary>
		string Name { get; }

        /// <summary>Gets a value indicating whether this <see cref="IMDBCrawler"/> is started.</summary>
        /// <value><c>true</c> if started; otherwise, <c>false</c>.</value>
        bool Started { get; }

        /// <summary>Starts this instance.</summary>
        void Start();

        /// <summary>Stops this instance.</summary>
        void Stop();

        /// <summary>Waits for completion of this instance.</summary>
        void Wait();
    }
}
