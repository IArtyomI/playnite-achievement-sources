using Playnite.SDK.Models;
using PlayniteAchievementSources.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievementSources.Sources
{
    public interface IAchievementSource
    {
        string Key { get; }

        bool IsCapable(Game game, GameTrackingOverride trackingOverride);

        Task<AchievementSourceResult> ReadAsync(
            Game game,
            GameTrackingOverride trackingOverride,
            CancellationToken cancellationToken);
    }
}
