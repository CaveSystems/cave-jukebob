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
