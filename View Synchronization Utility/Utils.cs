namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    internal static class Utils
    {
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
