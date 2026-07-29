using PlayniteAchievementSources.Models;
using PlayniteAchievementSources.Snapshots;
using System;
using System.IO;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class AchievementSnapshotWriterTests
    {
        [Fact]
        public void Write_PreservesUnknownStatePublishesIndexAndLeavesNoTemporaryFile()
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), "AchievementSourcesTests", Guid.NewGuid().ToString("N"));
            try
            {
                var snapshot = new AchievementSnapshot
                {
                    PlayniteGameId = Guid.NewGuid(),
                    PlayniteGameName = "Fixture game",
                    SourceKey = "gbe-compatible",
                    SourceGameId = "123456",
                    StateKnown = false,
                    IsCompleteSnapshot = false,
                    Achievements =
                    {
                        new AchievementRecord
                        {
                            AchievementId = "ACH_ONE",
                            DisplayName = "One",
                            IsUnlocked = false
                        }
                    }
                };

                var path = new AchievementSnapshotWriter().Write(rootDirectory, snapshot);
                var json = File.ReadAllText(path);
                var indexPath = new AchievementSnapshotCatalog().GetIndexPath(rootDirectory);
                var indexJson = File.ReadAllText(indexPath);

                Assert.True(File.Exists(path));
                Assert.True(File.Exists(indexPath));
                Assert.Contains("\"Format\":\"playnite-achievement-sources.snapshot\"", json);
                Assert.Contains("\"SchemaVersion\":1", json);
                Assert.Contains("\"StateKnown\":false", json);
                Assert.Contains("\"IsCompleteSnapshot\":false", json);
                Assert.Contains("\"Format\":\"playnite-achievement-sources.index\"", indexJson);
                Assert.Contains("\"SnapshotRelativePath\":\"snapshots/v1/", indexJson);
                Assert.DoesNotContain(rootDirectory, indexJson);
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp", SearchOption.TopDirectoryOnly));
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(indexPath), "*.tmp", SearchOption.TopDirectoryOnly));
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, true);
                }
            }
        }
    }
}
