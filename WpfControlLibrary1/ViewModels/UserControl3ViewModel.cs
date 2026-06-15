using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class UserControl3ViewModel : ObservableObject, IBarcodeReceiver
    {
        [ObservableProperty]
        private string _machineStatus = "機台待命準備中";

        [ObservableProperty]
        private string _lastScannedBarcode = "尚未掃描";

        
        /// <summary>
        /// 接收條碼的處理方法
        /// </summary>
        /// <param name="barcode"></param>
        public void ReceiveBarcode(string barcode)
        {
            // 檢查是否已經在 UI 執行緒，若是，直接更新，不要 Invoke
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            if (dispatcher.CheckAccess())
            {
                LastScannedBarcode = barcode;
                MachineStatus = "P3 讀取成功，處理中...";
            }
            else
            {
                dispatcher.Invoke(() => {
                    LastScannedBarcode = barcode;
                    MachineStatus = "P3 讀取成功，處理中...";
                });
            }
        }

        public UserControl3ViewModel()
        {

        }

    }
}
