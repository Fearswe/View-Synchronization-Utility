using System.Diagnostics;
using View_Synchronization_Utility;
using System.Reflection;
using System.Diagnostics.Tracing;
using System.Linq;


Trace.AutoFlush = true;
Trace.CorrelationManager.ActivityId = Guid.NewGuid();

Trace.Listeners.Add(new ConsoleTraceListener() { Name = "Console"});
try
{
    if (!Debugger.IsAttached)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            if (e.Exception is Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Utils.WriteToTrace(ex.Message, null, true);
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Utils.WriteToTrace(ex.Message, null, true);
            }
        };
    }
    var assemblyName = Assembly.GetExecutingAssembly().GetName();
    Utils.WriteToTrace($"{assemblyName.Name} v{assemblyName.Version} starting.");
    var configPath = @".\appconfig.json";
    if (args.Length > 0)
    {
        if (args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(args[0]))
        {
            configPath = args[0];
        }
    }
    var appConfig = AppConfig.GetAppConfig(configPath);
    appConfig.AssemblyName = assemblyName;
    Utils.WriteToTrace("Config loaded");
    appConfig.OnChange += (s, e) =>
    {
        appConfig.SetAppConfig(configPath);
        if(e.PropertyName == nameof(appConfig.DebugLog) || e.PropertyName == nameof(appConfig.LogPath))
        {
            if (appConfig.DebugLog)
            {
                if(Utils.FindTraceListenerByName("Debuglog", out TraceListener? traceListener))
                {
                    Trace.Listeners.Remove(traceListener);
                }
                if (!String.IsNullOrWhiteSpace(appConfig.LogPath))
                {
                    Directory.CreateDirectory(appConfig.LogPath);
                    Trace.Listeners.Add(new XmlWriterTraceListener(Path.Join(appConfig.LogPath, "vsu-debuglog.svclog")) { Name = "Debuglog" });
                }
            }
            else
            {
                if (Utils.FindTraceListenerByName("Debuglog", out TraceListener? traceListener))
                {
                    Trace.Listeners.Remove(traceListener);
                }
            }
        }

        Utils.WriteToTrace("Updated config");
    };
    if(appConfig.DebugLog)
    {
        if (!String.IsNullOrWhiteSpace(appConfig.LogPath))
        {
            Directory.CreateDirectory(appConfig.LogPath);
            Trace.Listeners.Add(new XmlWriterTraceListener(Path.Join(appConfig.LogPath, "debuglog.svclog")) { Name = "Debuglog" });            
        }
    }

    if (!Debugger.IsAttached)
    {
        appConfig.IsConsoleOpen = false;
        Utils.ToggleConsoleWindow(false);
    }
    else
    {
        appConfig.IsConsoleOpen = true;
        Utils.ToggleConsoleWindow(true);
    }


    var trayIcon = new TrayIcon(appConfig);
    Utils.WriteToTrace("TrayIcon initiated");
    var logger = new Logger(appConfig);
    Utils.WriteToTrace("Logger initiated");

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
            Utils.WriteToTrace(ex.Message, null, true);
        }
    };
    Utils.WriteToTrace("Watcher initiated");


    Application.Run(); // Wait for eternity until the sweet release of death at the hands of the merciful overlord in charge of the machine.
}
catch (Exception ex)
{
    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    Utils.WriteToTrace(ex.Message, null, true);
}