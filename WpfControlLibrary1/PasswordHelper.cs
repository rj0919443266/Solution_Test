using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;

namespace WpfControlLibrary1
{
    public static class PasswordHelper
    {
        // 1. 建立一個可以 Binding 的「假密碼屬性」
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordHelper),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

        // 2. 建立一個「開關」，用來啟動監聽事件
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordHelper), new PropertyMetadata(false, OnBindPasswordChanged));

        private static bool _isUpdating;

        // 當 ViewModel 的密碼改變時 (例如變成空字串)，這裡會被觸發，去更新 UI
        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox box)
            {
                box.PasswordChanged -= PasswordBox_PasswordChanged; // 暫停監聽，避免無窮迴圈
                if (!_isUpdating)
                {
                    box.Password = (string)e.NewValue; // 強制更新畫面上的點點
                }
                box.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        // 當 XAML 啟用這個開關時，幫忙掛上 UI 事件
        private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (dp is PasswordBox box)
            {
                bool wasBound = (bool)e.OldValue;
                bool needToBind = (bool)e.NewValue;

                if (wasBound) box.PasswordChanged -= PasswordBox_PasswordChanged;
                if (needToBind) box.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        // 當使用者在畫面上打字時，偷偷把密碼傳回給 ViewModel
        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox box)
            {
                _isUpdating = true;
                SetBoundPassword(box, box.Password);
                _isUpdating = false;
            }
        }

        // 必要的 Get/Set 封裝
        public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPasswordProperty, value);
        public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPasswordProperty);
        public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);
        public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
    }
}
