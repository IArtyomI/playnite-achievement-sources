using PlayniteAchievementSources.Models;
using PlayniteAchievementSources.Snapshots;
using System;
using System.IO;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class AchievementSnapshotCatalogTests
    {
        [Fact]
        public void Read_ReturnsValidatedDescriptorAndUpsertsByGameId()
        {
            var rootDirectory = CreateRoot();
            try
            {
                var gameId = Guid.NewGuid();
                var writer = new AchievementSnapshotWriter();
                writer.Write(rootDirectory, CreateSnapshot(gameId, false, 1));
                writer.Write(rootDirectory, CreateSnapshot(gameId, true, 2));

                var result = new AchievementSnapshotCatalog().Read(rootDirectory);
                var descriptor = Assert.Single(result.Snapshots);

                Assert.True(result.IndexFound);
                Assert.Empty(result.Diagnostics);
                Assert.True(File.Exists(descriptor.FullPath));
                Assert.Equal(gameId.ToString("D"), descriptor.Entry.PlayniteGameId);
                Assert.True(descriptor.Entry.StateKnown);
                Assert.Equal(2, descriptor.Entry.AchievementCount);
            }
            finally
            {
                DeleteRoot(rootDirectory);
            }
        }

        [Fact]
        public void Read_RejectsPathTraversal()
        {
            var rootDirectory = CreateRoot();
            try
            {
                var indexPath = new AchievementSnapshotCatalog().GetIndexPath(rootDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath));
                File.WriteAllText(indexPath, @"{
  ""Format"": ""playnite-achievement-sources.index"",
  ""SchemaVersion"": 1,
  ""Entries"": [
    {
      ""PlayniteGameId"": ""11111111-1111-1111-1111-111111111111"",
      ""SnapshotRelativePath"": ""../../outside.json""
    }
  ]
}");

                var result = new AchievementSnapshotCatalog().Read(rootDirectory);

                Assert.Empty(result.Snapshots);
                Assert.Contains(result.Diagnostics, message => message.IndexOf("unsafe path traversal", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            finally
            {
                DeleteRoot(rootDirectory);
            }
        }

        [Fact]
        public void Read_RejectsSnapshotGameIdMismatch()
        {
            var rootDirectory = CreateRoot();
            try
            {
                var gameId = Guid.NewGuid();
                var snapshotPath = new AchievementSnapshotWriter().Write(rootDirectory, CreateSnapshot(gameId, true, 1));
                var json = File.ReadAllText(snapshotPath)
                    .Replace(gameId.ToString("D"), Guid.NewGuid().ToString("D"));
                File.WriteAllText(snapshotPath, json);

                var result = new AchievementSnapshotCatalog().Read(rootDirectory);

                Assert.Empty(result.Snapshots);
                Assert.Contains(result.Diagnostics, message => message.IndexOf("does not match", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            finally
            {
                DeleteRoot(rootDirectory);
            }
        }

        private static AchievementSnapshot CreateSnapshot(Guid gameId, bool stateKnown, int achievementCount)
        {
            var snapshot = new AchievementSnapshot
            {
                PlayniteGameId = gameId,
                PlayniteGameName = "Fixture game",
                SourceKey = "gbe-compatible",
                SourceGameId = "123456",
                StateKnown = stateKnown,
                IsCompleteSnapshot = stateKnown
            };

            for (var index = 0; index < achievementCount; index++)
            {
                snapshot.Achievements.Add(new AchievementRecord
                {
                    AchievementId = "ACH_" + index,
                    DisplayName = "Achievement " + index,
                    IsUnlocked = stateKnown && index == 0
                });
            }

            return snapshot;
        }

        private static string CreateRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "AchievementSourcesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string rootDirectory)
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }
    }
}
