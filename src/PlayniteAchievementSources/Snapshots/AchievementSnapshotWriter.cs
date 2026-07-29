using PlayniteAchievementSources.Models;
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PlayniteAchievementSources.Snapshots
{
    public sealed class AchievementSnapshotWriter
    {
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
            var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            try
            {
                var json = serializer.Serialize(snapshot);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                if (File.Exists(destinationPath))
                {
                    File.Replace(temporaryPath, destinationPath, null);
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }

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
    }
}
