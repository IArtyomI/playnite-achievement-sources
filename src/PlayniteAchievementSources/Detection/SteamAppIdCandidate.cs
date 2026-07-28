namespace PlayniteAchievementSources.Detection
{
    public sealed class SteamAppIdCandidate
    {
        public uint AppId { get; set; }

        public string SourceKind { get; set; } = string.Empty;

        public string Evidence { get; set; } = string.Empty;

        public int Priority { get; set; }
    }
}
