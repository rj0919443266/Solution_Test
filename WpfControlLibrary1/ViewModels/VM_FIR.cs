using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects; // 🌟 確保有引用 DashEffect
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_FIR : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly IThemeService _themeService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWindowManager _windowManager;
        private readonly SystemConfig _config;

        // ======== 查詢條件 ========
        [ObservableProperty] private string _searchProductName = "";
        [ObservableProperty] private string _searchWorkStation = "";
        [ObservableProperty] private string _searchEqNo = "";

        // 搜尋模式控制
        [ObservableProperty] private bool _isSearchByLimit = true; // 預設使用筆數
        [ObservableProperty] private bool _isSearchByDate = false;

        [ObservableProperty] private int _searchLimit = 50;
        [ObservableProperty] private DateTime _searchStartDate;
        [ObservableProperty] private DateTime _searchEndDate;


        [ObservableProperty] private bool _isSearching = false;

        // ======== 下拉選單資料源 ========
        [ObservableProperty] private ObservableCollection<string> _productNameList = new();
        [ObservableProperty] private ObservableCollection<ListWorkStationModel> _workStationList = new();
        [ObservableProperty] private ObservableCollection<EqNoModel> _eqNoList = new();

        // ======== 查詢結果 ========
        [ObservableProperty] private ObservableCollection<FirRecordModel> _firDataList = new(); // 給 DataGrid 用
        [ObservableProperty] private ObservableCollection<ChartContainerModel> _measurementCharts = new(); // 給圖表用

      


        private string[] _currentLotNos; // 暫存查詢後的批號清單

        public VM_FIR(ApiService apiService, IThemeService themeService, IServiceProvider serviceProvider, IWindowManager windowManager, SystemConfig config)
        {
            _apiService = apiService;
            _themeService = themeService;
            _serviceProvider = serviceProvider;
            _windowManager = windowManager;
            _config = config;

            DateTime today = DateTime.Today;
            SearchStartDate = new DateTime(today.Year, today.Month, 1).AddMonths(-3);
            SearchEndDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            _ = LoadBaseDataAsync();
        }

        // ==========================================
        // 初始化下拉選單 (支援優先權排序)
        // ==========================================
        private async Task LoadBaseDataAsync()
        {
            var prodRes = await _apiService.GetProductNamesAsync();
            if (prodRes.IsSuccess && prodRes.Data != null)
                ProductNameList = new ObservableCollection<string>(prodRes.Data);

            var wsRes = await _apiService.PostAsync<ObservableCollection<ListWorkStationModel>>("get_work_stations", new { });
            if (wsRes.IsSuccess && wsRes.Data != null)
                WorkStationList = SortByPriority(wsRes.Data, x => $"{x.BarCode} {x.Definition}");

            var eqRes = await _apiService.PostAsync<ObservableCollection<EqNoModel>>("get_eq_nos", new { });
            if (eqRes.IsSuccess && eqRes.Data != null)
                EqNoList = SortByPriority(eqRes.Data, x => $"{x.BarCode} {x.Definition}");
        }

        private ObservableCollection<T> SortByPriority<T>(IEnumerable<T> source, Func<T, string> textSelector)
        {
            if (source == null) return new ObservableCollection<T>();
            if (_config.DepartmentPriorityKeywords == null || !_config.DepartmentPriorityKeywords.Any())
                return new ObservableCollection<T>(source);

            var sortedList = source.OrderByDescending(item =>
            {
                string text = textSelector(item) ?? string.Empty;
                return _config.DepartmentPriorityKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }).ThenBy(item => textSelector(item)).ToList();

            return new ObservableCollection<T>(sortedList);
        }

        // ==========================================
        // 執行查詢與繪圖
        // ==========================================
        [RelayCommand]
        private async Task SearchDataAsync()
        {
            if (IsSearching) return;
            if (string.IsNullOrWhiteSpace(SearchProductName))
            {
                await MaterialMessageBox.ShowAsync("請輸入產品名稱進行查詢！", "提示");
                return;
            }

            IsSearching = true;
            FirDataList.Clear();
            MeasurementCharts.Clear();

            try
            {
                // 🌟 根據選擇的模式，傳遞對應的 payload 參數
                var payload = new
                {
                    product_name = SearchProductName,
                    work_station = SearchWorkStation,
                    eq_no = SearchEqNo,
                    search_mode = IsSearchByLimit ? "limit" : "date", // 告訴 PHP 使用哪種條件
                    limit = SearchLimit,
                    start_date = SearchStartDate.ToString("yyyy-MM-dd"),
                    end_date = SearchEndDate.ToString("yyyy-MM-dd")
                };

                var result = await _apiService.PostAsync<List<FirRecordModel>>("get_fir_trend", payload);

                if (result.IsSuccess && result.Data != null && result.Data.Any())
                {
                    // 每個 Lot_no 只保留最新的一筆紀錄
                    var uniqueRecords = result.Data
                        .GroupBy(r => r.LotNo)
                        .Select(g => g.OrderByDescending(r => DateTime.Parse(r.CTime)).First())
                        .ToList();

                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // 🌟 核心：針對每一筆紀錄，精算 Alarm, Warning, Normal 的數量
                    foreach (var record in uniqueRecords)
                    {
                        int normal = 0, warning = 0, alarm = 0, unknown = 0;

                        if (!string.IsNullOrWhiteSpace(record.DataJsonString))
                        {
                            try
                            {
                                var rawData = JsonSerializer.Deserialize<FirRawDataModel[]>(record.DataJsonString, jsonOptions);
                                var setData = string.IsNullOrWhiteSpace(record.SetDataJsonString)
                                              ? Array.Empty<FirSetDataModel>()
                                              : JsonSerializer.Deserialize<FirSetDataModel[]>(record.SetDataJsonString, jsonOptions);

                                if (rawData != null)
                                {
                                    foreach (var raw in rawData)
                                    {
                                        var setting = setData?.FirstOrDefault(s => s.Name == raw.Name);
                                        if (setting == null || !double.TryParse(raw.Value, out double actualVal))
                                        {
                                            unknown++;
                                            continue;
                                        }

                                        // 完全對齊 VM_LotDetail_FIR 的判定邏輯
                                        if (setting.LimitsUpper.HasValue && actualVal >= setting.LimitsUpper.Value) alarm++;
                                        else if (setting.LimitsLower.HasValue && actualVal <= setting.LimitsLower.Value) alarm++;
                                        else if (setting.LimitsUpperWarning.HasValue && actualVal >= setting.LimitsUpperWarning.Value) warning++;
                                        else if (setting.LimitsLowerWarning.HasValue && actualVal <= setting.LimitsLowerWarning.Value) warning++;
                                        else normal++;
                                    }
                                }
                            }
                            catch { /* 忽略解析錯誤 */ }
                        }

                        // 寫回 Model 中
                        record.NormalCount = normal;
                        record.WarningCount = warning;
                        record.AlarmCount = alarm;
                        record.UnknownCount = unknown;
                    }

                    FirDataList = new ObservableCollection<FirRecordModel>(uniqueRecords);
                    GenerateCharts(uniqueRecords);
                }
                else
                {
                    await MaterialMessageBox.ShowAsync("查無資料或符合條件的紀錄！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"查詢發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
            }
        }

        private void GenerateCharts(List<FirRecordModel> records)
        {
            var orderedRecords = records.OrderBy(r => r.CTime).ToList();
            int recordCount = orderedRecords.Count;

            var xLabels = new string[recordCount];
            var lotNos = new string[recordCount]; // 🌟 建立一個專門儲存 Lot_no 的陣列給 Tooltip 用

            var chartDataMap = new Dictionary<string, ChartDataBuffer>();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1. 初始化所有測量項目，長度固定，預設皆為 null
            foreach (var record in orderedRecords)
            {
                if (string.IsNullOrWhiteSpace(record.DataJsonString)) continue;
                try
                {
                    var rawData = JsonSerializer.Deserialize<FirRawDataModel[]>(record.DataJsonString, jsonOptions);
                    if (rawData == null) continue;
                    foreach (var raw in rawData)
                    {
                        if (!chartDataMap.ContainsKey(raw.Name))
                            chartDataMap[raw.Name] = new ChartDataBuffer(recordCount);
                    }
                }
                catch { }
            }

            // 2. 填入真實數值
            for (int i = 0; i < recordCount; i++)
            {
                var record = orderedRecords[i];

                // 🌟 需求：X軸只顯示時間，Lot_no 存在陣列裡給 Tooltip 用
                lotNos[i] = record.LotNo;
                xLabels[i] = $"{DateTime.Parse(record.CTime):MM/dd HH:mm}";

                if (string.IsNullOrWhiteSpace(record.DataJsonString)) continue;

                try
                {
                    var rawData = JsonSerializer.Deserialize<FirRawDataModel[]>(record.DataJsonString, jsonOptions);
                    var setData = string.IsNullOrWhiteSpace(record.SetDataJsonString)
                                  ? Array.Empty<FirSetDataModel>()
                                  : JsonSerializer.Deserialize<FirSetDataModel[]>(record.SetDataJsonString, jsonOptions);

                    if (rawData == null) continue;

                    foreach (var raw in rawData)
                    {
                        var buffer = chartDataMap[raw.Name];
                        var setting = setData?.FirstOrDefault(s => s.Name == raw.Name);

                        buffer.Values[i] = double.TryParse(raw.Value, out double parsed) ? parsed : (double?)null;
                        buffer.UpperLimits[i] = setting?.LimitsUpper;
                        buffer.UpperWarnings[i] = setting?.LimitsUpperWarning;
                        buffer.Mids[i] = setting?.LimitsMid;
                        buffer.LowerWarnings[i] = setting?.LimitsLowerWarning;
                        buffer.LowerLimits[i] = setting?.LimitsLower;
                    }
                }
                catch { }
            }
            _currentLotNos = lotNos;//  將陣列存入全域變數，供雙擊事件使用
            BuildLiveCharts(chartDataMap, xLabels, lotNos);
        }

        #region ""
        // 🌟 接收 lotNos 陣列，準備餵給 Tooltip
        //private void BuildLiveCharts(Dictionary<string, ChartDataBuffer> chartDataMap, string[] xLabels, string[] lotNos)
        //{
        //    var textPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
        //    var linePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };

        //    var colorLimit = new SKColor(239, 83, 80);
        //    var colorWarn = new SKColor(255, 167, 38);
        //    var colorMid = new SKColor(102, 187, 106);
        //    var colorActual = new SKColor(41, 182, 246);

        //    foreach (var kvp in chartDataMap)
        //    {
        //        var name = kvp.Key;
        //        var buffer = kvp.Value;

        //        // ==========================================
        //        // Y 軸縮放邏輯 (維持不變，保持線條展開)
        //        // ==========================================
        //        double min = double.MaxValue;
        //        double max = double.MinValue;

        //        void CheckMinMax(double?[] arr)
        //        {
        //            foreach (var val in arr)
        //            {
        //                if (val.HasValue && !double.IsNaN(val.Value))
        //                {
        //                    if (val.Value > max) max = val.Value;
        //                    if (val.Value < min) min = val.Value;
        //                }
        //            }
        //        }

        //        CheckMinMax(buffer.Values);
        //        CheckMinMax(buffer.UpperLimits);
        //        CheckMinMax(buffer.LowerLimits);
        //        CheckMinMax(buffer.Mids);

        //        if (min == double.MaxValue) min = 0;
        //        if (max == double.MinValue) max = 1;

        //        double range = max - min;
        //        if (range == 0) range = 1;

        //        double yMinLimit = min - (range * 0.1);
        //        double yMaxLimit = max + (range * 0.1);

        //        // ==========================================

        //        var seriesList = new List<ISeries>
        //        {
        //            // 🌟 秘技：加入一條「透明的隱形線」，專門用來在 Tooltip 產生獨立的批號欄位！
        //            new LineSeries<double?>
        //            {
        //                Name = "批號",
        //                Values = buffer.Values, // 綁定一樣的實測值，確保觸發位置一致
        //                Stroke = new SolidColorPaint(SKColors.Transparent), // 設為透明，圖表上完全隱形
        //                GeometrySize = 0, // 不畫圓點
        //                Fill = null,
        //                YToolTipLabelFormatter = point =>
        //                {
        //                    int index = (int)point.Coordinate.SecondaryValue;
        //                    return (index >= 0 && index < lotNos.Length) ? lotNos[index] : "";
        //                }
        //            },

        //            // 正常的上下限規格線
        //            CreateLineSeries("上限", buffer.UpperLimits, colorLimit, 2, null),
        //            CreateLineSeries("上預警", buffer.UpperWarnings, colorWarn, 2, new float[] { 5, 5 }),

        //            // 實際測量值 (恢復最純淨的數值顯示)
        //            new LineSeries<double?>
        //            {
        //                Name = "實際測量值",
        //                Values = buffer.Values,
        //                Stroke = new SolidColorPaint(colorActual) { StrokeThickness = 3 },
        //                GeometrySize = 6,
        //                GeometryFill = new SolidColorPaint(colorActual),
        //                Fill = null,

        //                // 🌟 恢復純粹顯示數字，不再硬塞批號
        //                YToolTipLabelFormatter = point => point.Coordinate.PrimaryValue.ToString("0.####")
        //            },

        //            CreateLineSeries("基準中值", buffer.Mids, colorMid, 1, new float[] { 3, 5 }),
        //            CreateLineSeries("下預警", buffer.LowerWarnings, colorWarn, 2, new float[] { 5, 5 }),
        //            CreateLineSeries("下限", buffer.LowerLimits, colorLimit, 2, null)
        //        };
        //        MeasurementCharts.Add(new ChartContainerModel
        //        {
        //            ChartTitle = $"項目: {name}",
        //            Series = seriesList.ToArray(),
        //            XAxes = new Axis[] { new Axis { Labels = xLabels.ToList(), LabelsRotation = 20, LabelsPaint = textPaint, SeparatorsPaint = linePaint } },
        //            YAxes = new Axis[] { new Axis { MinLimit = yMinLimit, MaxLimit = yMaxLimit, LabelsPaint = textPaint, SeparatorsPaint = linePaint } }
        //        });
        //    }
        //}

        //// 🌟 恢復最乾淨的建立線條方法，移除了所有干擾屬性
        //private LineSeries<double?> CreateLineSeries(string name, double?[] values, SKColor color, float thickness, float[] dashArray)
        //{
        //    var paint = new SolidColorPaint(color) { StrokeThickness = thickness };
        //    if (dashArray != null) paint.PathEffect = new DashEffect(dashArray);

        //    return new LineSeries<double?>
        //    {
        //        Name = name,
        //        Values = values,
        //        Stroke = paint,
        //        GeometrySize = 0, // 規格線隱藏圓點
        //        Fill = null,
        //        // 統一讓原生的 Tooltip 顯示小數點後 4 位
        //        YToolTipLabelFormatter = point => point.Coordinate.PrimaryValue.ToString("0.####")
        //    };
        //}
        #endregion
        private void BuildLiveCharts(Dictionary<string, ChartDataBuffer> chartDataMap, string[] xLabels, string[] lotNos)
        {
            var textPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
            var linePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };

            var colorLimit = new SKColor(239, 83, 80);
            var colorWarn = new SKColor(255, 167, 38);
            var colorMid = new SKColor(102, 187, 106);
            var colorActual = new SKColor(41, 182, 246);

            foreach (var kvp in chartDataMap)
            {
                var name = kvp.Key;
                var buffer = kvp.Value;

                // ==========================================
                // Y 軸精算縮放 (保持線條完美展開)
                // ==========================================
                double min = double.MaxValue;
                double max = double.MinValue;

                void CheckMinMax(double?[] arr)
                {
                    foreach (var val in arr)
                    {
                        if (val.HasValue && !double.IsNaN(val.Value))
                        {
                            if (val.Value > max) max = val.Value;
                            if (val.Value < min) min = val.Value;
                        }
                    }
                }

                CheckMinMax(buffer.Values);
                CheckMinMax(buffer.UpperLimits);
                CheckMinMax(buffer.LowerLimits);
                CheckMinMax(buffer.Mids);

                if (min == double.MaxValue) min = 0;
                if (max == double.MinValue) max = 1;

                double range = max - min;
                if (range == 0) range = 1;

                double yMinLimit = min - (range * 0.1);
                double yMaxLimit = max + (range * 0.1);

                // ==========================================

                var seriesList = new List<ISeries>
                {
                    // 🌟 秘技：全透明的隱形線，專門用來產生獨立乾淨的「批號」欄位
                    new LineSeries<double?>
                    {
                        Name = "批號",
                        Values = buffer.Values,
                        Stroke = new SolidColorPaint(SKColors.Transparent), // 設為透明，圖表上隱形
                        GeometrySize = 0, // 不畫圓點
                        Fill = null,
                        YToolTipLabelFormatter = point =>
                        {
                            int index = (int)point.Coordinate.SecondaryValue;
                            return (index >= 0 && index < lotNos.Length) ? lotNos[index] : "";
                        }
                    },

                    // 正常的上下限規格線
                    CreateLineSeries("上限", buffer.UpperLimits, colorLimit, 2, null),
                    CreateLineSeries("上預警", buffer.UpperWarnings, colorWarn, 2, new float[] { 5, 5 }),
                    
                    // 實際測量值
                    new LineSeries<double?>
                    {
                        Name = "實際測量值",
                        Values = buffer.Values,
                        Stroke = new SolidColorPaint(colorActual) { StrokeThickness = 3 },
                        GeometrySize = 6,
                        GeometryFill = new SolidColorPaint(colorActual),
                        Fill = null,
                        YToolTipLabelFormatter = point => point.Coordinate.PrimaryValue.ToString("0.####")
                    },

                    CreateLineSeries("基準中值", buffer.Mids, colorMid, 1, new float[] { 3, 5 }),
                    CreateLineSeries("下預警", buffer.LowerWarnings, colorWarn, 2, new float[] { 5, 5 }),
                    CreateLineSeries("下限", buffer.LowerLimits, colorLimit, 2, null)
                };

                MeasurementCharts.Add(new ChartContainerModel
                {
                    ChartTitle = $"項目: {name}",
                    Series = seriesList.ToArray(),
                    XAxes = new Axis[] { new Axis { Labels = xLabels.ToList(), LabelsRotation = 20, LabelsPaint = textPaint, SeparatorsPaint = linePaint } },
                    YAxes = new Axis[] { new Axis { MinLimit = yMinLimit, MaxLimit = yMaxLimit, LabelsPaint = textPaint, SeparatorsPaint = linePaint } }
                });
            }
        }

        // 🌟 加上 zIndex 參數的共用方法
        private LineSeries<double?> CreateLineSeries(string name, double?[] values, SKColor color, float thickness, float[] dashArray)
        {
            var paint = new SolidColorPaint(color) { StrokeThickness = thickness };
            if (dashArray != null) paint.PathEffect = new DashEffect(dashArray);

            return new LineSeries<double?>
            {
                Name = name,
                Values = values,
                Stroke = paint,
                GeometrySize = 0,
                Fill = null,
             
                YToolTipLabelFormatter = point => point.Coordinate.PrimaryValue.ToString("0.####")
            };
        }

        // 內部類別改為 double? (預設產生實體時內容物全為 null)
        private class ChartDataBuffer
        {
            public double?[] Values { get; set; }
            public double?[] UpperLimits { get; set; }
            public double?[] UpperWarnings { get; set; }
            public double?[] Mids { get; set; }
            public double?[] LowerWarnings { get; set; }
            public double?[] LowerLimits { get; set; }

            public ChartDataBuffer(int size)
            {
                Values = new double?[size];
                UpperLimits = new double?[size];
                UpperWarnings = new double?[size];
                Mids = new double?[size];
                LowerWarnings = new double?[size];
                LowerLimits = new double?[size];
            }
        }

        // ==========================================
        // 開啟明細功能
        // ==========================================
        [RelayCommand]
        private async Task ShowDetailAsync(FirRecordModel selectedItem)
        {
            if (selectedItem == null || string.IsNullOrWhiteSpace(SearchProductName)) return;

            // 1. 從 DI 容器要一個全新的子 ViewModel
            var newDetailVM = _serviceProvider.GetRequiredService<VM_LotDetail>();

            // 2. 讓子 ViewModel 準備資料
            await newDetailVM.LoadDetailAsync(selectedItem.LotNo, SearchProductName);

            // 3. 呼叫服務開啟視窗！
            _windowManager.ShowWindow(newDetailVM);
        }

        [RelayCommand]
        private async Task ChartDoubleClickedAsync(object args)
        {
            // LiveCharts 2.0.4 傳遞的點擊物件轉型
            if (args is LiveChartsCore.Kernel.ChartPoint point)
            {
                // 取得該點在 X 軸的 Index
                int index = (int)point.Coordinate.SecondaryValue;

                // 防呆保護，確保陣列存在且索引正確
                if (_currentLotNos != null && index >= 0 && index < _currentLotNos.Length)
                {
                    string targetLotNo = _currentLotNos[index];

                    // 從 DataGrid 的清單中找出對應的整筆紀錄
                    var record = FirDataList.FirstOrDefault(r => r.LotNo == targetLotNo);
                    if (record != null)
                    {
                        // 呼叫原本寫好的明細視窗開啟方法
                        await ShowDetailAsync(record);
                    }
                }
            }
        }
    }
}