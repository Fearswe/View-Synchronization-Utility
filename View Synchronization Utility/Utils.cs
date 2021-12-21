namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    using System.Diagnostics;

    internal static class Utils
    {

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public static Boolean ToggleConsoleWindow(Boolean show)
        {
            var window = GetConsoleWindow();
            return ShowWindow(window, show ? SW_SHOW : SW_HIDE);
        }


        public static Icon GetEmbeddedIcon(String fileName)
        {
            using (var stream = GetEmbeddedResourceStream(fileName))
            {
                return new Icon(stream);
            }
        }

        public static Image GetEmbeddedImage(String fileName)
        {
            using (var stream = GetEmbeddedResourceStream(fileName))
            {
                 return Image.FromStream(stream);
            }

        }

        public static Boolean FindTraceListenerByName(String name, out TraceListener? traceListener)
        {
            traceListener = null;
            foreach(TraceListener listener in Trace.Listeners)
            {
                if(listener.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    traceListener = listener;
                    return true;
                }
            }
            return false;
        }

        private static Stream GetEmbeddedResourceStream (String fileName) 
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(fileName));
            if(resourceName != null)
            {
                var stream = assembly.GetManifestResourceStream(resourceName);
                if(stream != null)
                {
                    return stream;
                }
                else
                {
                    throw new Exception("Unable to open manifest resource stream");
                }
            }
            else
            {
                throw new FileNotFoundException("No embedded resource with provided name found.", fileName);
            }
        }
    }
}
