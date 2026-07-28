using PlayniteAchievementSources.Models;

namespace PlayniteAchievementSources.Services
{
    public static class GameTrackingPolicy
    {
        public static AchievementTrackingMode Resolve(
            AchievementTrackingMode globalMode,
            GameTrackingOverride trackingOverride)
        {
            if (trackingOverride == null || trackingOverride.Mode == AchievementTrackingMode.Inherit)
            {
                return globalMode;
            }

            return trackingOverride.Mode;
        }
    }
}
