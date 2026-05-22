using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // 為了使用 [RelayCommand]
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfControlLibrary1;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.ViewModels;


namespace WpfApp1.ViewModels
{
    public partial class MVVM_MainWindow : ObservableObject, IDisposable
    {
        private readonly IServiceProvider _serviceProvider; //  宣告 DI 容器
        private readonly SystemConfig _config;
        private readonly IMessenger _messenger;// 宣告 Messenger
        private readonly BarcodeScannerService _scannerService; //掃描槍服務

        //===========================================================主畫面
        [ObservableProperty]
        private object _currentView = null;
        //===========================================================導覽列
        public ObservableCollection<NavItem> MenuItems { get; set; }
        //===========================================================
        [ObservableProperty]
        private string _fileVersion;
        //===========================================================
        [ObservableProperty]
        private string _phpConnectionStatus = "未連線";
        //===========================================================COM Port 連線狀態
        [ObservableProperty]
        private string _scannerStatus = "NA";

        [ObservableProperty]
        private ScannerConnectionState _connectionState = ScannerConnectionState.NotSet;

        // 電腦目前可用的 COM Port 清單
        public ObservableCollection<string> AvailablePorts { get; set; } = new();

        // 使用者目前選中的 COM Port
        [ObservableProperty] 
        private string _selectedPort = "COM3";
        //===========================================================
        [ObservableProperty]
        private string _user_ID = "NA";
        [ObservableProperty]
        private string _user_Name = "NA";

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

     

        public MVVM_MainWindow(IServiceProvider serviceProvider, IMessenger messenger, BarcodeScannerService scannerService, SystemConfig config)
        {
            //===========================================================DI
            _serviceProvider = serviceProvider;
            //===========================================================版本
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName);
            FileVersion = versionInfo.FileVersion;
            //===========================================================設定檔
            _config = config;
            //===========================================================
            _scannerService = scannerService; // 接收條碼槍服務
            SelectedPort = _config.BarcodeComPort;
            //自動偵測電腦當前所有的實體 COM Port
            RefreshAvailablePorts();

            //初始化嘗試連線預設的 Port
            ExecuteConnect(SelectedPort);
            //===========================================================
            _messenger = messenger;
            //============================================================由主視窗來負責監聽硬體條碼槍的廣播
            _messenger.Register<BarcodeScannedMessage>(this, (recipient, message) =>
            {
                // 核心路由器邏輯：
                // 檢查目前的畫面 (CurrentView) 是不是一個「條碼接收器」
                if (CurrentView is IBarcodeReceiver receiver)
                {
                    // 如果是，就把條碼專屬遞給這個當前畫面！
                    receiver.ReceiveBarcode(message.Barcode);
                }
            });

