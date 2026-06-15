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
    public partial class UserControl2ViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string _machineStatus = "機台待命準備中";

        [ObservableProperty]
        private string _lastScannedBarcode = "尚未掃描";

        public UserControl2ViewModel()
        {

            WeakReferenceMessenger.Default.Register<UserControl2ViewModel, BarcodeScannedMessage>(this, (r, message) =>
            {
                // 1. 改用 InvokeAsync 避免死鎖
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    r.LastScannedBarcode = message.Barcode;
                    r.MachineStatus = "讀取成功，處理中...";
                });
            });
        }

        // 2. 實作 Dispose 方法，解除所有訂閱！
        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

    }
}
