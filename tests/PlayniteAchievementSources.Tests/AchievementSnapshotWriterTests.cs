using PlayniteAchievementSources.Models;
using PlayniteAchievementSources.Snapshots;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class AchievementSnapshotWriterTests
    {
        [Fact]
        public void Write_PreservesUnknownStateAndLeavesNoTemporaryFile()
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

                Assert.True(File.Exists(path));
                Assert.Contains("\"Format\":\"playnite-achievement-sources.snapshot\"", json);
                Assert.Contains("\"SchemaVersion\":1", json);
                Assert.Contains("\"StateKnown\":false", json);
                Assert.Contains("\"IsCompleteSnapshot\":false", json);
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp", SearchOption.TopDirectoryOnly));
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
