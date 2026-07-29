using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteAchievementSources.Models;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteAchievementSources.Settings
{
    public sealed class AchievementSourcesSettings : ObservableObject
    {
        private AchievementTrackingMode defaultTrackingMode = AchievementTrackingMode.Automatic;
        private bool enableLocalSources = true;
        private bool enableOnlineMetadata = true;
        private bool useSteamApiWhenLocalMetadataMissing = true;
        private string encryptedSteamApiKey = string.Empty;
        private string steamApiKey = string.Empty;
        private List<GameTrackingOverride> gameTrackingOverrides = new List<GameTrackingOverride>();

        public AchievementTrackingMode DefaultTrackingMode
        {
            get => defaultTrackingMode;
            set => SetValue(ref defaultTrackingMode, value);
        }

        public bool EnableLocalSources
        {
            get => enableLocalSources;
            set => SetValue(ref enableLocalSources, value);
        }

        public bool EnableOnlineMetadata
        {
            get => enableOnlineMetadata;
            set => SetValue(ref enableOnlineMetadata, value);
        }

        public bool UseSteamApiWhenLocalMetadataMissing
        {
            get => useSteamApiWhenLocalMetadataMissing;
            set => SetValue(ref useSteamApiWhenLocalMetadataMissing, value);
        }

        public string EncryptedSteamApiKey
        {
            get => encryptedSteamApiKey;
            set => SetValue(ref encryptedSteamApiKey, value ?? string.Empty);
        }

        public List<GameTrackingOverride> GameTrackingOverrides
        {
            get => gameTrackingOverrides;
            set => SetValue(ref gameTrackingOverrides, value ?? new List<GameTrackingOverride>());
        }

        [DontSerialize]
        public string SteamApiKey
        {
            get => steamApiKey;
            set
            {
                SetValue(ref steamApiKey, value ?? string.Empty);
                OnPropertyChanged(nameof(SteamApiKeyStatus));
            }
        }

        [DontSerialize]
        public string SteamApiKeyStatus => string.IsNullOrWhiteSpace(SteamApiKey)
            ? "No Steam Web API key is configured."
            : "A Steam Web API key is configured and will be stored for this Windows user.";

        public void LoadProtectedValues()
        {
            SteamApiKey = SecretProtection.Unprotect(EncryptedSteamApiKey);
            GameTrackingOverrides = GameTrackingOverrides ?? new List<GameTrackingOverride>();
        }

        public void PrepareForSave()
        {
            EncryptedSteamApiKey = SecretProtection.Protect(SteamApiKey);
            GameTrackingOverrides = GameTrackingOverrides
                .Where(item => item != null && item.PlayniteGameId != System.Guid.Empty && item.Mode != AchievementTrackingMode.Inherit)
                .GroupBy(item => item.PlayniteGameId)
                .Select(group => group.Last())
                .ToList();
        }

        public AchievementSourcesSettings Clone()
        {
            return new AchievementSourcesSettings
            {
                DefaultTrackingMode = DefaultTrackingMode,
                EnableLocalSources = EnableLocalSources,
                EnableOnlineMetadata = EnableOnlineMetadata,
                UseSteamApiWhenLocalMetadataMissing = UseSteamApiWhenLocalMetadataMissing,
                EncryptedSteamApiKey = EncryptedSteamApiKey,
                SteamApiKey = SteamApiKey,
                GameTrackingOverrides = GameTrackingOverrides
                    .Where(item => item != null)
                    .Select(item => new GameTrackingOverride
                    {
                        PlayniteGameId = item.PlayniteGameId,
                        Mode = item.Mode,
                        PreferredSourceKey = item.PreferredSourceKey,
                        LocalAdapterKey = item.LocalAdapterKey,
                        SourceGameId = item.SourceGameId,
                        StatePath = item.StatePath
                    })
                    .ToList()
            };
        }
    }
}
