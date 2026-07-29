using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteAchievementSources.Detection;
using PlayniteAchievementSources.Models;
using PlayniteAchievementSources.Settings;
using PlayniteAchievementSources.Snapshots;
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

        private static readonly AchievementTrackingMode[] GameTrackingModes =
        {
            AchievementTrackingMode.Inherit,
            AchievementTrackingMode.Automatic,
            AchievementTrackingMode.NativeOnly,
            AchievementTrackingMode.LocalOnly,
            AchievementTrackingMode.Hybrid,
            AchievementTrackingMode.Disabled
        };

        private readonly SteamAppIdDetector steamAppIdDetector = new SteamAppIdDetector();
        private readonly GbeAchievementReader gbeAchievementReader = new GbeAchievementReader();
        private readonly AchievementSnapshotWriter snapshotWriter = new AchievementSnapshotWriter();

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
                Description = "Show snapshot folder location",
                Action = _ => PlayniteApi.Dialogs.ShowMessage(
                    Path.Combine(GetPluginUserDataPath(), "snapshots", $"v{AchievementSnapshot.CurrentSchemaVersion}"),
                    "Achievement Sources — Snapshot folder")
            };

            yield return new MainMenuItem
            {
                MenuSection = "@Achievement Sources",
                Description = "Show development status",
                Action = _ => PlayniteApi.Dialogs.ShowMessage(
                    "Achievement Sources is installed. Settings, per-game tracking modes, Steam AppID diagnostics, read-only GBE/Goldberg-compatible inspection, and versioned local snapshots are enabled. Live monitoring, notifications, and Playnite Achievements bridge output are not enabled yet.",
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

            var overrideMode = Settings.GetOverrideMode(game.Id);
            var effectiveMode = Settings.GetEffectiveMode(game.Id);

            yield return new GameMenuItem
            {
                MenuSection = "Achievement Sources",
                Description = $"Tracking status: {overrideMode} (effective {effectiveMode})",
                Action = _ => ShowTrackingStatus(game)
            };

            foreach (var mode in GameTrackingModes)
            {
                var capturedMode = mode;
                yield return new GameMenuItem
                {
                    MenuSection = "Achievement Sources|Tracking mode",
                    Description = FormatTrackingModeMenuText(capturedMode, overrideMode),
                    Action = _ => SetTrackingMode(game, capturedMode)
                };
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

            yield return new GameMenuItem
            {
                MenuSection = "Achievement Sources",
                Description = "Write local snapshot",
                Action = _ => WriteLocalSnapshot(game)
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

        private void ShowTrackingStatus(Game game)
        {
            var overrideMode = Settings.GetOverrideMode(game.Id);
            var effectiveMode = Settings.GetEffectiveMode(game.Id);
            PlayniteApi.Dialogs.ShowMessage(
                $"{game.Name}\n\nPer-game mode: {overrideMode}\nGlobal default: {Settings.Settings.DefaultTrackingMode}\nEffective mode: {effectiveMode}",
                "Achievement Sources — Tracking mode");
        }

        private void SetTrackingMode(Game game, AchievementTrackingMode mode)
        {
            Settings.SetGameTrackingMode(game.Id, mode);
            var effectiveMode = Settings.GetEffectiveMode(game.Id);
            PlayniteApi.Dialogs.ShowMessage(
                $"{game.Name}\n\nPer-game mode: {mode}\nEffective mode: {effectiveMode}",
                "Achievement Sources — Tracking mode");
        }

        private string FormatTrackingModeMenuText(
            AchievementTrackingMode mode,
            AchievementTrackingMode selectedOverride)
        {
            var label = mode == AchievementTrackingMode.Inherit
                ? $"Use global default ({Settings.Settings.DefaultTrackingMode})"
                : mode.ToString();
            return mode == selectedOverride ? "✓ " + label : label;
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
                    message.AppendLine("Conflicting AppIDs were found. Automatic tracking requires a per-game override until the conflict is resolved.");
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
            if (!TryReadLocalAchievementData(game, out var context, out var result, out var error))
            {
                PlayniteApi.Dialogs.ShowMessage(error, "Achievement Sources — Local data");
                return;
            }

            var message = BuildLocalAchievementMessage(game, context, result);
            PlayniteApi.Dialogs.ShowMessage(message, "Achievement Sources — Local data");
        }

        private void WriteLocalSnapshot(Game game)
        {
            if (!TryReadLocalAchievementData(game, out var context, out var result, out var error))
            {
                PlayniteApi.Dialogs.ShowMessage(error, "Achievement Sources — Snapshot");
                return;
            }

            if (!result.HasDefinitions || result.Achievements.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(
                    "No supported achievement definitions were found, so no snapshot was written.",
                    "Achievement Sources — Snapshot");
                return;
            }

            var snapshot = new AchievementSnapshot
            {
                PlayniteGameId = game.Id,
                PlayniteGameName = game.Name ?? string.Empty,
                OverrideMode = Settings.GetOverrideMode(game.Id),
                EffectiveTrackingMode = Settings.GetEffectiveMode(game.Id),
                SourceKey = "gbe-compatible",
                SourceGameId = context.AppId.ToString(),
                StateKnown = result.HasState,
                IsCompleteSnapshot = result.IsCompleteSnapshot,
                DefinitionPath = result.DefinitionPath ?? string.Empty,
                StatePath = result.StatePath ?? string.Empty,
                Achievements = result.Achievements.ToList(),
                Diagnostics = result.Diagnostics.ToList()
            };

            try
            {
                var path = snapshotWriter.Write(GetPluginUserDataPath(), snapshot);
                PlayniteApi.Dialogs.ShowMessage(
                    $"Snapshot written successfully.\n\n{path}\n\nState known: {(snapshot.StateKnown ? "Yes" : "No")}\nComplete snapshot: {(snapshot.IsCompleteSnapshot ? "Yes" : "No")}",
                    "Achievement Sources — Snapshot");
            }
            catch (Exception exception)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "The local snapshot could not be written.",
                    "Achievement Sources — Snapshot",
                    exception);
            }
        }

        private bool TryReadLocalAchievementData(
            Game game,
            out GbeAchievementReadContext context,
            out GbeAchievementReadResult result,
            out string error)
        {
            context = null;
            result = null;
            error = string.Empty;

            var effectiveMode = Settings.GetEffectiveMode(game.Id);
            if (effectiveMode == AchievementTrackingMode.Disabled)
            {
                error = "Achievement tracking is disabled for this game.";
                return false;
            }

            if (effectiveMode == AchievementTrackingMode.NativeOnly)
            {
                error = "This game is configured for native-only tracking, so local source inspection is disabled.";
                return false;
            }

            if (!Settings.Settings.EnableLocalSources)
            {
                error = "Local achievement sources are disabled in Achievement Sources settings.";
                return false;
            }

            var appIdResult = DetectSteamAppId(game);
            if (!appIdResult.HasResult || appIdResult.IsAmbiguous)
            {
                error = appIdResult.IsAmbiguous
                    ? "Conflicting Steam AppIDs were detected. Resolve the AppID before reading local achievement data."
                    : "No Steam AppID was detected for this game.";
                return false;
            }

            context = new GbeAchievementReadContext
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

            result = gbeAchievementReader.Read(context);
            return true;
        }

        private string BuildLocalAchievementMessage(
            Game game,
            GbeAchievementReadContext context,
            GbeAchievementReadResult result)
        {
            var unlockedCount = result.Achievements.Count(item => item.IsUnlocked);
            var progressCount = result.Achievements.Count(item => item.CurrentProgress.HasValue);
            var message = new StringBuilder();
            message.AppendLine(game.Name);
            message.AppendLine();
            message.AppendLine($"AppID: {context.AppId}");
            message.AppendLine($"Definitions: {result.Achievements.Count}");
            message.AppendLine($"Unlocked: {unlockedCount}");
            message.AppendLine($"With progress state: {progressCount}");
            message.AppendLine($"State known: {(result.HasState ? "Yes" : "No")}");
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
                            : result.HasState ? "Locked" : "State unknown";
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

            return message.ToString().TrimEnd();
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
