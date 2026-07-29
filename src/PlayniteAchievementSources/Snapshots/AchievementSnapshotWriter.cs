using PlayniteAchievementSources.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PlayniteAchievementSources.Snapshots
{
    public sealed class AchievementSnapshotWriter
    {
        private readonly AchievementSnapshotCatalog catalog = new AchievementSnapshotCatalog();

        public string Write(string pluginDataDirectory, AchievementSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(pluginDataDirectory))
            {
                throw new ArgumentException("A plugin data directory is required.", nameof(pluginDataDirectory));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.PlayniteGameId == Guid.Empty)
            {
                throw new ArgumentException("A snapshot must contain a Playnite game ID.", nameof(snapshot));
            }

            snapshot.Format = AchievementSnapshot.CurrentFormat;
            snapshot.SchemaVersion = AchievementSnapshot.CurrentSchemaVersion;
            snapshot.GeneratedAtUtc = DateTime.UtcNow;

            var snapshotDirectory = Path.Combine(
                pluginDataDirectory,
                "snapshots",
                $"v{AchievementSnapshot.CurrentSchemaVersion}");
            Directory.CreateDirectory(snapshotDirectory);

            var destinationPath = Path.Combine(snapshotDirectory, $"{snapshot.PlayniteGameId:N}.json");
            var signaturePath = destinationPath + ".content.sha256";
            var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            var originalGeneratedAt = snapshot.GeneratedAtUtc;
            snapshot.GeneratedAtUtc = DateTime.MinValue;
            var contentSignature = ComputeSha256(serializer.Serialize(CreatePayload(snapshot)));
            snapshot.GeneratedAtUtc = originalGeneratedAt;
            if (File.Exists(destinationPath) &&
                File.Exists(signaturePath) &&
                string.Equals(File.ReadAllText(signaturePath).Trim(), contentSignature, StringComparison.OrdinalIgnoreCase))
            {
                return destinationPath;
            }

            snapshot.GeneratedAtUtc = DateTime.UtcNow;

            try
            {
                var json = serializer.Serialize(CreatePayload(snapshot));
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                if (File.Exists(destinationPath))
                {
                    File.Replace(temporaryPath, destinationPath, null);
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }

                catalog.Update(pluginDataDirectory, snapshot, destinationPath);
                WriteTextAtomically(signaturePath, contentSignature);
                return destinationPath;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string ComputeSha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm
                    .ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))
                    .Select(item => item.ToString("X2", CultureInfo.InvariantCulture)));
            }
        }

        private static void WriteTextAtomically(string destinationPath, string value)
        {
            var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, value, new UTF8Encoding(false));
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

        private static IDictionary<string, object> CreatePayload(AchievementSnapshot snapshot)
        {
            var achievements = (snapshot.Achievements ?? new List<AchievementRecord>())
                .Select(record => (object)new Dictionary<string, object>
                {
                    ["SourceKey"] = record.SourceKey ?? string.Empty,
                    ["SourceGameId"] = record.SourceGameId ?? string.Empty,
                    ["AchievementId"] = record.AchievementId ?? string.Empty,
                    ["DisplayName"] = record.DisplayName ?? string.Empty,
                    ["Description"] = record.Description ?? string.Empty,
                    ["LockedIconPath"] = record.LockedIconPath ?? string.Empty,
                    ["UnlockedIconPath"] = record.UnlockedIconPath ?? string.Empty,
                    ["IsHidden"] = record.IsHidden,
                    ["IsUnlocked"] = record.IsUnlocked,
                    ["UnlockTimeUtc"] = record.UnlockTimeUtc.HasValue
                        ? record.UnlockTimeUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
                        : null,
                    ["CurrentProgress"] = record.CurrentProgress,
                    ["MaximumProgress"] = record.MaximumProgress,
                    ["EvidencePath"] = record.EvidencePath ?? string.Empty
                })
                .ToList();

            return new Dictionary<string, object>
            {
                ["Format"] = AchievementSnapshot.CurrentFormat,
                ["SchemaVersion"] = AchievementSnapshot.CurrentSchemaVersion,
                ["GeneratedAtUtc"] = snapshot.GeneratedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                ["PlayniteGameId"] = snapshot.PlayniteGameId.ToString("D"),
                ["PlayniteGameName"] = snapshot.PlayniteGameName ?? string.Empty,
                ["OverrideMode"] = snapshot.OverrideMode.ToString(),
                ["EffectiveTrackingMode"] = snapshot.EffectiveTrackingMode.ToString(),
                ["SourceKey"] = snapshot.SourceKey ?? string.Empty,
                ["SourceGameId"] = snapshot.SourceGameId ?? string.Empty,
                ["StateKnown"] = snapshot.StateKnown,
                ["IsCompleteSnapshot"] = snapshot.IsCompleteSnapshot,
                ["DefinitionPath"] = snapshot.DefinitionPath ?? string.Empty,
                ["StatePath"] = snapshot.StatePath ?? string.Empty,
                ["Achievements"] = achievements,
                ["Diagnostics"] = snapshot.Diagnostics ?? new List<string>()
            };
        }
    }
}
