using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Core.MediaFiles.TorrentInfo;

namespace NzbDrone.Core.Download.FakeRelease
{
    public interface IInspectReleaseContents
    {
        FakeReleaseDetection Inspect(TorrentContents contents);
    }

    public class FakeReleaseInspector : IInspectReleaseContents
    {
        // A torrent with no archive and no video larger than this has nothing worth importing.
        // Deliberately low: even a short special comfortably exceeds it, so legitimate
        // releases are never caught by the NoUsableContent rule.
        private const long MinimumVideoFileSize = 10 * 1024 * 1024;

        private static readonly HashSet<string> ExecutableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".scr", ".com", ".pif", ".bat", ".cmd", ".msi", ".msp", ".vbs", ".vbe",
            ".js", ".jse", ".wsf", ".wsh", ".ps1", ".psm1", ".hta", ".cpl", ".msc", ".reg",
            ".jar", ".apk", ".lnk", ".url", ".gadget", ".inf", ".dll", ".sys"
        };

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".avi", ".m4v", ".mpg", ".mpeg", ".wmv", ".mov", ".flv", ".ts",
            ".m2ts", ".mts", ".webm", ".ogm", ".ogv", ".divx", ".vob", ".rmvb", ".rm", ".asf",
            ".mk3d", ".iso", ".img", ".m2v", ".mpv", ".3gp", ".f4v", ".strm"
        };

        private static readonly HashSet<string> ArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".rar", ".zip", ".7z", ".tar", ".gz", ".bz2", ".xz", ".z", ".cab", ".arj", ".lzh", ".ace"
        };

        // Matches "Something.mkv.exe" - a media extension immediately followed by an executable one.
        private static readonly Regex DisguisedExecutableRegex = new Regex(
            @"\.(mkv|mp4|avi|m4v|mpg|mpeg|wmv|mov|flv|ts|webm|iso|srt|sub|nfo|jpg|png)\s*\.(exe|scr|com|pif|bat|cmd|msi|vbs|js|jar|lnk|hta|apk)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Split archive volumes: .r00-.r99, .001-.999, .part01.rar, .z01
        private static readonly Regex SplitArchiveRegex = new Regex(
            @"\.(r\d{2,3}|\d{3}|z\d{2}|part\d+\.rar)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // BEP-47 padding files, which are structural and not real content.
        private static readonly Regex PaddingFileRegex = new Regex(
            @"(^|[\\/])(\.pad[\\/]|_____padding_file)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PasswordBaitRegex = new Regex(
            @"(password|passwort|contrase|mot.?de.?passe|how.?to.?(open|watch|play)|read.?me.?first)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Logger _logger;

        public FakeReleaseInspector(Logger logger)
        {
            _logger = logger;
        }

        public FakeReleaseDetection Inspect(TorrentContents contents)
        {
            if (contents?.Files == null || contents.Files.Count == 0)
            {
                // Nothing to judge; let the release through rather than guessing.
                return FakeReleaseDetection.Clean();
            }

            var files = contents.Files
                                .Where(f => !PaddingFileRegex.IsMatch(f.Path ?? string.Empty))
                                .ToList();

            if (files.Count == 0)
            {
                return FakeReleaseDetection.Clean();
            }

            // 1. Disguised executables - the highest-confidence signal there is.
            foreach (var file in files)
            {
                var name = GetFileName(file.Path);

                if (DisguisedExecutableRegex.IsMatch(name))
                {
                    return FakeReleaseDetection.Fake(
                        FakeReleaseRejectionReason.DisguisedExecutable,
                        $"Contains an executable disguised as a media file: '{name}'",
                        file.Path);
                }
            }

            // 2. Any executable, installer or script payload.
            foreach (var file in files)
            {
                var name = GetFileName(file.Path);
                var extension = GetExtension(name);

                if (extension.Length > 0 && ExecutableExtensions.Contains(extension))
                {
                    return FakeReleaseDetection.Fake(
                        FakeReleaseRejectionReason.ExecutablePayload,
                        $"Contains an executable or script payload: '{name}'",
                        file.Path);
                }
            }

            var hasArchive = files.Any(f =>
            {
                var name = GetFileName(f.Path);
                return ArchiveExtensions.Contains(GetExtension(name)) || SplitArchiveRegex.IsMatch(name);
            });

            var hasRealVideo = files.Any(f =>
                VideoExtensions.Contains(GetExtension(GetFileName(f.Path))) && f.Length >= MinimumVideoFileSize);

            // 3. Password bait: archives, no video, and a file advertising a password.
            // Requires the absence of video because plenty of legitimate releases ship as archives.
            if (!hasRealVideo && hasArchive)
            {
                var bait = files.FirstOrDefault(f => PasswordBaitRegex.IsMatch(GetFileName(f.Path)));

                if (bait != null)
                {
                    return FakeReleaseDetection.Fake(
                        FakeReleaseRejectionReason.PasswordBait,
                        $"Contains archives with no video alongside '{GetFileName(bait.Path)}', a common password-scam pattern",
                        bait.Path);
                }
            }

            // 4. Nothing usable at all. Archives are given the benefit of the doubt,
            // since their contents cannot be inspected from the torrent metadata.
            if (!hasRealVideo && !hasArchive)
            {
                return FakeReleaseDetection.Fake(
                    FakeReleaseRejectionReason.NoUsableContent,
                    "Contains no video files and no archives");
            }

            _logger.Trace("Release contents passed fake-release inspection ({0} files)", files.Count);

            return FakeReleaseDetection.Clean();
        }

        private static string GetFileName(string path)
        {
            if (path == null)
            {
                return string.Empty;
            }

            var index = path.LastIndexOfAny(new[] { '/', '\\' });

            return index >= 0 ? path.Substring(index + 1) : path;
        }

        private static string GetExtension(string fileName)
        {
            var index = fileName.LastIndexOf('.');

            return index >= 0 ? fileName.Substring(index) : string.Empty;
        }
    }
}
