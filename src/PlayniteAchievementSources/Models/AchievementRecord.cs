using System;

namespace PlayniteAchievementSources.Models
{
    public sealed class AchievementRecord
    {
        public string SourceKey { get; set; } = string.Empty;

        public string SourceGameId { get; set; } = string.Empty;

        public string AchievementId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string LockedIconPath { get; set; } = string.Empty;

        public string UnlockedIconPath { get; set; } = string.Empty;

        public bool IsHidden { get; set; }

        public bool IsUnlocked { get; set; }

        public DateTime? UnlockTimeUtc { get; set; }

        public double? CurrentProgress { get; set; }

        public double? MaximumProgress { get; set; }

        public string EvidencePath { get; set; } = string.Empty;
    }
}
