using System;

namespace PlayniteAchievementSources.Models
{
    public sealed class GameTrackingOverride
    {
        public Guid PlayniteGameId { get; set; }

        public AchievementTrackingMode Mode { get; set; } = AchievementTrackingMode.Inherit;

        public string PreferredSourceKey { get; set; } = string.Empty;

        public string LocalAdapterKey { get; set; } = string.Empty;

        public string SourceGameId { get; set; } = string.Empty;

        public string StatePath { get; set; } = string.Empty;

        public string DefinitionPath { get; set; } = string.Empty;
    }
}
