using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteAchievementSources.Detection;
using PlayniteAchievementSources.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace PlayniteAchievementSources
{
    public sealed class AchievementSourcesPlugin : GenericPlugin
    {
        public static readonly Guid PluginId = Guid.Parse("0391911C-BF98-4A2D-8200-8641AF0973E9");

        private readonly SteamAppIdDetector steamAppIdDetector = new SteamAppIdDetector();

        public override Guid Id => PluginId;

        public AchievementSourcesSettingsViewModel Settings { get; }

        public AchievementSourcesPlugin(IPlayniteAPI playniteApi)
            : base(playniteApi)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            Settings = new AchievementSourcesSettingsViewModel(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new AchievementSourcesSettingsView();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@Achievement Sources",
                Description = "Open settings",
                Action = _ => OpenSettingsView()
            };

            yield return new MainMenuItem
            {
                MenuSection = "@Achievement Sources",
                Description = "Show development status",
                Action = _ => PlayniteApi.Dialogs.ShowMessage(
                    "Achievement Sources is installed. Settings and Steam AppID diagnostics are enabled. Achievement-state import and live monitoring are not enabled yet.",
                    "Achievement Sources")
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var game = args?.Games?.FirstOrDefault();
            if (game == null || args.Games.Count != 1)
            {
                yield break;
            }

            yield return new GameMenuItem
            {
                MenuSection = "Achievement Sources",
                Description = "Inspect Steam AppID sources",
                Action = _ => InspectSteamAppId(game)
            };
        }

        private void InspectSteamAppId(Game game)
        {
            var installDirectory = game.InstallDirectory ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(installDirectory))
            {
                try
                {
                    installDirectory = PlayniteApi.ExpandGameVariables(game, installDirectory);
                }
                catch
                {
                    // Keep the original path so diagnostics can explain that it was unusable.
                }
            }

            var context = new SteamAppIdDetectionContext
            {
                InstallDirectory = installDirectory,
                PlayniteSourceName = game.Source?.Name ?? string.Empty,
                PlayniteSourceGameId = game.GameId ?? string.Empty,
                LinkUrls = game.Links?
                    .Where(link => !string.IsNullOrWhiteSpace(link?.Url))
                    .Select(link => link.Url)
                    .ToList() ?? new List<string>()
            };

            var result = steamAppIdDetector.Detect(context);
            var message = new StringBuilder();
            message.AppendLine(game.Name);
            message.AppendLine();

            if (!result.HasResult)
            {
                message.AppendLine("No Steam AppID was detected.");
                message.AppendLine();
                message.AppendLine("Checked Playnite source metadata, Steam store links, and supported files inside the install directory.");
            }
            else
            {
                message.AppendLine($"Selected AppID: {result.BestCandidate.AppId}");
                message.AppendLine($"Source: {result.BestCandidate.SourceKind}");
                message.AppendLine($"Evidence: {result.BestCandidate.Evidence}");

                if (result.IsAmbiguous)
                {
                    message.AppendLine();
                    message.AppendLine("Conflicting AppIDs were found. Automatic tracking will require a per-game override until the conflict is resolved.");
                }

                message.AppendLine();
                message.AppendLine("Detected candidates:");
                foreach (var candidate in result.Candidates)
                {
                    message.AppendLine($"• {candidate.AppId} — {candidate.SourceKind}: {candidate.Evidence}");
                }
            }

            if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
            {
                message.AppendLine();
                message.AppendLine(result.DiagnosticMessage);
            }

            PlayniteApi.Dialogs.ShowMessage(message.ToString().TrimEnd(), "Achievement Sources — Steam AppID");
        }
    }
}
