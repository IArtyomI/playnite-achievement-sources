using System.Collections.Generic;

namespace PlayniteAchievementSources.Sources.Gbe
{
    public sealed class GbeAchievementReadContext
    {
        public uint AppId { get; set; }

        public string InstallDirectory { get; set; } = string.Empty;

        public string ExplicitDefinitionPath { get; set; } = string.Empty;

        public string ExplicitStatePath { get; set; } = string.Empty;

        public string PreferredLanguage { get; set; } = "english";

        public IList<string> SaveRootDirectories { get; } = new List<string>();

        public IList<string> ExpectedStatePaths { get; } = new List<string>();
    }
}
