using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;

namespace PlayniteAchievementSources
{
    public sealed class AchievementSourcesPlugin : GenericPlugin
    {
        public static readonly Guid PluginId = Guid.Parse("0391911C-BF98-4A2D-8200-8641AF0973E9");

        public override Guid Id => PluginId;

        public AchievementSourcesPlugin(IPlayniteAPI playniteApi)
            : base(playniteApi)
        {
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@Achievement Sources",
                Description = "Show development status",
                Action = _ => PlayniteApi.Dialogs.ShowMessage(
                    "Achievement Sources is installed. Source discovery and tracking are not enabled in this foundation build.",
                    "Achievement Sources")
            };
        }
    }
}
