namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Watcher
    {
        private FileSystemWatcher? FileSystemWatcher { get; set; }
        public event EventHandler<ChangedEventArgs>? OnChange;
        private AppConfig AppConfig { get; }

        public Watcher(AppConfig appConfig)
        {
            this.AppConfig = appConfig;

            this.CreateOrUpdateWatcher();
            this.AppConfig.OnChange += this.AppConfig_OnChange;

        }

        private void AppConfig_OnChange(Object? sender, AppConfig.PropertyUpdatedEventArgs e)
        {
            if(e.PropertyName == nameof(this.AppConfig.SolutionPath))
            {
                this.CreateOrUpdateWatcher();
            }
            else if(e.PropertyName == nameof(this.AppConfig.DestinationPath))
            {
                if (String.IsNullOrWhiteSpace(this.AppConfig.DestinationPath))
                {
                    throw new ArgumentNullException(nameof(this.AppConfig.DestinationPath));
                }
                if (!Directory.Exists(this.AppConfig.DestinationPath))
                {
                    throw new DirectoryNotFoundException($"Path '{this.AppConfig.DestinationPath}' does not exist.");
                }
            }
        }

        private void CreateOrUpdateWatcher()
        {
            if (String.IsNullOrEmpty(this.AppConfig.SolutionPath))
            {
                throw new ArgumentNullException(nameof(this.AppConfig.SolutionPath));
            }
            if(!Directory.Exists(this.AppConfig.SolutionPath))
            {
                throw new DirectoryNotFoundException($"Path '{this.AppConfig.SolutionPath}' does not exist.");
            }
            if(String.IsNullOrWhiteSpace(this.AppConfig.DestinationPath))
            {
                throw new ArgumentNullException(nameof(this.AppConfig.DestinationPath));
            }
            if(!Directory.Exists(this.AppConfig.DestinationPath))
            {
                throw new DirectoryNotFoundException($"Path '{this.AppConfig.DestinationPath}' does not exist.");
            }

            this.FileSystemWatcher?.Dispose();
            this.FileSystemWatcher = new FileSystemWatcher(this.AppConfig.SolutionPath, "*.cshtml")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };
            this.FileSystemWatcher.Changed += FileSystemWatcher_Changed;
            this.FileSystemWatcher.Created += FileSystemWatcher_Changed;
            this.FileSystemWatcher.Deleted += FileSystemWatcher_Changed;
            this.FileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            this.FileSystemWatcher.Error += FileSystemWatcher_Error;

        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            var eventArgs = new ChangedEventArgs()
            {
                Title = "Error",
                Text = e.GetException().Message,
                ChangeEventType = WatcherChangeTypes.All

            };
            this.OnChange?.Invoke(this, eventArgs);
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (this.AppConfig.Paused)
            {
                return;
            }
            if (e.FullPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                var mappedPath = this.MapDestinationPath(e.FullPath);
                var eventArgs = new ChangedEventArgs();

                if (!String.IsNullOrWhiteSpace(mappedPath))
                {
                    this.HandleUpdate(e.FullPath, mappedPath);

                    if (e.OldFullPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                    {
                        // True rename
                        var mappedOldPath = this.MapDestinationPath(e.OldFullPath);
                        if (!String.IsNullOrWhiteSpace(mappedOldPath) && File.Exists(mappedOldPath))
                        {
                            this.HandleDeletion(mappedOldPath);
                        }
                        eventArgs.Title = e.ChangeType.ToString();
                        eventArgs.Text = $"{mappedOldPath} -> {mappedPath}";
                        eventArgs.ChangeEventType = e.ChangeType;

                    }
                    else
                    {
                        eventArgs.ChangeEventType = WatcherChangeTypes.Changed;
                        eventArgs.Title = eventArgs.ChangeEventType.ToString();
                        eventArgs.Text = mappedPath;                        
                    }
                   
                }

                this.OnChange?.Invoke(this, eventArgs);
            }
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if(this.AppConfig.Paused)
            {
                return;
            }
            var mappedPath = this.MapDestinationPath(e.FullPath);
            var eventArgs = new ChangedEventArgs()
            {
                Title = e.ChangeType.ToString(),
                Text = mappedPath,
                ChangeEventType = e.ChangeType
            };            
            if (!String.IsNullOrWhiteSpace(mappedPath))
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if(File.Exists(mappedPath))
                    {
                        this.HandleDeletion(mappedPath);
                    }
                }
                else
                {
                   this.HandleUpdate(e.FullPath, mappedPath);
                }
            }
            else
            {
                eventArgs.Title = "Error";
                eventArgs.Text = $"Failed to map path: '{e.FullPath}' -> ${this.AppConfig.DestinationPath}";
                eventArgs.ChangeEventType = WatcherChangeTypes.All;
            }

            this.OnChange?.Invoke(this, eventArgs);
        }

        private String MapDestinationPath(String source)
        {
            var relativePath = source[source.IndexOf($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}")..];
            return Path.Join(this.AppConfig.DestinationPath, relativePath);            
        }

        private void HandleUpdate(String source, String destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }

        private void HandleDeletion(String fileToDelete)
        {
            File.Delete(fileToDelete);
            var destinationDirectory = Path.GetDirectoryName(fileToDelete);
            if (!String.IsNullOrWhiteSpace(destinationDirectory) && !destinationDirectory.EndsWith($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}") && !destinationDirectory.EndsWith($"{Path.DirectorySeparatorChar}Views"))
            {
                if (!Directory.GetFiles(destinationDirectory).Any())
                {
                    Directory.Delete(destinationDirectory, false);
                }
            }
        }



        public class ChangedEventArgs : EventArgs
        {
            public String Title { get; set; } = String.Empty;
            public String Text { get; set; } = String.Empty;
            public WatcherChangeTypes ChangeEventType { get; set; } = WatcherChangeTypes.Changed;
        }

    }
}
