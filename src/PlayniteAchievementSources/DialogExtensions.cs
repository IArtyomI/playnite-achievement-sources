using Playnite.SDK;
using System;
using System.Windows;

namespace PlayniteAchievementSources
{
    internal static class DialogExtensions
    {
        public static MessageBoxResult ShowErrorMessage(
            this IDialogsFactory dialogs,
            string message,
            string caption,
            Exception exception)
        {
            var detail = exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? string.Empty
                : $"\n\n{exception.Message}";
            return dialogs.ShowErrorMessage(message + detail, caption);
        }
    }
}
