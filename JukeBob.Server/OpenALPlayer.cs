using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cave;
using Cave.Data;
using Cave.IO;
using Cave.Logging;
using Cave.Media;
using Cave.Media.Audio;
using Cave.Media.Audio.MP3;

namespace JukeBob
{
	class OpenALPlayer : IPlayer, ILogSource, IDisposable
	{
		TimeSpan OneSecond = TimeSpan.FromSeconds(1);
		TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

		MusicDataBase mdb;
		bool exit;
		bool skip;
		bool disposed;
		MDBNowPlaying currentNowPlaying;
		DateTime nextStart;
		Thread playerThread;
		IAudioDevice device;
		MDBAudioFileSelector selector;

        IAudioDevice SelectDevice()
		{
			IAudioAPI[] apis = AudioAPI.GetAvailableAudioAPIs();
			List<IAudioDevice> devices = GetDevices(apis);
			foreach (var device in devices)
			{
				try
				{
					this.LogDebug("Trying Audio API <cyan>{0}<default> Device <cyan>{1}", device.API, device);
					using (AudioOut check = device.CreateAudioOut(new AudioConfiguration(44100, AudioSampleFormat.Int16, 2)))
					{
						this.LogInfo("Selecting Audio API <cyan>{0}<default> Device <cyan>{1}", device.API, device);
						return device;
					}
				}
				catch (Exception ex)
				{
					this.LogWarning(ex, "Cannot startup audio device <red>{0}", device);
				}
			}
			return null;
		}

		List<IAudioDevice> GetDevices(IAudioAPI[] apis)
		{
			string[] AudioAPIBlackList = mdb.Config.ReadSection("AudioAPI.BlackList", true);
			string[] AudioDeviceBlackList = mdb.Config.ReadSection("AudioDevice.BlackList", true);

			List<IAudioDevice> result = new List<IAudioDevice>();
			foreach (IAudioAPI api in apis)
			{
				if (AudioAPIBlackList.Contains(api.ToString()))
				{
					this.LogDebug("Ignoring blacklisted Audio API <red>{0}", api);
					continue;
				}
				if (!api.IsAvailable)
				{
					this.LogDebug("Audio API <red>{0}<default> is not available.", api);
					continue;
				}

				IAudioDevice[] devices = api.OutputDevices;
				foreach (IAudioDevice device in devices)
				{
					if (AudioDeviceBlackList.Contains(device.ToString())) continue;
					result.Add(device);
				}
			}
			return result;
		}

		/// <summary>Gets the current play list item.</summary>
		/// <value>The current play list item.</value>
		public MDBFileSelection CurrentPlayListItem { get; private set; }

        public MDBStreamSetting CurrentStreamSettings => mdb.GetStreamSettings(StreamID);

        /// <summary>Gets the stream identifier.</summary>
        /// <value>The stream identifier.</value>
        public long StreamID { get; }

		//
		public string LogSourceName => "Player " + StreamID;

		~OpenALPlayer()
		{
			exit = true;
			disposed = true;
		}

		public OpenALPlayer(MusicDataBase mdb, long streamID)
		{
			this.mdb = mdb;
			selector = new MDBAudioFileSelector(mdb);
			StreamID = streamID;
		}

		public void Start()
		{
			if (playerThread != null) throw new Exception("Already started!");
			exit = false;
			playerThread = new Thread(playerProc);
			playerThread.IsBackground = true;
			playerThread.Priority = ThreadPriority.Highest;
			playerThread.Name = LogSourceName;
			playerThread.Start();
		}

		public void Stop()
		{
			exit = true;
			playerThread?.Join();
			playerThread = null;
		}

		private void playerProc()
		{
			int errors = 0;
			while (!exit)
			{
				try
				{
					var item = CurrentPlayListItem = selector.SelectNextFile(StreamID);
					if (item != null && item.File.ID > 0)
					{
						PlayFile(item);
						errors = 0;
					}
					else
					{
						Thread.Sleep(1000);
					}
					continue;
				}
				catch (Exception ex)
				{
					this.LogError(ex, "Unhandled exception in player proc.");
					Thread.Sleep(1000);
					if (++errors > 10)
					{
						device?.Dispose();
						device = null;
					}
				}
			}
		}

