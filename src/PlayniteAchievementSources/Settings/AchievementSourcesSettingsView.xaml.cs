using System.Windows;
using System.Windows.Controls;

namespace PlayniteAchievementSources.Settings
{
    public partial class AchievementSourcesSettingsView : UserControl
    {
        private bool isSynchronizingPassword;

        public AchievementSourcesSettingsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SynchronizePasswordBox();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SynchronizePasswordBox();
        }

        private void SynchronizePasswordBox()
        {
            if (!(DataContext is AchievementSourcesSettingsViewModel viewModel) || SteamApiKeyBox == null)
            {
                return;
            }

            isSynchronizingPassword = true;
            SteamApiKeyBox.Password = viewModel.Settings.SteamApiKey ?? string.Empty;
            isSynchronizingPassword = false;
        }

        private void OnSteamApiKeyChanged(object sender, RoutedEventArgs e)
        {
            if (isSynchronizingPassword || !(DataContext is AchievementSourcesSettingsViewModel viewModel))
            {
                return;
            }

            viewModel.Settings.SteamApiKey = SteamApiKeyBox.Password;
        }

        private void OnClearSteamApiKey(object sender, RoutedEventArgs e)
        {
            SteamApiKeyBox.Clear();
        }
    }
}
