using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            // Set for correct parsing of the EDL file regardless of servers culture
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

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
            {
                Log.Debug("PlaybackStart: Plugin is disabled.");
                return;
            }

            string filePath = e.MediaInfo.Path;
            string session = e.Session.Id;
            Log.Debug("Playback Session = " + session + " Path = " + filePath);

            // Remove any stragglers
            RemoveFromList(session);
            
            AddTimestamp(session);

            ReadEdlFile(e);
        }

        /// <summary>
        /// Executed on a playback prorgress Emby event. See if it is in a identified commercial and skip if it is.
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

            // This should allow for people who are watching as it is being recorded to skip commercials
            if (e.Item.IsActiveRecording() == true && Plugin.Instance.Configuration.RealTimeEnabled == true)
            {
                // Reload EDL info every minute
                long ns = DateTimeOffset.Now.ToUnixTimeSeconds();
                EdlTimestamp tsfound = timestamps.Find(x => x.sessionId == session);
                if(tsfound != null)
                {
                    if ((ns - tsfound.timeLoaded) >= 60)
                    {
                        RemoveFromList(session);
                        Log.Debug("Reloading EDL data for Session " + tsfound.sessionId);
                     
                        ReadEdlFile(e);

                        AddTimestamp(session);
                    }
                }
            }
          
            long playbackPositionTicks = e.PlaybackPositionTicks.Value;

            EdlSequence found = commercialList.Find(x => x.sessionId == session && x.skipped == false && playbackPositionTicks >= x.startTicks && playbackPositionTicks < (x.endTicks - 1000));
            if (found != null)
            {
                string controlSession = (e.Session.SupportsRemoteControl)
                    ? e.Session.Id
                    : SessionManager.Sessions.Where(i => i.DeviceId == e.Session.DeviceId && i.SupportsRemoteControl).FirstOrDefault().Id;

                if(string.IsNullOrEmpty(controlSession))
                {
                    Log.Debug($"No control session for SessionID {e.Session.Id}");
                    return;
                }

                found.skipped = true;
                SkipCommercial(controlSession, found.endTicks);

                if (Plugin.Instance.Configuration.DisableMessage == false && e.Session.Capabilities.SupportedCommands.Contains("DisplayMessage"))
                    SendMessageToClient(controlSession, ((found.endTicks - found.startTicks) / TimeSpan.TicksPerSecond).ToString());

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

        /// <summary>
        /// Read and process the comskip EDL file
        /// </summary>
        /// <param name="e"></param>
        private void ReadEdlFile(PlaybackProgressEventArgs e)
        {
            string filePath = e.MediaInfo.Path;
            string session = e.Session.Id;

            string edlFile = Path.ChangeExtension(filePath, ".edl");
            Log.Debug("Media File: " + filePath + "   EDL file " + edlFile);

            // Check for edl file and load skip list if found
            // Seconds to ticks = seconds * TimeSpan.TicksPerSecond
            
            if (!File.Exists(edlFile))
            {
                Log.Debug($"Comskip EDL file [{edlFile}] does not exist.");
                return;
            }

            // Remove any stragglers
            lock (commercialList)
            {
                commercialList.RemoveAll(x => x.sessionId == session);
            }

            Log.Info($"EDL file {edlFile} found.");

            List<EdlSequence> commTempList = new List<EdlSequence>();

            try
            {
                string line;
                using (StreamReader reader = File.OpenText(edlFile))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        Log.Debug("parts " + parts[0] + " " + parts[1] + " " + parts[2]);

                        // 1 indicates it is meant to mute audio, not skip
                        if (parts[2] != "1")
                        {
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

            Log.Debug("Commercial List in seconds for " + e.MediaInfo.Name + ":");
            foreach (EdlSequence s in commTempList)
            {
                Log.Debug("Start: " + (s.startTicks / TimeSpan.TicksPerSecond).ToString() + "  End: " + (s.endTicks / TimeSpan.TicksPerSecond).ToString());
            }
        }

        /// <summary>
        /// Remove a session from various lists
        /// </summary>
        /// <param name="sessionID"></param>
        private void RemoveFromList(string sessionID)
        {
            if (Plugin.Instance.Configuration.RealTimeEnabled == true)
            {
                // Remove all items in timestamp list with this session ID
                lock (timestamps)
                {
                    timestamps.RemoveAll(x => x.sessionId == sessionID);
                }
            }

            // Remove all items in skip list with this session ID
            lock (commercialList)
            {
                commercialList.RemoveAll(x => x.sessionId == sessionID);
            }
        }

        /// <summary>
        /// Add timestamp to the list for a session
        /// </summary>
        /// <param name="sessionId"></param>
        private void AddTimestamp(string sessionId)
        {
            if (Plugin.Instance.Configuration.RealTimeEnabled == true)
            {
                lock (timestamps)
                {
                    EdlTimestamp ts = new EdlTimestamp();
                    ts.sessionId = sessionId;
                    ts.timeLoaded = DateTimeOffset.Now.ToUnixTimeSeconds();
                    timestamps.Add(ts);
                }
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

            UserQuery userListQuery = new UserQuery();
            userListQuery.IsAdministrator = true;
            playstateRequest.ControllingUserId =  this.UserManager.GetUserList(userListQuery).FirstOrDefault().Id.ToString();
            playstateRequest.SeekPositionTicks = new long?(seek);
            SessionManager.SendPlaystateCommand((string)null, sessionID, playstateRequest, CancellationToken.None);
        }

        /// <summary>
        /// Send Commercial Skipped message to client
        /// </summary>
        /// <param name="session"></param>
        private async void SendMessageToClient(string sessionID, string duration)
        {
            try
            {
                string message = "Commercial Skipped";
                if (Plugin.Instance.Configuration.ShowTimeInMessage == true)
                    message = message + " {" + duration + " seconds)";

                MessageCommand messageCommand = new MessageCommand();
                messageCommand.Header = String.Empty;
                messageCommand.Text = Localize.localize(message, Locale);
                messageCommand.TimeoutMs = new long?(1000L);
                await SessionManager.SendMessageCommand(sessionID, sessionID, messageCommand, CancellationToken.None);
            }
            catch { }
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
