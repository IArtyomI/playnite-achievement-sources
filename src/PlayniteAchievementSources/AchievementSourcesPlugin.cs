using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteAchievementSources.Detection;
using PlayniteAchievementSources.Settings;
using PlayniteAchievementSources.Sources.Gbe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace PlayniteAchievementSources
{
    public sealed class AchievementSourcesPlugin : GenericPlugin
    {
        public static readonly Guid PluginId = Guid.Parse("0391911C-BF98-4A2D-8200-8641AF0973E9");

        private readonly SteamAppIdDetector steamAppIdDetector = new SteamAppIdDetector();
        private readonly GbeAchievementReader gbeAchievementReader = new GbeAchievementReader();

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
                    "Achievement Sources is installed. Settings, Steam AppID diagnostics, and read-only GBE/Goldberg-compatible achievement inspection are enabled. Import, live monitoring, notifications, and Playnite Achievements bridge output are not enabled yet.",
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

            yield return new GameMenuItem
            {
                MenuSection = "Achievement Sources",
                Description = "Inspect local achievement data",
                Action = _ => InspectLocalAchievementData(game)
            };
        }

        private SteamAppIdDetectionResult DetectSteamAppId(Game game)
        {
            var installDirectory = ExpandInstallDirectory(game);
            return steamAppIdDetector.Detect(new SteamAppIdDetectionContext
            {
                InstallDirectory = installDirectory,
                PlayniteSourceName = game.Source?.Name ?? string.Empty,
                PlayniteSourceGameId = game.GameId ?? string.Empty,
                LinkUrls = game.Links?
                    .Where(link => !string.IsNullOrWhiteSpace(link?.Url))
                    .Select(link => link.Url)
                    .ToList() ?? new List<string>()
            });
        }

        private void InspectSteamAppId(Game game)
        {
            var result = DetectSteamAppId(game);
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

        private void InspectLocalAchievementData(Game game)
        {
            if (!Settings.Settings.EnableLocalSources)
            {
                PlayniteApi.Dialogs.ShowMessage(
                    "Local achievement sources are disabled in Achievement Sources settings.",
                    "Achievement Sources — Local data");
                return;
            }

            var appIdResult = DetectSteamAppId(game);
            if (!appIdResult.HasResult || appIdResult.IsAmbiguous)
            {
                PlayniteApi.Dialogs.ShowMessage(
                    appIdResult.IsAmbiguous
                        ? "Conflicting Steam AppIDs were detected. Resolve the AppID before reading local achievement data."
                        : "No Steam AppID was detected for this game.",
                    "Achievement Sources — Local data");
                return;
            }

            var context = new GbeAchievementReadContext
            {
                AppId = appIdResult.BestCandidate.AppId,
                InstallDirectory = ExpandInstallDirectory(game),
                PreferredLanguage = "english"
            };

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                context.SaveRootDirectories.Add(Path.Combine(appData, "GSE Saves"));
                context.SaveRootDirectories.Add(Path.Combine(appData, "Goldberg SteamEmu Saves"));
            }

            var result = gbeAchievementReader.Read(context);
            var unlockedCount = result.Achievements.Count(item => item.IsUnlocked);
            var progressCount = result.Achievements.Count(item => item.CurrentProgress.HasValue);
            var message = new StringBuilder();
            message.AppendLine(game.Name);
            message.AppendLine();
            message.AppendLine($"AppID: {context.AppId}");
            message.AppendLine($"Definitions: {result.Achievements.Count}");
            message.AppendLine($"Unlocked: {unlockedCount}");
            message.AppendLine($"With progress state: {progressCount}");
            message.AppendLine($"Complete snapshot: {(result.IsCompleteSnapshot ? "Yes" : "No")}");

            if (!string.IsNullOrWhiteSpace(result.DefinitionPath))
            {
                message.AppendLine();
                message.AppendLine($"Definition file: {result.DefinitionPath}");
            }

            if (!string.IsNullOrWhiteSpace(result.StatePath))
            {
                message.AppendLine($"State file: {result.StatePath}");
            }

            if (result.Achievements.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Achievement sample:");
                foreach (var achievement in result.Achievements.Take(12))
                {
                    var state = achievement.IsUnlocked
                        ? "Unlocked"
                        : achievement.CurrentProgress.HasValue
                            ? $"Progress {achievement.CurrentProgress}/{achievement.MaximumProgress}"
                            : "Locked";
                    message.AppendLine($"• {achievement.DisplayName} — {state}");
                }

                if (result.Achievements.Count > 12)
                {
                    message.AppendLine($"• …and {result.Achievements.Count - 12} more");
                }
            }

            if (result.Diagnostics.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Diagnostics:");
                foreach (var diagnostic in result.Diagnostics)
                {
                    message.AppendLine($"• {diagnostic}");
                }
            }

            PlayniteApi.Dialogs.ShowMessage(message.ToString().TrimEnd(), "Achievement Sources — Local data");
        }

        private string ExpandInstallDirectory(Game game)
        {
            var installDirectory = game.InstallDirectory ?? string.Empty;
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                return string.Empty;
            }

            try
            {
                return PlayniteApi.ExpandGameVariables(game, installDirectory);
            }
            catch
            {
                return installDirectory;
            }
        }
    }
}
