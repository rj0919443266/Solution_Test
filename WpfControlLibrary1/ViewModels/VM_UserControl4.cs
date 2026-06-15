using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;//解析 ChartPoint
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WpfControlLibrary1.Services;



namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_UserControl4 : ObservableObject 
    {
        private readonly IThemeService _themeService;  // 注入主題服務
        [ObservableProperty]
        private SolidColorPaint _legendTextPaint;
        //======================================
        // 1. 定義圖表的資料序列 (Series)
        [ObservableProperty]
        private ISeries[] _machineOutputSeries;

        // 2. 定義 X 軸 (例如：時間、班別)
        [ObservableProperty]
        private Axis[] _xAxes;

        // 3. 定義 Y 軸 (例如：產量數量)
        [ObservableProperty]
        private Axis[] _yAxes;

        //======================================
        [ObservableProperty]
        private ISeries[] _machineOutputSeries_2;
        // 定義 X 軸
        [ObservableProperty]
        private Axis[] _xAxes_2;

        // 定義 Y 軸 
        [ObservableProperty]
        private Axis[] _yAxes_2;
        //======================================
        public VM_UserControl4(IThemeService themeService)
        {
            _themeService = themeService;
            // 初始化圖表設定
            InitializeChart();
            InitializeChart_2();
        }

        #region "直方圖"
        private void InitializeChart()
        {
            // 直接使用注入的服務，維持純淨的 MVVM
            var themeTextPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
            var themeLinePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };
            var themeAccentPaint = new SolidColorPaint(_themeService.GetSkiaColor("SecondaryHueMidBrush", "#CDDC39"));

            LegendTextPaint = themeTextPaint;

            MachineOutputSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "A機台產能",
                    Values = new ObservableCollection<int> { 45, 68, 55, 72, 85, 60, 92, 50 },
                    Fill = themeAccentPaint,
                    DataLabelsPaint = themeTextPaint,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "08:00", "09:00", "10:00", "11:00", "13:00", "14:00", "15:00", "16:00" },
                    LabelsRotation = 15,
                    LabelsPaint = themeTextPaint,
                    SeparatorsPaint = themeLinePaint
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    Name = "生產數量 (PCS)",
                    MinLimit = 0,
                    NamePaint = themeTextPaint,
                    LabelsPaint = themeTextPaint,
                    SeparatorsPaint = themeLinePaint
                }
            };
        }

        // 模擬 MES 系統收到新資料的動作
        [RelayCommand]
        private void AddRandomData()
        {
            // 取得目前的資料集合
            var series = (ColumnSeries<int>)MachineOutputSeries[0];
            var values = (ObservableCollection<int>)series.Values!;

            // 移除最舊的一筆 (最左邊)，加入最新的一筆 (最右邊)
            values.RemoveAt(0);
            values.Add(new Random().Next(40, 100));
        }
        #endregion

        #region ""
        private void InitializeChart_2()
        {
            var themeTextPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
            var themeLinePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };

            // 🌟 1. 建立多條「曲線」 (LineSeries)
            MachineOutputSeries_2 = new ISeries[]
            {
                new LineSeries<DateTimePoint>
                {
                    Name = "A機台溫度", // 這個名稱會顯示在圖例 (Legend) 上
                    // LineSmoothness = 0 代表折線，0.5 ~ 1 代表平滑曲線
                    LineSmoothness = 0.6,
                    Fill = null, // 設為 null 代表純線條，不填滿下方區域
                    GeometrySize = 8, // 資料點的圓圈大小
                    Stroke = new SolidColorPaint(_themeService.GetSkiaColor("PrimaryHueMidBrush", "#673AB7")) { StrokeThickness = 3 },
                    GeometryStroke = new SolidColorPaint(_themeService.GetSkiaColor("PrimaryHueMidBrush", "#673AB7")) { StrokeThickness = 3 },
                    
                    // 🌟 直接給予 XY 資料 (時間, 數值)
                    Values = new ObservableCollection<DateTimePoint>
                    {
                        new DateTimePoint(new DateTime(2024, 5, 26, 8, 0, 0), 45),
                        new DateTimePoint(new DateTime(2024, 5, 26, 8, 15, 0), 68), // 15分鐘後
                        new DateTimePoint(new DateTime(2024, 5, 26, 9, 0, 0), 55)   // 45分鐘後，X軸距離會自動拉長！
                    }
                },
                new LineSeries<DateTimePoint>
                {
                    Name = "B機台溫度",
                    LineSmoothness = 0.6,
                    Fill = null,
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(_themeService.GetSkiaColor("SecondaryHueMidBrush", "#CDDC39")) { StrokeThickness = 3 },
                    GeometryStroke = new SolidColorPaint(_themeService.GetSkiaColor("SecondaryHueMidBrush", "#CDDC39")) { StrokeThickness = 3 },

                    Values = new ObservableCollection<DateTimePoint>
                    {
                        // 兩條線的時間點可以完全不一樣，LiveCharts 會自動對齊 X 軸
                        new DateTimePoint(new DateTime(2024, 5, 26, 8, 10, 0), 50),
                        new DateTimePoint(new DateTime(2024, 5, 26, 8, 40, 0), 75),
                        new DateTimePoint(new DateTime(2024, 5, 26, 9, 20, 0), 60)
                    }
                }
            };

            // 🌟 2. 將 X 軸定義為真正的「時間軸」
            XAxes_2 = new Axis[]
            {
                new DateTimeAxis(TimeSpan.FromMinutes(30), date => date.ToString("HH:mm")) // 每 30 分鐘一個刻度，格式化為 時:分
                {
                    Name = "紀錄時間",
                    LabelsRotation = 15,
                    LabelsPaint = themeTextPaint,
                    SeparatorsPaint = themeLinePaint,
                    NamePaint = themeTextPaint
                }
            };

            YAxes_2 = new Axis[]
            {
                new Axis
                {
                    Name = "溫度 (°C)",
                    MinLimit = 0,
                    NamePaint = themeTextPaint,
                    LabelsPaint = themeTextPaint,
                    SeparatorsPaint = themeLinePaint
                }
            };
        }

        // 🌟 3. 模擬從 API / MES 系統直接匯入資料的行為
        [RelayCommand]
        private void AddRandomData_2()
        {
            // 取得 A 機台的資料集合
            var seriesA = (LineSeries<DateTimePoint>)MachineOutputSeries_2[0];
            var valuesA = (ObservableCollection<DateTimePoint>)seriesA.Values!;

            // 取得當前時間與隨機數值
            DateTime now = DateTime.Now;
            int randomTemp = new Random().Next(40, 100);

            // 直接把 XY 座標 (時間點與數值) 塞進去，圖表就會即時往右長出曲線！
            valuesA.Add(new DateTimePoint(now, randomTemp));

            // (可選) 保持圖表效能，移除太舊的資料，例如只保留最新 20 筆
            if (valuesA.Count > 20)
            {
                valuesA.RemoveAt(0);
            }
        }
        #endregion

        [RelayCommand]
        private  async Task ChartPointClicked(IEnumerable<ChartPoint> points)
        {
            // 防呆：如果沒有點擊到任何東西就返回
            if (points == null || !points.Any()) return;

            // 取得使用者點中範圍內的第一個資料點
            var point = points.First();

            // LiveCharts2 中，原始資料放在 Context.DataSource
            if (point.Context.DataSource is int intValue)
            {
                // 如果點擊的是第一張圖 (直方圖)，它的資料型態是 int
                await MaterialMessageBox.ShowAsync($"【A機台產能】\n該時段的生產數量為：{intValue} PCS", "資料點詳細資訊");
            }
            else if (point.Context.DataSource is LiveChartsCore.Defaults.DateTimePoint dtPoint)
            {
                // 如果點擊的是第二張圖 (曲線圖)，它的資料型態是 DateTimePoint
                await MaterialMessageBox.ShowAsync($"【機台溫度紀錄】\n紀錄時間：{dtPoint.DateTime:HH:mm:ss}\n測量溫度：{dtPoint.Value} °C", "資料點詳細資訊");
            }
        }
    }
}
