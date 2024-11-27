using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EFT_OverlayAPP
{
    internal class LogWatcher
    {
    }

    public class LogMonitor
    {
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

        private void OnLogFileChanged(object sender, FileSystemEventArgs e)
        {
            Task.Run(() => ReadNewLogEntries());
        }

        private void ReadNewLogEntries()
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
                        string newEntries = reader.ReadToEnd();
                        lastFileSize = fileInfo.Length;
                        OnLogChanged(newEntries);
                    }
                }
            }
            catch (IOException)
            {
                // Handle exceptions if necessary
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading log file: {ex.Message}");
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
        public event EventHandler MatchingStarted;
        public event EventHandler MatchingCancelled;
        public event EventHandler<RaidEventArgs> RaidStarted;
        public event EventHandler RaidEnded;
        public event EventHandler<RaidEventArgs> MapChanged;

        public void Parse(string logContent)
        {
            var lines = logContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ProcessLogLine(line);
            }
        }

        private void ProcessLogLine(string line)
        {
            if (line.Contains("Matching with group id"))
            {
                MatchingStarted?.Invoke(this, EventArgs.Empty);
            }
            else if (line.Contains("Network game matching cancelled") || line.Contains("Network game matching aborted"))
            {
                MatchingCancelled?.Invoke(this, EventArgs.Empty);
            }
            else if (line.Contains("TRACE-NetworkGameCreate profileStatus"))
            {
                string mapName = ExtractMapName(line);
                MapChanged?.Invoke(this, new RaidEventArgs(mapName));
            }
            else if (line.Contains("GameStarting"))
            {
                // Raid is about to start
                // You can raise an event here if needed
            }
            else if (line.Contains("GameStarted"))
            {
                string mapName = ExtractMapName(line);
                RaidStarted?.Invoke(this, new RaidEventArgs(mapName));
            }
            else if (line.Contains("OnGameSessionEnd"))
            {
                RaidEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        private string ExtractMapName(string line)
        {
            var match = Regex.Match(line, @"Location: (?<location>[^,]+),");
            if (match.Success)
            {
                string locationIdentifier = match.Groups["location"].Value;
                return MapLocationIdentifierToName(locationIdentifier);
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
    }

    public class GameState
    {
        public bool IsMatching { get; set; }
        public bool IsInRaid { get; set; }
        public string CurrentMap { get; set; }
    }

    public class GameStateManager
    {
        private readonly GameWatcher gameWatcher;
        private readonly LogParser logParser;
        private readonly GameState gameState;

        public GameState GameState => gameState;

        public event EventHandler GameStateChanged;

        public GameStateManager(string logsDirectory)
        {
            gameWatcher = new GameWatcher(logsDirectory);
            logParser = new LogParser();
            gameState = new GameState();

            gameWatcher.LogChanged += GameWatcher_LogChanged;
            SubscribeToParserEvents();
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
                OnGameStateChanged();
            };

            logParser.MatchingCancelled += (s, e) =>
            {
                gameState.IsMatching = false;
                OnGameStateChanged();
            };

            logParser.RaidStarted += (s, e) =>
            {
                gameState.IsInRaid = true;
                gameState.IsMatching = false;
                gameState.CurrentMap = e.MapName;
                OnGameStateChanged();
            };

            logParser.RaidEnded += (s, e) =>
            {
                gameState.IsInRaid = false;
                gameState.CurrentMap = null;
                OnGameStateChanged();
            };

            logParser.MapChanged += (s, e) =>
            {
                gameState.CurrentMap = e.MapName;
                OnGameStateChanged();
            };
        }

        protected virtual void OnGameStateChanged()
        {
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

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly GameStateManager gameStateManager;

        public bool IsMatching => gameStateManager.GameState.IsMatching;
        public bool IsInRaid => gameStateManager.GameState.IsInRaid;
        public string CurrentMap => gameStateManager.GameState.CurrentMap;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel(string logFilePath)
        {
            gameStateManager = new GameStateManager(logFilePath);
            gameStateManager.GameStateChanged += GameStateManager_GameStateChanged;
        }

        private void GameStateManager_GameStateChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsMatching));
            OnPropertyChanged(nameof(IsInRaid));
            OnPropertyChanged(nameof(CurrentMap));
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class GameWatcher
    {
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
            string latestLogFolder = GetLatestLogFolder();
            WatchLogsFolder(latestLogFolder);
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

        private void OnLogFileCreated(object sender, FileSystemEventArgs e)
        {
            // Handle new log files or folders
            if (Directory.Exists(e.FullPath) && e.Name.StartsWith("log_"))
            {
                StartMonitoringLatestLogFolder();
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
}
