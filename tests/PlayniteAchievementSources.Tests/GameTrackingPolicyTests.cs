using PlayniteAchievementSources.Models;
using PlayniteAchievementSources.Tracking;
using System;
using System.Collections.Generic;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class GameTrackingPolicyTests
    {
        [Fact]
        public void Resolve_UsesPerGameOverrideAndFallsBackToDefault()
        {
            var firstGameId = Guid.NewGuid();
            var secondGameId = Guid.NewGuid();
            var overrides = new List<GameTrackingOverride>
            {
                new GameTrackingOverride
                {
                    PlayniteGameId = firstGameId,
                    Mode = AchievementTrackingMode.LocalOnly
                }
            };

            Assert.Equal(
                AchievementTrackingMode.LocalOnly,
                GameTrackingPolicy.Resolve(AchievementTrackingMode.Automatic, overrides, firstGameId));
            Assert.Equal(
                AchievementTrackingMode.Automatic,
                GameTrackingPolicy.Resolve(AchievementTrackingMode.Automatic, overrides, secondGameId));
        }

        [Fact]
        public void SetOverride_ReplacesDuplicatesAndRemovesInheritance()
        {
            var gameId = Guid.NewGuid();
            var overrides = new List<GameTrackingOverride>
            {
                new GameTrackingOverride { PlayniteGameId = gameId, Mode = AchievementTrackingMode.Automatic },
                new GameTrackingOverride { PlayniteGameId = gameId, Mode = AchievementTrackingMode.LocalOnly }
            };

            GameTrackingPolicy.SetOverride(overrides, gameId, AchievementTrackingMode.Disabled);

            var gameOverride = Assert.Single(overrides);
            Assert.Equal(AchievementTrackingMode.Disabled, gameOverride.Mode);

            GameTrackingPolicy.SetOverride(overrides, gameId, AchievementTrackingMode.Inherit);
            Assert.Empty(overrides);
        }
    }
}
