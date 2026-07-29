using System.Collections.Generic;

namespace PlayniteAchievementSources.Models
{
    public sealed class AchievementSnapshotIndex
    {
        public const string CurrentFormat = "playnite-achievement-sources.index";
        public const int CurrentSchemaVersion = 1;

        public string Format { get; set; } = CurrentFormat;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string ProducerId { get; set; } = string.Empty;

        public string ProducerName { get; set; } = string.Empty;

        public string ProducerVersion { get; set; } = string.Empty;

        public string GeneratedAtUtc { get; set; } = string.Empty;

        public List<AchievementSnapshotIndexEntry> Entries { get; set; } = new List<AchievementSnapshotIndexEntry>();
    }

    public sealed class AchievementSnapshotIndexEntry
    {
        public string PlayniteGameId { get; set; } = string.Empty;

        public string PlayniteGameName { get; set; } = string.Empty;

        public string SnapshotRelativePath { get; set; } = string.Empty;

        public string SnapshotGeneratedAtUtc { get; set; } = string.Empty;

        public string SourceKey { get; set; } = string.Empty;

        public string SourceGameId { get; set; } = string.Empty;

        public bool StateKnown { get; set; }

        public bool IsCompleteSnapshot { get; set; }

        public int AchievementCount { get; set; }
    }
}
