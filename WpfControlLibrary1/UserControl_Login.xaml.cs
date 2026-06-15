using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfControlLibrary1.ViewModels;

namespace WpfControlLibrary1
{
    /// <summary>
    /// UserControl_Login.xaml 的互動邏輯
    /// </summary>
    public partial class UserControl_Login : UserControl
    {
        public UserControl_Login()
        {
            InitializeComponent();
        }
        //private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        //{
        //    // 確認目前的 DataContext 是我們的 LoginViewModel
        //    if (this.DataContext is VM_Login vm)
        //    {
        //        // 將 PasswordBox 裡面的密碼，手動指派給 ViewModel 的屬性
        //        vm.Password = ((PasswordBox)sender).Password;
        //    }
        //}
    }
}
