namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Collections.Concurrent;

    public class Watcher
    {
        private FileSystemWatcher? FileSystemWatcher { get; set; }
        public event EventHandler<ChangedEventArgs>? OnChange;
        private AppConfig AppConfig { get; }
        private List<String> FileHandles { get; }

        public Watcher(AppConfig appConfig)
        {
            this.AppConfig = appConfig;
            this.FileHandles = new List<String>();

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
                IncludeSubdirectories = true,
                InternalBufferSize = 65536
            };
            this.FileSystemWatcher.Changed += FileSystemWatcher_Changed;
            this.FileSystemWatcher.Created += FileSystemWatcher_Changed;
            this.FileSystemWatcher.Deleted += FileSystemWatcher_Changed;
            this.FileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            this.FileSystemWatcher.Error += FileSystemWatcher_Error;

        }

        private Boolean ReserveFileHandle(String path)
        {
            lock (this.FileHandles)
            {
                if (this.FileHandles.Contains(path))
                {
                    return false;
                }
                this.FileHandles.Add(path);
                return true;
            }
        }

        private void ReleaseFileHandle(String path)
        {
            lock (this.FileHandles)
            {
                if (this.FileHandles.Contains(path))
                {
                    this.FileHandles.Remove(path);
                }
            }
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            var eventArgs = new ChangedEventArgs()
            {
                Title = "Error",
                Text = e.GetException().Message,
                ChangeEventType = WatcherChangeTypes.All

            };
            Utils.WriteToTrace(e.GetException().Message, this, true);
            this.OnChange?.Invoke(this, eventArgs);
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (this.AppConfig.Paused)
            {
                return;
            }
            // Push to background thread in an attempt at preventing the limited buffersize of the FileSystemWatcher class from overflowing.
            Task.Run(async () =>
            {
                if (e.FullPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    var eventArgs = new ChangedEventArgs();
                    if (this.MapDestinationPath(e.FullPath, out String mappedPath))
                    {
                        try
                        {
                            if (this.ReserveFileHandle(mappedPath))
                            {
                                try
                                {
                                    this.HandleUpdate(e.FullPath, mappedPath);
                                    Utils.WriteToTrace($"Updated: {mappedPath}", this);
                                }
                                catch (IOException)
                                {
                                    try
                                    {
                                        await Task.Delay(this.AppConfig.RetryWait);
                                        this.HandleUpdate(e.FullPath, mappedPath);
                                        Utils.WriteToTrace($"Updated 2nd attempt: {mappedPath}", this);
                                    }
                                    catch (IOException ex)
                                    {
                                        eventArgs.Title = ex.Message;
                                        eventArgs.Text = $"Failed to update after 2nd try {mappedPath}";
                                        eventArgs.ChangeEventType = WatcherChangeTypes.All;
                                        Utils.WriteToTrace($"Failed to update: {mappedPath}{Environment.NewLine}{ex.Message}", this, true);
                                    }
                                }

                                if (e.OldFullPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                                {
                                    // True rename
                                    if (this.MapDestinationPath(e.OldFullPath, out String mappedOldPath))
                                    {

                                        eventArgs.Title = e.ChangeType.ToString();
                                        eventArgs.Text = $"{mappedOldPath} -> {mappedPath}";
                                        eventArgs.ChangeEventType = e.ChangeType;

                                        if (!String.IsNullOrWhiteSpace(mappedOldPath) && File.Exists(mappedOldPath))
                                        {
                                            try
                                            {
                                                this.HandleDeletion(mappedPath);
                                                Utils.WriteToTrace($"Deleted: {mappedPath}", this);
                                            }
                                            catch (IOException)
                                            {
                                                try
                                                {
                                                    await Task.Delay(this.AppConfig.RetryWait);
                                                    this.HandleDeletion(mappedPath);
                                                    Utils.WriteToTrace($"Deleted 2nd attempt: {mappedPath}", this);
                                                }
                                                catch (IOException ex)
                                                {
                                                    eventArgs.Title = ex.Message;
                                                    eventArgs.Text = $"Failed to delete after 2nd try {mappedPath}";
                                                    eventArgs.ChangeEventType = WatcherChangeTypes.All;
                                                    Utils.WriteToTrace($"Failed to delete: {mappedPath}{Environment.NewLine}{ex.Message}", this, true);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            eventArgs.Title = "Exception thrown";
                            eventArgs.Text = ex.Message;
                            eventArgs.ChangeEventType = WatcherChangeTypes.All;
                            Utils.WriteToTrace(ex.Message, this, true);
                        }
                        finally
                        {
                            this.ReleaseFileHandle(mappedPath);
                        }
                    }
                    else
                    {
                        eventArgs.Title = "Error";
                        eventArgs.Text = $"Failed to map path: '{e.FullPath}' -> {this.AppConfig.DestinationPath}";
                        eventArgs.ChangeEventType = WatcherChangeTypes.All;
                        Utils.WriteToTrace($"Failed to map path: '{e.FullPath}' -> {this.AppConfig.DestinationPath}", this, true);
                    }


                    this.OnChange?.Invoke(this, eventArgs);
                }
            });
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if(this.AppConfig.Paused)
            {
                return;
            }
            // Push to background thread in an attempt at preventing the limited buffersize of the FileSystemWatcher class from overflowing.
            Task.Run(async () =>
            {
                var eventArgs = new ChangedEventArgs();
                if (this.MapDestinationPath(e.FullPath, out String mappedPath))
                {

                    eventArgs.Title = e.ChangeType.ToString();
                    eventArgs.Text = mappedPath;
                    eventArgs.ChangeEventType = e.ChangeType;
                    try
                    {
                            if (this.ReserveFileHandle(mappedPath))
                            {
                                if (e.ChangeType == WatcherChangeTypes.Deleted)
                                {
                                    if (File.Exists(mappedPath))
                                    {
                                        try
                                        {
                                            this.HandleDeletion(mappedPath);
                                            Utils.WriteToTrace($"{e.ChangeType}: {mappedPath}", this);
                                        }
                                        catch (IOException)
                                        {
                                            try
                                            {
                                                await Task.Delay(this.AppConfig.RetryWait);
                                                this.HandleDeletion(mappedPath);
                                                Utils.WriteToTrace($"{e.ChangeType} 2nd attempt: {mappedPath}", this);
                                            }
                                            catch (IOException ex)
                                            {
                                                eventArgs.Title = ex.Message;
                                                eventArgs.Text = $"Failed to {e.ChangeType} after 2nd try {mappedPath}";
                                                eventArgs.ChangeEventType = WatcherChangeTypes.All;
                                                Utils.WriteToTrace($"Failed to {e.ChangeType}: {mappedPath}{Environment.NewLine}{ex.Message}", this, true);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        this.HandleUpdate(e.FullPath, mappedPath);
                                        Utils.WriteToTrace($"{e.ChangeType}: {mappedPath}", this);
                                    }
                                    catch (IOException)
                                    {
                                        try
                                        {
                                            await Task.Delay(this.AppConfig.RetryWait);
                                            this.HandleUpdate(e.FullPath, mappedPath);
                                            Utils.WriteToTrace($"{e.ChangeType} 2nd attempt: {mappedPath}", this);
                                        }
                                        catch (IOException ex)
                                        {
                                            eventArgs.Title = ex.Message;
                                            eventArgs.Text = $"Failed to update after 2nd try {mappedPath}";
                                            eventArgs.ChangeEventType = WatcherChangeTypes.All;
                                            Utils.WriteToTrace($"Failed to {e.ChangeType}: {mappedPath}{Environment.NewLine}{ex.Message}", this, true);
                                        }
                                    }
                                }
                            }
                       
                    }
                    catch (Exception ex)
                    {
                        eventArgs.Title = "Exception thrown";
                        eventArgs.Text = ex.Message;
                        eventArgs.ChangeEventType = WatcherChangeTypes.All;
                        Utils.WriteToTrace($"Exception: {ex.Message}");
                    }
                    finally
                    {
                        this.ReleaseFileHandle(mappedPath);
                    }
                    
                }
                else
                {
                    eventArgs.Title = "Error";
                    eventArgs.Text = $"Failed to map path: '{e.FullPath}' -> {this.AppConfig.DestinationPath}";
                    eventArgs.ChangeEventType = WatcherChangeTypes.All;
                    Utils.WriteToTrace($"Failed to map path: '{e.FullPath}' -> {this.AppConfig.DestinationPath}", this, true);
                }
                this.OnChange?.Invoke(this, eventArgs);
            });
            
        }

        private Boolean MapDestinationPath(String source, out String mappedPath)
        {
            mappedPath = String.Empty;
            
            // Exclude some of the directories, like obj and bin, because we don't want to sync those anyways.
            if (!this.AppConfig.PathsToIgnore.Any(path => source.IndexOf($"{Path.DirectorySeparatorChar}{path}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) > 0))
            {
                // If the path does not contain the Views folder, ignore it.
                var viewsIndex = source.IndexOf($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
                if (viewsIndex >= 0)
                {
                    var relativePath = source[viewsIndex..];
                    mappedPath = Path.Join(this.AppConfig.DestinationPath, relativePath);
                    
                    return true;
                }
            }
            return false;
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
                if (!Directory.GetFiles(destinationDirectory).Any() && !Directory.GetDirectories(destinationDirectory).Any())
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
