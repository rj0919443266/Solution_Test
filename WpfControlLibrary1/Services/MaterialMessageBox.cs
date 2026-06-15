using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using System.Windows;
using WpfControlLibrary1.ViewModels; // 請確保引用您的 VM_CommonDialog 命名空間

namespace WpfControlLibrary1
{
    /// <summary>
    /// 全域通用 Material Design 對話框呼叫器
    /// 用法完全比照原生的 MessageBox，只是加上 await 與 Async
    /// </summary>
    public static class MaterialMessageBox
    {
        public static async Task<MessageBoxResult> ShowAsync(
            string messageBoxText,
            string caption = "系統提示",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            // 1. 將原生的 MessageBoxImage 映射到我們的 DialogType
            DialogType dialogType = DialogType.Info;
            switch (icon)
            {
                case MessageBoxImage.Error:
                    dialogType = DialogType.Error;
                    break;
                case MessageBoxImage.Warning:
                    dialogType = DialogType.Warning;
                    break;
                case MessageBoxImage.Question:
                    dialogType = DialogType.Question;
                    break;
                case MessageBoxImage.Information:
                default:
                    dialogType = DialogType.Info;
                    break;
            }

            // 2. 建立通用對話框的 ViewModel
            var dialogVM = new VM_CommonDialog(caption, messageBoxText, dialogType);

            // 3. 依照傳入的 MessageBoxButton 覆寫按鈕文字與顯示狀態
            if (button == MessageBoxButton.YesNo)
            {
                dialogVM.ShowCancelButton = true;
                dialogVM.ConfirmText = "是 (Yes)";
                dialogVM.CancelText = "否 (No)";
            }
            else if (button == MessageBoxButton.OKCancel)
            {
                dialogVM.ShowCancelButton = true;
                dialogVM.ConfirmText = "確定 (OK)";
                dialogVM.CancelText = "取消 (Cancel)";
            }
            else // MessageBoxButton.OK
            {
                dialogVM.ShowCancelButton = false;
                dialogVM.ConfirmText = "確定 (OK)";
            }

            // 4. 呼叫 Material Design 的 DialogHost 並等待結果
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            // 5. 將對話框的回傳值 (true/false) 轉譯回原生的 MessageBoxResult
            if (result is bool isConfirmed)
            {
                if (isConfirmed)
                {
                    // 點擊了左邊/確認按鈕
                    return button == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
                }
                else
                {
                    // 點擊了右邊/取消按鈕
                    return button == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
                }
            }

            // 防呆預設回傳
            return MessageBoxResult.None;
        }
    }
}