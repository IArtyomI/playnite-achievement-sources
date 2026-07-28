using PlayniteAchievementSources.Models;
using System.Collections.Generic;

namespace PlayniteAchievementSources.Sources.Gbe
{
    public sealed class GbeAchievementReadResult
    {
        public string DefinitionPath { get; set; } = string.Empty;

        public string StatePath { get; set; } = string.Empty;

        public bool HasDefinitions { get; set; }

        public bool HasState { get; set; }

        public bool IsCompleteSnapshot { get; set; }

        public IList<AchievementRecord> Achievements { get; } = new List<AchievementRecord>();

        public IList<string> Diagnostics { get; } = new List<string>();
    }
}
