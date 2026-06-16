using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // 為了使用 [RelayCommand]
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Wpf;
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
using System.Windows.Threading;
using WpfControlLibrary1;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;
using WpfControlLibrary1.ViewModels;


namespace WpfApp1.ViewModels
{
    public partial class MVVM_MainWindow : ObservableObject, IDisposable
    {
        private readonly IServiceProvider _serviceProvider; //  宣告 DI 容器

        private readonly ISnackbarService _snackbarService;
        private readonly INotificationService _notificationService; // 吐司 訊息通知服務

        private readonly ILogService _logService; //  Log 服務

        private readonly SystemConfig _config;
        private readonly IMessenger _messenger;// 宣告 Messenger
        private readonly ApiService _apiService;
        private readonly BarcodeScannerService _scannerService; //掃描槍服務
        //===========================================================
       
     
         public ISnackbarMessageQueue MainSnackbarMessageQueue { get; }
        [ObservableProperty]
        private SnackbarMessageType _currentSnackbarType = SnackbarMessageType.Information;
        //===========================================================主畫面
        [ObservableProperty]
        private object _currentView = null;

        [ObservableProperty]
        private bool _isMenuOpen = false; // 控制左側抽屜開關

        //===========================================================導覽列
        // 🌟 將 public 改為 private！
        [ObservableProperty]
        private ObservableCollection<NavigationItem> _menuItems = new ObservableCollection<NavigationItem>();

        [ObservableProperty]
        private NavigationItem _selectedMenuItem;
        //===========================================================版本
        [ObservableProperty]
        private string _fileVersion;


        //===========================================================php server
        //[ObservableProperty]
        //private string _phpConnectionStatus = "未連線";

        [ObservableProperty]
        private string _stateDurationString = "";

        [ObservableProperty]
        private PhpServerState _serverState = PhpServerState.Testing;

        // 狀態起始時間
        private DateTime? _stateStartTime = null;

        //負責每秒更新 UI (時間) 的計時器
        private DispatcherTimer _uiTimer;
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
        private bool _isLoggedIn = false;

        [ObservableProperty]
        private string _loginButtonText = "系統登入";

        [ObservableProperty]
        private PackIconKind _loginButtonIcon = PackIconKind.Login;
        //===========================================================
        [ObservableProperty]
        private string _user_ID = "NA";
        [ObservableProperty]
        private string _user_Name = "NA";

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

     

        public MVVM_MainWindow(
            IServiceProvider serviceProvider,
            ILogService logService,
            ISnackbarService snackbarService,
            INotificationService notificationService,
            IMessenger messenger,
            ApiService apiService,
            BarcodeScannerService scannerService,
            SystemConfig config)
        {

            //===========================================================DI容器
            _serviceProvider = serviceProvider;
            //===========================================================吐司訊息 服務
            _notificationService = notificationService;
            //===========================================================Snackbar 服務
            _snackbarService = snackbarService;
            MainSnackbarMessageQueue = _serviceProvider.GetRequiredService<ISnackbarMessageQueue>();
            //===========================================================設定檔
            _config = config;
            //===========================================================Log 服務
            _logService = logService;
            //===========================================================php server
            _apiService = apiService;
            StartHeartbeat(); //啟動心跳偵測
            StartUiTimer(); //一秒一次
            //===========================================================
            _scannerService = scannerService; // 條碼槍服務
            //===========================================================版本
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName);
            FileVersion = versionInfo.FileVersion;

            //===========================================================ComPort
            SelectedPort = _config.BarcodeComPort;
            //自動偵測電腦當前所有的實體 COM Port
            RefreshAvailablePorts();

