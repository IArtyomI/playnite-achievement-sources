using System.Collections.Generic;

namespace PlayniteAchievementSources.Detection
{
    public sealed class SteamAppIdDetectionResult
    {
        public SteamAppIdCandidate BestCandidate { get; set; }

        public IReadOnlyList<SteamAppIdCandidate> Candidates { get; set; } = new List<SteamAppIdCandidate>();

        public bool IsAmbiguous { get; set; }

        public string DiagnosticMessage { get; set; } = string.Empty;

        public bool HasResult => BestCandidate != null;
    }
}
