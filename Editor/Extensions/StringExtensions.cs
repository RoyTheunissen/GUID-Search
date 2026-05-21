using System.IO;

namespace RoyTheunissen.GuidSearch
{
    internal static class StringExtensions
    {
        public static string RemovePrefix(this string name, string prefix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prefix))
                return name;

            if (!name.StartsWith(prefix))
                return name;

            return name.Substring(prefix.Length);
        }
        
        public static string RemoveSuffix(this string name, string suffix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(suffix))
                return name;

            if (!name.EndsWith(suffix))
                return name;

            return name.Substring(0, name.Length - suffix.Length);
        }

        private static readonly char[] DirectorySeparators =
            { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        public static string GetParentDirectory(this string path)
        {
            int lastDirectorySeparator = path.LastIndexOfAny(DirectorySeparators);
            if (lastDirectorySeparator == -1)
                return path;

            return path.Substring(0, lastDirectorySeparator);
        }
    }
}
