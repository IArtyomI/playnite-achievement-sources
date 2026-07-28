using PlayniteAchievementSources.Models;
using System.Collections.Generic;

namespace PlayniteAchievementSources.Sources
{
    public sealed class AchievementSourceResult
    {
        public string SourceKey { get; set; } = string.Empty;

        public string SourceGameId { get; set; } = string.Empty;

        public bool IsCompleteSnapshot { get; set; }

        public IList<AchievementRecord> Achievements { get; } = new List<AchievementRecord>();

        public IList<string> Diagnostics { get; } = new List<string>();
    }
}