		private void PlayFile(MDBFileSelection selection)
		{
			var silenceCompression = mdb.Config.ReadBool("Player", "SilenceCompression", false);

			IAudioDecoder decoder = new Mpg123(false);
			if (!decoder.IsAvailable) decoder = new MP3AudioDecoder();
			this.LogInfo("Prepare playing {0} using audio decoder <cyan>{1}<default> silence compression {2}", selection.AudioFile, decoder, silenceCompression);

			string fileName = selection.File.GetFullPath(mdb);
			this.LogDebug("Open file {0}", fileName);
			var mp3FileStream = ResistantFileStream.OpenSequentialRead(fileName);

			decoder.BeginDecode(mp3FileStream);
			currentNowPlaying = MDBNowPlaying.Create(mdb, selection.PlayListItem.StreamID, selection.PlayListItem.OwnerID, selection.PlayListItem.SubsetID, DateTime.MinValue, selection.AudioFile);

			IAudioData audioData = decoder.Decode();
			if (device == null) device = SelectDevice();
			var audioOut = device.CreateAudioOut(audioData);
			this.LogInfo("Start buffering {0} <cyan>{1}", selection.AudioFile, audioData);

			TimeSpan inSilenceTime = TimeSpan.Zero;
			TimeSpan fileReadPosition = TimeSpan.Zero;
			skip = false;
			bool started = false;
			long underflow = 0;

			while (!exit && !skip)
			{
				//buffer until we got at least one second, or ten during playback
				var sleepTime = started ? audioOut.TimeBuffered - TenSeconds : audioOut.TimeBuffered - OneSecond;
				//buffer filled ?
				if (sleepTime > TimeSpan.Zero)
				{
                    if (audioOut.Volume != Math.Max(0, CurrentStreamSettings.Volume)) audioOut.Volume = CurrentStreamSettings.Volume;

					//already started ?
					if (started)
					{
						//yes, check for a gap/buffer underrun ?
						if (audioOut.BufferUnderflowCount != underflow)
						{
							//we got a gap, fix starttime
							underflow = audioOut.BufferUnderflowCount;
							this.LogWarning("Player GAP {0}, Buffer was empty!", underflow);
							currentNowPlaying.StartDateTime = DateTime.UtcNow - fileReadPosition + audioOut.TimeBuffered;
							ThreadPool.QueueUserWorkItem(delegate { mdb.NowPlaying.Replace(currentNowPlaying); });
						}
						else
						{
							Thread.Sleep(Math.Min(1000, (int)sleepTime.TotalMilliseconds));
						}
					}
					else
					{
						//do start if we are allowed to (check playing previous title)
						sleepTime = nextStart - DateTime.UtcNow;
						if (sleepTime > TimeSpan.Zero)
						{
							this.LogVerbose("Sleep {0}", sleepTime.FormatTime());
							Thread.Sleep(sleepTime);
						}

						currentNowPlaying.StartDateTime = DateTime.UtcNow;
						audioOut.Start();
						started = true;
						this.LogInfo("Start playing {0}", selection.AudioFile);
						//write to now playing
						ThreadPool.QueueUserWorkItem(delegate
						{
							mdb.NowPlaying.Replace(currentNowPlaying);
							mdb.PlayListItems.TryDelete(nameof(MDBPlayListItem.ID), selection.PlayListItem.ID);
						});
					}
				}

				audioData = decoder.Decode();
				//end of file ?
				if (audioData == null) break;
				//add packet duration to file position
				fileReadPosition += audioData.Duration;
				//skip silence
				if (silenceCompression)
				{
					if (audioData.Peak < 0.001f)
					{
						if (inSilenceTime > OneSecond) { continue; }
						inSilenceTime += audioData.Duration;
					}
					else
					{
						if (inSilenceTime > OneSecond)
						{
							//we skipped some silence, fix starttime
							this.LogDebug("Silence compression {0}", inSilenceTime.FormatTime());
							currentNowPlaying.StartDateTime = DateTime.UtcNow - fileReadPosition + audioOut.TimeBuffered;
							ThreadPool.QueueUserWorkItem(delegate { mdb.NowPlaying.Replace(currentNowPlaying); });
						}
						inSilenceTime = TimeSpan.Zero;
					}
				}

				if (!audioOut.Configuration.Equals(audioData))
				{
					this.LogWarning("Frankenstein Stream in file <red>{0}", fileName);
					break;
				}
				audioOut.Write(audioData);
			}
			nextStart = DateTime.UtcNow;
			if (skip)
			{
				//skipped, start in 2s
				nextStart += TimeSpan.FromSeconds(1);
			}
			else
			{
				//start after current title
				nextStart += audioOut.TimeBuffered - TimeSpan.FromSeconds(1);
			}

			this.LogInfo("Finish playing {0}", selection.AudioFile);
			if (exit) CloseAudioOut(audioOut); else CloseAudioOutAsync(audioOut);
		}

		private void CloseAudioOutAsync(AudioOut audioOut)
		{
			Task.Factory.StartNew((a) => { CloseAudioOut(a as AudioOut); }, audioOut);
		}

		private void CloseAudioOut(AudioOut audioOut)
		{
			if (audioOut == null) return;
			var endTime = (exit ? DateTime.UtcNow : nextStart) + TimeSpan.FromSeconds(1);
			try
			{
				float secondsRemaining() => Math.Max(0, Math.Min((float)audioOut.TimeBuffered.TotalSeconds, (float)(endTime - DateTime.UtcNow).TotalSeconds));
				while (secondsRemaining() > 1) Thread.Sleep(1);
				this.LogDebug("Volume slide down start.");
				float volume = audioOut.Volume;
				while (audioOut.Volume > 0)
				{
					audioOut.Volume = Math.Max(0, secondsRemaining() * volume);
					Thread.Sleep(1);
				}
			}
			catch { }
			this.LogDebug("Volume slide down completed.");
			try { lock (audioOut) audioOut.Dispose(); } catch { }
		}

		public void Skip()
		{
			skip = true;
		}

		public void Dispose()
		{
			exit = true;
			if (disposed) return;
			disposed = true;
			Stop();
			GC.SuppressFinalize(this);
		}

		public override string ToString()
		{
			return $"Streamer {StreamID} {currentNowPlaying}";
		}
	}
}
