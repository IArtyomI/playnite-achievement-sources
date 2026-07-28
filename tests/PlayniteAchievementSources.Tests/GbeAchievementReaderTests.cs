using PlayniteAchievementSources.Sources.Gbe;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class GbeAchievementReaderTests
    {
        [Fact]
        public void Read_NormalizesDefinitionsUnlocksAndProgress()
        {
            using (var fixture = new TemporaryFixture())
            {
                var settingsDirectory = Path.Combine(fixture.InstallDirectory, "Game_Data", "Plugins", "x86_64", "steam_settings");
                Directory.CreateDirectory(Path.Combine(settingsDirectory, "achievement_images"));
                File.WriteAllText(Path.Combine(settingsDirectory, "achievement_images", "done.png"), "image");
                File.WriteAllText(Path.Combine(settingsDirectory, "achievement_images", "done_gray.png"), "image");
                File.WriteAllText(Path.Combine(settingsDirectory, "achievements.json"), @"
[
  {
    ""name"": ""ACH_LOCKED"",
    ""displayName"": { ""english"": ""Locked achievement"", ""french"": ""Verrouille"" },
    ""description"": ""Stay locked"",
    ""hidden"": ""0"",
    ""icon"": ""locked.png"",
    ""icon_gray"": ""locked_gray.png""
  },
  {
    ""name"": ""ACH_DONE"",
    ""displayName"": ""Completed achievement"",
    ""description"": { ""english"": ""Finish the task"" },
    ""hidden"": ""1"",
    ""icon"": ""done.png"",
    ""icongray"": ""done_gray.png""
  },
  {
    ""name"": ""ACH_PROGRESS"",
    ""displayName"": ""Progress achievement"",
    ""description"": ""Make progress"",
    ""hidden"": 0,
    ""icon"": ""progress.png"",
    ""icon_gray"": ""progress_gray.png""
  }
]");

                var stateDirectory = Path.Combine(fixture.SaveRoot, fixture.AppId.ToString());
                Directory.CreateDirectory(stateDirectory);
                File.WriteAllText(Path.Combine(stateDirectory, "achievements.json"), @"
{
  ""ACH_DONE"": { ""earned"": true, ""earned_time"": 1700000000 },
  ""ACH_PROGRESS"": { ""earned"": false, ""progress"": 3, ""max_progress"": 10 }
}");

                var result = new GbeAchievementReader().Read(fixture.CreateContext());

                Assert.True(result.HasDefinitions);
                Assert.True(result.HasState);
                Assert.True(result.IsCompleteSnapshot);
                Assert.Equal(3, result.Achievements.Count);

                var completed = result.Achievements.Single(item => item.AchievementId == "ACH_DONE");
                Assert.True(completed.IsUnlocked);
                Assert.True(completed.IsHidden);
                Assert.Equal("Completed achievement", completed.DisplayName);
                Assert.Equal("Finish the task", completed.Description);
                Assert.NotNull(completed.UnlockTimeUtc);
                Assert.EndsWith("done.png", completed.UnlockedIconPath, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith("done_gray.png", completed.LockedIconPath, StringComparison.OrdinalIgnoreCase);

                var progress = result.Achievements.Single(item => item.AchievementId == "ACH_PROGRESS");
                Assert.False(progress.IsUnlocked);
                Assert.True(progress.CurrentProgress.HasValue);
                Assert.True(progress.MaximumProgress.HasValue);
                Assert.Equal(3d, progress.CurrentProgress.Value);
                Assert.Equal(10d, progress.MaximumProgress.Value);

                var locked = result.Achievements.Single(item => item.AchievementId == "ACH_LOCKED");
                Assert.Equal("Locked achievement", locked.DisplayName);
                Assert.False(locked.IsUnlocked);
                Assert.False(locked.IsHidden);
            }
        }

        [Fact]
        public void Read_ReportsMissingStateHonestly()
        {
            using (var fixture = new TemporaryFixture())
            {
                var settingsDirectory = Path.Combine(fixture.InstallDirectory, "steam_settings");
                Directory.CreateDirectory(settingsDirectory);
                File.WriteAllText(Path.Combine(settingsDirectory, "achievements.json"), @"
[
  {
    ""name"": ""ACH_ONE"",
    ""displayName"": ""One"",
    ""description"": ""First"",
    ""hidden"": false
  }
]");

                var result = new GbeAchievementReader().Read(fixture.CreateContext());

                Assert.True(result.HasDefinitions);
                Assert.False(result.HasState);
                Assert.False(result.IsCompleteSnapshot);
                Assert.Single(result.Achievements);
                Assert.False(result.Achievements[0].IsUnlocked);
                Assert.Contains(result.Diagnostics, message => message.IndexOf("No supported AppID-scoped", StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        [Fact]
        public void Read_FailsSafelyForMalformedPartialDefinitionWrite()
        {
            using (var fixture = new TemporaryFixture())
            {
                var settingsDirectory = Path.Combine(fixture.InstallDirectory, "steam_settings");
                Directory.CreateDirectory(settingsDirectory);
                File.WriteAllText(Path.Combine(settingsDirectory, "achievements.json"), "[{\"name\":");

                var result = new GbeAchievementReader().Read(fixture.CreateContext());

                Assert.False(result.HasDefinitions);
                Assert.Empty(result.Achievements);
                Assert.Contains(result.Diagnostics, message => message.IndexOf("No supported", StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        private sealed class TemporaryFixture : IDisposable
        {
            private readonly string rootDirectory;

            public uint AppId { get; } = 123456;

            public string InstallDirectory { get; }

            public string SaveRoot { get; }

            public TemporaryFixture()
            {
                rootDirectory = Path.Combine(Path.GetTempPath(), "AchievementSourcesTests", Guid.NewGuid().ToString("N"));
                InstallDirectory = Path.Combine(rootDirectory, "Install");
                SaveRoot = Path.Combine(rootDirectory, "GSE Saves");
                Directory.CreateDirectory(InstallDirectory);
                Directory.CreateDirectory(SaveRoot);
            }

            public GbeAchievementReadContext CreateContext()
            {
                var context = new GbeAchievementReadContext
                {
                    AppId = AppId,
                    InstallDirectory = InstallDirectory,
                    PreferredLanguage = "english"
                };
                context.SaveRootDirectories.Add(SaveRoot);
                return context;
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(rootDirectory))
                    {
                        Directory.Delete(rootDirectory, true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