            //  判斷設定檔的 COM Port 是否存在於當前硬體清單中
            if (!string.IsNullOrEmpty(SelectedPort) && !AvailablePorts.Contains(SelectedPort))
            {
                // 將狀態設為 Failed，這會觸發 MainWindow.xaml 裡的 DataTrigger 變成「紅字」
                ConnectionState = ScannerConnectionState.Failed;
                ScannerStatus = $"{SelectedPort} (連線失敗)";
               

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _snackbarService.ShowSnackbar($"條碼槍 [{SelectedPort}] 已中斷連線！請檢查 USB 線路。",
                                SnackbarMessageType.Error,
                                "重新連線", () => Reconnect()
                     );
                }), System.Windows.Threading.DispatcherPriority.Loaded);

            }
            else
            {
                // 如果存在，才執行預設的自動連線
                ExecuteConnect(SelectedPort);
            }
           
            //===========================================================
            _messenger = messenger;

            //=============================註冊監聽 SnackbarService 發送來的廣播
            _messenger.Register<MVVM_MainWindow, SnackbarRequestMessage>(this, (r, message) =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 🌟 加上 r. 存取
                    if (message.ClearQueue && r.MainSnackbarMessageQueue is SnackbarMessageQueue queue)
                    {
                        queue.Clear();
                    }

                    var snackbarModel = new CustomSnackbarModel
                    {
                        Text = message.Message,
                        MessageType = message.Type.ToString()
                    };

                    if (!string.IsNullOrEmpty(message.ActionContent) && message.ActionHandler != null)
                    {
                        r.MainSnackbarMessageQueue.Enqueue(snackbarModel, message.ActionContent, message.ActionHandler);
                    }
                    else
                    {
                        r.MainSnackbarMessageQueue.Enqueue(snackbarModel);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            });

            //=============================由主視窗來負責監聽硬體條碼槍的廣播
            _messenger.Register<MVVM_MainWindow, BarcodeScannedMessage>(this, (r, message) =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    //  加上 r. 存取
                    if (r.CurrentView is IBarcodeReceiver receiver)
                    {
                        receiver.ReceiveBarcode(message.Barcode);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            });

            //============================================================ 偵聽「ComPort硬體實體斷線」廣播
            _messenger.Register<MVVM_MainWindow, DeviceDisconnectedMessage>(this, (r, message) =>
            {
                // 改用 InvokeAsync 防止死鎖
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 加上 r. 存取
                    r.ScannerStatus = $"{message.PortName} (異常斷線)";
                    r.ConnectionState = ScannerConnectionState.Disconnected;
                    r.RefreshAvailablePorts();

                    r._snackbarService.ShowSnackbar($"條碼槍 [{message.PortName}] 已中斷連線！請檢查 USB 線路。",
                                SnackbarMessageType.Error,
                                "重新連線", () => r.Reconnect()
                    );
                });
            });

            //============================================================ 監聽「設定已改變」廣播 
            _messenger.Register<MVVM_MainWindow, SystemConfig_Change_Message>(this, (r, message) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    r.SelectedPort = r._config.BarcodeComPort;
                    r.Reconnect();
                });
            });
            //============================================================
            GenerateMenu(0);
            //============================================================
            Navigate(typeof(VM_WorkPageDataEdit));
            //Navigate(typeof(VM_UserControl_temp));
            
            //============================================================
            _logService.Log("系統啟動，主畫面已成功載入。");
        }


        public void Dispose()
        {
            // 1. 停止並清空所有的計時器 (非常重要！切斷 Timer 對 ViewModel 的綁架)
            if (_pingTimer != null)
            {
                _pingTimer.Stop();
                _pingTimer = null;
            }

            if (_uiTimer != null)
            {
                _uiTimer.Stop();
                _uiTimer = null;
            }

            // 2. 解除所有 Messenger 廣播監聽
            _messenger.UnregisterAll(this);
        }
        #region "PHP 伺服器心跳檢測 (Heartbeat)"

        private DispatcherTimer _pingTimer;
        private void StartHeartbeat()
        {
            _pingTimer = new DispatcherTimer();
            _pingTimer.Interval = TimeSpan.FromSeconds(3);

            // 🌟 關鍵修正：進入時先 Stop，執行完再 Start，防止請求重疊塞車
            _pingTimer.Tick += async (s, e) =>
            {
                _pingTimer.Stop();
                await CheckServerStatusAsync();
                _pingTimer.Start();
            };

            _pingTimer.Start();
            _ = CheckServerStatusAsync();
        }

        // ==========================================
        // 持續運行的 UI 計時器
        // ==========================================
        private void StartUiTimer()
        {
            _uiTimer = new DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromSeconds(1);
            _uiTimer.Tick += (s, e) =>
            {
                // (可選) 讓畫面上的時鐘也跟著跳
                CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 只要有記錄起點，就持續計算「當前狀態」維持了多久
                if (_stateStartTime.HasValue)
                {
                    TimeSpan duration = DateTime.Now - _stateStartTime.Value;
                    StateDurationString = $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
                }
            };
            _uiTimer.Start(); 
        }



        private async Task CheckServerStatusAsync()
        {
            // 呼叫 API 進行 Ping (現在是打靜態檔案了)
            var result = await _apiService.PingServerAsync();

            // 先決定這次 Ping 回來的「新狀態」是什麼
            PhpServerState newState;

            if (result.IsSuccess)
            {
                // 依據是否登入給予對應狀態
                newState =  PhpServerState.Online ;

                // 伺服器健康，把下次偵測時間拉長到 30 秒！
                if (_pingTimer.Interval.TotalSeconds != 30)
                {
                    _pingTimer.Interval = TimeSpan.FromSeconds(30);
                }
            }
            else
            {
                newState = PhpServerState.Offline;

                // ：斷線了！把偵測時間縮短為 3 秒，積極尋找網路恢復的瞬間！
                if (_pingTimer.Interval.TotalSeconds != 3)
                {
                    _pingTimer.Interval = TimeSpan.FromSeconds(3);
                }
            }

            // 如果狀態發生改變，就重新開始計算「狀態持續時間」
            if (ServerState != newState)
            {
                // 🌟 核心關鍵：在重置時間起點之前，先把「上一個狀態維持的總時長」拿出來備用！
                // (例如：如果是從斷線恢復，這串文字就會是 "00:05:23"，代表總共斷線了五分多鐘)
                string lastDuration = string.IsNullOrEmpty(StateDurationString) ? "00:00:00" : StateDurationString;

                // 1. 紀錄狀態改變的當下時間 (重置下一個狀態的起始時間)
                _stateStartTime = DateTime.Now;

                // 2. 判斷是斷線還是恢復連線，並寫入 Log
                if (newState == PhpServerState.Offline)
                {
                    // 變成斷線狀態 (黃色警告)
                    _logService.Log($"PHP 伺服器心跳檢測失敗，已中斷連線！(前次穩定連線時長: {lastDuration}，目標網址: {_config.PhpServerUrl})", LogLevel.Warning);
                }
                else if (newState == PhpServerState.Online && ServerState == PhpServerState.Offline)
                {
                    // 從斷線恢復為正常連線 (成功資訊)
                    _logService.Log($"PHP 伺服器連線已成功恢復！(本次異常斷線總時長: {lastDuration})", LogLevel.Success);
                }
                else if (newState == PhpServerState.Online && ServerState == PhpServerState.Testing)
                {
                    // 程式剛開啟時的初次連線成功 (成功資訊)
                    _logService.Log($"PHP 伺服器初次連線成功。(連線準備耗時: {lastDuration}，目標網址: {_config.PhpServerUrl})", LogLevel.Success);
                }
            }

            // 正式更新綁定給 UI 的狀態
            ServerState = newState;
        }

        #endregion

        #region "導覽選單 (Permission Filter)"

        private readonly List<NavigationItem> _allSystemMenus = new()
        {
            // 首頁與總覽
            //new NavigationItem { Title = "Home", Icon = PackIconKind.Home, RequiredLevel = 0, TargetViewModelType = typeof(UserControl1ViewModel) },
            new NavigationItem {Title = "工單 資訊輸入",Icon = PackIconKind.FileDocumentEditOutline,TargetViewModelType = typeof(VM_WorkPageDataEdit)},
            new NavigationItem { Title = "IPI ", Icon = PackIconKind.FileDocumentEditOutline, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
            new NavigationItem { Title = "FIR", Icon = PackIconKind.FileDocumentEditOutline, RequiredLevel = 0, TargetViewModelType = typeof(VM_FIR) },
            new NavigationItem { Title = "品質異常單", Icon = PackIconKind.FileDocumentEditOutline, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
            new NavigationItem { Title = "設備及時狀態", Icon = PackIconKind.MonitorDashboard, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
    
   
            // 群組節點：資訊查詢
            new NavigationItem
            {
                Title = "資訊查詢",
                Icon = PackIconKind.DatabaseSearchOutline,
                RequiredLevel = 0,
                TargetViewModelType = null,
                SubItems =
                {
                     new NavigationItem { Title = "過站狀態", Icon = PackIconKind.TransitConnection, RequiredLevel = 0, TargetViewModelType = typeof(VM_WipStatus) },
                     new NavigationItem { Title = "IPI趨勢", Icon = PackIconKind.ChartLine, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                     new NavigationItem { Title = "FIR趨勢", Icon = PackIconKind.ChartBellCurve, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                     new NavigationItem { Title = "設備生產紀錄", Icon = PackIconKind.ClipboardTextClockOutline, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                     new NavigationItem { Title = "庫存查詢", Icon = PackIconKind.Warehouse, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                     new NavigationItem { Title = "刀具使用紀錄", Icon = PackIconKind.SawBlade, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                }
            },

             // 群組節點：系統設定
            new NavigationItem
            {
                Title = "系統設定",
                Icon = PackIconKind.Cogs,
                RequiredLevel = 0,
                TargetViewModelType = null,
                SubItems =
                {
                    new NavigationItem { Title = "工單建立", Icon = PackIconKind.FileDocumentPlusOutline, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                    new NavigationItem { Title = "參數設定", Icon = PackIconKind.CogBox, RequiredLevel = 0, TargetViewModelType = typeof(VM_System_SetDetail) },
                    new NavigationItem { Title = "進階設定", Icon = PackIconKind.HammerWrench, RequiredLevel = 3, TargetViewModelType = typeof(VM_UserControl_temp) },
                     new NavigationItem { Title = "進階設定", Icon = PackIconKind.HammerWrench, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                    //  new NavigationItem
                    //{
                    //    Title = "系統設定XXXX",
                    //    Icon = PackIconKind.Cogs,
                    //    RequiredLevel = 0,
                    //    TargetViewModelType = null,
                    //    SubItems =
                    //    {
                    //        new NavigationItem { Title = "工單建立", Icon = PackIconKind.FileDocumentPlusOutline, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                    //        new NavigationItem { Title = "參數設定", Icon = PackIconKind.TuneVariant, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                    //        new NavigationItem { Title = "進階設定", Icon = PackIconKind.HammerWrench, RequiredLevel = 3, TargetViewModelType = typeof(VM_UserControl_temp) },
                    //         new NavigationItem { Title = "進階設定", Icon = PackIconKind.HammerWrench, RequiredLevel = 0, TargetViewModelType = typeof(VM_UserControl_temp) },
                    //    }
                    //}
                }
            },

             // 本機設定
             new NavigationItem { Title = "本機設定", Icon = PackIconKind.MonitorEdit, RequiredLevel = 0, TargetViewModelType = typeof(MVVM_SystemConfig) }
            };

        /// <summary>
        /// 重新依據權限生成菜單
        /// </summary>
        /// <param name="userLevel"></param>
        private void GenerateMenu(int userLevel)
        {

            MenuItems.Clear();

            // 將完整菜單丟進過濾器，把符合權限的菜單加進畫面上
            foreach (var item in FilterMenus(_allSystemMenus, userLevel))
            {
                MenuItems.Add(item);
            }
        }

        // ==========================================
        // 核心過濾引擎 (遞迴處理 Recursion)
        // ==========================================
        private IEnumerable<NavigationItem> FilterMenus(IEnumerable<NavigationItem> sourceMenus, int userLevel)
        {
            foreach (var item in sourceMenus)
            {
                // 條件一：如果這項功能的權限要求高於使用者，直接剪掉 (剔除)
                if (item.RequiredLevel > userLevel)
                    continue;

                // 必須建立一個「複本 (Clone)」，否則我們會破壞原本的 _allSystemMenus 結構
                var clonedItem = new NavigationItem
                {
                    Title = item.Title,
                    Icon = item.Icon,
                    RequiredLevel = item.RequiredLevel,
                    TargetViewModelType = item.TargetViewModelType
                };

                // 如果這個節點有子選單，就「遞迴」去過濾它的子選單
                if (item.SubItems != null && item.SubItems.Any())
                {
                    foreach (var subItem in FilterMenus(item.SubItems, userLevel))
                    {
                        clonedItem.SubItems.Add(subItem);
                    }

                    // 防呆 UX：
                    // 如果它是一個「純資料夾 (TargetViewModelType == null)」，而且過濾完之後，
                    // 發現底下的子選單全部都被權限擋住了 (SubItems 是空的)，
                    // 那這個空的空資料夾就不要顯示出來，避免干擾使用者！
                    if (clonedItem.TargetViewModelType == null && !clonedItem.SubItems.Any())
                    {
                        continue;
                    }
                }

                yield return clonedItem;
            }

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

                // 🌟 切換畫面後，自動把側邊抽屜收起來 (UX 體驗優化)
                IsMenuOpen = false;
            }
        }

        #endregion

        #region "登入登出邏輯"

        [RelayCommand]
        private async Task ToggleLoginState()
        {
            // 【情境 A：使用者目前已登入，想要登出】
            if (IsLoggedIn)
            {
                // 1. 彈出確認視窗 (防呆)
                var confirmResult = await MaterialMessageBox.ShowAsync("確定要登出系統嗎？", "登出確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmResult == MessageBoxResult.Yes)
                {
                    // 2. 清除登入者的資料
                    User_Name = "NA";
                    User_ID = "NA";
                    //PhpConnectionStatus = "未連線";

                    // 3. 切換按鈕狀態與文字
                    IsLoggedIn = false;
                    LoginButtonText = "系統登入";
                    LoginButtonIcon = PackIconKind.Login;

                    // 將權限降回 0 (訪客)，選單會自動把機台控制等高權限功能藏起來！
                    GenerateMenu(0);

                    // 安全防護：強制把畫面切換回首頁 (避免使用者登出後還停留在機台參數設定畫面)
                    //Navigate(typeof(UserControl1ViewModel));
                    Navigate(typeof(VM_WorkPageDataEdit));
                }
            }
            // 【情境 B：使用者未登入，想要登入】
            else
            {
                // 1. 透過 DI 產生登入卡片並彈出
                var loginVM = _serviceProvider.GetService(typeof(VM_Login));
                var result = await DialogHost.Show(loginVM, "RootDialog");

                // 2. 判斷是否登入成功
                if (result is LoginResponseModel loginData)
                {
                    // 更新使用者資訊
                    User_ID = loginData.User_ID;
                    User_Name = loginData.UserName;
                    //PhpConnectionStatus = "已連線 (已登入)";

                    // 3. 切換按鈕狀態與文字
                    IsLoggedIn = true;
                    LoginButtonText = "登出";
                    LoginButtonIcon = PackIconKind.Logout;

                    // 4. 重新依據權限生成菜單
                    GenerateMenu(loginData.Level);

                    //MessageBox.Show($"登入成功！\n歡迎回來，{loginData.UserName}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        #endregion

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
            //else if (AvailablePorts.Count > 0)
            //{
            //    // 如果原本選的 Port 已經被拔掉了（不存在了），才預設選第一個
            //    SelectedPort = AvailablePorts[0];
            //}
            else
            {
                //SelectedPort = "未設定";
                // 保留原字串，讓程式確切知道「哪個 Port 未連接」
                SelectedPort = currentSelectedBackup;
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
            // 防呆：如果是空值或是我們自定義的異常字串，就不要浪費資源去嘗試開啟 Port
            if (string.IsNullOrEmpty(portName) || portName == "未設定" || portName == "未連接")
            {
                ScannerStatus = "未設定/未連接";
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


                _snackbarService.ShowSnackbar($"條碼槍 {portName} 已成功連線", SnackbarMessageType.Success);
            }
            else
            {
                ScannerStatus = $"{portName} (連線失敗)";
                ConnectionState = ScannerConnectionState.Failed;

                _snackbarService.ShowSnackbar($"無法開啟 {portName}！請檢查線路是否鬆脫或被其他軟體佔用。",
                            SnackbarMessageType.Error,
                            "重試", () => Reconnect()
                );
            }
        }
        #endregion 




    }



}
