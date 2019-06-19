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
using System.IO;
using Cave;
using Cave.IO;
using Cave.Logging;
using Cave.Media;
using Cave.Web;

namespace JukeBob
{
	/// <summary>
	/// Provides image data and mime type
	/// </summary>
	public class WebImage : ILogSource
    {
		/// <summary>Renders a thumbnail image.</summary>
		/// <param name="bitmap32">The bitmap32.</param>
		/// <param name="thumbFileName">Name of the thumbnail file to save to.</param>
		/// <param name="type">The type.</param>
		/// <returns></returns>
		public static byte[] RenderThumb(Bitmap32 bitmap32, string thumbFileName, ImageType type = ImageType.Jpeg)
		{
			bitmap32.LogDebug("Creating new thumbnail {0}", thumbFileName);
			using (var thumbImageStream = new MemoryStream())
			using (var thumbBitmap = bitmap32.Resize(300, 300, ResizeMode.TouchFromInside))
			{
				thumbBitmap.Save(thumbImageStream, type, 50);
				byte[] data = thumbImageStream.ToArray();
				Directory.CreateDirectory(Path.GetDirectoryName(thumbFileName));
				File.WriteAllBytes(thumbFileName, data);
				return data;
			}
		}

		/// <summary>Loads an image file.</summary>
		/// <param name="fileName">Name of the file.</param>
		/// <returns></returns>
		internal static WebImage FromFile(string fileName, string cachePath)
        {
            return new WebImage(fileName, cachePath);
        }

        /// <summary>Gets the name of the file.</summary>
        /// <value>The name of the file.</value>
        public string FileName { get; }

        /// <summary>Gets the mime type of the image.</summary>
        /// <value>The mime type of the image.</value>
        public string MimeType { get; }

        /// <summary>Gets the data.</summary>
        /// <value>The data.</value>
        public byte[] Data { get; }

		/// <summary>Gets the thumbnail data.</summary>
		/// <value>The thumbnail data.</value>
		public byte[] ThumbData { get; }

		/// <summary>
		/// Retrieves the log source name
		/// </summary>
		public string LogSourceName => "WebImage";

		WebImage(string fileName, string cachePath)
		{
			string ext = Path.GetExtension(fileName);
			FileName = fileName;
			MimeType = MimeTypes.FromExtension(ext);
			this.LogDebug("Loading Bitmap32 from file {0}", fileName);
			Data = File.ReadAllBytes(fileName);
			//read thumb if present
			var ThumbFileName = FileSystem.Combine(cachePath, Path.GetFileNameWithoutExtension(fileName) + "-thumb" + ext);
			if (File.Exists(ThumbFileName))
			{
				//but check if the image was changed first.
				if (FileSystem.GetLastWriteTimeUtc(ThumbFileName) > FileSystem.GetLastWriteTimeUtc(fileName))
				{
					ThumbData = File.ReadAllBytes(ThumbFileName);
					return;
				}
			}
			//need to recreate thumb
			using (var bmp = Bitmap32.Create(Data))
			{
				switch (MimeType)
				{
					case "image/jpeg": ThumbData = RenderThumb(bmp, ThumbFileName, ImageType.Jpeg); break;
					case "image/png": ThumbData = RenderThumb(bmp, ThumbFileName, ImageType.Png); break;
					default: throw new NotImplementedException();
				}
			}

		}
    }
}
