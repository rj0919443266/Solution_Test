using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Notifications.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WpfControlLibrary1.Services;


namespace WpfControlLibrary1.ViewModels
{
    public partial class MVVM_SystemConfig : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly SystemConfig _config;
        private readonly ISnackbarService _snackbarService;

        // 畫面上用於繫結/修改的屬性
        [ObservableProperty]
        private string _phpServerUrl;

        [ObservableProperty]
        private string _barcodeComPort;

        // 提供預設 COM Port 的下拉選單清單
        public ObservableCollection<string> AvailablePorts { get; set; } = new();


        [ObservableProperty]
        private bool _isPrioritySortingEnabled;
        public ObservableCollection<string> PriorityKeywords { get; set; }

        [ObservableProperty]
        private string _newKeywordInput;


        public MVVM_SystemConfig(IMessenger messenger, SystemConfig config, ISnackbarService snackbarService)
        {
            _messenger = messenger;
            _config = config;
            _snackbarService = snackbarService;
            // 載入當前系統運作中的設定值
            PhpServerUrl = _config.PhpServerUrl;
            BarcodeComPort = _config.BarcodeComPort;

            IsPrioritySortingEnabled = _config.IsPrioritySortingEnabled;
            // 初始化關鍵字清單
            PriorityKeywords = new ObservableCollection<string>(_config.DepartmentPriorityKeywords ?? new List<string>());

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

        [RelayCommand]
        private void AddKeyword()
        {
            if (string.IsNullOrWhiteSpace(NewKeywordInput)) return;

            string cleaned = NewKeywordInput.Trim();
            if (!PriorityKeywords.Contains(cleaned))
            {
                PriorityKeywords.Add(cleaned);
                NewKeywordInput = string.Empty; // 清空輸入框
            }
        }

        [RelayCommand]
        private void RemoveKeyword(string keyword)
        {
            if (PriorityKeywords.Contains(keyword))
            {
                PriorityKeywords.Remove(keyword);
            }
        }

        // 儲存按鈕觸發的命令
        [RelayCommand]
        private async Task SaveConfig()
        {
            try
            {
                _config.PhpServerUrl = PhpServerUrl;
                _config.BarcodeComPort = BarcodeComPort;

                // 3. 儲存時同步開關狀態回系統實體設定
                _config.IsPrioritySortingEnabled = IsPrioritySortingEnabled;
                _config.DepartmentPriorityKeywords = PriorityKeywords.ToList();

                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemConfig.xml");
                C_XML.Save_XML_from_object(_config, configPath);

                _messenger.Send(new SystemConfig_Change_Message());
                _snackbarService.ShowSnackbar("儲存成功！設定已更新並寫入本機設定檔。", SnackbarMessageType.Success);
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"儲存失敗: {ex.Message}", "錯誤");
            }
        }



    }
}
