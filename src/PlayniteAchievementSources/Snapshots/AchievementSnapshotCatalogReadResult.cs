using PlayniteAchievementSources.Models;
using System.Collections.Generic;

namespace PlayniteAchievementSources.Snapshots
{
    public sealed class AchievementSnapshotDescriptor
    {
        public AchievementSnapshotIndexEntry Entry { get; set; }

        public string FullPath { get; set; } = string.Empty;
    }

    public sealed class AchievementSnapshotCatalogReadResult
    {
        public bool IndexFound { get; set; }

        public string IndexPath { get; set; } = string.Empty;

        public IList<AchievementSnapshotDescriptor> Snapshots { get; } = new List<AchievementSnapshotDescriptor>();

        public IList<string> Diagnostics { get; } = new List<string>();
    }
}
