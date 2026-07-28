using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayniteAchievementSources.Detection
{
    public sealed class SteamAppIdDetector
    {
        private const int MaximumDirectoryDepth = 6;
        private const int MaximumDirectoriesVisited = 2500;

        private static readonly Regex IniAppIdPattern = new Regex(
            @"(?im)^\s*(?:app_?id|appid|steam_?app_?id|steamappid)\s*=\s*[""']?(\d{1,10})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SteamStoreLinkPattern = new Regex(
            @"https?://(?:store\.)?steampowered\.com/app/(\d{1,10})(?:/|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public SteamAppIdDetectionResult Detect(SteamAppIdDetectionContext context)
        {
            context = context ?? new SteamAppIdDetectionContext();
            var candidates = new List<SteamAppIdCandidate>();
            var diagnostics = new List<string>();

            AddPlayniteSourceCandidate(context, candidates);
            AddLinkCandidates(context.LinkUrls, candidates);
            AddFileCandidates(context.InstallDirectory, candidates, diagnostics);

            var orderedCandidates = candidates
                .Where(candidate => candidate.AppId > 0)
                .GroupBy(candidate => new
                {
                    candidate.AppId,
                    candidate.SourceKind,
                    candidate.Evidence
                })
                .Select(group => group.OrderByDescending(candidate => candidate.Priority).First())
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.AppId)
                .ThenBy(candidate => candidate.Evidence, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new SteamAppIdDetectionResult
            {
                BestCandidate = orderedCandidates.FirstOrDefault(),
                Candidates = orderedCandidates,
                IsAmbiguous = orderedCandidates.Select(candidate => candidate.AppId).Distinct().Skip(1).Any(),
                DiagnosticMessage = string.Join(Environment.NewLine, diagnostics)
            };
        }

        private static void AddPlayniteSourceCandidate(
            SteamAppIdDetectionContext context,
            ICollection<SteamAppIdCandidate> candidates)
        {
            if (context.PlayniteSourceName.IndexOf("steam", StringComparison.OrdinalIgnoreCase) < 0 ||
                !TryParseAppId(context.PlayniteSourceGameId, out var appId))
            {
                return;
            }

            candidates.Add(new SteamAppIdCandidate
            {
                AppId = appId,
                SourceKind = "Playnite Steam library metadata",
                Evidence = context.PlayniteSourceGameId.Trim(),
                Priority = 1100
            });
        }

        private static void AddLinkCandidates(
            IEnumerable<string> linkUrls,
            ICollection<SteamAppIdCandidate> candidates)
        {
            foreach (var linkUrl in linkUrls ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(linkUrl))
                {
                    continue;
                }

                var match = SteamStoreLinkPattern.Match(linkUrl.Trim());
                if (!match.Success || !TryParseAppId(match.Groups[1].Value, out var appId))
                {
                    continue;
                }

                candidates.Add(new SteamAppIdCandidate
                {
                    AppId = appId,
                    SourceKind = "Steam store link",
                    Evidence = linkUrl.Trim(),
                    Priority = 1050
                });
            }
        }

        private static void AddFileCandidates(
            string installDirectory,
            ICollection<SteamAppIdCandidate> candidates,
            ICollection<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                diagnostics.Add("The Playnite game does not have an installation directory configured.");
                return;
            }

            string fullInstallDirectory;
            try
            {
                fullInstallDirectory = Path.GetFullPath(installDirectory.Trim());
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                diagnostics.Add("The configured installation directory is not a valid Windows path.");
                return;
            }

            if (!Directory.Exists(fullInstallDirectory))
            {
                diagnostics.Add($"The configured installation directory does not exist: {fullInstallDirectory}");
                return;
            }

            foreach (var file in EnumerateCandidateFiles(fullInstallDirectory, diagnostics))
            {
                if (!TryReadAppId(file.FullName, out var appId))
                {
                    continue;
                }

                var relativePath = GetRelativePath(fullInstallDirectory, file.FullName);
                candidates.Add(new SteamAppIdCandidate
                {
                    AppId = appId,
                    SourceKind = GetSourceKind(file.Name),
                    Evidence = relativePath,
                    Priority = GetFilePriority(file.Name, relativePath)
                });
            }
        }

        private static IEnumerable<FileInfo> EnumerateCandidateFiles(
            string installDirectory,
            ICollection<string> diagnostics)
        {
            var pending = new Queue<DirectoryScanItem>();
            pending.Enqueue(new DirectoryScanItem(new DirectoryInfo(installDirectory), 0));
            var visitedDirectories = 0;

            while (pending.Count > 0 && visitedDirectories < MaximumDirectoriesVisited)
            {
                var current = pending.Dequeue();
                visitedDirectories++;

                FileInfo[] files;
                try
                {
                    files = current.Directory.GetFiles();
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    if (IsCandidateFileName(file.Name))
                    {
                        yield return file;
                    }
                }

                if (current.Depth >= MaximumDirectoryDepth)
                {
                    continue;
                }

                DirectoryInfo[] childDirectories;
                try
                {
                    childDirectories = current.Directory.GetDirectories();
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException)
                {
                    continue;
                }

                foreach (var childDirectory in childDirectories.OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if ((childDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    pending.Enqueue(new DirectoryScanItem(childDirectory, current.Depth + 1));
                }
            }

            if (pending.Count > 0)
            {
                diagnostics.Add($"The installation scan stopped after {MaximumDirectoriesVisited} directories to remain bounded.");
            }
        }

        private static bool IsCandidateFileName(string fileName)
        {
            return fileName.Equals("steam_appid.txt", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("configs.app.ini", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("configs.main.ini", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadAppId(string filePath, out uint appId)
        {
            appId = 0;

            try
            {
                var content = File.ReadAllText(filePath, Encoding.UTF8);
                if (Path.GetFileName(filePath).Equals("steam_appid.txt", StringComparison.OrdinalIgnoreCase))
                {
                    var firstValue = content
                        .Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault();
                    return TryParseAppId(firstValue, out appId);
                }

                var match = IniAppIdPattern.Match(content);
                return match.Success && TryParseAppId(match.Groups[1].Value, out appId);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException ||
                exception is IOException ||
                exception is NotSupportedException)
            {
                return false;
            }
        }

        private static bool TryParseAppId(string value, out uint appId)
        {
            appId = 0;
            return !string.IsNullOrWhiteSpace(value) &&
                   uint.TryParse(value.Trim().Trim('\ufeff'), out appId) &&
                   appId > 0;
        }

        private static string GetSourceKind(string fileName)
        {
            if (fileName.Equals("steam_appid.txt", StringComparison.OrdinalIgnoreCase))
            {
                return "steam_appid.txt";
            }

            return "Steam-compatible INI configuration";
        }

        private static int GetFilePriority(string fileName, string relativePath)
        {
            var priority = fileName.Equals("steam_appid.txt", StringComparison.OrdinalIgnoreCase) ? 900 :
                           fileName.Equals("configs.app.ini", StringComparison.OrdinalIgnoreCase) ? 800 : 700;

            if (relativePath.IndexOf("steam_settings", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                priority += 50;
            }

            var depth = relativePath.Count(character => character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar);
            return priority - Math.Min(depth, 25);
        }

        private static string GetRelativePath(string baseDirectory, string filePath)
        {
            try
            {
                var baseUri = new Uri(AppendDirectorySeparator(baseDirectory));
                var fileUri = new Uri(filePath);
                return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString())
                    .Replace('/', Path.DirectorySeparatorChar);
            }
            catch (UriFormatException)
            {
                return filePath;
            }
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private sealed class DirectoryScanItem
        {
            public DirectoryInfo Directory { get; }

            public int Depth { get; }

            public DirectoryScanItem(DirectoryInfo directory, int depth)
            {
                Directory = directory;
                Depth = depth;
            }
        }
    }
}
