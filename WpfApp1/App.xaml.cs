using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using WpfApp1.ViewModels;
using WpfControlLibrary1;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;
using WpfControlLibrary1.ViewModels;


namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        // 定義全域的 DI 容器
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            var services = new ServiceCollection();
            //=================================吐司 通知服務 (單例：全域唯一)
            services.AddSingleton<INotificationService, NotificationService>();

            //=================================Snackbar 服務 (單例：全域唯一)
            // 註冊 MaterialDesign 的 Queue (通常已由元件提供)
            services.AddSingleton<ISnackbarMessageQueue>(new SnackbarMessageQueue(TimeSpan.FromSeconds(3)));
            // 註冊您的服務
            services.AddSingleton<ISnackbarService, SnackbarService>();
     
            //=================================設定檔
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemConfig.xml");
            SystemConfig appConfig;

            if (File.Exists(configPath))
            {
                // 如果檔案存在，使用你的 C_XML 工具反序列化讀取
                appConfig = C_XML.Get_obj_From_XML<SystemConfig>(configPath);

                // 預防萬一檔案內容損毀讀出 null，給個保底的全新實例
                if (appConfig == null) appConfig = new SystemConfig();
            }
            else
            {
                // 如果檔案不存在，建立預設值，並立刻存成 XML 檔案！(這對現場裝機非常友善)
                appConfig = new SystemConfig();

                // 🌟 修正：只有在電腦「第一次開程式、全新裝機」時，才塞入出廠預設值
                appConfig.DepartmentPriorityKeywords.Add("pd0001");
                appConfig.DepartmentPriorityKeywords.Add("研發");
                appConfig.DepartmentPriorityKeywords.Add("RJ");

                C_XML.Save_XML_from_object(appConfig, configPath);
            }

            services.AddSingleton(appConfig);

            //================================= 註冊 Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default); //StrongReferenceMessenger
            //=================================log 服務 (單例：全域唯一)
            services.AddSingleton<ILogService, LogService>();
            //=================================  註冊條碼槍服務 (單例：全域唯一)
            services.AddSingleton<BarcodeScannerService>();
            //=================================  註冊 API 服務 (單例：全域唯一)
            services.AddHttpClient<ApiService>();
            //=================================  註冊主題色服務 (單例：全域唯一)
            services.AddSingleton<IThemeService, ThemeService>();
            //=================================  註冊 ViewModel (傳入 IMessenger)
            services.AddTransient<MVVM_MainWindow>();
            //================================= 呼叫視窗管理服務，讓它知道要用 WPF 的方式開視窗
            services.AddSingleton<IWindowManager, WpfWindowManager>(); 
            //=================================測試用
            //services.AddSingleton<UserControl1ViewModel>();
            //services.AddSingleton<UserControl2ViewModel>();
            //services.AddSingleton<UserControl3ViewModel>();
            //services.AddSingleton<VM_UserControl4>();

            //=================================
            services.AddSingleton<MVVM_SystemConfig>();
            //=================================登入
            services.AddTransient<VM_Login>();
            //=================================主頁面
            services.AddSingleton<VM_WorkPageDataEdit>(); // 
            //=================================FIR
            services.AddSingleton<VM_FIR>();
            //=================================過站資訊
            services.AddSingleton<VM_WipStatus>();
            //================================LotDetail
            services.AddTransient<Window_LotDetail>();//要支援多開

            services.AddTransient<VM_LotDetail>();
            
            services.AddTransient<VM_LotDetail_WorkData>();
            services.AddTransient<VM_LotDetail_FIR>();
            services.AddTransient<VM_LotDetail_IPI>();
            services.AddTransient<VM_LotDetail_Quality_Nonconformity_Report>();
            services.AddTransient<VM_LotDetail_Temp>();
            //=================================System set detail
            services.AddSingleton<VM_System_SetDetail>();

            //services.AddTransient<VM_System_SetDetail_ListUser>();

            //services.AddTransient<VM_System_SetDetail_ListUserEditDialog>();
            //services.AddTransient<VM_System_SetDetail_ProcessDepartmentEditDialog>();
            //=================================施工中
            services.AddSingleton<VM_UserControl_temp>();
            //=================================
            // 把 MainWindow 本身也註冊進 DI 容器中
            services.AddSingleton<MainWindow>();
            //=================================
            Services = services.BuildServiceProvider();
        }

        //4. 重寫啟動事件，從 DI 容器中把 MainWindow 撈出來開啟
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            //強制喚醒(Instantiate)
            //在畫面彈出前，先把條碼槍服務撈出來，並呼叫 Start() 打開通訊埠
            //var scannerService = Services.GetRequiredService<WpfControlLibrary1.BarcodeScannerService>();
            //scannerService.Start();

            //Services.GetRequiredService<UserControl1ViewModel>();
            //Services.GetRequiredService<UserControl2ViewModel>();
            //Services.GetRequiredService<UserControl3ViewModel>();
            //Services.GetRequiredService<VM_UserControl4>();

            Services.GetRequiredService<VM_WipStatus>();
            Services.GetRequiredService<VM_System_SetDetail>();


            // 從 DI 容器拿到由系統完全注入好的 MainWindow 實例
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        // 覆寫 OnExit，在程式關閉時安全釋放 COM Port
        protected override void OnExit(ExitEventArgs e)
        {
            //==============================    
            try
            {
                //  從 DI 容器中取出 Log 服務，並寫入關閉紀錄
                var logService = Services.GetRequiredService<ILogService>();
                logService.Log("系統關閉，程式已正常結束。");

                //  原本的釋放 COM Port 資源邏輯
                var scannerService = Services.GetRequiredService<BarcodeScannerService>();
                scannerService.Stop();
            }
            catch (Exception ex)
            {
                // 確保關閉程式時就算發生意外，也不會彈出報錯卡死進程
                Console.WriteLine($"關閉程式時發生錯誤: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }

}
