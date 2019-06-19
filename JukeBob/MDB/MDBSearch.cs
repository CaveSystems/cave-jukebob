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
using Cave.IO;

namespace JukeBob
{
    /// <summary>
    /// Provides search extentions
    /// </summary>
    public static class MDBSearch
    {
        /// <summary>Builds a search text from the specified text.</summary>
        /// <param name="text">The text.</param>
        /// <returns></returns>
        /// <remarks>Space, Point, Star, Percent, Underscore and Questionmark are used as wildcard.</remarks>
        public static string Text(string text)
        {
			if (ReferenceEquals(text, null)) return null;
			string result = "%" + text.Trim().ReplaceChars(" .*%_?", "%") + "%";
            while (result.Contains("%%")) result = result.Replace("%%", "%");
            return result;
        }

		/// <summary>Builds a search text from the specified text.</summary>
		/// <param name="text">The text.</param>
		/// <returns></returns>
		/// <remarks>Space, Point, Star, Percent, Underscore and Questionmark are used as wildcard.</remarks>
		public static string Text(Utf8string text)
		{
			if (ReferenceEquals(text, null)) return null;
			return Text(text.ToString());
		}
    }
}
