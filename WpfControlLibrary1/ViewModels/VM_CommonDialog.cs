using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;

namespace WpfControlLibrary1.ViewModels
{
    // 定義對話框類型
    public enum DialogType
    {
        Info,       // 一般提示 (單按鈕)
        Warning,    // 警告 (單按鈕)
        Error,      // 錯誤 (單按鈕)
        Question    // 詢問確認 (雙按鈕：是/否)
    }

    public partial class VM_CommonDialog : ObservableObject
    {
        [ObservableProperty] private string _title;
        [ObservableProperty] private string _message;
        [ObservableProperty] private PackIconKind _iconKind;
        [ObservableProperty] private string _iconColor;
        [ObservableProperty] private bool _showCancelButton;
        [ObservableProperty] private string _confirmText = "確定";
        [ObservableProperty] private string _cancelText = "取消";

        public VM_CommonDialog(string title, string message, DialogType type = DialogType.Info)
        {
            Title = title;
            Message = message;

            // 依據類型自動設定外觀與按鈕
            switch (type)
            {
                case DialogType.Info:
                    IconKind = PackIconKind.InformationCircle;
                    IconColor = "#64B5F6"; // 柔和藍色
                    ShowCancelButton = false;
                    break;
                case DialogType.Warning:
                    IconKind = PackIconKind.AlertCircle;
                    IconColor = "#FFB300"; // 警告橘黃
                    ShowCancelButton = false;
                    break;
                case DialogType.Error:
                    IconKind = PackIconKind.CloseCircle;
                    IconColor = "#EF5350"; // 錯誤紅色
                    ShowCancelButton = false;
                    break;
                case DialogType.Question:
                    IconKind = PackIconKind.QuestionMarkCircle;
                    IconColor = "#81C784"; // 詢問綠色
                    ShowCancelButton = true;
                    ConfirmText = "是，確定執行";
                    CancelText = "取消";
                    break;
            }
        }

        // 點擊確定回傳 true
        [RelayCommand]
        private void Confirm() => DialogHost.Close("RootDialog", true);

        // 點擊取消回傳 false
        [RelayCommand]
        private void Cancel() => DialogHost.Close("RootDialog", false);
    }
}