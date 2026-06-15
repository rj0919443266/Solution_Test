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

namespace WpfControlLibrary1
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class UserControl1 : UserControl
    {
        public UserControl1()
        {
            InitializeComponent();
            this.Unloaded += UserControl1_Unloaded;
        }

        private void UserControl1_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dispose the view model when the control is unloaded
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

}
