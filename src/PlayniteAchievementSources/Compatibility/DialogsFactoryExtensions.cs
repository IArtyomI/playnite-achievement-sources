using Playnite.SDK;
using System;
using System.Windows;

namespace PlayniteAchievementSources
{
    internal static class DialogsFactoryExtensions
    {
        public static MessageBoxResult ShowErrorMessage(
            this IDialogsFactory dialogs,
            string messageBoxText,
            string caption,
            Exception exception)
        {
            if (dialogs == null)
            {
                throw new ArgumentNullException(nameof(dialogs));
            }

            var detail = exception?.Message;
            var message = string.IsNullOrWhiteSpace(detail)
                ? messageBoxText
                : messageBoxText + "\n\n" + detail;
            return dialogs.ShowErrorMessage(message, caption);
        }
    }
}
