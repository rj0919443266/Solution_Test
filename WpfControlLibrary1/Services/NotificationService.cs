using Notifications.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfControlLibrary1.Services
{
    #region 吐司通知服務
    public interface INotificationService
    {
        void Show(string title, string message, NotificationType type = NotificationType.Information);
    }

    public class NotificationService : INotificationService
    {
        private readonly NotificationManager _manager = new NotificationManager();

        public void Show(string title, string message, NotificationType type = NotificationType.Information)
        {
            // 確保通知顯示在主 UI 執行緒
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _manager.Show(new NotificationContent
                {
                    Title = title,
                    Message = message,
                    Type = type
                });
            });
        }
    }
    #endregion
}
