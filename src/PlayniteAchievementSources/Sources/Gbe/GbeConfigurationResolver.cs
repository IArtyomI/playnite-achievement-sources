using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PlayniteAchievementSources.Sources.Gbe
{
    public sealed class GbeConfigurationResolution
    {
        public IList<string> DefinitionCandidates { get; } = new List<string>();
        public IList<string> StateCandidates { get; } = new List<string>();
        public IList<string> WatchDirectories { get; } = new List<string>();
        public IList<string> Diagnostics { get; } = new List<string>();
    }

    public sealed class GbeConfigurationResolver
    {
        private const int MaximumDepth = 6;
        private const int MaximumDirectories = 2500;
        private const int MaximumAchievements = 10000;
        private const long MaximumDefinitionBytes = 25L * 1024L * 1024L;
        private const long MaximumRuneStateBytes = 4L * 1024L * 1024L;
        private const string PluginDataDirectoryName = "0391911c-bf98-4a2d-8200-8641af0973e9";
        private const long MaximumUnixTime = 253402300799L;

        private readonly Func<string> commonDocumentsResolver;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 100
        };

        public GbeConfigurationResolver(Func<string> commonDocumentsResolver = null)
        {
            this.commonDocumentsResolver = commonDocumentsResolver ??
                (() => Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments));
        }

        public GbeConfigurationResolution Resolve(
            uint appId,
            string installDirectory,
            string appDataDirectory,
            string explicitDefinitionPath,
            string explicitStatePath)
        {
            var result = new GbeConfigurationResolution();
            if (appId == 0)
            {
                result.Diagnostics.Add("A non-zero AppID is required.");
                return result;
            }

            AddExplicitFile(explicitDefinitionPath, "achievements.json", result.DefinitionCandidates, result.Diagnostics);
            if (result.DefinitionCandidates.Count > 0)
            {
                AddUnique(result.WatchDirectories, Path.GetDirectoryName(result.DefinitionCandidates[0]));
            }
            AddExplicitState(explicitStatePath, appId, result);

            foreach (var settingsDirectory in FindSteamSettingsDirectories(installDirectory, result.Diagnostics))
            {
                AddUnique(result.WatchDirectories, settingsDirectory);
                AddUnique(result.DefinitionCandidates, Path.Combine(settingsDirectory, "achievements.json"));

                var userConfig = Path.Combine(settingsDirectory, "configs.user.ini");
                var values = ReadUserSaveOptions(userConfig, result.Diagnostics);
                if (values.TryGetValue("local_save_path", out var localSavePath) &&
                    !string.IsNullOrWhiteSpace(localSavePath))
                {
                    var baseDirectory = Path.GetDirectoryName(settingsDirectory);
                    var localRoot = Path.IsPathRooted(localSavePath)
                        ? localSavePath
                        : Path.Combine(baseDirectory ?? installDirectory ?? string.Empty, localSavePath);
                    AddStateLayouts(localRoot, appId, result);
                    result.Diagnostics.Add("GBE local_save_path configuration was resolved.");
                }

                if (values.TryGetValue("saves_folder_name", out var savesFolderName) &&
                    !string.IsNullOrWhiteSpace(savesFolderName) &&
                    !string.IsNullOrWhiteSpace(appDataDirectory))
                {
                    AddStateLayouts(Path.Combine(appDataDirectory, savesFolderName), appId, result);
                    result.Diagnostics.Add("GBE saves_folder_name configuration was resolved.");
                }
            }

            AddRuneLayout(appId, installDirectory, appDataDirectory, result);

            if (!string.IsNullOrWhiteSpace(appDataDirectory))
            {
                AddStateLayouts(Path.Combine(appDataDirectory, "GSE Saves"), appId, result);
                AddStateLayouts(Path.Combine(appDataDirectory, "Goldberg SteamEmu Saves"), appId, result);
            }

            return result;
        }

        private void AddRuneLayout(
            uint appId,
            string installDirectory,
            string appDataDirectory,
            GbeConfigurationResolution result)
        {
            var configurationPath = FindRuneConfiguration(installDirectory, appId, result.Diagnostics);
            if (string.IsNullOrWhiteSpace(configurationPath))
            {
                return;
            }

            var pluginDataRoot = TryFullPath(Path.Combine(
                appDataDirectory ?? string.Empty,
                "Playnite",
                "ExtensionsData",
                PluginDataDirectoryName));
            if (string.IsNullOrWhiteSpace(pluginDataRoot))
            {
                result.Diagnostics.Add("RUNE was detected, but the extension metadata-cache path was invalid.");
                return;
            }

            var cacheRoot = TryContainedPath(
                pluginDataRoot,
                Path.Combine(pluginDataRoot, "metadata-cache", "rune", appId.ToString(CultureInfo.InvariantCulture)));
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                result.Diagnostics.Add("RUNE was detected, but its extension-local cache path was rejected.");
                return;
            }

            var settingsDirectory = Path.Combine(cacheRoot, "steam_settings");
            var normalizedStateDirectory = Path.Combine(cacheRoot, "state");
            try
            {
                Directory.CreateDirectory(settingsDirectory);
                Directory.CreateDirectory(normalizedStateDirectory);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                result.Diagnostics.Add("RUNE was detected, but its extension-local cache directories could not be prepared: " + exception.Message);
                return;
            }

            var definitionPath = Path.Combine(settingsDirectory, "achievements.json");
            var normalizedStatePath = Path.Combine(normalizedStateDirectory, "achievements.json");
            var commonDocuments = TryFullPath(commonDocumentsResolver());
            var runeStatePath = string.IsNullOrWhiteSpace(commonDocuments)
                ? string.Empty
                : Path.Combine(
                    commonDocuments,
                    "Steam",
                    "RUNE",
                    appId.ToString(CultureInfo.InvariantCulture),
                    "achievements.ini");

            AddUnique(result.DefinitionCandidates, definitionPath);
            AddUnique(result.WatchDirectories, settingsDirectory);
            AddUnique(result.WatchDirectories, normalizedStateDirectory);
            AddUnique(result.StateCandidates, normalizedStatePath);

            if (!string.IsNullOrWhiteSpace(runeStatePath))
            {
                AddUnique(result.StateCandidates, runeStatePath);
                AddUnique(result.WatchDirectories, Path.GetDirectoryName(runeStatePath));
            }

            result.Diagnostics.Add("Selected adapter: RUNE-compatible steam_emu.ini and steam_api64.rne.");

            if (!File.Exists(definitionPath))
            {
                DeleteIfExists(normalizedStatePath);
                result.Diagnostics.Add("RUNE runtime state was found, but official achievement definitions have not been prepared in the extension metadata cache yet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(runeStatePath) || !File.Exists(runeStatePath))
            {
                DeleteIfExists(normalizedStatePath);
                result.Diagnostics.Add("The RUNE achievements.ini state file does not exist yet. It will be monitored for future creation.");
                return;
            }

            if (!TryNormalizeRuneState(definitionPath, runeStatePath, normalizedStatePath, out var error))
            {
                DeleteIfExists(normalizedStatePath);
                result.Diagnostics.Add("The RUNE achievements.ini state was rejected: " + error);
                return;
            }

            result.Diagnostics.Add("RUNE achievements.ini was validated read-only and normalized into the extension-owned state cache.");
        }

        private string FindRuneConfiguration(
            string installDirectory,
            uint expectedAppId,
            ICollection<string> diagnostics)
        {
            var root = TryFullPath(installDirectory);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return string.Empty;
            }

            var queue = new Queue<Tuple<DirectoryInfo, int>>();
            queue.Enqueue(Tuple.Create(new DirectoryInfo(root), 0));
            var visited = 0;
            while (queue.Count > 0 && visited < MaximumDirectories)
            {
                var item = queue.Dequeue();
                visited++;

                var configurationPath = Path.Combine(item.Item1.FullName, "steam_emu.ini");
                if (File.Exists(configurationPath) &&
                    (File.Exists(Path.Combine(item.Item1.FullName, "steam_api64.rne")) ||
                     File.Exists(Path.Combine(item.Item1.FullName, "steam_api.rne"))))
                {
                    if (TryReadIniAppId(configurationPath, out var appId))
                    {
                        if (appId == expectedAppId)
                        {
                            return configurationPath;
                        }

                        diagnostics?.Add(
                            $"A RUNE steam_emu.ini for AppID {appId} was ignored because the detected game AppID is {expectedAppId}.");
                    }
                }

                if (item.Item2 >= MaximumDepth)
                {
                    continue;
                }

                DirectoryInfo[] children;
                try
                {
                    children = item.Item1.GetDirectories();
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var child in children.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        queue.Enqueue(Tuple.Create(child, item.Item2 + 1));
                    }
                }
            }

            if (queue.Count > 0)
            {
                diagnostics?.Add($"RUNE configuration discovery stopped after {MaximumDirectories} directories to remain bounded.");
            }

            return string.Empty;
        }

        private static bool TryReadIniAppId(string path, out uint appId)
        {
            appId = 0;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 1024 * 1024)
                {
                    return false;
                }

                foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = (rawLine ?? string.Empty).Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    var separator = line.IndexOf('=');
                    if (separator <= 0 ||
                        !string.Equals(line.Substring(0, separator).Trim(), "AppId", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return uint.TryParse(line.Substring(separator + 1).Trim().Trim('"'), out appId) && appId > 0;
                }
            }
            catch
            {
            }

            return false;
        }

        private bool TryNormalizeRuneState(
            string definitionPath,
            string runeStatePath,
            string normalizedStatePath,
            out string error)
        {
            error = string.Empty;
            if (!TryLoadDefinitionIds(definitionPath, out var definitionIds, out error))
            {
                return false;
            }

            if (!TryReadIni(runeStatePath, out var sections, out error))
            {
                return false;
            }

            if (!sections.TryGetValue("SteamAchievements", out var indexSection) ||
                !indexSection.TryGetValue("Count", out var countValue) ||
                !int.TryParse(countValue, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                count < 0 ||
                count > MaximumAchievements)
            {
                error = $"[SteamAchievements] Count must be an integer between 0 and {MaximumAchievements}";
                return false;
            }

            var indexedKeys = indexSection.Keys
                .Where(key => key.Length == 5 && key.All(char.IsDigit))
                .ToList();
            if (indexedKeys.Count != count)
            {
                error = "the indexed achievement entry count did not match Count";
                return false;
            }

            var unlockedIds = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < count; index++)
            {
                var key = index.ToString("D5", CultureInfo.InvariantCulture);
                if (!indexSection.TryGetValue(key, out var achievementId) ||
                    string.IsNullOrWhiteSpace(achievementId))
                {
                    error = "an indexed achievement entry was missing";
                    return false;
                }

                achievementId = achievementId.Trim();
                if (!seenIds.Add(achievementId))
                {
                    error = "the indexed achievement list contained a duplicate ID";
                    return false;
                }

                if (!definitionIds.Contains(achievementId))
                {
                    error = "the state listed an achievement ID not present in the official schema: " + achievementId;
                    return false;
                }

                unlockedIds.Add(achievementId);
            }

            var normalized = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var achievementId in unlockedIds.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!sections.TryGetValue(achievementId, out var stateSection))
                {
                    error = "a listed achievement did not have a matching state section: " + achievementId;
                    return false;
                }

                if (!stateSection.TryGetValue("Achieved", out var achievedValue) ||
                    achievedValue.Trim() != "1")
                {
                    error = "a listed achievement was not marked Achieved=1: " + achievementId;
                    return false;
                }

                var state = new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["earned"] = true
                };

                if (stateSection.TryGetValue("UnlockTime", out var unlockValue))
                {
                    if (!long.TryParse(unlockValue, NumberStyles.None, CultureInfo.InvariantCulture, out var unlockTime) ||
                        unlockTime < 0 ||
                        unlockTime > MaximumUnixTime)
                    {
                        error = "an achievement had an invalid Unix UnlockTime: " + achievementId;
                        return false;
                    }

                    state["earned_time"] = unlockTime;
                }

                if (!TryAddProgress(stateSection, state, "CurProgress", "progress", out error) ||
                    !TryAddProgress(stateSection, state, "MaxProgress", "max_progress", out error))
                {
                    error += ": " + achievementId;
                    return false;
                }

                normalized[achievementId] = state;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(serializer.Serialize(normalized));
                AtomicWriteIfChanged(normalizedStatePath, bytes);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                error = "the extension-owned normalized state cache could not be written: " + exception.Message;
                return false;
            }
        }

        private bool TryLoadDefinitionIds(
            string path,
            out HashSet<string> ids,
            out string error)
        {
            ids = null;
            error = string.Empty;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.Length > MaximumDefinitionBytes)
                {
                    error = "the cached definition file was missing, empty, or oversized";
                    return false;
                }

                var root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as object[];
                if (root == null || root.Length == 0 || root.Length > MaximumAchievements)
                {
                    error = "the cached definition JSON was not a bounded non-empty array";
                    return false;
                }

                ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in root)
                {
                    var definition = item as Dictionary<string, object>;
                    if (definition == null ||
                        !definition.TryGetValue("name", out var value) ||
                        string.IsNullOrWhiteSpace(Convert.ToString(value)) ||
                        !ids.Add(Convert.ToString(value).Trim()))
                    {
                        error = "the cached definition JSON contained a malformed or duplicate achievement ID";
                        ids = null;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                error = exception.Message;
                ids = null;
                return false;
            }
        }

        private static bool TryReadIni(
            string path,
            out Dictionary<string, Dictionary<string, string>> sections,
            out string error)
        {
            sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.Length > MaximumRuneStateBytes)
                {
                    error = "the RUNE state file was missing, empty, or exceeded 4 MiB";
                    return false;
                }

                Dictionary<string, string> current = null;
                foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = (rawLine ?? string.Empty).Trim().Trim('\ufeff');
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        var name = line.Substring(1, line.Length - 2).Trim();
                        if (name.Length == 0 || sections.ContainsKey(name))
                        {
                            error = "the RUNE state contained an empty or duplicate section";
                            return false;
                        }

                        current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        sections[name] = current;
                        continue;
                    }

                    var separator = line.IndexOf('=');
                    if (current == null || separator <= 0)
                    {
                        error = "the RUNE state contained a value outside a section or without a key";
                        return false;
                    }

                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    if (key.Length == 0 || current.ContainsKey(key))
                    {
                        error = "the RUNE state contained an empty or duplicate key";
                        return false;
                    }

                    current[key] = value;
                }

                return sections.Count > 0;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                error = exception.Message;
                sections = null;
                return false;
            }
        }

        private static bool TryAddProgress(
            IDictionary<string, string> source,
            IDictionary<string, object> target,
            string sourceKey,
            string targetKey,
            out string error)
        {
            error = string.Empty;
            if (!source.TryGetValue(sourceKey, out var value))
            {
                return true;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                double.IsNaN(parsed) ||
                double.IsInfinity(parsed) ||
                parsed < 0)
            {
                error = "an achievement had invalid progress data";
                return false;
            }

            target[targetKey] = parsed;
            return true;
        }

        private static void AtomicWriteIfChanged(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                var existing = File.ReadAllBytes(path);
                if (existing.Length == bytes.Length && existing.SequenceEqual(bytes))
                {
                    return;
                }
            }

            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var replacement = path + "." + Guid.NewGuid().ToString("N") + ".replace";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, replacement);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                DeleteIfExists(temporary);
                DeleteIfExists(replacement);
            }
        }

        private static IEnumerable<string> FindSteamSettingsDirectories(
            string installDirectory,
            ICollection<string> diagnostics)
        {
            var root = TryFullPath(installDirectory);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                yield break;
            }

            var queue = new Queue<Tuple<DirectoryInfo, int>>();
            queue.Enqueue(Tuple.Create(new DirectoryInfo(root), 0));
            var visited = 0;
            while (queue.Count > 0 && visited < MaximumDirectories)
            {
                var item = queue.Dequeue();
                visited++;
                if (string.Equals(item.Item1.Name, "steam_settings", StringComparison.OrdinalIgnoreCase))
                {
                    yield return item.Item1.FullName;
                    continue;
                }

                if (item.Item2 >= MaximumDepth)
                {
                    continue;
                }

                DirectoryInfo[] children;
                try
                {
                    children = item.Item1.GetDirectories();
                }
                catch (Exception exception)
                {
                    diagnostics?.Add("A configuration directory could not be enumerated: " + exception.Message);
                    continue;
                }

                foreach (var child in children.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        queue.Enqueue(Tuple.Create(child, item.Item2 + 1));
                    }
                }
            }
        }

        private static Dictionary<string, string> ReadUserSaveOptions(
            string path,
            ICollection<string> diagnostics)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return result;
            }

            try
            {
                if (new FileInfo(path).Length > 1024 * 1024)
                {
                    diagnostics?.Add("A configs.user.ini file exceeded the 1 MiB limit.");
                    return result;
                }

                var inSaveSection = false;
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = (rawLine ?? string.Empty).Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inSaveSection = string.Equals(
                            line.Substring(1, line.Length - 2).Trim(),
                            "user::saves",
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    var separator = line.IndexOf('=');
                    if (!inSaveSection || separator <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim().Trim('"');
                    if (key == "local_save_path" || key == "saves_folder_name")
                    {
                        result[key] = value;
                    }
                }
            }
            catch (Exception exception)
            {
                diagnostics?.Add("A configs.user.ini file could not be read: " + exception.Message);
            }

            return result;
        }

        private static void AddExplicitFile(
            string path,
            string expectedName,
            ICollection<string> target,
            ICollection<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path.Trim());
                if (Directory.Exists(fullPath))
                {
                    fullPath = Path.Combine(fullPath, expectedName);
                }

                AddUnique(target, fullPath);
            }
            catch (Exception exception)
            {
                diagnostics?.Add("An explicit path was invalid: " + exception.Message);
            }
        }

        private static void AddExplicitState(
            string path,
            uint appId,
            GbeConfigurationResolution result)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path.Trim());
                if (string.Equals(Path.GetFileName(fullPath), "achievements.json", StringComparison.OrdinalIgnoreCase))
                {
                    var parentName = new DirectoryInfo(Path.GetDirectoryName(fullPath)).Name;
                    if (uint.TryParse(parentName, out var pathAppId) && pathAppId != appId)
                    {
                        result.Diagnostics.Add(
                            $"The explicit state file belongs to AppID {pathAppId}, not detected AppID {appId}; it was rejected.");
                        return;
                    }

                    AddUnique(result.StateCandidates, fullPath);
                    AddUnique(result.WatchDirectories, Path.GetDirectoryName(fullPath));
                    result.Diagnostics.Add(
                        uint.TryParse(parentName, out pathAppId)
                            ? $"The explicit state file matches detected AppID {appId}."
                            : "The explicit state file has no AppID directory evidence and was accepted only because the user selected it.");
                    return;
                }

                AddStateLayouts(fullPath, appId, result);
            }
            catch (Exception exception)
            {
                result.Diagnostics.Add("The explicit state path was invalid: " + exception.Message);
            }
        }

        private static void AddStateLayouts(string root, uint appId, GbeConfigurationResolution result)
        {
            var fullRoot = TryFullPath(root);
            if (string.IsNullOrWhiteSpace(fullRoot))
            {
                return;
            }

            var appDirectory = string.Equals(
                new DirectoryInfo(fullRoot).Name,
                appId.ToString(),
                StringComparison.OrdinalIgnoreCase)
                ? fullRoot
                : Path.Combine(fullRoot, appId.ToString());
            AddUnique(result.StateCandidates, Path.Combine(appDirectory, "achievements.json"));
            AddUnique(result.WatchDirectories, appDirectory);
        }

        private static string TryContainedPath(string root, string candidate)
        {
            try
            {
                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var normalizedCandidate = Path.GetFullPath(candidate);
                return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    ? normalizedCandidate
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryFullPath(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void AddUnique(ICollection<string> target, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value) ||
                target.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            target.Add(value);
        }
    }
}