            //============================================================ 偵聽「ComPort硬體實體斷線」廣播
            _messenger.Register<DeviceDisconnectedMessage>(this, (recipient, message) =>
            {
                // 因為 Timer 是在背景執行緒觸發的，修改 UI 綁定的屬性必須回到主執行緒
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 變更標題列的狀態文字
                    ScannerStatus = $"{message.PortName} (異常斷線)";
                    ConnectionState = ScannerConnectionState.Disconnected;
                    // 強制更新一次可用的 Port 清單 (讓 ComboBox 的失效選項消失)
                    RefreshAvailablePorts();

                    // 彈出嚴重警告視窗
                    MessageBox.Show(
                        $"系統偵測到條碼槍 [{message.PortName}] 已中斷連線！\n\n請檢查 USB 線路是否鬆脫，重新插拔後點擊右上角「重新連線」按鈕。",
                        "硬體斷線警告",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            });

            //============================================================ 監聽「設定已改變」廣播 
            _messenger.Register<SystemConfig_Change_Message>(this, (recipient, message) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 1. 將 MainWindow 自己的 SelectedPort 更新為最新的設定檔數值

                    SelectedPort = _config.BarcodeComPort;

                    // 2. 自動觸發重新連線流程 (使用者連按鈕都不用按)
                    Reconnect();
                });
            });
            // --

            // 初始化選單項目
            MenuItems = new ObservableCollection<NavItem>();

            MenuItems.Add(new NavItem { Title = "Home"    , Icon = MaterialDesignThemes.Wpf.PackIconKind.Home    , TargetViewModelType = typeof(UserControl1ViewModel) });
            MenuItems.Add(new NavItem { Title = "Controls", Icon = MaterialDesignThemes.Wpf.PackIconKind.Settings, TargetViewModelType = typeof(UserControl2ViewModel) });
            MenuItems.Add(new NavItem { Title = "Controls", Icon = MaterialDesignThemes.Wpf.PackIconKind.Settings, TargetViewModelType = typeof(UserControl3ViewModel) });
            var controlsItem = new NavItem { Title = "Controls", Icon = PackIconKind.ViewDashboardOutline };
            controlsItem.SubItems.Add(new NavItem { Title = "Buttons", Icon = PackIconKind.GestureTapButton });
            controlsItem.SubItems.Add(new NavItem { Title = "Cards", Icon = PackIconKind.CardsOutline });
            controlsItem.SubItems.Add(new NavItem { Title = "Chips", Icon = PackIconKind.SizeS });
            MenuItems.Add(controlsItem);
            MenuItems.Add(new NavItem { Title = "About", Icon = MaterialDesignThemes.Wpf.PackIconKind.Information });
            MenuItems.Add(new NavItem { Title = "系統設定", Icon = PackIconKind.Cog, TargetViewModelType = typeof(MVVM_SystemConfig) });
            //============================================================
            Navigate(typeof(UserControl1ViewModel));//預設初始畫面

        }


        public void Dispose()
        {
            // ViewModel 被釋放時呼叫
            _messenger.UnregisterAll(this);
        }
        /// <summary>
        /// 處理導覽切換的核心邏輯
        /// </summary>
        /// <param name="targetType"></param>
        [RelayCommand]
        public void Navigate(Type targetType)
        {
            if (targetType != null)
            {
                // 請 DI 容器幫我們生出對應的 ViewModel，並塞給 CurrentView
                CurrentView = _serviceProvider.GetService(targetType);
            }
        }


        [RelayCommand]
        private async Task Login()
        {
            // 1. 從 DI 容器拿到由系統組裝好 ApiService 的 LoginViewModel
            // (注意：你的 MVVM_MainWindow 建構子裡必須有 _serviceProvider 喔)
            var loginVM = _serviceProvider.GetService(typeof(VM_Login));

            // 2. 呼叫對話框並等待
            var result = await DialogHost.Show(loginVM, "RootDialog");

            // 3. 檢查回傳的結果，是不是我們剛剛傳過來的 LoginResponseModel？
            if (result is LoginResponseModel loginData)
            {
                // 登入成功！把 API 給的真實名字和權限設定到畫面上
                User_Name = loginData.UserName;

                // 這裡你可以依據 loginData.Level 做不同處理
                // 例如：if (loginData.Level >= 5) { 開啟管理者權限 }

                PhpConnectionStatus = "已連線 (已登入)";

                MessageBox.Show($"登入成功！\n歡迎回來，{loginData.UserName} (權限等級: {loginData.Level})", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 使用者點了取消 (收到 null)，甚麼都不做
            }
        }

        #region "COM Port"



        /// <summary>
        /// 刷新電腦的 COM Port 清單
        /// </summary>
        /// <summary>
        /// 處理 ComboBox 重新整理的地雷改進版
        /// </summary>
        public void RefreshAvailablePorts()
        {
            // 先將使用者目前選的 Port 備份到記憶體變數中！
            string currentSelectedBackup = SelectedPort;

            // 取得電腦目前實際有的 Ports
            string[] currentSystemPorts = SerialPort.GetPortNames();

            // 清空舊清單（此時 UI 會觸發雙向綁定，將 SelectedPort 刷成 null，但不用怕）
            AvailablePorts.Clear();

            // 重新加回所有可用的 Ports
            foreach (string port in currentSystemPorts)
            {
                AvailablePorts.Add(port);
            }

            // 還原機制：
            // 檢查我們剛剛備份的那個 Port（例如 COM5），是不是還存在於新電腦的清單中
            if (!string.IsNullOrEmpty(currentSelectedBackup) && AvailablePorts.Contains(currentSelectedBackup))
            {
                // 如果還在，乖乖還原它，畫面就會完美保持在使用者剛才選的那一項！
                SelectedPort = currentSelectedBackup;
            }
            else if (AvailablePorts.Count > 0)
            {
                // 如果原本選的 Port 已經被拔掉了（不存在了），才預設選第一個
                SelectedPort = AvailablePorts[0];
            }
            else
            {
                SelectedPort = "未設定";
            }
        }
        [RelayCommand]
        private void Re_GetComPorts()
        {
            RefreshAvailablePorts();
        }

        /// <summary>
        /// 核心連線邏輯（供內部與重新連線按鈕使用）
        /// </summary>
        [RelayCommand]
        private void Reconnect()
        {
            // 重新連線前，先重新整理一次電腦的 COM 埠，防止使用者剛拔插 USB
            RefreshAvailablePorts();

            // 執行連線
            ExecuteConnect(SelectedPort);
        }

        private void ExecuteConnect(string portName)
        {
            if (string.IsNullOrEmpty(portName))
            {
                ScannerStatus = "未設定 COM 埠";
                ConnectionState = ScannerConnectionState.NotSet;
                return;
            }

            // 呼叫服務開關 COM Port
            bool isSuccess = _scannerService.Start(portName);

            if (isSuccess)
            {
                // 顯示連接的 ComPort 與 狀態
                ScannerStatus = $"{portName} (已連線)";
                ConnectionState = ScannerConnectionState.Connected;
            }
            else
            {
                ScannerStatus = $"{portName} (連線失敗)";
                ConnectionState = ScannerConnectionState.Failed;

                //連線失敗彈出訊息提示
                MessageBox.Show(
                    $"無法開啟 {portName}！\n請檢查：\n1. 條碼槍 USB 是否鬆脫？\n2. 該 COM 埠是否被其他軟體（如 SecureCRT）佔用？",
                    "條碼槍硬體連線錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        #endregion 




    }



}
