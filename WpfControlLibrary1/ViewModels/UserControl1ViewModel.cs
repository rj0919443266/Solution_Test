using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class UserControl1ViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _machineStatus = "機台待命準備中";

        [ObservableProperty]
        private string _lastScannedBarcode = "尚未掃描";
        public UserControl1ViewModel()
        {
            //WeakReferenceMessenger.Default.Register<BarcodeScannedMessage>(this, (recipient, message) =>
            //{
            //    // 使用 Application.Current.Dispatcher 將動作排入 UI 執行緒
            //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        LastScannedBarcode = message.Barcode;
            //        MachineStatus = "讀取成功，處理中...";
            //        // 如果是操作 ObservableCollection.Add()，絕對必須包在這裡面！
            //    });
            //});
            
            WeakReferenceMessenger.Default.Register<UserControl1ViewModel, BarcodeScannedMessage>(this, (r, message) =>
            {
                // 1. 改用 InvokeAsync 避免死鎖
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 2. 此時 r 已經被強型別確認為 UserControl1ViewModel，編譯會完美通過！
                    r.LastScannedBarcode = message.Barcode;
                    r.MachineStatus = "讀取成功，處理中...";
                });
            });
        }
    }
}
