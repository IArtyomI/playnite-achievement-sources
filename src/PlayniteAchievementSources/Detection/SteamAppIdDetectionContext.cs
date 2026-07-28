using System.Collections.Generic;

namespace PlayniteAchievementSources.Detection
{
    public sealed class SteamAppIdDetectionContext
    {
        public string InstallDirectory { get; set; } = string.Empty;

        public string PlayniteSourceName { get; set; } = string.Empty;

        public string PlayniteSourceGameId { get; set; } = string.Empty;

        public IReadOnlyList<string> LinkUrls { get; set; } = new List<string>();
    }
}
