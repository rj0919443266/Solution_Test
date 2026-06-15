using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using WpfApp1.ViewModels;
using WpfControlLibrary1;

using MahApps.Metro.Controls;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        // 🌟 透過建構子注入，DI 容器會自動把已經裝好 IMessenger 的 ViewModel 傳進來！
        public MainWindow(MVVM_MainWindow viewModel)
        {
            InitializeComponent();

            // 🌟 將系統傳進來的 viewModel 綁定給 DataContext
            this.DataContext = viewModel;

            
        }

        // 關閉視窗邏輯
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 呼叫視窗內建的 Close() 方法來關閉程式
            this.Close();
        }

        // 縮小視窗邏輯
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // 放大/還原視窗邏輯
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal; // 如果已放大，就還原
            }
            else
            {
                this.WindowState = WindowState.Maximized; // 如果是正常大小，就放大
            }
        }

        ///// <summary>
        ///// 處理樹狀選單點擊事件
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        //{
        //    // 確定點擊到的是 NavItem，而且有設定 TargetViewModelType
        //    if (e.NewValue is NavigationItem selectedItem && selectedItem.TargetViewModelType != null)
        //    {
        //        // 拿到我們的 MVVM_MainWindow
        //        var vm = (MVVM_MainWindow)this.DataContext;

        //        // 呼叫 NavigateCommand 進行畫面切換
        //        vm.NavigateCommand.Execute(selectedItem.TargetViewModelType);

        //        // (可選) 切換畫面後，自動把側邊抽屜收起來
        //        MainDrawerHost.IsLeftDrawerOpen = false;
        //    }
        //}

    
    }
}