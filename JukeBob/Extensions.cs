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

using System.Linq;
using Cave;
using Cave.Auth;
using Cave.Data;
using Cave.Logging;
using Cave.Web.Auth;

namespace JukeBob
{
	/// <summary>
	/// Provides extensions for auth tables
	/// </summary>
	public static class Extensions
    {
		/// <summary>Checks if an admin user present.</summary>
		/// <param name="authTables">The authentication tables.</param>
		public static User CheckAdminPresent(this AuthTables authTables)
		{
			var users = authTables.Users.ToArray();
			var admin = users.Where(u => ((MDBUserLevel)u.AuthLevel).HasFlag(MDBUserLevel.Admin));
			bool adminPresent = admin.Any();
			if (adminPresent)
			{
				authTables.LogNotice("Admin Users: {0}", admin.Join(", "));
				return admin.First();
			}
			//promote existing user ?
			if (users.Length > 0)
			{
				//yes
				User user = users.First();
				if (user.ID > 0)
				{
					user.AuthLevel = (int)MDBUserLevel.Admin;
					authTables.Users.Replace(user);
					authTables.LogNotice("Promoted user {0} to admin!", user);
					return user;
				}
			}
			//create admin
			{
				string pass = Base32.Safe.Encode(AppDom.ProgramID);
				//string pass = "admin";
				authTables.CreateUser("admin", "admin", pass, UserState.Confirmed, MDBUserLevel.Admin, out User user, out EmailAddress email);
				authTables.LogNotice("Added admin {0} password {1}.", user, pass);
				if (authTables.Database == null) authTables.Save();
				return user;
			}
		}
	}
}
