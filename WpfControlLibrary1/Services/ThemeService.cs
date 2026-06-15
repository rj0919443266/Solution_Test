using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;

namespace WpfControlLibrary1.Services
{
    public class ThemeService : IThemeService
    {
        public SKColor GetSkiaColor(string resourceKey, string fallbackHex)
        {
            // 確保在 UI 執行緒上取得資源
            var color = SKColor.Parse(fallbackHex);

            Application.Current.Dispatcher.Invoke(() =>
            {
                var resource = Application.Current.TryFindResource(resourceKey);
                if (resource is SolidColorBrush brush)
                {
                    color = new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);
                }
            });

            return color;
        }
    }

    //=============================================
    /// <summary>
    /// 獲得 WPF 主題色的服務介面
    /// </summary>
    public interface IThemeService
    {
        SKColor GetSkiaColor(string resourceKey, string fallbackHex);
    }

    public interface IWindowManager
    {
        // 傳入準備好的 ViewModel，由服務負責把對應的視窗開起來
        void ShowWindow(object viewModel);
    }
}
