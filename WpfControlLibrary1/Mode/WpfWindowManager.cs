using System;
using System.Windows;
using WpfControlLibrary1.Services;
using WpfControlLibrary1.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace WpfControlLibrary1.Mode
{
    /// <summary>
    /// 彈窗管理器，負責根據 ViewModel 類型決定要開啟哪個視窗，並將資料綁定到視窗上。
    /// </summary>
    public class WpfWindowManager : IWindowManager
    {
        private readonly IServiceProvider _serviceProvider;

        public WpfWindowManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void ShowWindow(object viewModel)
        {
            // 將原本在 ViewModel 裡的 UI 邏輯移到這裡
            Application.Current.Dispatcher.Invoke(() =>
            {
                Window window = null;

                // 根據傳進來的 ViewModel 型別，決定要開哪個視窗
                if (viewModel is VM_LotDetail)
                {
                    window = _serviceProvider.GetRequiredService<Window_LotDetail>();// 從 DI 容器取得視窗實例 // 
                }
                // 未來如果有其他視窗...
                // else if (viewModel is VM_Another...) { ... }

                
                if (window != null)
                {
                    window.DataContext = viewModel; // 綁定資料
                    window.Show(); // 開啟視窗
                }
            });
        }
    }
}
