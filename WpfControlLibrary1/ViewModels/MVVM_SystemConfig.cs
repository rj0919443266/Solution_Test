using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;


namespace WpfControlLibrary1.ViewModels
{
    public partial class MVVM_SystemConfig : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly SystemConfig _config;

        // 畫面上用於繫結/修改的屬性
        [ObservableProperty]
        private string _phpServerUrl;

        [ObservableProperty]
        private string _barcodeComPort;

        // 提供預設 COM Port 的下拉選單清單
        public ObservableCollection<string> AvailablePorts { get; set; } = new();


        public MVVM_SystemConfig(IMessenger messenger, SystemConfig config)
        {
            _messenger = messenger;
            _config = config;

            // 載入當前系統運作中的設定值
            PhpServerUrl = _config.PhpServerUrl;
            BarcodeComPort = _config.BarcodeComPort;

            // 讀取當前電腦實體可用串口，供現場工程師下拉選取
            foreach (string port in SerialPort.GetPortNames())
            {
                AvailablePorts.Add(port);
            }

            // 防呆：如果設定檔寫的 COM 埠目前沒插上，也強制加進選單，避免畫面顯示空白
            if (!string.IsNullOrEmpty(BarcodeComPort) && !AvailablePorts.Contains(BarcodeComPort))
            {
                AvailablePorts.Add(BarcodeComPort);
            }
        }
        public void Dispose()
        {
            // ViewModel 被釋放時呼叫
            _messenger.UnregisterAll(this);
        }

        [RelayCommand]
        private void RefreshAvailablePorts() // 工具包會自動生成 RefreshAvailablePortsCommand
        {
            // 1. 備份目前選的 Port
            string currentSelectedBackup = BarcodeComPort;

            // 2. 清空並重新取得
            AvailablePorts.Clear();
            foreach (string port in System.IO.Ports.SerialPort.GetPortNames())
            {
                AvailablePorts.Add(port);
            }

            // 3. 還原或防呆
            if (!string.IsNullOrEmpty(currentSelectedBackup) && AvailablePorts.Contains(currentSelectedBackup))
            {
                BarcodeComPort = currentSelectedBackup;
            }
            else if (!string.IsNullOrEmpty(currentSelectedBackup))
            {
                // 如果是已被拔除的幽靈 Port，因為是設定檔，我們還是允許保留它
                AvailablePorts.Add(currentSelectedBackup);
                BarcodeComPort = currentSelectedBackup;
            }
        }
        /// <summary>
        /// 🌟 儲存按鈕觸發的命令
        /// </summary>
        [RelayCommand]
        private void SaveConfig()
        {
            try
            {
                // 1. 將畫面的新設定，同步回記憶體中的全域單例
                _config.PhpServerUrl = PhpServerUrl;
                _config.BarcodeComPort = BarcodeComPort;

                // 2. 計算 XML 實體路徑
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemConfig.xml");
                //WeakReferenceMessenger.Default.Send(new SystemConfig_Change_Message());
                _messenger.Send(new SystemConfig_Change_Message());
                // 3. 呼叫你寫好的 C_XML 靜態類別存檔
                C_XML.Save_XML_from_object(_config, configPath);

                MessageBox.Show(
                    "設定檔已成功儲存！\n\n提示：伺服器網址變更將於下次開啟程式時生效；條碼槍 COM 埠變更可至主畫面右上角點擊「重新連線」直接載入新埠。",
                    "系統提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存設定檔失敗: {ex.Message}", "系統錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
