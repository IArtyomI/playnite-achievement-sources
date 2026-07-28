using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteAchievementSources.Models;
using System.Collections.Generic;

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
        }

        public void PrepareForSave()
        {
            EncryptedSteamApiKey = SecretProtection.Protect(SteamApiKey);
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
                SteamApiKey = SteamApiKey
            };
        }
    }
}
