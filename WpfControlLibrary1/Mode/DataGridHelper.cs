using System.Windows;
using System.Windows.Controls;

namespace WpfControlLibrary1
{
    /// <summary>
    /// 提供 DataGrid 專用的附加屬性，實現純 MVVM 模式下的自動滾動檢視功能
    /// </summary>
    public static class DataGridHelper
    {
        public static readonly DependencyProperty EnableScrollIntoViewProperty =
            DependencyProperty.RegisterAttached(
                "EnableScrollIntoView",
                typeof(bool),
                typeof(DataGridHelper),
                new PropertyMetadata(false, OnEnableScrollIntoViewChanged));

        public static bool GetEnableScrollIntoView(DependencyObject obj) => (bool)obj.GetValue(EnableScrollIntoViewProperty);
        public static void SetEnableScrollIntoView(DependencyObject obj, bool value) => obj.SetValue(EnableScrollIntoViewProperty, value);

        private static void OnEnableScrollIntoViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                if ((bool)e.NewValue)
                {
                    dataGrid.SelectionChanged += DataGrid_SelectionChanged;
                }
                else
                {
                    dataGrid.SelectionChanged -= DataGrid_SelectionChanged;
                }
            }
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                // 當選取項改變時，強制讓 UI 滾動聚焦到該列項目
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(dataGrid.SelectedItem);
            }
        }
    }
}