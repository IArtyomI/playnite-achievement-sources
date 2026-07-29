using PlayniteAchievementSources.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PlayniteAchievementSources.Snapshots
{
    public sealed class AchievementSnapshotCatalog
    {
        public const string ProducerId = "0391911C-BF98-4A2D-8200-8641AF0973E9";
        public const string ProducerName = "Achievement Sources";
        public const string ProducerVersion = "0.5.0";

        public string Update(string pluginDataDirectory, AchievementSnapshot snapshot, string snapshotPath)
        {
            ValidatePluginDataDirectory(pluginDataDirectory);
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.PlayniteGameId == Guid.Empty)
            {
                throw new ArgumentException("A snapshot must contain a Playnite game ID.", nameof(snapshot));
            }

            var relativePath = GetSafeRelativePath(pluginDataDirectory, snapshotPath);
            var indexPath = GetIndexPath(pluginDataDirectory);
            var index = LoadExistingIndex(indexPath) ?? CreateEmptyIndex();
            var gameId = snapshot.PlayniteGameId.ToString("D");
            var entries = index.Entries ?? new List<AchievementSnapshotIndexEntry>();
            entries.RemoveAll(entry => string.Equals(entry?.PlayniteGameId, gameId, StringComparison.OrdinalIgnoreCase));
            entries.Add(new AchievementSnapshotIndexEntry
            {
                PlayniteGameId = gameId,
                PlayniteGameName = snapshot.PlayniteGameName ?? string.Empty,
                SnapshotRelativePath = relativePath,
                SnapshotGeneratedAtUtc = snapshot.GeneratedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                SourceKey = snapshot.SourceKey ?? string.Empty,
                SourceGameId = snapshot.SourceGameId ?? string.Empty,
                StateKnown = snapshot.StateKnown,
                IsCompleteSnapshot = snapshot.IsCompleteSnapshot,
                AchievementCount = snapshot.Achievements?.Count ?? 0
            });

            index.Format = AchievementSnapshotIndex.CurrentFormat;
            index.SchemaVersion = AchievementSnapshotIndex.CurrentSchemaVersion;
            index.ProducerId = ProducerId;
            index.ProducerName = ProducerName;
            index.ProducerVersion = ProducerVersion;
            index.GeneratedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            index.Entries = entries
                .Where(entry => entry != null)
                .OrderBy(entry => entry.PlayniteGameId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            WriteAtomically(indexPath, Serialize(index));
            return indexPath;
        }

        public AchievementSnapshotCatalogReadResult Read(string pluginDataDirectory)
        {
            ValidatePluginDataDirectory(pluginDataDirectory);
            var result = new AchievementSnapshotCatalogReadResult
            {
                IndexPath = GetIndexPath(pluginDataDirectory)
            };

            if (!File.Exists(result.IndexPath))
            {
                return result;
            }

            result.IndexFound = true;
            AchievementSnapshotIndex index;
            try
            {
                index = Deserialize(File.ReadAllText(result.IndexPath));
                ValidateIndex(index);
            }
            catch (Exception exception)
            {
                result.Diagnostics.Add("The snapshot index could not be read: " + exception.Message);
                return result;
            }

            foreach (var entry in index.Entries ?? new List<AchievementSnapshotIndexEntry>())
            {
                if (entry == null)
                {
                    result.Diagnostics.Add("The snapshot index contained an empty entry.");
                    continue;
                }

                if (!Guid.TryParse(entry.PlayniteGameId, out var indexedGameId))
                {
                    result.Diagnostics.Add("A snapshot index entry contained an invalid Playnite game ID.");
                    continue;
                }

                if (!TryResolveSafePath(pluginDataDirectory, entry.SnapshotRelativePath, out var fullPath, out var pathError))
                {
                    result.Diagnostics.Add(pathError);
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    result.Diagnostics.Add($"The indexed snapshot file does not exist: {entry.SnapshotRelativePath}");
                    continue;
                }

                if (!TryValidateSnapshotHeader(fullPath, indexedGameId, out var snapshotError))
                {
                    result.Diagnostics.Add(snapshotError);
                    continue;
                }

                result.Snapshots.Add(new AchievementSnapshotDescriptor
                {
                    Entry = entry,
                    FullPath = fullPath
                });
            }

            return result;
        }

        public string GetIndexPath(string pluginDataDirectory)
        {
            return Path.Combine(
                pluginDataDirectory,
                "bridge",
                $"v{AchievementSnapshotIndex.CurrentSchemaVersion}",
                "index.json");
        }

        private static AchievementSnapshotIndex CreateEmptyIndex()
        {
            return new AchievementSnapshotIndex
            {
                ProducerId = ProducerId,
                ProducerName = ProducerName,
                ProducerVersion = ProducerVersion
            };
        }

        private static AchievementSnapshotIndex LoadExistingIndex(string indexPath)
        {
            if (!File.Exists(indexPath))
            {
                return null;
            }

            var index = Deserialize(File.ReadAllText(indexPath));
            ValidateIndex(index);
            return index;
        }

        private static void ValidateIndex(AchievementSnapshotIndex index)
        {
            if (index == null)
            {
                throw new InvalidDataException("The snapshot index is empty.");
            }

            if (!string.Equals(index.Format, AchievementSnapshotIndex.CurrentFormat, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The snapshot index format is not supported.");
            }

            if (index.SchemaVersion != AchievementSnapshotIndex.CurrentSchemaVersion)
            {
                throw new InvalidDataException($"Snapshot index schema version {index.SchemaVersion} is not supported.");
            }
        }

        private static string GetSafeRelativePath(string pluginDataDirectory, string snapshotPath)
        {
            if (string.IsNullOrWhiteSpace(snapshotPath))
            {
                throw new ArgumentException("A snapshot path is required.", nameof(snapshotPath));
            }

            var root = GetRootWithSeparator(pluginDataDirectory);
            var fullPath = Path.GetFullPath(snapshotPath);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The snapshot path is outside the plugin data directory.");
            }

            return fullPath.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static bool TryResolveSafePath(
            string pluginDataDirectory,
            string relativePath,
            out string fullPath,
            out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                error = "The snapshot index contained an unsafe rooted or empty path.";
                return false;
            }

            try
            {
                var root = GetRootWithSeparator(pluginDataDirectory);
                var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                fullPath = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    error = "The snapshot index contained an unsafe path traversal.";
                    fullPath = string.Empty;
                    return false;
                }

                if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    error = "The snapshot index referenced a non-JSON file.";
                    fullPath = string.Empty;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "The snapshot index path could not be resolved: " + exception.Message;
                fullPath = string.Empty;
                return false;
            }
        }

        private static bool TryValidateSnapshotHeader(string snapshotPath, Guid indexedGameId, out string error)
        {
            error = string.Empty;
            try
            {
                var serializer = CreateSerializer();
                var payload = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(snapshotPath));
                if (payload == null)
                {
                    error = "An indexed snapshot is empty.";
                    return false;
                }

                var format = ReadString(payload, "Format");
                if (!string.Equals(format, AchievementSnapshot.CurrentFormat, StringComparison.Ordinal))
                {
                    error = "An indexed snapshot has an unsupported format.";
                    return false;
                }

                var schemaVersion = ReadInt32(payload, "SchemaVersion");
                if (schemaVersion != AchievementSnapshot.CurrentSchemaVersion)
                {
                    error = $"An indexed snapshot uses unsupported schema version {schemaVersion}.";
                    return false;
                }

                if (!Guid.TryParse(ReadString(payload, "PlayniteGameId"), out var snapshotGameId) || snapshotGameId != indexedGameId)
                {
                    error = "An indexed snapshot Playnite game ID does not match its catalog entry.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "An indexed snapshot could not be validated: " + exception.Message;
                return false;
            }
        }

        private static string ReadString(IDictionary<string, object> payload, string key)
        {
            return payload.TryGetValue(key, out var value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
        }

        private static int ReadInt32(IDictionary<string, object> payload, string key)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string GetRootWithSeparator(string pluginDataDirectory)
        {
            var root = Path.GetFullPath(pluginDataDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return root + Path.DirectorySeparatorChar;
        }

        private static void ValidatePluginDataDirectory(string pluginDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(pluginDataDirectory))
            {
                throw new ArgumentException("A plugin data directory is required.", nameof(pluginDataDirectory));
            }
        }

        private static string Serialize(AchievementSnapshotIndex index)
        {
            return CreateSerializer().Serialize(index);
        }

        private static AchievementSnapshotIndex Deserialize(string json)
        {
            return CreateSerializer().Deserialize<AchievementSnapshotIndex>(json);
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };
        }

        private static void WriteAtomically(string destinationPath, string content)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            Directory.CreateDirectory(directory);
            var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
                if (File.Exists(destinationPath))
                {
                    File.Replace(temporaryPath, destinationPath, null);
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
