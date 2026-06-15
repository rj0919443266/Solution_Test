using System.Linq; // 💡 必須引用這個，才能使用 LastOrDefault 和 Cast
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.ViewModels;

namespace WpfControlLibrary1
{
    public partial class UserControl_WorkPageDataEdit : UserControl
    {
        public UserControl_WorkPageDataEdit()
        {
            InitializeComponent();

            // 註冊接收捲動訊息
            WeakReferenceMessenger.Default.Register<ScrollToLastRowMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 1. 捲動左側 (實際作業歷程)：永遠捲動到最底部的最後一筆 (包含非標準站)
                    if (DgActualWorkHistory.Items.Count > 0)
                    {
                        var lastActualItem = DgActualWorkHistory.Items[DgActualWorkHistory.Items.Count - 1] as VM_WorkPageDataEdit.ActualWorkItemModel;
                        if (lastActualItem != null)
                        {
                            DgActualWorkHistory.ScrollIntoView(lastActualItem);
                        }
                    }

                    // 2. 捲動右側 (標準作業工序)：精準對齊最後一個標準站
                    if (DgActualWorkHistory.Items.Count > 0 && DgStandardWorkFlow.Items.Count > 0)
                    {
                        // 💡 核心邏輯：從左側的歷史紀錄中，找到「最後一個標準站點 (非 Extra)」
                        var lastStandardItem = DgActualWorkHistory.Items
                            .Cast<VM_WorkPageDataEdit.ActualWorkItemModel>()
                            .LastOrDefault(x => x.IsExtra == false);

                        if (lastStandardItem != null)
                        {
                            // 尋找右側表格中，站點名稱與該標準站相同的項目
                            var matchingRouteItem = DgStandardWorkFlow.Items
                                .Cast<VM_WorkPageDataEdit.SOPFlowItemModel>()
                                .FirstOrDefault(route => route.WorkStation == lastStandardItem.WorkStation);

                            if (matchingRouteItem != null)
                            {
                                // 精準捲動到對應的標準站點
                                DgStandardWorkFlow.ScrollIntoView(matchingRouteItem);
                            }
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            });
        }
    }
}