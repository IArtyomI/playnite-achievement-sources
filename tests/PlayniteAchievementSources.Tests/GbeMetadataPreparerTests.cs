using PlayniteAchievementSources.Metadata;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class GbeMetadataPreparerTests : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "AchievementSourcesMetadataTests",
            Guid.NewGuid().ToString("N"));

        public GbeMetadataPreparerTests()
        {
            Directory.CreateDirectory(root);
        }

        [Fact]
        public void ValidImportIsConfirmedAtomicAndIdempotent()
        {
            var source = WriteDefinitions("source.json", "ACH_ONE");
            var settings = Path.Combine(root, "steam_settings");
            Directory.CreateDirectory(settings);
            var preparer = new GbeMetadataPreparer();
            var plan = preparer.CreateImportPlan(10, source, settings);

            var result = preparer.Execute(plan, true, true);
            var repeated = preparer.Execute(plan, true, true);

            Assert.True(result.Success);
            Assert.True(result.Changed);
            Assert.True(repeated.Success);
            Assert.False(repeated.Changed);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(Path.Combine(settings, "achievements.json")));
            Assert.Empty(Directory.GetFiles(settings, "*.tmp"));
        }

        [Fact]
        public void ReplacementCreatesBackupAndPreservesOriginal()
        {
            var source = WriteDefinitions("source.json", "ACH_NEW");
            var settings = Path.Combine(root, "steam_settings");
            Directory.CreateDirectory(settings);
            var destination = Path.Combine(settings, "achievements.json");
            var original = "[{\"name\":\"ACH_OLD\",\"displayName\":\"Old\",\"description\":\"Old\"}]";
            File.WriteAllText(destination, original);
            var preparer = new GbeMetadataPreparer();

            var result = preparer.Execute(preparer.CreateImportPlan(10, source, settings), true, true);

            Assert.True(result.Success);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Equal(original, File.ReadAllText(result.BackupPath));
        }

        [Theory]
        [InlineData(false, true, "disabled")]
        [InlineData(true, false, "confirmation")]
        public void RefusesWriteWithoutBothGates(bool permission, bool confirmation, string expected)
        {
            var source = WriteDefinitions("source.json", "ACH_ONE");
            var settings = Path.Combine(root, "steam_settings");
            Directory.CreateDirectory(settings);
            var preparer = new GbeMetadataPreparer();

            var result = preparer.Execute(
                preparer.CreateImportPlan(10, source, settings),
                permission,
                confirmation);

            Assert.False(result.Success);
            Assert.Contains(expected, result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(settings, "achievements.json")));
        }

        [Fact]
        public void MalformedImportAndInvalidAppIdAreRejected()
        {
            var malformed = Path.Combine(root, "bad.json");
            File.WriteAllText(malformed, "{}");
            var settings = Path.Combine(root, "steam_settings");
            Directory.CreateDirectory(settings);
            var preparer = new GbeMetadataPreparer();

            Assert.False(preparer.CreateImportPlan(10, malformed, settings).IsValid);
            Assert.False(preparer.CreateImportPlan(0, malformed, settings).IsValid);
        }

        [Fact]
        public void PreparationNeverCreatesStatsOrRuntimeState()
        {
            var source = WriteDefinitions("source.json", "ACH_ONE");
            var settings = Path.Combine(root, "steam_settings");
            Directory.CreateDirectory(settings);
            var preparer = new GbeMetadataPreparer();

            var result = preparer.Execute(preparer.CreateImportPlan(10, source, settings), true, true);

            Assert.True(result.Success);
            Assert.False(File.Exists(Path.Combine(settings, "stats.json")));
            Assert.Single(
                Directory.GetFiles(settings),
                path => Path.GetFileName(path) == "achievements.json");
        }

        [Fact]
        public void ReadBackFailureAutomaticallyRestoresOriginal()
        {
            var source = WriteDefinitions("source.json", "ACH_NEW");
            var settings = Path.Combine(root, "steam_settings");
            Directory.CreateDirectory(settings);
            var destination = Path.Combine(settings, "achievements.json");
            var original = "[{\"name\":\"ACH_OLD\",\"displayName\":\"Old\",\"description\":\"Old\"}]";
            File.WriteAllText(destination, original);
            var preparer = new GbeMetadataPreparer(
                new ExplicitJsonDefinitionMetadataSource(),
                (_, __) => false);

            var result = preparer.Execute(
                preparer.CreateImportPlan(10, source, settings),
                true,
                true);

            Assert.False(result.Success);
            Assert.Equal(original, File.ReadAllText(destination));
            Assert.Empty(Directory.GetFiles(settings, "*.tmp"));
            Assert.Empty(Directory.GetFiles(settings, "*.restore"));
        }

        private string WriteDefinitions(string name, string apiName)
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(
                path,
                "[{\"name\":\"" + apiName + "\",\"displayName\":\"Display\",\"description\":\"Description\",\"hidden\":0}]");
            return path;
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
