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

using Cave.Data;
using Cave.Logging;
using System;

namespace JukeBob
{
    /// <summary>
    /// Extensions
    /// </summary>
    public static class MDBExtension
    {
        /// <summary>Determines whether [is album art].</summary>
        /// <param name="imageType">Type of the image.</param>
        /// <returns><c>true</c> if [is album art] [the specified image type]; otherwise, <c>false</c>.</returns>
        public static bool IsAlbumArt(this MDBImageType imageType)
        {
            switch (imageType)
            {
				case MDBImageType.UserCover:
				case MDBImageType.AlbumCover:
                case MDBImageType.AlbumCDArt:
                case MDBImageType.AlbumCoverFront:
                    return true;
                default: return false;
            }
        }

        /// <summary>Determines whether [is artist art].</summary>
        /// <param name="imageType">Type of the image.</param>
        /// <returns><c>true</c> if [is artist art] [the specified image type]; otherwise, <c>false</c>.</returns>
        public static bool IsArtistArt(this MDBImageType imageType)
        {
            switch (imageType)
            {
                case MDBImageType.ArtistBackground:
                case MDBImageType.ArtistMusicBanner:
                case MDBImageType.ArtistMusicLogo:
                case MDBImageType.ArtistMusicLogoHD:
                case MDBImageType.ArtistThumb:
                    return true;
                default: return false;
            }
        }

        /// <summary>Updates the dataset at the database or inserts a new one.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="table">The table.</param>
        /// <param name="dataSet">The dataset.</param>
        /// <param name="fieldNames">The field names.</param>
        /// <returns>Returns true on update and false on insert</returns>
        /// <exception cref="System.Exception">Unique field exception!</exception>
        public static T UpdateOrInsert<T>(this ITable<T> table, T dataSet, params string[] fieldNames) where T : struct
        {
            lock (table)
            {
                Row row = new Row(table.Layout.GetValues(dataSet));
                Search search = Search.None;
                foreach (string fieldName in fieldNames)
                {
                    int field = table.Layout.GetFieldIndex(fieldName);
                    if (field < 0) throw new Exception("Field " + fieldName + " not found!");
                    object value = row.GetValue(field);
                    search &= Search.FieldEquals(fieldName, value);
                }

                //already exists ?
                var datasets = table.GetRows(search);
                if (datasets.Count > 1)
                {
                    table.LogError("Table {0} has multiple entries for search {1}!", table.Name, search);
                    for (int i = 1; i < datasets.Count; i++)
                    {
                        long id = table.Layout.GetID(datasets[i]);
                        table.Delete(id);
                    }
                }
                if (datasets.Count == 0)
                {
                    //create new
                    table.LogInfo($"New {typeof(T).Name}: <cyan>{dataSet}");
                    row = row.SetID(table.Layout.IDFieldIndex, table.Insert(row));
                }
                else
                {
                    //load old
                    row = datasets[0];
                }
                //return
                return row.GetStruct<T>(table.Layout);
            }
        }
    }
}
