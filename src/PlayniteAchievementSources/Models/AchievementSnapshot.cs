using System;
using System.Collections.Generic;

namespace PlayniteAchievementSources.Models
{
    public sealed class AchievementSnapshot
    {
        public const string CurrentFormat = "playnite-achievement-sources.snapshot";
        public const int CurrentSchemaVersion = 1;

        public string Format { get; set; } = CurrentFormat;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        public Guid PlayniteGameId { get; set; }

        public string PlayniteGameName { get; set; } = string.Empty;

        public AchievementTrackingMode OverrideMode { get; set; } = AchievementTrackingMode.Inherit;

        public AchievementTrackingMode EffectiveTrackingMode { get; set; } = AchievementTrackingMode.Automatic;

        public string SourceKey { get; set; } = string.Empty;

        public string SourceGameId { get; set; } = string.Empty;

        public bool StateKnown { get; set; }

        public bool IsCompleteSnapshot { get; set; }

        public string DefinitionPath { get; set; } = string.Empty;

        public string StatePath { get; set; } = string.Empty;

        public List<AchievementRecord> Achievements { get; set; } = new List<AchievementRecord>();

        public List<string> Diagnostics { get; set; } = new List<string>();
    }
}
