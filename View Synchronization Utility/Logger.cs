namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class Logger
    {
        private AppConfig AppConfig { get; }
        private StreamWriter? StreamWriter { get; set; }
        private String FileName { get; }

        public Logger(AppConfig appConfig)
        {
            this.AppConfig = appConfig;
            this.FileName = $"vsu_{DateTime.Now:yyyy-MM-dd}.log";

            this.CreateOrUpdateStreamWriter();
            this.AppConfig.OnChange += this.AppConfig_OnChange;
        }

        private void CreateOrUpdateStreamWriter()
        {
            this.StreamWriter?.Close();
            if (this.AppConfig.Log && !String.IsNullOrWhiteSpace(this.AppConfig.LogPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Join(this.AppConfig.LogPath, this.FileName)));
                this.StreamWriter = new StreamWriter(Path.Join(this.AppConfig.LogPath, this.FileName), Encoding.UTF8, new FileStreamOptions()
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.OpenOrCreate,
                    Share = FileShare.Read
                })
                {
                    AutoFlush = true
                };
            }
            else
            {
                this.StreamWriter = null;
            }
        }

        private void AppConfig_OnChange(Object? sender, AppConfig.PropertyUpdatedEventArgs e)
        {
            if(e.PropertyName == nameof(this.AppConfig.LogPath) || e.PropertyName == nameof(this.AppConfig.Log))
            {
                this.CreateOrUpdateStreamWriter();
            }

        }

        public void WriteToLog(String title, String text)
        {
            if(this.AppConfig.Log && this.StreamWriter != null)
            {
                this.StreamWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}: {text}");
            }
        }
    }
}
