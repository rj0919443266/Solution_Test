using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfControlLibrary1.ViewModels
{
    public partial class UserControl2ViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _machineStatus = "機台待命準備中";

        [ObservableProperty]
        private string _lastScannedBarcode = "尚未掃描";

        public UserControl2ViewModel()
        {

            //StrongReferenceMessenger.Default.Send(new BarcodeScannedMessage("690123456789"));
            //StrongReferenceMessenger.Default.UnregisterAll(this) 來釋放記憶體
            //string 實際掃到的條碼 = "690123456789";
            //WeakReferenceMessenger.Default.Send(new BarcodeScannedMessage(實際掃到的條碼));

            WeakReferenceMessenger.Default.Register<BarcodeScannedMessage>(this, (recipient, message) =>
            {
                // 使用 Application.Current.Dispatcher 將動作排入 UI 執行緒
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LastScannedBarcode = message.Barcode;
                    MachineStatus = "讀取成功，處理中...";
                    // 如果是操作 ObservableCollection.Add()，絕對必須包在這裡面！
                });
            });
        }

    }
}
