using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfControlLibrary1
{
    public partial class UserControl_Chart : UserControl
    {

        // ==========================================
        // 註冊 Dependency Property，讓外部 ViewModel 可以綁定圖表雙擊事件
        // ==========================================
        public static readonly DependencyProperty ChartDoubleClickCommandProperty =
            DependencyProperty.Register("ChartDoubleClickCommand", typeof(ICommand), typeof(UserControl_Chart), new PropertyMetadata(null));

        public ICommand ChartDoubleClickCommand
        {
            get { return (ICommand)GetValue(ChartDoubleClickCommandProperty); }
            set { SetValue(ChartDoubleClickCommandProperty, value); }
        }

        public UserControl_Chart()
        {
            InitializeComponent();
        }

        // ==========================================
        // 輔助函式：利用反射(Reflection)自動完整複製座標軸的所有屬性
        // ==========================================
        private Axis CloneAxis(Axis originalAxis, bool isYAxis)
        {
            var newAxis = new Axis();

            // 1. 抓出 Axis 類別裡面「所有可以讀取且可以寫入的公開屬性」
            var properties = typeof(Axis).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    try
                    {
                        // 2. 自動將原屬性的值，完整貼到新座標軸上 (再也不怕漏掉任何設定！)
                        prop.SetValue(newAxis, prop.GetValue(originalAxis));
                    }
                    catch
                    {
                        // 忽略 LiveCharts 內部受保護或唯讀的狀態 
                    }
                }
            }

            // 3. 針對新視窗，覆寫我們需要的「自適應邊界」邏輯
            if (isYAxis)
            {
                newAxis.MinLimit = null;
                newAxis.MaxLimit = null;
            }
            else // X 軸
            {
                newAxis.MinLimit = originalAxis.MinLimit; // 保留原有的起點
                newAxis.MaxLimit = null;                  // 右側空間釋放，自動適應寬度
            }

            return newAxis;
        }


        // ==========================================
        //1. 註冊 Dependency Properties 讓外部可以 Binding
        //將 ObservableCollection 改為通用的 IEnumerable 
        // ==========================================
        public static readonly DependencyProperty SeriesProperty =
            DependencyProperty.Register("Series", typeof(IEnumerable<ISeries>), typeof(UserControl_Chart), new PropertyMetadata(null));

        public IEnumerable<ISeries> Series
        {
            get { return (IEnumerable<ISeries>)GetValue(SeriesProperty); }
            set { SetValue(SeriesProperty, value); }
        }

        public static readonly DependencyProperty XAxesProperty =
            DependencyProperty.Register("XAxes", typeof(IEnumerable<Axis>), typeof(UserControl_Chart), new PropertyMetadata(null));

        public IEnumerable<Axis> XAxes
        {
            get { return (IEnumerable<Axis>)GetValue(XAxesProperty); }
            set { SetValue(XAxesProperty, value); }
        }

        public static readonly DependencyProperty YAxesProperty =
            DependencyProperty.Register("YAxes", typeof(IEnumerable<Axis>), typeof(UserControl_Chart), new PropertyMetadata(null));

        public IEnumerable<Axis> YAxes
        {
            get { return (IEnumerable<Axis>)GetValue(YAxesProperty); }
            set { SetValue(YAxesProperty, value); }
        }

        // ==========================================
        //  封裝阻擋滾輪事件的邏輯 
        // ==========================================
        private void Chart_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        // ==========================================
        // 解決 LiveCharts 吃掉右鍵事件的問題
        // ==========================================
        private void Chart_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 確保選單存在
            if (ChartMenu != null)
            {
                // 強制展開右鍵選單
                ChartMenu.IsOpen = true;

                // 告訴 WPF 這個滑鼠事件已經處理完了，請 LiveCharts 不要再往下執行
                e.Handled = true;
            }
        }

        // ==========================================
        // 🌟 核心共用方法：把目前的圖表轉換為高畫質圖片
        // ==========================================
        private RenderTargetBitmap GetChartImage()
        {
            GanttChartControl.UpdateLayout();
            int width = (int)GanttChartControl.ActualWidth;
            int height = (int)GanttChartControl.ActualHeight;

            if (width == 0 || height == 0) return null;

            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            DrawingVisual drawingVisual = new DrawingVisual();

            using (DrawingContext context = drawingVisual.RenderOpen())
            {
                // 墊一層深色背景，避免 PNG 透明底導致文字看不見
                //Brush darkBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                Brush currentThemeBrush = (Brush)Application.Current.FindResource("MaterialDesignPaper");
                context.DrawRectangle(currentThemeBrush, null, new Rect(0, 0, width, height));
                context.DrawRectangle(new VisualBrush(GanttChartControl), null, new Rect(0, 0, width, height));
            }

            rtb.Render(drawingVisual);
            return rtb;
        }

        // ==========================================
        // 功能 1：複製圖片 (放入剪貼簿)
        // ==========================================
        private async void MenuCopyImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var image = GetChartImage();
                if (image != null)
                {
                    Clipboard.SetImage(image);
                    // 可以選擇是否要跳出提示
                    // MessageBox.Show("已複製到剪貼簿！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"複製失敗：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 功能 2：儲存圖片 (跳出存檔視窗)
        // ==========================================
        private async void MenuSaveImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var image = GetChartImage();
                if (image == null) return;

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "PNG 圖片 (*.png)|*.png",
                    Title = "另存圖片",
                    FileName = $"{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));

                    using (FileStream stream = File.Create(saveFileDialog.FileName))
                    {
                        encoder.Save(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"儲存圖片時發生錯誤：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 功能 3：在開新視窗開啟 (全螢幕互動檢視)
        // ==========================================
        private void MenuOpenNewWindow_Click(object sender, RoutedEventArgs e)
        {
            // 🌟 1. 透過自動複製精靈，產生全新的 X 軸與 Y 軸
            var standaloneXAxes = new ObservableCollection<Axis>();
            if (this.XAxes != null)
            {
                foreach (var axis in this.XAxes)
                {
                    // 呼叫自動複製函式，並告知這是 X 軸
                    standaloneXAxes.Add(CloneAxis(axis, isYAxis: false));
                }
            }

            var standaloneYAxes = new ObservableCollection<Axis>();
            if (this.YAxes != null)
            {
                foreach (var axis in this.YAxes)
                {
                    // 呼叫自動複製函式，並告知這是 Y 軸
                    standaloneYAxes.Add(CloneAxis(axis, isYAxis: true));
                }
            }

            // 2. 複製一份獨立的 Series 集合 (快照)
            var standaloneSeries = this.Series != null
                ? new ObservableCollection<ISeries>(this.Series)
                : new ObservableCollection<ISeries>();

            // 3. 實例化我們自己寫的「萬用圖表元件」
            var interactiveChart = new UserControl_Chart
            {
                Series = standaloneSeries,
                XAxes = standaloneXAxes,
                YAxes = standaloneYAxes,
                Margin = new Thickness(16)
            };

            // 4. 隱藏新視窗中的「開新視窗」選項與分隔線
            if (interactiveChart.MenuItem_OpenNewWindow != null)
                interactiveChart.MenuItem_OpenNewWindow.Visibility = Visibility.Collapsed;

            if (interactiveChart.MenuSeparator != null)
                interactiveChart.MenuSeparator.Visibility = Visibility.Collapsed;

            // 5. 實例化專屬的深色主題 XAML 視窗 (MetroWindow)
            var viewerWindow = new Window_ChartViewer();
            viewerWindow.ChartContainer.Content = interactiveChart;
            viewerWindow.Show();
        }

        // ==========================================
        // 處理圖表滑鼠雙擊，並利用 LiveCharts 精準計算點擊到哪點
        // ==========================================
        private void Chart_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ChartDoubleClickCommand == null) return;

            // 1. 取得滑鼠在圖表上的相對座標
            Point mousePos = e.GetPosition(GanttChartControl);

            // 2. LiveCharts 2.0.4 正式版使用 LvcPointD (Double 精度)
            var lvcPoint = new LiveChartsCore.Drawing.LvcPointD(mousePos.X, mousePos.Y);

            // 3. 呼叫 LiveCharts 核心內建方法，撈出此滑鼠位置底下的所有資料點
            var points = GanttChartControl.GetPointsAt(lvcPoint);

            // 4. 取出被點擊到的第一個資料點
            var clickedPoint = points?.FirstOrDefault();
            if (clickedPoint != null)
            {
                // 泛用化修改：直接將整個 ChartPoint 傳給 ViewModel
                // 讓 ViewModel 決定是要拿 X軸、Y軸，還是原始綁定的資料模型 (Model)
                if (ChartDoubleClickCommand.CanExecute(clickedPoint))
                {
                    ChartDoubleClickCommand.Execute(clickedPoint);
                }
            }
        }
    }
}