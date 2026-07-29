using PlayniteAchievementSources.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteAchievementSources.Tracking
{
    public static class GameTrackingPolicy
    {
        public static AchievementTrackingMode Resolve(
            AchievementTrackingMode defaultMode,
            IEnumerable<GameTrackingOverride> overrides,
            Guid playniteGameId)
        {
            var gameOverride = overrides?
                .LastOrDefault(item => item != null && item.PlayniteGameId == playniteGameId);

            return gameOverride == null || gameOverride.Mode == AchievementTrackingMode.Inherit
                ? defaultMode
                : gameOverride.Mode;
        }

        public static AchievementTrackingMode GetOverrideMode(
            IEnumerable<GameTrackingOverride> overrides,
            Guid playniteGameId)
        {
            return overrides?
                .LastOrDefault(item => item != null && item.PlayniteGameId == playniteGameId)?
                .Mode ?? AchievementTrackingMode.Inherit;
        }

        public static void SetOverride(
            IList<GameTrackingOverride> overrides,
            Guid playniteGameId,
            AchievementTrackingMode mode)
        {
            if (overrides == null)
            {
                throw new ArgumentNullException(nameof(overrides));
            }

            GameTrackingOverride existing = null;
            for (var index = overrides.Count - 1; index >= 0; index--)
            {
                var item = overrides[index];
                if (item == null || item.PlayniteGameId != playniteGameId)
                {
                    continue;
                }

                if (existing == null)
                {
                    existing = item;
                }
                else
                {
                    overrides.RemoveAt(index);
                }
            }

            if (mode == AchievementTrackingMode.Inherit)
            {
                if (existing != null)
                {
                    overrides.Remove(existing);
                }

                return;
            }

            if (existing == null)
            {
                existing = new GameTrackingOverride
                {
                    PlayniteGameId = playniteGameId
                };
                overrides.Add(existing);
            }

            existing.Mode = mode;
        }
    }
}
