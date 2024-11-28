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

        public event EventHandler<LogChangedEventArgs> LogChanged;

        public LogMonitor(string logFilePath)
        {
            this.logFilePath = logFilePath;
        }

        public void Start()
        {
            if (isMonitoring)
                return;

            lastFileSize = new FileInfo(logFilePath).Length;

            fileWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(logFilePath),
                Filter = Path.GetFileName(logFilePath),
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite
            };

            fileWatcher.Changed += OnLogFileChanged;
            fileWatcher.EnableRaisingEvents = true;

            isMonitoring = true;
        }

        public void Stop()
        {
            if (!isMonitoring)
                return;

            fileWatcher.EnableRaisingEvents = false;
            fileWatcher.Dispose();
            fileWatcher = null;

            isMonitoring = false;
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

        private async Task ReadNewLogEntriesAsync()
        {
            try
            {
                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length == lastFileSize)
                    return;

                using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(lastFileSize, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        string newEntries = await reader.ReadToEndAsync();
                        lastFileSize = fileInfo.Length;
                        OnLogChanged(newEntries);
                    }
                }
            }
            catch (IOException ex)
            {
                // Handle exceptions
                logger.Error(ex, "IO Exception");
            }
            catch (Exception ex)
            {
                // Handle exceptions
                logger.Error(ex, "Error reading log file");
            }
        }


        protected virtual void OnLogChanged(string newEntries)
        {
            LogChanged?.Invoke(this, new LogChangedEventArgs(newEntries));
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

    public class LogParser
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public event EventHandler MatchingStarted;
        public event EventHandler MatchingCancelled;
        public event EventHandler RaidStarted;
        public event EventHandler RaidEnded;
        public event EventHandler<RaidEventArgs> MapChanged;

        public void Parse(string logContent)
        {
            logger.Info("Starting to parse log content");
            var lines = logContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ProcessLogLine(line);
            }
            logger.Info("Finished parsing log content");
        }

        // Ensure you store the last map name when MapChanged is invoked
        private string lastMapName;

        private void ProcessLogLine(string line)
        {
            try
            {
                logger.Info($"Processing log line: {line}");

                // Match matching started
                if (Regex.IsMatch(line, @"Matching with group id"))
                {
                    MatchingStarted?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // Match matching cancelled or aborted
                if (Regex.IsMatch(line, @"Network game matching (cancelled|aborted)"))
                {
                    MatchingCancelled?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // Match map loaded with new pattern
                var mapLoadedMatch = Regex.Match(line, @"TRACE-NetworkGameCreate profileStatus: '(?<content>.*)'");
                if (mapLoadedMatch.Success)
                {
                    string content = mapLoadedMatch.Groups["content"].Value;
                    string mapName = ExtractMapNameFromProfileStatus(content);
                    MapChanged?.Invoke(this, new RaidEventArgs(mapName));
                    logger.Info($"Map changed to: {mapName}");
                    return;
                }

                // Match raid started with new pattern
                if (Regex.IsMatch(line, @"\|Info\|application\|GameStarted:"))
                {
                    logger.Info($"RaidStarted event: lastMapName = {lastMapName}");
                    string mapName = lastMapName ?? "Unknown";
                    RaidStarted?.Invoke(this, new RaidEventArgs(mapName));
                    logger.Info($"RaidStarted event invoked with map: {mapName}");
                    return;
                }

                // Match raid ended
                if (Regex.IsMatch(line, @"\|Info\|application\|(SelectProfile ProfileId|GameLeft|LeaveGame)"))
                {
                    RaidEnded?.Invoke(this, EventArgs.Empty);
                    logger.Info("RaidEnded event invoked due to raid end indicator");
                    return;
                }

                // Match in-raid death
                if (Regex.IsMatch(line, @"Player died"))
                {
                    // Handle player death
                    // You can raise an event or update the state accordingly
                }

                // Match extraction
                if (Regex.IsMatch(line, @"Player extracted"))
                {
                    // Handle player extraction
                    // You can raise an event or update the state accordingly
                }

                if (mapLoadedMatch.Success)
                {
                    string content = mapLoadedMatch.Groups["content"].Value;
                    string mapName = ExtractMapNameFromProfileStatus(content);
                    lastMapName = mapName; // Store the map name
                    MapChanged?.Invoke(this, new RaidEventArgs(mapName));
                    logger.Info($"Map changed to: {mapName}");
                    return;
                }

                // Add other patterns as needed
            }
            catch (Exception ex)
            {
                // Log exception
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
            return identifier switch
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
                _ => identifier
            };
        }

        private string MapLocationIdentifierToMapGenieSegment(string locationIdentifier)
        {
            switch (locationIdentifier)
            {
                case "factory4_day":
                case "factory4_night":
                    return "factory";
                case "bigmap":
                    return "customs";
                case "woods":
                    return "woods";
                case "interchange":
                    return "interchange";
                case "rezervbase":
                    return "reserve";
                case "shoreline":
                    return "shoreline";
                case "laboratory":
                    return "lab";
                case "tarkovstreets":
                    return "streets";
                case "lighthouse":
                    return "lighthouse";
                case "suburbs":
                    return "ground-zero";
                default:
                    return null;
            }
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
            SubscribeToParserEvents();

            // Initialize the overlay URL
            UpdateOverlayUrl();
        }

        private void GameWatcher_LogChanged(object sender, LogChangedEventArgs e)
        {
            logParser.Parse(e.NewEntries);
        }

        private void SubscribeToParserEvents()
        {
            logParser.MatchingStarted += (s, e) =>
            {
                gameState.IsMatching = true;
                gameState.IsInRaid = false;
                gameState.CurrentMap = null;
                logger.Info("Matching started");
                OnGameStateChanged();
            };

            logParser.MatchingCancelled += (s, e) =>
            {
                gameState.IsMatching = false;
                logger.Info("Matching cancelled");
                OnGameStateChanged();
            };

            logParser.RaidStarted += (s, e) =>
            {
                gameState.IsInRaid = true;
                gameState.IsMatching = false;
                // Do not update CurrentMap here
                logger.Info($"Raid started on map: {gameState.CurrentMap}");
                OnGameStateChanged();
            };


            logParser.RaidEnded += (s, e) =>
            {
                gameState.IsInRaid = false;
                gameState.CurrentMap = null;
                logger.Info("Raid ended");
                OnGameStateChanged();
            };

            logParser.MapChanged += (s, e) =>
            {
                gameState.CurrentMap = e.MapName;
                logger.Info($"Map changed to: {e.MapName}");
                OnGameStateChanged();
            };
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
                    logger.Info("Map hasn't changed, not updating OverlayUrl");
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
                    return "ground-zero";
                default:
                    return null;
            }
        }

        protected virtual void OnGameStateChanged()
        {
            UpdateOverlayUrl();
            logger.Info($"GameState changed: IsInRaid={gameState.IsInRaid}, IsMatching={gameState.IsMatching}, CurrentMap={gameState.CurrentMap}, OverlayUrl={gameState.OverlayUrl}");
            GameStateChanged?.Invoke(this, EventArgs.Empty);
        }
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

        public GameWatcher(string logsDirectory)
        {
            this.logsDirectory = logsDirectory;
            Initialize();
        }

        private void Initialize()
        {
            directoryWatcher = new FileSystemWatcher
            {
                Path = logsDirectory,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
                IncludeSubdirectories = true
            };

            directoryWatcher.Created += OnLogFileCreated;
            directoryWatcher.Deleted += OnLogFileDeleted;
            directoryWatcher.EnableRaisingEvents = true;

            StartMonitoringLatestLogFolder();
        }

        private void StartMonitoringLatestLogFolder()
        {
            try
            {
                string latestLogFolder = GetLatestLogFolder();
                WatchLogsFolder(latestLogFolder);
            }
            catch (Exception ex)
            {
                // Handle exceptions
                logger.Error(ex, "Error starting monitoring}");
            }
        }

        private string GetLatestLogFolder()
        {
            var logFolders = Directory.GetDirectories(logsDirectory, "log_*");

            var latestLogFolder = logFolders
                .Select(dir => new DirectoryInfo(dir))
                .OrderByDescending(dir => dir.CreationTime)
                .FirstOrDefault();

            if (latestLogFolder != null)
            {
                return latestLogFolder.FullName;
            }

            throw new DirectoryNotFoundException("No log folders found.");
        }

        private void WatchLogsFolder(string folderPath)
        {
            var logFiles = Directory.GetFiles(folderPath);

            foreach (var file in logFiles)
            {
                if (file.EndsWith("application.log"))
                {
                    StartNewMonitor(file, GameLogType.Application);
                }
                else if (file.EndsWith("notifications.log"))
                {
                    StartNewMonitor(file, GameLogType.Notifications);
                }
                // Add other log types if needed
            }
        }

        private void StartNewMonitor(string filePath, GameLogType logType)
        {
            if (monitors.ContainsKey(logType))
            {
                monitors[logType].Stop();
                monitors.Remove(logType);
            }

            var monitor = new LogMonitor(filePath);
            monitor.LogChanged += (s, e) => LogChanged?.Invoke(this, e);
            monitor.Start();
            monitors[logType] = monitor;
        }

        private async void OnLogFileCreated(object sender, FileSystemEventArgs e)
        {
            // Handle new log files or folders
            if (Directory.Exists(e.FullPath) && e.Name.StartsWith("log_"))
            {
                // Start monitoring latest log folder asynchronously
                await Task.Run(() => StartMonitoringLatestLogFolder());
            }
        }

        private void OnLogFileDeleted(object sender, FileSystemEventArgs e)
        {
            // Handle deletion of log files or folders
            // You may need to adjust monitors accordingly
        }
    }

    public enum GameLogType
    {
        Application,
        Notifications,
        // Add other log types if needed
    }

    public static class GamePathHelper
    {
        public static string GetLogsDirectory()
        {
            string gameInstallPath = GetGameInstallPath();
            if (string.IsNullOrEmpty(gameInstallPath))
            {
                throw new Exception("Game installation path not found in the registry.");
            }

            string logsDirectory = Path.Combine(gameInstallPath, "Logs");
            if (Directory.Exists(logsDirectory))
            {
                return logsDirectory;
            }
            else
            {
                throw new Exception("Logs directory not found in the game installation path.");
            }
        }

        public static string GetGameInstallPath()
        {
            string gamePath = Properties.Settings.Default.GameInstallPath;
            if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
            {
                return gamePath;
            }
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";

            // Attempt to open the 64-bit registry key
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(uninstallKey))
            {
                if (key != null)
                {
                    object path = key.GetValue("InstallLocation");
                    if (path != null)
                    {
                        gamePath = path.ToString();
                    }
                }
            }

            // If not found, attempt to open the 32-bit registry key
            if (string.IsNullOrEmpty(gamePath))
            {
                uninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(uninstallKey))
                {
                    if (key != null)
                    {
                        object path = key.GetValue("InstallLocation");
                        if (path != null)
                        {
                            gamePath = path.ToString();
                        }
                    }
                }
            }

            // If still not found, prompt the user
            if (string.IsNullOrEmpty(gamePath))
            {
                // Prompt the user to select the game installation directory
                string selectedPath = PromptForGamePath();
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    gamePath = selectedPath;
                }
                else
                {
                    throw new Exception("Game installation path not found.");
                }
            }

            if (string.IsNullOrEmpty(gamePath))
            {
                gamePath = PromptForGamePath();
                if (!string.IsNullOrEmpty(gamePath))
                {
                    Properties.Settings.Default.GameInstallPath = gamePath;
                    Properties.Settings.Default.Save();
                }
            }

            return gamePath;
        }

        private static string PromptForGamePath()
        {
            string selectedPath = null;
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Escape from Tarkov installation directory not found automatically. Please select it manually."
            };
            bool? result = dialog.ShowDialog();

            if (result == true && Directory.Exists(dialog.SelectedPath))
            {
                selectedPath = dialog.SelectedPath;
            }

            return selectedPath;
        }
    }
}
