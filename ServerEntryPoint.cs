using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ComSkipper
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private List<EdlSequence> commercialList = new List<EdlSequence>();
        private List<EdlTimestamp> timestamps = new List<EdlTimestamp>();

        private ISessionManager SessionManager { get; set; }

        private IUserManager UserManager { get; set; }

        private IServerConfigurationManager ConfigManager { get; set; }

        private ILogger Log { get; set; }

        private string Locale = string.Empty;

        public ServerEntryPoint(ISessionManager sessionManager, IUserManager userManager, ILogManager logManager, IServerConfigurationManager configManager)
        {
            SessionManager = sessionManager;
            UserManager = userManager;
            ConfigManager = configManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }


        public void Dispose()
        {
            SessionManager.PlaybackStart -= PlaybackStart;
            SessionManager.PlaybackStopped -= PlaybackStopped;
            SessionManager.PlaybackProgress -= PlaybackProgress;
        }

        public void Run()
        {
            // TODO: When Emby adds clients locale to the Session object, use that instead of the servers locale below.
            Locale = ConfigManager.Configuration.UICulture;
            Log.Debug("Locale = " + Locale);

            SessionManager.PlaybackStart += PlaybackStart;
            SessionManager.PlaybackStopped += PlaybackStopped;
            SessionManager.PlaybackProgress += PlaybackProgress;

            Plugin.Instance.UpdateConfiguration(Plugin.Instance.Configuration);
        }

        /// <summary>
        /// Executed on a playback started Emby event. Read the EDL file and add to commercialList.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            if (Plugin.Instance.Configuration.EnableComSkipper == false)
                return;

            string filePath = e.MediaInfo.Path;
            string session = e.Session.Id;
            Log.Debug("Playback Session = " + session + " Path = " + filePath);

            lock (timestamps)
            {
                EdlTimestamp ts = new EdlTimestamp();
                ts.sessionId = session;
                ts.timeLoaded = DateTimeOffset.Now.ToUnixTimeSeconds();
                timestamps.Add(ts);
            }

            ReadEdlFile(e);
        }

        /// <summary>
        /// Executed on a playback prorgrss Emby event. See if it is in a identified commercial and skip if it is.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            if (Plugin.Instance.Configuration.EnableComSkipper == false)
                return;

            if (e.Session.PlayState.IsPaused || !e.PlaybackPositionTicks.HasValue)
                return;

            string session = e.Session.Id;

            if (e.Item.IsActiveRecording() == true && Plugin.Instance.Configuration.RealTimeEnabled == true)
            {
                // Reload EDL info every minute
                long ns = DateTimeOffset.Now.ToUnixTimeSeconds();
                EdlTimestamp tsfound = timestamps.Find(x => x.sessionId == session);
                if(tsfound != null)
                {
                    if ((ns - tsfound.timeLoaded) >= 60)
                    {
                        Log.Debug("Reloading EDL data for Session " + tsfound.sessionId);

                        RemoveFromList(session);
                        ReadEdlFile(e);
                    }
                }
            }
          
            long playbackPositionTicks = e.PlaybackPositionTicks.Value;

            EdlSequence found = commercialList.Find(x => x.sessionId == session && x.skipped == false && playbackPositionTicks >= x.startTicks && playbackPositionTicks < (x.endTicks - 1000));
            if (found != null)
            {
                found.skipped = true;
                SkipCommercial(session, found.endTicks);

                if (e.Session.Capabilities.SupportedCommands.Contains("DisplayMessage"))
                    SendMessageToClient(session);

                Log.Info("Skipping commercial. Session: " + session + " Start = " + found.startTicks.ToString() + "  End = " + found.endTicks.ToString());
            }
        }

        /// <summary>
        /// Executed on a playback stopped Emby event. Remove the commercialList entries for the session.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            if (Plugin.Instance.Configuration.EnableComSkipper == false)
                return;

            string name = e.MediaInfo.Name;
            string sessionID = e.Session.Id;
            Log.Debug("Playback Stopped. Session = " + sessionID + " Name = " + name);

            RemoveFromList(sessionID);
        }

        private void ReadEdlFile(PlaybackProgressEventArgs e)
        {
            string filePath = e.MediaInfo.Path;
            string session = e.Session.Id;

            // Check for edl file and load skip list if found
            // Seconds to ticks = seconds * TimeSpan.TicksPerSecond
            string edlFile = Path.ChangeExtension(filePath, ".edl");
            if (!File.Exists(edlFile))
                return;

            Log.Debug("EDL file " + edlFile + " found.");

            List<EdlSequence> commTempList = new List<EdlSequence>();

            try
            {
                string line;
                using (StreamReader reader = File.OpenText(edlFile))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        Log.Debug("parts " + parts[0] + " " + parts[1]);

                        EdlSequence seq = new EdlSequence();
                        seq.sessionId = session;
                        seq.startTicks = (long)(double.Parse(parts[0]) * (double)TimeSpan.TicksPerSecond);
                        if (seq.startTicks < TimeSpan.TicksPerSecond)
                            seq.startTicks = TimeSpan.TicksPerSecond;
                        seq.endTicks = (long)(double.Parse(parts[1]) * (double)TimeSpan.TicksPerSecond);

                        commTempList.Add(seq);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not parse EDL file " + edlFile + ". Exception: " + ex.Message);
                return;
            }

            lock (commercialList)
            {
                commercialList.AddRange(commTempList);
            }

            Log.Debug("Commmercial List in seconds for " + e.MediaInfo.Name + ":");
            foreach (EdlSequence s in commTempList)
            {
                Log.Debug("Start: " + (s.startTicks / TimeSpan.TicksPerSecond).ToString() + "  End: " + (s.endTicks / TimeSpan.TicksPerSecond).ToString());
            }
        }

        private void RemoveFromList(string sessionID)
        {
            // Remove all items is skip list with this session ID
            lock (commercialList)
            {
                commercialList.RemoveAll(x => x.sessionId == sessionID);
            }

            lock (timestamps)
            {
                timestamps.RemoveAll(x => x.sessionId == sessionID);
            }
        }

        /// <summary>
        /// Skip the commercial for the given session by seeking to the end of the commercial.
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="seek"></param>
        private void SkipCommercial(string sessionID, long seek)
        {
            PlaystateRequest playstateRequest = new PlaystateRequest();
            playstateRequest.Command = PlaystateCommand.Seek;
            playstateRequest.ControllingUserId = ((BaseItem)((IEnumerable<User>)this.UserManager.Users).FirstOrDefault<User>((Func<User, bool>)(u => u.Policy.IsAdministrator)))?.Id.ToString();
            playstateRequest.SeekPositionTicks = new long?(seek);
            SessionManager.SendPlaystateCommand((string)null, sessionID, playstateRequest, CancellationToken.None);
        }

        /// <summary>
        /// Send Commercial Skipped message to client
        /// </summary>
        /// <param name="session"></param>
        private async void SendMessageToClient(string sessionID)
        {
            MessageCommand messageCommand = new MessageCommand();
            messageCommand.Header = String.Empty;
            messageCommand.Text = Localize.localize("Commercial Skipped", Locale);
            messageCommand.TimeoutMs = new long?(1000L);
            await SessionManager.SendMessageCommand(sessionID, sessionID, messageCommand, CancellationToken.None);
        }
    }

    /// <summary>
    /// EDL file representation
    /// </summary>
    public class EdlSequence
    {
        public string sessionId { get; set; }
        public bool skipped { get; set; } = false;
        public long startTicks { get; set; }
        public long endTicks { get; set; }
    }

    /// <summary>
    /// EDL timestamp
    /// </summary>
    public class EdlTimestamp
    {
        public long timeLoaded { get; set; }
        public string sessionId { get; set; }
    }
}
