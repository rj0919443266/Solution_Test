using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            // 一樣使用 Dispatcher 切回 UI 執行緒
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LastScannedBarcode = barcode;
                MachineStatus = "P3 讀取成功，處理中...";
            });
        }

        public UserControl3ViewModel()
        {

        }

    }
}
