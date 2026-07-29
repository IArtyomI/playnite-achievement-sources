using Playnite.SDK;
using PlayniteAchievementSources.Models;
using PlayniteAchievementSources.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteAchievementSources.Settings
{
    public sealed class AchievementSourcesSettingsViewModel : ObservableObject, ISettings
    {
        private readonly AchievementSourcesPlugin plugin;
        private AchievementSourcesSettings editingClone;
        private AchievementSourcesSettings settings;

        public AchievementSourcesSettings Settings
        {
            get => settings;
            private set => SetValue(ref settings, value);
        }

        public IReadOnlyList<AchievementTrackingMode> TrackingModes { get; } = new[]
        {
            AchievementTrackingMode.Automatic,
            AchievementTrackingMode.NativeOnly,
            AchievementTrackingMode.LocalOnly,
            AchievementTrackingMode.Hybrid,
            AchievementTrackingMode.Disabled
        };

        public AchievementSourcesSettingsViewModel(AchievementSourcesPlugin plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<AchievementSourcesSettings>() ?? new AchievementSourcesSettings();
            Settings.LoadProtectedValues();
        }

        public void BeginEdit()
        {
            editingClone = Settings.Clone();
        }

        public void CancelEdit()
        {
            if (editingClone != null)
            {
                Settings = editingClone;
            }
        }

        public void EndEdit()
        {
            SaveImmediately();
            plugin.RebuildMonitoring();
        }

        public AchievementTrackingMode GetOverrideMode(Guid playniteGameId)
        {
            return GameTrackingPolicy.GetOverrideMode(Settings.GameTrackingOverrides, playniteGameId);
        }

        public GameTrackingOverride GetGameOverride(Guid playniteGameId)
        {
            return Settings.GameTrackingOverrides?
                .LastOrDefault(item => item != null && item.PlayniteGameId == playniteGameId);
        }

        public void SetGamePaths(Guid playniteGameId, string definitionPath, string statePath)
        {
            var item = GetGameOverride(playniteGameId);
            if (item == null)
            {
                item = new GameTrackingOverride { PlayniteGameId = playniteGameId };
                Settings.GameTrackingOverrides.Add(item);
            }

            item.DefinitionPath = definitionPath?.Trim() ?? string.Empty;
            item.StatePath = statePath?.Trim() ?? string.Empty;
            SaveImmediately();
        }

        public AchievementTrackingMode GetEffectiveMode(Guid playniteGameId)
        {
            return GameTrackingPolicy.Resolve(
                Settings.DefaultTrackingMode,
                Settings.GameTrackingOverrides,
                playniteGameId);
        }

        public void SetGameTrackingMode(Guid playniteGameId, AchievementTrackingMode mode)
        {
            GameTrackingPolicy.SetOverride(Settings.GameTrackingOverrides, playniteGameId, mode);
            SaveImmediately();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            var key = Settings.SteamApiKey?.Trim() ?? string.Empty;

            if (key.Length > 0 && (key.Length != 32 || key.Any(character => !IsHexCharacter(character))))
            {
                errors.Add("The Steam Web API key must contain exactly 32 hexadecimal characters, or the field must be left empty.");
            }

            if (!Settings.EnableLocalSources && Settings.DefaultTrackingMode == AchievementTrackingMode.LocalOnly)
            {
                errors.Add("Local-only cannot be the default tracking mode while local sources are disabled.");
            }

            return errors.Count == 0;
        }

        private void SaveImmediately()
        {
            Settings.PrepareForSave();
            plugin.SavePluginSettings(Settings);
        }

        private static bool IsHexCharacter(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'a' && value <= 'f') ||
                   (value >= 'A' && value <= 'F');
        }
    }
}
