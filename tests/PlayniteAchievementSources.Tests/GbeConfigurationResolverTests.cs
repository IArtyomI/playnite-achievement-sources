using PlayniteAchievementSources.Sources.Gbe;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class GbeConfigurationResolverTests : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "AchievementSourcesResolverTests",
            Guid.NewGuid().ToString("N"));

        public GbeConfigurationResolverTests()
        {
            Directory.CreateDirectory(root);
        }

        [Fact]
        public void ResolvesRecognizedSettingsAndDefaultSaveLayouts()
        {
            var install = Path.Combine(root, "game");
            var settings = Path.Combine(install, "steam_settings");
            var appData = Path.Combine(root, "appdata");
            Directory.CreateDirectory(settings);

            var result = new GbeConfigurationResolver().Resolve(632470, install, appData, null, null);

            Assert.Contains(Path.Combine(settings, "achievements.json"), result.DefinitionCandidates);
            Assert.Contains(Path.Combine(appData, "GSE Saves", "632470", "achievements.json"), result.StateCandidates);
            Assert.Contains(Path.Combine(appData, "Goldberg SteamEmu Saves", "632470", "achievements.json"), result.StateCandidates);
        }

        [Fact]
        public void ResolvesConfiguredLocalAndNamedSaveRoots()
        {
            var install = Path.Combine(root, "game");
            var settings = Path.Combine(install, "bin", "steam_settings");
            var appData = Path.Combine(root, "appdata");
            Directory.CreateDirectory(settings);
            File.WriteAllText(
                Path.Combine(settings, "configs.user.ini"),
                "[user::saves]\nlocal_save_path=portable\nsaves_folder_name=Custom Saves\n");

            var result = new GbeConfigurationResolver().Resolve(10, install, appData, null, null);

            Assert.Contains(Path.Combine(install, "bin", "portable", "10", "achievements.json"), result.StateCandidates);
            Assert.Contains(Path.Combine(appData, "Custom Saves", "10", "achievements.json"), result.StateCandidates);
        }

        [Fact]
        public void ExplicitStatePathIsWiredAndWrongAppIdIsRejected()
        {
            var correct = Path.Combine(root, "632470", "achievements.json");
            var wrong = Path.Combine(root, "10", "achievements.json");

            var accepted = new GbeConfigurationResolver().Resolve(632470, null, null, null, correct);
            var rejected = new GbeConfigurationResolver().Resolve(632470, null, null, null, wrong);

            Assert.Contains(correct, accepted.StateCandidates);
            Assert.DoesNotContain(wrong, rejected.StateCandidates);
            Assert.Contains(rejected.Diagnostics, value => value.Contains("was rejected"));
        }

        [Fact]
        public void TraversalIsBoundedAndReparseDirectoriesAreNotRequired()
        {
            var install = Path.Combine(root, "game");
            var deep = install;
            for (var index = 0; index < 8; index++)
            {
                deep = Path.Combine(deep, "d" + index);
            }

            Directory.CreateDirectory(Path.Combine(deep, "steam_settings"));
            var result = new GbeConfigurationResolver().Resolve(10, install, null, null, null);
            Assert.Empty(result.DefinitionCandidates);
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
