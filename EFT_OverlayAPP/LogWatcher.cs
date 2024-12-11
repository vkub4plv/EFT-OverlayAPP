using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.Diagnostics;
using System.Text.Json;
using NLog;

namespace EFT_OverlayAPP
{
    internal class LogWatcher
    {
    }

    public class LogMonitor
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly string logFilePath;
        private long lastFileSize;
        private FileSystemWatcher fileWatcher;
        private bool isMonitoring;
        private readonly object readLock = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly TimeSpan pollingInterval = TimeSpan.FromSeconds(5); // Adjust as needed

        public event EventHandler<LogChangedEventArgs> LogChanged;
        public event EventHandler<ExceptionEventArgs> ExceptionOccurred;

        public LogMonitor(string logFilePath)
        {
            this.logFilePath = logFilePath;
        }

        public void Start()
        {
            if (isMonitoring)
                return;

            try
            {
                if (!File.Exists(logFilePath))
                {
                    logger.Warn($"Log file does not exist: {logFilePath}");
                }
                else
                {
                    lastFileSize = new FileInfo(logFilePath).Length;
                }

                // Initialize FileSystemWatcher
                fileWatcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(logFilePath),
                    Filter = Path.GetFileName(logFilePath),
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName
                };

                fileWatcher.Changed += OnLogFileChanged;
                fileWatcher.Renamed += OnLogFileRenamed;
                fileWatcher.Created += OnLogFileCreated;
                fileWatcher.Deleted += OnLogFileDeleted;
                fileWatcher.Error += OnFileWatcherError;

                fileWatcher.EnableRaisingEvents = true;

                // Start polling
                Task.Run(() => PollLogFileAsync(cancellationTokenSource.Token));

                isMonitoring = true;
                logger.Info($"Started monitoring log file: {logFilePath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to start LogMonitor for {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Starting LogMonitor"));
            }
        }

        public async void Start(bool processExistingEntries = false)
        {
            if (isMonitoring)
                return;

            try
            {
                if (!File.Exists(logFilePath))
                {
                    logger.Warn($"Log file does not exist: {logFilePath}");
                }
                else
                {
                    if (processExistingEntries)
                    {
                        // Read and process existing log entries
                        await ReadExistingEntriesAsync();
                    }
                    else
                    {
                        // Initialize lastFileSize to current size to skip existing entries
                        lock (readLock)
                        {
                            lastFileSize = new FileInfo(logFilePath).Length;
                        }
                    }
                }

                // Initialize FileSystemWatcher
                fileWatcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(logFilePath),
                    Filter = Path.GetFileName(logFilePath),
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName
                };

                fileWatcher.Changed += OnLogFileChanged;
                fileWatcher.Renamed += OnLogFileRenamed;
                fileWatcher.Created += OnLogFileCreated;
                fileWatcher.Deleted += OnLogFileDeleted;
                fileWatcher.Error += OnFileWatcherError;

                fileWatcher.EnableRaisingEvents = true;

                // Start polling
                Task.Run(() => PollLogFileAsync(cancellationTokenSource.Token));

                isMonitoring = true;
                logger.Info($"Started monitoring log file: {logFilePath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to start LogMonitor for {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Starting LogMonitor"));
            }
        }

        public void Stop()
        {
            if (!isMonitoring)
                return;

            try
            {
                cancellationTokenSource.Cancel();
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Dispose();
                fileWatcher = null;
                isMonitoring = false;
                logger.Info($"Stopped monitoring log file: {logFilePath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to stop LogMonitor for {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Stopping LogMonitor"));
            }
        }

        private async Task PollLogFileAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ReadNewLogEntriesAsync();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error during polling of {logFilePath}");
                    ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Polling LogMonitor"));
                }

                await Task.Delay(pollingInterval, token);
            }
        }

        private async void OnLogFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                await ReadNewLogEntriesAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in OnLogFileChanged");
            }
        }

        private void OnLogFileRenamed(object sender, RenamedEventArgs e)
        {
            logger.Info($"Log file renamed from {e.OldFullPath} to {e.FullPath}");
            // Reset read position if necessary
            lock (readLock)
            {
                lastFileSize = 0;
            }
        }

        private void OnLogFileCreated(object sender, FileSystemEventArgs e)
        {
            logger.Info($"Log file created: {e.FullPath}");
            // Reset read position
            lock (readLock)
            {
                lastFileSize = 0;
            }
        }

        private void OnLogFileDeleted(object sender, FileSystemEventArgs e)
        {
            logger.Warn($"Log file deleted: {e.FullPath}");
            // Handle deletion if necessary
        }

        private void OnFileWatcherError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.GetException(), $"FileSystemWatcher encountered an error for {logFilePath}");
            ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(e.GetException(), "FileSystemWatcher Error"));
            // Optionally, restart the watcher or switch to exclusive polling
        }

        private async Task ReadNewLogEntriesAsync()
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    logger.Warn($"Log file does not exist during read: {logFilePath}");
                    return;
                }

                var fileInfo = new FileInfo(logFilePath);
                long currentFileSize = fileInfo.Length;

                lock (readLock)
                {
                    if (currentFileSize < lastFileSize)
                    {
                        // Log rotation detected
                        logger.Info($"Log rotation detected for {logFilePath}. Resetting read position.");
                        lastFileSize = 0;
                    }
                }

                if (currentFileSize > lastFileSize)
                {
                    using (var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs))
                    {
                        fs.Seek(lastFileSize, SeekOrigin.Begin);
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            LogChanged?.Invoke(this, new LogChangedEventArgs(line));
                        }

                        lock (readLock)
                        {
                            lastFileSize = fs.Position;
                        }
                    }

                    logger.Debug($"Read new entries from log file: {logFilePath}");
                }
            }
            catch (IOException ex)
            {
                logger.Error(ex, $"IO Exception while reading log file: {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Reading LogMonitor"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error while reading log file: {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Reading LogMonitor"));
            }
        }

        // New method to read existing log entries
        public async Task ReadExistingEntriesAsync()
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    logger.Warn($"Log file does not exist: {logFilePath}");
                    return;
                }

                using (var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        // Emit each line as a LogChanged event
                        LogChanged?.Invoke(this, new LogChangedEventArgs(line));
                    }

                    // Update lastFileSize to current position
                    lock (readLock)
                    {
                        lastFileSize = fs.Position;
                    }
                }

                logger.Info($"Processed existing log entries for {logFilePath}");
            }
            catch (IOException ex)
            {
                logger.Error(ex, $"IO Exception while reading existing log file: {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Reading Existing LogMonitor"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error while reading existing log file: {logFilePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "Reading Existing LogMonitor"));
            }
        }
    }


    public class LogChangedEventArgs : EventArgs
    {
        public string NewEntries { get; }

        public LogChangedEventArgs(string newEntries)
        {
            NewEntries = newEntries;
        }
    }

    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Context { get; }

        public ExceptionEventArgs(Exception exception, string context)
        {
            Exception = exception;
            Context = context;
        }
    }

    public class LogParser
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public event EventHandler MatchingStarted;
        public event EventHandler MatchingCancelled;
        public event EventHandler RaidStarted;
        public event EventHandler RaidEnded;
        public event EventHandler<RaidEventArgs> MapChanged;
        public event EventHandler<SessionModeChangedEventArgs> SessionModeChanged;

        public void Parse(string logLine)
        {
            logger.Debug("Starting to parse log line");
            ProcessLogLine(logLine);
            logger.Debug("Finished parsing log line");
        }

        // Ensure you store the last map name when MapChanged is invoked
        private string lastMapName;

        private void ProcessLogLine(string line)
        {
            try
            {
                logger.Debug($"Processing log line: {line}");

                // Match matching started
                if (Regex.IsMatch(line, @"Matching with group id"))
                {
                    MatchingStarted?.Invoke(this, EventArgs.Empty);
                    logger.Info("Matching started");
                    return;
                }

                // Match matching cancelled or aborted
                if (Regex.IsMatch(line, @"Network game matching (cancelled|aborted)"))
                {
                    MatchingCancelled?.Invoke(this, EventArgs.Empty);
                    logger.Info("Matching cancelled");
                    return;
                }

                // Match map loaded with new pattern
                var mapLoadedMatch = Regex.Match(line, @"TRACE-NetworkGameCreate profileStatus: '(?<content>.*)'");
                if (mapLoadedMatch.Success)
                {
                    string content = mapLoadedMatch.Groups["content"].Value;
                    string mapName = ExtractMapNameFromProfileStatus(content);
                    if (!string.IsNullOrEmpty(mapName))
                    {
                        MapChanged?.Invoke(this, new RaidEventArgs(mapName));
                        lastMapName = mapName;
                        logger.Info($"Map changed to: {mapName}");
                    }
                    return;
                }

                // Match raid started
                if (Regex.IsMatch(line, @"\|Info\|application\|GameStarted:"))
                {
                    RaidStarted?.Invoke(this, EventArgs.Empty);
                    logger.Info("Raid started");
                    return;
                }

                // Match raid ended
                if (Regex.IsMatch(line, @"\|Info\|application\|(SelectProfile ProfileId|GameLeft|LeaveGame)"))
                {
                    RaidEnded?.Invoke(this, EventArgs.Empty);
                    logger.Info("Raid ended");
                    return;
                }

                // Detect session mode changes
                var sessionModeMatch = Regex.Match(line, @"\|Info\|application\|Session mode:\s*(?<mode>\w+)", RegexOptions.IgnoreCase);
                if (sessionModeMatch.Success)
                {
                    string sessionMode = sessionModeMatch.Groups["mode"].Value;
                    if (Enum.TryParse(sessionMode, true, out SessionMode mode))
                    {
                        SessionModeChanged?.Invoke(this, new SessionModeChangedEventArgs(mode));
                        logger.Info($"Session mode changed to: {mode}");
                    }
                    else
                    {
                        logger.Warn($"Unknown session mode: {sessionMode}");
                    }
                    return;
                }

                // Add other patterns as needed
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing log line");
            }
        }

        private string ExtractMapNameFromProfileStatus(string content)
        {
            try
            {
                // Extract the Location field from the content
                var locationMatch = Regex.Match(content, @"Location:\s*(?<location>[^,]+)");
                if (locationMatch.Success)
                {
                    string locationIdentifier = locationMatch.Groups["location"].Value.Trim();
                    return MapLocationIdentifierToName(locationIdentifier);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error extracting map name from profile status");
            }
            return "Unknown";
        }

        private string MapLocationIdentifierToName(string identifier)
        {
            return identifier.ToLower() switch
            {
                "factory4_day" => "Factory",
                "factory4_night" => "Factory (Night)",
                "bigmap" => "Customs",
                "woods" => "Woods",
                "shoreline" => "Shoreline",
                "interchange" => "Interchange",
                "rezervbase" => "Reserve",
                "laboratory" => "The Lab",
                "lighthouse" => "Lighthouse",
                "tarkovstreets" => "Streets of Tarkov",
                "sandbox" => "Ground Zero",
                "sandbox_high" => "Ground Zero 21+",
                _ => identifier
            };
        }
    }

    public class GameState : INotifyPropertyChanged
    {
        private string currentMap;
        public string CurrentMap
        {
            get => currentMap;
            set
            {
                if (currentMap != value)
                {
                    currentMap = value;
                    OnPropertyChanged(nameof(CurrentMap));
                }
            }
        }

        private bool isInRaid;
        public bool IsInRaid
        {
            get => isInRaid;
            set
            {
                if (isInRaid != value)
                {
                    isInRaid = value;
                    OnPropertyChanged(nameof(IsInRaid));
                }
            }
        }

        private bool isMatching;
        public bool IsMatching
        {
            get => isMatching;
            set
            {
                if (isMatching != value)
                {
                    isMatching = value;
                    OnPropertyChanged(nameof(IsMatching));
                }
            }
        }

        private string overlayUrl;
        public string OverlayUrl
        {
            get => overlayUrl;
            set
            {
                if (overlayUrl != value)
                {
                    overlayUrl = value;
                    OnPropertyChanged(nameof(OverlayUrl));
                }
            }
        }

        private SessionMode sessionMode;
        public SessionMode SessionMode
        {
            get => sessionMode;
            set
            {
                if (sessionMode != value)
                {
                    sessionMode = value;
                    OnPropertyChanged(nameof(SessionMode));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class GameStateManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly GameWatcher gameWatcher;
        private readonly LogParser logParser;
        private readonly GameState gameState;
        private string lastOverlayMapName = null; // Add this field

        public GameState GameState => gameState;

        public event EventHandler GameStateChanged;

        public GameStateManager(string logsDirectory)
        {
            gameWatcher = new GameWatcher(logsDirectory);
            logParser = new LogParser();
            gameState = new GameState();

            gameWatcher.LogChanged += GameWatcher_LogChanged;
            gameWatcher.ExceptionOccurred += GameWatcher_ExceptionOccurred;
            logParser.MatchingStarted += LogParser_MatchingStarted;
            logParser.MatchingCancelled += LogParser_MatchingCancelled;
            logParser.RaidStarted += LogParser_RaidStarted;
            logParser.RaidEnded += LogParser_RaidEnded;
            logParser.MapChanged += LogParser_MapChanged;
            logParser.SessionModeChanged += LogParser_SessionModeChanged;

            // Initialize the overlay URL
            UpdateOverlayUrl();

            // Start monitoring with existing entries processing
            gameWatcher.StartAllMonitors();
        }

        private void GameWatcher_LogChanged(object sender, LogChangedEventArgs e)
        {
            logParser.Parse(e.NewEntries);
        }

        private void GameWatcher_ExceptionOccurred(object sender, ExceptionEventArgs e)
        {
            // Handle exceptions from LogWatcher
            logger.Error(e.Exception, $"Exception in GameWatcher: {e.Context}");
            // Optionally, propagate the exception or handle it accordingly
        }

        private void LogParser_MatchingStarted(object sender, EventArgs e)
        {
            gameState.IsMatching = true;
            gameState.IsInRaid = false;
            gameState.CurrentMap = null;
            logger.Info("Matching started");
            OnGameStateChanged();
        }

        private void LogParser_MatchingCancelled(object sender, EventArgs e)
        {
            gameState.IsMatching = false;
            logger.Info("Matching cancelled");
            OnGameStateChanged();
        }

        private void LogParser_RaidStarted(object sender, EventArgs e)
        {
            gameState.IsInRaid = true;
            gameState.IsMatching = false;
            // Do not update CurrentMap here; it should be updated via MapChanged event
            logger.Info("Raid started");
            OnGameStateChanged();
        }

        private void LogParser_RaidEnded(object sender, EventArgs e)
        {
            gameState.IsInRaid = false;
            gameState.CurrentMap = null;
            logger.Info("Raid ended");
            OnGameStateChanged();
        }

        private void LogParser_MapChanged(object sender, RaidEventArgs e)
        {
            gameState.CurrentMap = e.MapName;
            logger.Info($"Map changed to: {e.MapName}");
            UpdateOverlayUrl();
            OnGameStateChanged();
        }

        private void LogParser_SessionModeChanged(object sender, SessionModeChangedEventArgs e)
        {
            gameState.SessionMode = e.SessionMode;
            logger.Info($"Session mode changed to: {e.SessionMode}");
            OnGameStateChanged();
        }

        private void UpdateOverlayUrl()
        {
            if (!string.IsNullOrEmpty(gameState.CurrentMap))
            {
                if (gameState.CurrentMap != lastOverlayMapName)
                {
                    // Map has changed, update the overlay URL
                    string mapSegment = MapNameToMapGenieSegment(gameState.CurrentMap);
                    if (!string.IsNullOrEmpty(mapSegment))
                    {
                        gameState.OverlayUrl = $"https://mapgenie.io/tarkov/maps/{mapSegment}";
                    }
                    else
                    {
                        // Map not recognized, set to default URL
                        gameState.OverlayUrl = "https://mapgenie.io/tarkov";
                    }

                    // Update lastOverlayMapName
                    lastOverlayMapName = gameState.CurrentMap;
                }
                else
                {
                    // Map hasn't changed, do not update OverlayUrl
                    logger.Debug("Map hasn't changed, not updating OverlayUrl");
                }
            }
            else
            {
                // No current map, set overlay to default URL
                if (lastOverlayMapName != null)
                {
                    gameState.OverlayUrl = "https://mapgenie.io/tarkov";
                    lastOverlayMapName = null;
                }
            }
        }

        private string MapNameToMapGenieSegment(string mapName)
        {
            switch (mapName.ToLower())
            {
                case "factory":
                case "factory (night)":
                    return "factory";
                case "customs":
                    return "customs";
                case "woods":
                    return "woods";
                case "interchange":
                    return "interchange";
                case "reserve":
                    return "reserve";
                case "shoreline":
                    return "shoreline";
                case "the lab":
                case "lab":
                    return "lab";
                case "lighthouse":
                    return "lighthouse";
                case "streets of tarkov":
                case "streets":
                    return "streets";
                case "ground zero":
                case "ground zero 21+":
                    return "ground-zero";
                default:
                    return null;
            }
        }

        protected virtual void OnGameStateChanged()
        {
            logger.Debug($"GameState changed: IsInRaid={gameState.IsInRaid}, IsMatching={gameState.IsMatching}, CurrentMap={gameState.CurrentMap}, SessionMode={gameState.SessionMode}, OverlayUrl={gameState.OverlayUrl}");
            GameStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Start()
        {
            gameWatcher.StartAllMonitors();
        }

        public void Stop()
        {
            gameWatcher.StopAllMonitors();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public class RaidEventArgs : EventArgs
    {
        public string MapName { get; }

        public RaidEventArgs(string mapName)
        {
            MapName = mapName;
        }
    }

    public class GameWatcher
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private string logsDirectory;
        private Dictionary<GameLogType, LogMonitor> monitors = new();
        private FileSystemWatcher directoryWatcher;

        public event EventHandler<LogChangedEventArgs> LogChanged;
        public event EventHandler<ExceptionEventArgs> ExceptionOccurred;

        public GameWatcher(string logsDirectory)
        {
            this.logsDirectory = logsDirectory;
            // Initialize FileSystemWatcher to monitor the logs directory for new log folders
            directoryWatcher = new FileSystemWatcher
            {
                Path = logsDirectory,
                Filter = "log_*",
                NotifyFilter = NotifyFilters.DirectoryName
            };

            directoryWatcher.Created += OnLogFolderCreated;
            directoryWatcher.Deleted += OnLogFolderDeleted;
            directoryWatcher.Renamed += OnLogFolderRenamed;
            directoryWatcher.Error += OnDirectoryWatcherError;
            directoryWatcher.EnableRaisingEvents = true;

            // Start monitoring the latest log folder
            StartMonitoringLatestLogFolder(processExistingEntries: true);
        }

        public void StartAllMonitors()
        {
            StartMonitoringLatestLogFolder(processExistingEntries: true);
        }

        private void StartMonitoringLatestLogFolder(bool processExistingEntries)
        {
            try
            {
                string latestLogFolder = GetLatestLogFolder();
                WatchLogsFolder(latestLogFolder, processExistingEntries);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error starting monitoring of the latest log folder.");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "StartMonitoringLatestLogFolder"));
            }
        }

        private string GetLatestLogFolder()
        {
            var logFolders = Directory.GetDirectories(logsDirectory, "log_*");
            if (logFolders.Length == 0)
            {
                throw new DirectoryNotFoundException($"No log folders found in {logsDirectory}");
            }

            var latestLogFolder = new DirectoryInfo(logFolders[0]);

            foreach (var folder in logFolders)
            {
                var dirInfo = new DirectoryInfo(folder);
                if (dirInfo.CreationTime > latestLogFolder.CreationTime)
                {
                    latestLogFolder = dirInfo;
                }
            }

            logger.Info($"Latest log folder identified: {latestLogFolder.FullName}");
            return latestLogFolder.FullName;
        }

        private void WatchLogsFolder(string folderPath, bool processExistingEntries)
        {
            try
            {
                var logFiles = Directory.GetFiles(folderPath);

                foreach (var file in logFiles)
                {
                    if (file.EndsWith("application.log", StringComparison.OrdinalIgnoreCase))
                    {
                        StartNewMonitor(file, GameLogType.Application, processExistingEntries);
                    }
                    else if (file.EndsWith("notifications.log", StringComparison.OrdinalIgnoreCase))
                    {
                        StartNewMonitor(file, GameLogType.Notifications, processExistingEntries);
                    }
                    // Add other log types if necessary
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error watching logs folder: {folderPath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "WatchLogsFolder"));
            }
        }

        private void StartNewMonitor(string filePath, GameLogType logType, bool processExistingEntries)
        {
            try
            {
                if (monitors.ContainsKey(logType))
                {
                    monitors[logType].Stop();
                    monitors.Remove(logType);
                }

                var monitor = new LogMonitor(filePath);
                monitor.LogChanged += (s, e) => LogChanged?.Invoke(this, e);
                monitor.ExceptionOccurred += (s, e) => ExceptionOccurred?.Invoke(this, e);
                monitor.Start(processExistingEntries); // Pass the flag to process existing entries
                monitors[logType] = monitor;

                logger.Info($"Started LogMonitor for {logType}: {filePath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to start LogMonitor for {filePath}");
                ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(ex, "StartNewMonitor"));
            }
        }

        private void OnLogFolderCreated(object sender, FileSystemEventArgs e)
        {
            logger.Info($"New log folder created: {e.FullPath}");
            WatchLogsFolder(e.FullPath, processExistingEntries: true);
        }

        private void OnLogFolderDeleted(object sender, FileSystemEventArgs e)
        {
            logger.Warn($"Log folder deleted: {e.FullPath}");
            // Optionally, stop monitors related to this folder
        }

        private void OnLogFolderRenamed(object sender, RenamedEventArgs e)
        {
            logger.Info($"Log folder renamed from {e.OldFullPath} to {e.FullPath}");
            // Handle renaming if necessary
        }

        private void OnDirectoryWatcherError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.GetException(), $"DirectoryWatcher encountered an error for {logsDirectory}");
            ExceptionOccurred?.Invoke(this, new ExceptionEventArgs(e.GetException(), "DirectoryWatcher Error"));
            // Optionally, restart the watcher or switch to exclusive polling
        }

        public void StopAllMonitors()
        {
            foreach (var monitor in monitors.Values)
            {
                monitor.Stop();
            }
            monitors.Clear();
            logger.Info("All LogMonitors have been stopped.");
        }
    }

    public enum GameLogType
    {
        Application,
        Notifications,
        // Add other log types if needed
    }

    public class SessionModeChangedEventArgs : EventArgs
    {
        public SessionMode SessionMode { get; }

        public SessionModeChangedEventArgs(SessionMode sessionMode)
        {
            SessionMode = sessionMode;
        }
    }

    public enum SessionMode
    {
        Regular,
        Pve
    }
}
