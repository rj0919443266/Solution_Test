using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using Notifications.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public  class C_Base
    {
    }

    #region 導航選單項目

    public partial class NavigationItem : ObservableObject
    {
        public string Title { get; set; }
        public PackIconKind Icon { get; set; }

        public int RequiredLevel { get; set; }
        public ObservableCollection<NavigationItem> SubItems { get; set; }
        public Type TargetViewModelType { get; set; }

        // 提供給 XAML 的 DataTrigger 判斷是否有子節點
        public bool HasSubItems => SubItems != null && SubItems.Count > 0;

        // 控制選單展開與收合的屬性
        [ObservableProperty]
        private bool _isExpanded;

        public NavigationItem()
        {
            SubItems = new ObservableCollection<NavigationItem>();
        }
    }
    #endregion

    
}
