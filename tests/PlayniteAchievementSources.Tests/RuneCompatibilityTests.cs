using PlayniteAchievementSources.Detection;
using PlayniteAchievementSources.Sources.Gbe;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class RuneCompatibilityTests : IDisposable
    {
        private const uint AppId = 2863680;
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "AchievementSourcesRuneTests",
            Guid.NewGuid().ToString("N"));
        private readonly string installDirectory;
        private readonly string runtimeDirectory;
        private readonly string appDataDirectory;
        private readonly string commonDocumentsDirectory;
        private readonly string runeStateDirectory;
        private readonly string runeStatePath;

        public RuneCompatibilityTests()
        {
            installDirectory = Path.Combine(root, "game");
            runtimeDirectory = Path.Combine(installDirectory, "Game_Data", "Plugins", "x86_64");
            appDataDirectory = Path.Combine(root, "AppData", "Roaming");
            commonDocumentsDirectory = Path.Combine(root, "Public", "Documents");
            runeStateDirectory = Path.Combine(commonDocumentsDirectory, "Steam", "RUNE", AppId.ToString());
            runeStatePath = Path.Combine(runeStateDirectory, "achievements.ini");

            Directory.CreateDirectory(runtimeDirectory);
            Directory.CreateDirectory(appDataDirectory);
            Directory.CreateDirectory(runeStateDirectory);
            File.WriteAllText(Path.Combine(runtimeDirectory, "steam_emu.ini"), "AppId=2863680\nUserName=Test\n");
            File.WriteAllText(Path.Combine(runtimeDirectory, "steam_api64.rne"), "marker");
        }

        [Fact]
        public void DetectorReadsRuneSteamEmuIni()
        {
            var result = new SteamAppIdDetector().Detect(new SteamAppIdDetectionContext
            {
                InstallDirectory = installDirectory
            });

            Assert.True(result.HasResult);
            Assert.False(result.IsAmbiguous);
            Assert.Equal(AppId, result.BestCandidate.AppId);
            Assert.Contains("RUNE", result.BestCandidate.SourceKind);
        }

        [Fact]
        public void ResolverCreatesExtensionOwnedDefinitionTargetAndWiresRuneState()
        {
            var result = CreateResolver().Resolve(AppId, installDirectory, appDataDirectory, null, null);

            var definitionPath = RuneDefinitionPath();
            var normalizedStatePath = RuneNormalizedStatePath();
            Assert.Contains(definitionPath, result.DefinitionCandidates);
            Assert.Contains(normalizedStatePath, result.StateCandidates);
            Assert.Contains(runeStatePath, result.StateCandidates);
            Assert.True(Directory.Exists(Path.GetDirectoryName(definitionPath)));
            Assert.Contains(result.Diagnostics, value => value.IndexOf("RUNE-compatible", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void EmptyRuneStateBecomesAuthoritativeLockedBaseline()
        {
            WriteDefinitions();
            File.WriteAllText(runeStatePath, "[SteamAchievements]\r\nCount=0\r\n\r\n");

            var resolution = CreateResolver().Resolve(AppId, installDirectory, appDataDirectory, null, null);
            var read = ReadResolved(resolution);

            Assert.True(read.HasDefinitions);
            Assert.True(read.HasState);
            Assert.True(read.IsCompleteSnapshot);
            Assert.Equal(3, read.Achievements.Count);
            Assert.All(read.Achievements, achievement => Assert.False(achievement.IsUnlocked));
        }

        [Fact]
        public void NamedAndNumericAchievementIdsRemainOpaqueStrings()
        {
            WriteDefinitions();
            File.WriteAllText(runeStatePath, @"[SteamAchievements]
00000=ACH_FIRST
00001=10
Count=2

[ACH_FIRST]
Achieved=1
CurProgress=0
MaxProgress=0
UnlockTime=1763014253

[10]
Achieved=1
CurProgress=2
MaxProgress=5
UnlockTime=1763015128
");

            var resolution = CreateResolver().Resolve(AppId, installDirectory, appDataDirectory, null, null);
            var read = ReadResolved(resolution);

            Assert.True(read.HasState);
            Assert.True(read.IsCompleteSnapshot);
            Assert.True(read.Achievements.Single(item => item.AchievementId == "ACH_FIRST").IsUnlocked);
            var numeric = read.Achievements.Single(item => item.AchievementId == "10");
            Assert.True(numeric.IsUnlocked);
            Assert.True(numeric.CurrentProgress.HasValue);
            Assert.True(numeric.MaximumProgress.HasValue);
            Assert.Equal(2d, numeric.CurrentProgress.Value);
            Assert.Equal(5d, numeric.MaximumProgress.Value);
            Assert.NotNull(numeric.UnlockTimeUtc);
            Assert.False(read.Achievements.Single(item => item.AchievementId == "ACH_LOCKED").IsUnlocked);
        }

        [Fact]
        public void InvalidRuneStateRemovesStaleNormalizedAuthority()
        {
            WriteDefinitions();
            File.WriteAllText(runeStatePath, "[SteamAchievements]\nCount=0\n");
            CreateResolver().Resolve(AppId, installDirectory, appDataDirectory, null, null);
            Assert.True(File.Exists(RuneNormalizedStatePath()));

            File.WriteAllText(runeStatePath, @"[SteamAchievements]
00000=UNKNOWN_ID
Count=1

[UNKNOWN_ID]
Achieved=1
UnlockTime=1763014253
");

            var resolution = CreateResolver().Resolve(AppId, installDirectory, appDataDirectory, null, null);

            Assert.False(File.Exists(RuneNormalizedStatePath()));
            Assert.Contains(resolution.Diagnostics, value => value.IndexOf("not present in the official schema", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void MalformedCountIsNonAuthoritative()
        {
            WriteDefinitions();
            File.WriteAllText(runeStatePath, @"[SteamAchievements]
00000=ACH_FIRST
Count=2

[ACH_FIRST]
Achieved=1
UnlockTime=1763014253
");

            var resolution = CreateResolver().Resolve(AppId, installDirectory, appDataDirectory, null, null);

            Assert.False(File.Exists(RuneNormalizedStatePath()));
            Assert.Contains(resolution.Diagnostics, value => value.IndexOf("did not match Count", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void UnchangedRuneStateDoesNotRewriteNormalizedCache()
        {
            WriteDefinitions();
            File.WriteAllText(runeStatePath, "[SteamAchievements]\nCount=0\n");
            var resolver = CreateResolver();
            resolver.Resolve(AppId, installDirectory, appDataDirectory, null, null);

            var normalized = RuneNormalizedStatePath();
            var markerTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(normalized, markerTime);

            resolver.Resolve(AppId, installDirectory, appDataDirectory, null, null);

            Assert.Equal(markerTime, File.GetLastWriteTimeUtc(normalized));
        }

        private GbeConfigurationResolver CreateResolver()
        {
            return new GbeConfigurationResolver(() => commonDocumentsDirectory);
        }

        private GbeAchievementReadResult ReadResolved(GbeConfigurationResolution resolution)
        {
            var context = new GbeAchievementReadContext
            {
                AppId = AppId,
                ExplicitDefinitionPath = RuneDefinitionPath(),
                PreferredLanguage = "english"
            };
            foreach (var statePath in resolution.StateCandidates)
            {
                context.ExpectedStatePaths.Add(statePath);
            }

            return new GbeAchievementReader().Read(context);
        }

        private void WriteDefinitions()
        {
            var path = RuneDefinitionPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, @"[
  { ""name"": ""ACH_FIRST"", ""displayName"": ""First"", ""description"": ""First"", ""hidden"": 0 },
  { ""name"": ""10"", ""displayName"": ""Ten"", ""description"": ""Numeric"", ""hidden"": 0 },
  { ""name"": ""ACH_LOCKED"", ""displayName"": ""Locked"", ""description"": ""Locked"", ""hidden"": 0 }
]");
        }

        private string RuneDefinitionPath()
        {
            return Path.Combine(
                appDataDirectory,
                "Playnite",
                "ExtensionsData",
                "0391911c-bf98-4a2d-8200-8641af0973e9",
                "metadata-cache",
                "rune",
                AppId.ToString(),
                "steam_settings",
                "achievements.json");
        }

        private string RuneNormalizedStatePath()
        {
            return Path.Combine(
                appDataDirectory,
                "Playnite",
                "ExtensionsData",
                "0391911c-bf98-4a2d-8200-8641af0973e9",
                "metadata-cache",
                "rune",
                AppId.ToString(),
                "state",
                "achievements.json");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch
            {
            }
        }
    }
}
