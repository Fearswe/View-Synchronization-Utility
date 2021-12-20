using System.Diagnostics;
using View_Synchronization_Utility;



var configPath = @".\appconfig.json";


if(args.Length > 0)
{
    if(args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(args[0]))
    {
        configPath = args[0];
    }
}

var appConfig = AppConfig.GetAppConfig(configPath);
appConfig.OnChange += (s, e) => { 
    appConfig.SetAppConfig(configPath);
    Debug.WriteLine("Updated config");
};


var trayIcon = new TrayIcon(appConfig);
var logger = new Logger(appConfig);

var watcher = new Watcher(appConfig);
watcher.OnChange += (s, e) =>
{
    trayIcon.SendNotification(e.Title, e.Text, e.ChangeEventType);
    try
    {
        logger.WriteToLog(e.Title, e.Text);
    }
    catch (Exception ex)
    {
        trayIcon.SendNotification("Failed to write to log", ex.Message, WatcherChangeTypes.All);
    }
};


Application.Run(); // Wait for eternity until the sweet release of death at the hands of the merciful overlord in charge of the machine.