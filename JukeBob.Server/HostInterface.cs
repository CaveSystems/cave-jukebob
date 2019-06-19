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
using Cave.Logging;
using Cave.Service;
using Cave.Web;

namespace JukeBob
{
    public class HostInterface : ILogSource
	{
		ServiceProgram program;

		public string LogSourceName => "HostInterface";

		public HostInterface(ServiceProgram program)
		{
			this.program = program;
		}

		#region /host/shutdown
		[WebPage(Paths = "/host/shutdown", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
		public void Shutdown(WebData webData, bool? wholeSystem = null)
		{
			if (true == wholeSystem)
			{
				string command;
				string parameters;
				switch (Platform.Type)
				{
					case PlatformType.Windows:
					{
						command = "shutdown";
						parameters = $"/s /t 60 /f /d u:00:00 /c \"JukeBox Shutdown Command Recevied by {webData.Session.GetUser()}\"";
						break;
					}
					case PlatformType.Linux:
					{
						command = "shutdown";
						parameters = $"-h +1 \"JukeBox Shutdown Command Recevied by {webData.Session.GetUser()}\"";
						break;
					}
					default: throw new NotSupportedException("This platform does not support shutdown wholeSystem!");
				}
				var result = ProcessRunner.Run(command, parameters, 10000);
				if (result.ExitCode == 0)
				{
					webData.Result.AddMessage(webData.Method, "System will shutdown in 60s...");
				}
				else
				{
					throw new WebServerException(WebError.InternalServerError, result.Combined);
				}
			}
			webData.Result.AddMessage(webData.Method, "JukeBox will shutdown...");
			program.ServiceParameters.CommitShutdown();
		}
		#endregion
	}
}
