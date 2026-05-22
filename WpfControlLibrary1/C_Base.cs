using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfControlLibrary1
{
    public class C_Base
    {
    }

    /// <summary>
    /// 掃描槍連線狀態列舉
    /// </summary>
    public enum ScannerConnectionState
    {
        NotSet,       // 未設定 / 初始狀態
        Connected,    // 已成功連線
        Failed,       // 連線失敗
        Disconnected  // 異常斷線
    }

    public class NavItem
    {
        // 選單的顯示文字 (例如 "Home", "Controls")
        public string Title { get; set; }

        // 選單前面的小圖示
        public PackIconKind Icon { get; set; }

        // 【核心關鍵】用來存放子選單的集合
        // 如果這個集合裡面有東西，Material Design 就會自動幫你畫出「展開箭頭 (v)」
        public ObservableCollection<NavItem> SubItems { get; set; }

        // 用來記錄這個選單對應的 ViewModel 型別
        public Type TargetViewModelType { get; set; }

        public NavItem()
        {
            // 初始化集合，避免發生 NullReferenceException
            SubItems = new ObservableCollection<NavItem>();
        }
    }

    // 用於 條碼機  Messenger 的通訊 Class
    public class BarcodeScannedMessage
    {
        public string Barcode { get; }
        public BarcodeScannedMessage(string barcode) => Barcode = barcode;
    }

    // 當ComPort硬體實體斷線時發送的訊息
    public class DeviceDisconnectedMessage
    {
        public string PortName { get; }

        public DeviceDisconnectedMessage(string portName)
        {
            PortName = portName;
        }
    }

    public class SystemConfig
    {
        // PHP 伺服器的位址 (預設值)
        public string PhpServerUrl { get; set; } = "http://192.168.1.100/api/";

        // 預設的條碼槍 COM Port (預設值)
        public string BarcodeComPort { get; set; } = "COM3";

        // 未來如果有其他設定 (例如機台編號、更新頻率)，都可以繼續加在這裡
    }

    public class SystemConfig_Change_Message
    {
        // 這裡不需要放屬性，我們只需要它作為一個「觸發信號」
    }
}
