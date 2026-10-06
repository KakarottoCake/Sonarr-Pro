using System;
using System.IO;

namespace NzbDrone.Core.LibraryTools
{
    public static class ArchivePathGuard
    {
        public static string Resolve(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new IOException("Select a file within the configured folder.");
            }

            var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(basePath, relativePath));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(basePath + Path.DirectorySeparatorChar, comparison))
            {
                throw new IOException("The file must stay within its configured folder.");
            }

            var current = path;
            while (!string.Equals(current, basePath, comparison))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Restore cannot traverse symbolic links or junctions.");
                }

                current = Path.GetDirectoryName(current);
            }

            return path;
        }
    }
}
