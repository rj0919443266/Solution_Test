using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Data;
using System.Windows;
using WpfApp1.ViewModels;

using System.IO;
using WpfControlLibrary1;
using WpfControlLibrary1.ViewModels;
using WpfControlLibrary1.Services;


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
                C_XML.Save_XML_from_object(appConfig, configPath);
            }
            services.AddSingleton(appConfig);

            //================================= 註冊 Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default); //StrongReferenceMessenger

            //=================================  註冊條碼槍服務 (單例：全域唯一)
            services.AddSingleton<WpfControlLibrary1.BarcodeScannerService>();
            //=================================  註冊 API 服務 (單例：全域唯一)
            services.AddHttpClient<ApiService>();
            //=================================  註冊 ViewModel (傳入 IMessenger)
            services.AddTransient<MVVM_MainWindow>();

            services.AddSingleton<UserControl1ViewModel>();
            services.AddSingleton<UserControl2ViewModel>();
            services.AddSingleton<UserControl3ViewModel>();
            services.AddSingleton<MVVM_SystemConfig>();
            services.AddTransient<VM_Login>();

            // 3. 關鍵：把 MainWindow 本身也註冊進 DI 容器中
            services.AddSingleton<MainWindow>();

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

            Services.GetRequiredService<UserControl1ViewModel>();
            Services.GetRequiredService<UserControl2ViewModel>();
            Services.GetRequiredService<UserControl3ViewModel>();

            // 從 DI 容器拿到由系統完全注入好的 MainWindow 實例
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        // 覆寫 OnExit，在程式關閉時安全釋放 COM Port
        protected override void OnExit(ExitEventArgs e)
        {
            var scannerService = Services.GetRequiredService<WpfControlLibrary1.BarcodeScannerService>();
            scannerService.Stop();

            base.OnExit(e);
        }
    }

}
