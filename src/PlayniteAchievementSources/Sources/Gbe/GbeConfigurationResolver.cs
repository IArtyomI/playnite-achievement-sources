using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            if (!string.IsNullOrWhiteSpace(appDataDirectory))
            {
                AddStateLayouts(Path.Combine(appDataDirectory, "GSE Saves"), appId, result);
                AddStateLayouts(Path.Combine(appDataDirectory, "Goldberg SteamEmu Saves"), appId, result);
            }

            return result;
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
