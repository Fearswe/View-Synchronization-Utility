namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Runtime.CompilerServices;
    using System.Reflection;

    public class AppConfig
    {
        public AppConfig()
        {
            this.ShouldTriggerEvent = false;
        }

        public event EventHandler<PropertyUpdatedEventArgs>? OnChange;

        [JsonIgnore]
        public Boolean Paused { get; set; }

        [JsonIgnore]
        public Boolean IsConsoleOpen { get; set; } = true;

        [JsonIgnore]
        public AssemblyName AssemblyName { get; set; }

        [JsonIgnore]
        public Boolean ShouldTriggerEvent { get; set; }

        private Boolean _debugLog = false;
        [TrayIcon.TrayButtonInfo("Debug log", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean DebugLog 
        { 
            get => this._debugLog; 
            set
            {
                this._debugLog = value;
                this.TriggerEvent();
            } 
        }

        public List<String> PathsToIgnore { get; set; }

        private String _solutionPath = String.Empty;
        [TrayIcon.TrayButtonInfo("Solution path", TrayIcon.ButtonIcons.FolderUp)]
        public String SolutionPath {
            get => this._solutionPath;
            set
            {
                if (this._solutionPath != value)
                {
                    this._solutionPath = value;
                    this.TriggerEvent();
                }                
            }

        }

        private String _destinationPath = String.Empty;
        [TrayIcon.TrayButtonInfo("Destination path", TrayIcon.ButtonIcons.FolderDown)]
        public String DestinationPath {  
            get => this._destinationPath; 
            set
            {
                if(this._destinationPath != value)
                {
                    this._destinationPath = value;
                    this.TriggerEvent();
                }
            }
        }

        private Boolean _log = true;
        [TrayIcon.TrayButtonInfo("Should log", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean Log
        {
            get => this._log; set
            {
                if(this._log != value)
                {
                    this._log = value;
                    this.TriggerEvent();
                }
            }
        }

        private String _logPath = String.Empty;
        [TrayIcon.TrayButtonInfo("Log file folder", TrayIcon.ButtonIcons.FolderRight)]
        public String LogPath
        {
            get => this._logPath;
            set
            {
                if (this._logPath != value)
                {
                    this._logPath = value;
                    this.TriggerEvent();
                }
            }
        }

        private Boolean _notify = false;
        [TrayIcon.TrayButtonInfo("Should notify", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean Notify
        {
            get => this._notify; 
            set
            {
                if(this._notify != value)
                {
                    this._notify = value;
                    this.TriggerEvent();
                }
            }
        }

        private Boolean _notiyCreated = true;
        [TrayIcon.TrayButtonInfo("Should notify on created", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean NotifyCreated
        {
            get => this._notiyCreated;
            set
            {
                if (this._notiyCreated != value)
                {
                    this._notiyCreated = value;
                    this.TriggerEvent();
                }
            }
        }

        private Boolean _notifyChanged = true;
        [TrayIcon.TrayButtonInfo("Should notify on changed", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean NotifyChanged
        {
            get => this._notifyChanged;
            set
            {
                if (this._notifyChanged != value)
                {
                    this._notifyChanged = value;
                    this.TriggerEvent();
                }
            }
        }

        private Boolean _notifyRenamed = true;
        [TrayIcon.TrayButtonInfo("Should notify on renamed", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean NotifyRenamed
        {
            get => this._notifyRenamed;
            set
            {
                if (this._notifyRenamed != value)
                {
                    this._notifyRenamed = value;
                    this.TriggerEvent();
                }
            }
        }

        private Boolean _notifyRemoved = true;
        [TrayIcon.TrayButtonInfo("Should notify on removed", TrayIcon.ButtonIcons.Checkbox)]
        public Boolean NotifyRemoved
        {
            get => this._notifyRemoved;
            set
            {
                if (this._notifyRemoved != value)
                {
                    this._notifyRemoved = value;
                    this.TriggerEvent();
                }
            }
        }

        private int _retryWait = 2000;
        public int RetryWait 
        { 
            get => this._retryWait;
            set 
            { 
                this._retryWait = value;
                this.TriggerEvent();
            }
        }

        public static AppConfig GetAppConfig(String path)
        {
            if(!String.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var appConfig = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? throw new JsonException("Failed to serialize appconfig");
                appConfig.ShouldTriggerEvent = true;
                appConfig.Paused = false;
                return appConfig;
            }
            throw new FileNotFoundException("Provided path does not exist.", path);
        }

        public void SetAppConfig(String path)
        {
            if (!String.IsNullOrWhiteSpace(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(this), Encoding.UTF8);
            }
        }

        private void TriggerEvent([CallerMemberName] String propertyName = "")
        {
            if(this.ShouldTriggerEvent)
            {
                this.OnChange?.Invoke(this, new PropertyUpdatedEventArgs() { PropertyName = propertyName });
            }
        }

        public class PropertyUpdatedEventArgs : EventArgs
        {
            public String PropertyName { get; set; } = String.Empty;
        }


            
    }
}
