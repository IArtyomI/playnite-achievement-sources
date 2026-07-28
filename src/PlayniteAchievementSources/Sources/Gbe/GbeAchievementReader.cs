using PlayniteAchievementSources.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlayniteAchievementSources.Sources.Gbe
{
    public sealed class GbeAchievementReader
    {
        private const int MaximumDirectoryDepth = 6;
        private const int MaximumDirectoriesVisited = 2500;
        private const string SourceKey = "gbe-compatible-local";

        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 100
        };

        public GbeAchievementReadResult Read(GbeAchievementReadContext context)
        {
            context = context ?? new GbeAchievementReadContext();
            var result = new GbeAchievementReadResult();

            if (context.AppId == 0)
            {
                result.Diagnostics.Add("A non-zero Steam AppID is required before local achievement data can be read.");
                return result;
            }

            var definitionPath = FindDefinitionPath(context, result.Diagnostics);
            if (string.IsNullOrWhiteSpace(definitionPath))
            {
                result.Diagnostics.Add("No supported GBE/Goldberg-compatible achievement definition file was found.");
                return result;
            }

            object[] definitions;
            string definitionError;
            if (!TryLoadDefinitions(definitionPath, out definitions, out definitionError))
            {
                result.Diagnostics.Add($"The selected definition file could not be read safely: {definitionError}");
                return result;
            }

            result.DefinitionPath = definitionPath;
            result.HasDefinitions = true;

            Dictionary<string, object> state = null;
            var statePath = FindStatePath(context, result.Diagnostics, out state);
            if (!string.IsNullOrWhiteSpace(statePath) && state != null)
            {
                result.StatePath = statePath;
                result.HasState = true;
            }
            else
            {
                state = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                result.Diagnostics.Add("No supported AppID-scoped achievement state file was found. Definitions are shown as locked, but unlock state is not considered a complete snapshot.");
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in definitions)
            {
                var definition = item as Dictionary<string, object>;
                if (definition == null)
                {
                    result.Diagnostics.Add("An achievement definition entry was ignored because it was not a JSON object.");
                    continue;
                }

                var achievementId = GetString(definition, "name").Trim();
                if (string.IsNullOrWhiteSpace(achievementId))
                {
                    result.Diagnostics.Add("An achievement definition entry was ignored because it did not contain a non-empty name.");
                    continue;
                }

                if (!seenIds.Add(achievementId))
                {
                    result.Diagnostics.Add($"Duplicate achievement definition ignored: {achievementId}");
                    continue;
                }

                Dictionary<string, object> stateEntry;
                TryGetObject(state, achievementId, out stateEntry);

                var unlocked = stateEntry != null && GetBoolean(stateEntry, "earned");
                var record = new AchievementRecord
                {
                    SourceKey = SourceKey,
                    SourceGameId = context.AppId.ToString(CultureInfo.InvariantCulture),
                    AchievementId = achievementId,
                    DisplayName = GetLocalizedString(definition, "displayName", context.PreferredLanguage, achievementId),
                    Description = GetLocalizedString(definition, "description", context.PreferredLanguage, string.Empty),
                    IsHidden = GetBoolean(definition, "hidden"),
                    IsUnlocked = unlocked,
                    UnlockTimeUtc = unlocked ? GetUnixTime(stateEntry, "earned_time") : null,
                    CurrentProgress = GetNullableDouble(stateEntry, "progress"),
                    MaximumProgress = GetNullableDouble(stateEntry, "max_progress"),
                    UnlockedIconPath = ResolveIconPath(definitionPath, GetString(definition, "icon")),
                    LockedIconPath = ResolveIconPath(
                        definitionPath,
                        FirstNonEmpty(GetString(definition, "icon_gray"), GetString(definition, "icongray"))),
                    EvidencePath = definitionPath
                };

                result.Achievements.Add(record);
            }

            result.IsCompleteSnapshot = result.HasDefinitions && result.HasState;
            return result;
        }

        private string FindDefinitionPath(GbeAchievementReadContext context, ICollection<string> diagnostics)
        {
            if (!string.IsNullOrWhiteSpace(context.ExplicitDefinitionPath))
            {
                var explicitPath = TryGetFullPath(context.ExplicitDefinitionPath);
                if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
                {
                    object[] explicitDefinitions;
                    string explicitError;
                    if (TryLoadDefinitions(explicitPath, out explicitDefinitions, out explicitError))
                    {
                        return explicitPath;
                    }

                    diagnostics.Add($"The explicitly configured definition path was not a supported definition array: {explicitError}");
                }
                else
                {
                    diagnostics.Add("The explicitly configured definition path does not exist or is invalid.");
                }
            }

            var installDirectory = TryGetFullPath(context.InstallDirectory);
            if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
            {
                diagnostics.Add("The Playnite installation directory is unavailable, so local definition discovery could not run.");
                return string.Empty;
            }

            var candidates = EnumerateAchievementJsonFiles(installDirectory, diagnostics)
                .OrderByDescending(path => GetDefinitionPriority(installDirectory, path))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                object[] definitions;
                string error;
                if (TryLoadDefinitions(candidate, out definitions, out error))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private string FindStatePath(
            GbeAchievementReadContext context,
            ICollection<string> diagnostics,
            out Dictionary<string, object> state)
        {
            state = null;
            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(context.ExplicitStatePath))
            {
                candidates.Add(context.ExplicitStatePath);
            }

            var appId = context.AppId.ToString(CultureInfo.InvariantCulture);
            foreach (var root in context.SaveRootDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var fullRoot = TryGetFullPath(root);
                if (string.IsNullOrWhiteSpace(fullRoot))
                {
                    continue;
                }

                var rootName = new DirectoryInfo(fullRoot).Name;
                candidates.Add(string.Equals(rootName, appId, StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(fullRoot, "achievements.json")
                    : Path.Combine(fullRoot, appId, "achievements.json"));
            }

            foreach (var candidateValue in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidate = TryGetFullPath(candidateValue);
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                {
                    continue;
                }

                string error;
                if (TryLoadState(candidate, out state, out error))
                {
                    return candidate;
                }

                diagnostics.Add($"A candidate state file was ignored because it was malformed or used an unsupported shape: {candidate} ({error})");
            }

            return string.Empty;
        }

        private IEnumerable<string> EnumerateAchievementJsonFiles(
            string installDirectory,
            ICollection<string> diagnostics)
        {
            var results = new List<string>();
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
                    files = current.Directory.GetFiles("achievements.json");
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException)
                {
                    continue;
                }

                results.AddRange(files.Select(file => file.FullName));

                if (current.Depth >= MaximumDirectoryDepth)
                {
                    continue;
                }

                DirectoryInfo[] directories;
                try
                {
                    directories = current.Directory.GetDirectories();
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException)
                {
                    continue;
                }

                foreach (var directory in directories.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if ((directory.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Enqueue(new DirectoryScanItem(directory, current.Depth + 1));
                    }
                }
            }

            if (pending.Count > 0)
            {
                diagnostics.Add($"Definition discovery stopped after {MaximumDirectoriesVisited} directories to remain bounded.");
            }

            return results;
        }

        private bool TryLoadDefinitions(string path, out object[] definitions, out string error)
        {
            definitions = null;
            error = string.Empty;

            object value;
            if (!TryDeserialize(path, out value, out error))
            {
                return false;
            }

            definitions = value as object[];
            if (definitions == null)
            {
                error = "the JSON root was not an array";
                return false;
            }

            if (definitions.Length > 0 && !definitions.Any(item =>
            {
                var definition = item as Dictionary<string, object>;
                return definition != null && !string.IsNullOrWhiteSpace(GetString(definition, "name"));
            }))
            {
                error = "the array did not contain any named achievement definitions";
                definitions = null;
                return false;
            }

            return true;
        }

        private bool TryLoadState(string path, out Dictionary<string, object> state, out string error)
        {
            state = null;
            error = string.Empty;

            object value;
            if (!TryDeserialize(path, out value, out error))
            {
                return false;
            }

            state = value as Dictionary<string, object>;
            if (state == null)
            {
                error = "the JSON root was not an object keyed by achievement name";
                return false;
            }

            return true;
        }

        private bool TryDeserialize(string path, out object value, out string error)
        {
            value = null;
            error = string.Empty;

            try
            {
                var json = File.ReadAllText(path);
                value = serializer.DeserializeObject(json);
                return value != null;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static int GetDefinitionPriority(string installDirectory, string path)
        {
            var relative = GetRelativePath(installDirectory, path);
            var priority = relative.IndexOf("steam_settings", StringComparison.OrdinalIgnoreCase) >= 0 ? 1000 : 100;
            var depth = relative.Count(character => character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar);
            return priority - Math.Min(depth, 50);
        }

        private static string GetLocalizedString(
            IDictionary<string, object> source,
            string key,
            string preferredLanguage,
            string fallback)
        {
            object value;
            if (!TryGetValue(source, key, out value) || value == null)
            {
                return fallback;
            }

            var text = value as string;
            if (text != null)
            {
                return string.IsNullOrWhiteSpace(text) ? fallback : text;
            }

            var localized = value as Dictionary<string, object>;
            if (localized == null || localized.Count == 0)
            {
                return fallback;
            }

            object localizedValue;
            if (!string.IsNullOrWhiteSpace(preferredLanguage) &&
                TryGetValue(localized, preferredLanguage, out localizedValue) &&
                localizedValue != null)
            {
                return Convert.ToString(localizedValue, CultureInfo.InvariantCulture) ?? fallback;
            }

            if (TryGetValue(localized, "english", out localizedValue) && localizedValue != null)
            {
                return Convert.ToString(localizedValue, CultureInfo.InvariantCulture) ?? fallback;
            }

            var firstLanguage = localized.FirstOrDefault(pair =>
                !string.Equals(pair.Key, "token", StringComparison.OrdinalIgnoreCase) && pair.Value != null);
            if (!string.IsNullOrWhiteSpace(firstLanguage.Key))
            {
                return Convert.ToString(firstLanguage.Value, CultureInfo.InvariantCulture) ?? fallback;
            }

            if (TryGetValue(localized, "token", out localizedValue) && localizedValue != null)
            {
                return Convert.ToString(localizedValue, CultureInfo.InvariantCulture) ?? fallback;
            }

            return fallback;
        }

        private static string GetString(IDictionary<string, object> source, string key)
        {
            object value;
            return source != null && TryGetValue(source, key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
        }

        private static bool GetBoolean(IDictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !TryGetValue(source, key, out value) || value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            bool booleanValue;
            if (bool.TryParse(text, out booleanValue))
            {
                return booleanValue;
            }

            double number;
            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out number) && Math.Abs(number) > double.Epsilon;
        }

        private static double? GetNullableDouble(IDictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !TryGetValue(source, key, out value) || value == null)
            {
                return null;
            }

            double parsed;
            return double.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out parsed)
                ? (double?)parsed
                : null;
        }

        private static DateTime? GetUnixTime(IDictionary<string, object> source, string key)
        {
            object value;
            if (source == null || !TryGetValue(source, key, out value) || value == null)
            {
                return null;
            }

            long seconds;
            if (!long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) || seconds <= 0)
            {
                return null;
            }

            try
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static bool TryGetObject(
            IDictionary<string, object> source,
            string key,
            out Dictionary<string, object> value)
        {
            value = null;
            object raw;
            if (source == null || !TryGetValue(source, key, out raw))
            {
                return false;
            }

            value = raw as Dictionary<string, object>;
            return value != null;
        }

        private static bool TryGetValue(IDictionary<string, object> source, string key, out object value)
        {
            value = null;
            if (source == null)
            {
                return false;
            }

            foreach (var pair in source)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveIconPath(string definitionPath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            try
            {
                if (Path.IsPathRooted(configuredPath))
                {
                    return File.Exists(configuredPath) ? Path.GetFullPath(configuredPath) : string.Empty;
                }

                var definitionDirectory = Path.GetDirectoryName(definitionPath) ?? string.Empty;
                var directPath = Path.GetFullPath(Path.Combine(definitionDirectory, configuredPath));
                if (File.Exists(directPath))
                {
                    return directPath;
                }

                var imagePath = Path.GetFullPath(Path.Combine(definitionDirectory, "achievement_images", configuredPath));
                return File.Exists(imagePath) ? imagePath : string.Empty;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static string TryGetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static string GetRelativePath(string baseDirectory, string path)
        {
            try
            {
                var baseUri = new Uri(AppendDirectorySeparator(baseDirectory));
                var pathUri = new Uri(path);
                return Uri.UnescapeDataString(baseUri.MakeRelativeUri(pathUri).ToString())
                    .Replace('/', Path.DirectorySeparatorChar);
            }
            catch (UriFormatException)
            {
                return path;
            }
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second;
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
