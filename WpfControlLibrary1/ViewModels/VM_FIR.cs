using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
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
        [ObservableProperty] private bool _isSortByWorkStationTime = false;

        [ObservableProperty] private bool _isSearchByLimit = true;
        [ObservableProperty] private bool _isSearchByDate = false;

        [ObservableProperty] private int _searchLimit = 50;
        [ObservableProperty] private DateTime _searchStartDate;
        [ObservableProperty] private DateTime _searchEndDate;

        //[ObservableProperty] private bool _isSearching = false;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = false;

        // ======== 下拉選單 ========
        [ObservableProperty] private ObservableCollection<string> _productNameList = new();
        [ObservableProperty] private ObservableCollection<ListWorkStationModel> _workStationList = new();
        [ObservableProperty] private ObservableCollection<EqNoModel> _eqNoList = new();

        // ======== 查詢結果 ========
        [ObservableProperty] private ObservableCollection<FirRecordModel> _firDataList = new();
        [ObservableProperty] private ObservableCollection<ChartContainerModel> _measurementCharts = new();

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

        private async Task LoadBaseDataAsync()
        {
            IsLoading = true;
            try
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
            finally
            {
                IsLoading = false;
            }
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

        [RelayCommand]
        private async Task SearchDataAsync()
        {
            if (IsLoading) return; // 防止重複點擊
            if (string.IsNullOrWhiteSpace(SearchProductName))
            {
                await MaterialMessageBox.ShowAsync("請輸入產品名稱進行查詢！", "提示");
                return;
            }

            IsLoading = true;
            HasNoData = false; // 查詢時先隱藏無資料提示
            FirDataList.Clear();
            MeasurementCharts.Clear();

            try
            {
                var payload = new
                {
                    product_name = SearchProductName,
                    work_station = SearchWorkStation,
                    eq_no = SearchEqNo,
                    search_mode = IsSearchByLimit ? "limit" : "date",
                    limit = SearchLimit,
                    start_date = SearchStartDate.ToString("yyyy-MM-dd"),
                    end_date = SearchEndDate.ToString("yyyy-MM-dd"),
                    sort_by_ws_time = IsSortByWorkStationTime
                };

                var result = await _apiService.PostAsync<List<FirRecordModel>>("get_fir_trend", payload);

                if (result.IsSuccess && result.Data != null && result.Data.Any())
                {
                    var uniqueRecords = result.Data
                        .GroupBy(r => r.LotNo)
                        .Select(g => g.OrderByDescending(r => DateTime.Parse(r.CTime)).First())
                        .ToList();

                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var validRecords = new List<FirRecordModel>();
                    var recordTimeMap = new Dictionary<FirRecordModel, DateTime>();

                    // 🌟 重構：在建立清單前，先進行「真實過站時間」的解析與「嚴格二次過濾」
                    foreach (var record in uniqueRecords)
                    {
                        DateTime displayTime = DateTime.Parse(record.CTime); // 預設使用測量時間

                        // 1. 如果啟用了過站排序，從 JSON 撈出「指定的工序與機台」過站時間
                        if (IsSortByWorkStationTime && !string.IsNullOrWhiteSpace(record.ListWorkData))
                        {
                            try
                            {
                                var wsList = JsonSerializer.Deserialize<WsTimeData[]>(record.ListWorkData, jsonOptions);

                                // 🌟 精準匹配：同時滿足 WorkStation 與 EqNo (若有填寫)
                                var targetWs = wsList?.FirstOrDefault(w =>
                                    (string.IsNullOrWhiteSpace(SearchWorkStation) || w.work_station == SearchWorkStation) &&
                                    (string.IsNullOrWhiteSpace(SearchEqNo) || w.eq_no == SearchEqNo)
                                );

                                if (targetWs != null && (!string.IsNullOrWhiteSpace(SearchWorkStation) || !string.IsNullOrWhiteSpace(SearchEqNo)))
                                {
                                    if (DateTime.TryParse(targetWs.Work_time, out DateTime parsedTime))
                                    {
                                        displayTime = parsedTime;

                                        // 🌟 將 DataGrid 的顯示時間也覆蓋為過站時間，確保 UI 一致性！
                                        record.CTime = parsedTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                }
                            }
                            catch { }
                        }

                        // 🌟 2. 嚴格防呆過濾：保證範圍 100% 正確
                        // 如果啟用了「時間區段」搜尋，強制捨棄不在區段內的資料 (不論是 CTime 還是 Work_time)
                        if (IsSearchByDate)
                        {
                            if (displayTime.Date < SearchStartDate.Date || displayTime.Date > SearchEndDate.Date)
                            {
                                continue; // 拋棄此筆紀錄，不進入圖表與清單！
                            }
                        }

                        // 3. 計算 Alarm / Warning 數量
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

                                        if (setting.LimitsUpper.HasValue && actualVal >= setting.LimitsUpper.Value) alarm++;
                                        else if (setting.LimitsLower.HasValue && actualVal <= setting.LimitsLower.Value) alarm++;
                                        else if (setting.LimitsUpperWarning.HasValue && actualVal >= setting.LimitsUpperWarning.Value) warning++;
                                        else if (setting.LimitsLowerWarning.HasValue && actualVal <= setting.LimitsLowerWarning.Value) warning++;
                                        else normal++;
                                    }
                                }
                            }
                            catch { }
                        }

                        record.NormalCount = normal;
                        record.WarningCount = warning;
                        record.AlarmCount = alarm;
                        record.UnknownCount = unknown;

                        // 加入合格清單
                        validRecords.Add(record);
                        recordTimeMap[record] = displayTime;
                    }

                    // 🌟 4. 依照正確的時間軸進行最終排序，再賦予 DataGrid 與 Chart
                    validRecords = validRecords.OrderBy(r => recordTimeMap[r]).ToList();

                    FirDataList = new ObservableCollection<FirRecordModel>(validRecords);
                    GenerateCharts(validRecords, recordTimeMap);
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
                HasNoData = !FirDataList.Any();
                IsLoading = false;
            }
        }

        // 🌟 直接接收已經處理好並過濾完的資料與時間 Mapping
        private void GenerateCharts(List<FirRecordModel> orderedRecords, Dictionary<FirRecordModel, DateTime> recordTimeMap)
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 防呆：去除重複時間點，確保不會多筆疊加
            var usedTimes = new HashSet<long>();
            foreach (var record in orderedRecords)
            {
                long ticks = recordTimeMap[record].Ticks;
                while (usedTimes.Contains(ticks))
                {
                    ticks += TimeSpan.FromSeconds(1).Ticks;
                }
                usedTimes.Add(ticks);
                recordTimeMap[record] = new DateTime(ticks);
            }

            int recordCount = orderedRecords.Count;
            var chartDataMap = new Dictionary<string, ChartDataBuffer>();

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

            for (int i = 0; i < recordCount; i++)
            {
                var record = orderedRecords[i];
                DateTime displayTime = recordTimeMap[record];

                foreach (var kvp in chartDataMap)
                {
                    kvp.Value.Values[i] = new SPCPoint(displayTime, null, record.LotNo);
                    kvp.Value.UpperLimits[i] = new SPCPoint(displayTime, null, record.LotNo);
                    kvp.Value.UpperWarnings[i] = new SPCPoint(displayTime, null, record.LotNo);
                    kvp.Value.Mids[i] = new SPCPoint(displayTime, null, record.LotNo);
                    kvp.Value.LowerWarnings[i] = new SPCPoint(displayTime, null, record.LotNo);
                    kvp.Value.LowerLimits[i] = new SPCPoint(displayTime, null, record.LotNo);
                }

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

                        double? parsedVal = double.TryParse(raw.Value, out double parsed) ? parsed : (double?)null;

                        // 將數值塞入，不再綁定無關的屬性
                        if (buffer.Values[i] != null)
                        {
                            buffer.Values[i].Y = parsedVal;
                            buffer.Values[i].OriginalValue = parsedVal;
                        }

                        if (buffer.UpperLimits[i] != null) buffer.UpperLimits[i].Y = setting?.LimitsUpper;
                        if (buffer.UpperWarnings[i] != null) buffer.UpperWarnings[i].Y = setting?.LimitsUpperWarning;
                        if (buffer.Mids[i] != null) buffer.Mids[i].Y = setting?.LimitsMid;
                        if (buffer.LowerWarnings[i] != null) buffer.LowerWarnings[i].Y = setting?.LimitsLowerWarning;
                        if (buffer.LowerLimits[i] != null) buffer.LowerLimits[i].Y = setting?.LimitsLower;
                    }
                }
                catch { }
            }

            BuildLiveCharts(chartDataMap);
        }

        private void BuildLiveCharts(Dictionary<string, ChartDataBuffer> chartDataMap)
        {
            var textPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
            var linePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };

            var colorLimit = new SKColor(239, 83, 80);
            var colorWarn = new SKColor(255, 167, 38);
            var colorMid = new SKColor(102, 187, 106);
            var colorActual = new SKColor(41, 182, 246);

            bool useCustomScaling = true;

            foreach (var kvp in chartDataMap)
            {
                var name = kvp.Key;
                var buffer = kvp.Value;

                double xRange = 0;
                var validX = buffer.Values
                    .Where(p => p != null && p.X.HasValue)
                    .Select(p => p.X.Value)
                    .ToList();

                if (validX.Any())
                {
                    xRange = validX.Max() - validX.Min();
                }

                double dynamicMinStep = xRange / 8.0;
                if (dynamicMinStep < TimeSpan.FromMinutes(5).TotalDays)
                {
                    dynamicMinStep = TimeSpan.FromMinutes(5).TotalDays;
                }

                var seriesList = new List<ISeries>
                {
                    // 1. 隱形線 (只負責顯示批號，套用原生的簡潔格式)
                    new LineSeries<SPCPoint>
                    {
                        Name = "批號",
                        Values = buffer.Values.Where(p => p != null).Select(p => new SPCPoint(p.OriginalTime, p.OriginalValue, p.LotNo)).ToArray(),
                        Stroke = new SolidColorPaint(SKColors.Transparent),
                        GeometrySize = 0,
                        Fill = null,
                        LineSmoothness = 0,
                        YToolTipLabelFormatter = point => ((SPCPoint)point.Model)?.LotNo ?? ""
                    },

                    // 2. 規格線 (全部回歸原生 LineSeries，讓系統自動群組)
                    CreateLineSeries("上限", buffer.UpperLimits, colorLimit, 2, null),
                    CreateLineSeries("上預警", buffer.UpperWarnings, colorWarn, 2, new float[] { 5, 5 }),
                    
                    // 3. 實際測量值
                    new LineSeries<SPCPoint>
                    {
                        Name = "實際測量值",
                        Values = buffer.Values,
                        Stroke = new SolidColorPaint(colorActual) { StrokeThickness = 3 },
                        GeometrySize = 6,
                        GeometryFill = new SolidColorPaint(colorActual),
                        Fill = null,
                        LineSmoothness = 0,
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

                    XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value =>
                            {
                                if (value < 10000 || value > 2958465) return "";
                                return DateTime.FromOADate(value).ToString("MM/dd HH:mm");
                            },
                            LabelsRotation = -45,
                            LabelsPaint = textPaint,
                            SeparatorsPaint = linePaint,
                            MinStep = dynamicMinStep
                        }
                    },

                    YAxes = GetYAxes(useCustomScaling, buffer, textPaint, linePaint)
                });
            }
        }

        private LineSeries<SPCPoint> CreateLineSeries(string name, SPCPoint[] values, SKColor color, float thickness, float[] dashArray)
        {
            var paint = new SolidColorPaint(color) { StrokeThickness = thickness };
            if (dashArray != null) paint.PathEffect = new DashEffect(dashArray);

            return new LineSeries<SPCPoint>
            {
                Name = name,
                Values = values,
                Stroke = paint,
                GeometrySize = 0,
                Fill = null,
                LineSmoothness = 0,
                YToolTipLabelFormatter = point => point.Coordinate.PrimaryValue.ToString("0.####")
            };
        }

        private Axis[] GetYAxes(bool useCustomScaling, ChartDataBuffer buffer, SolidColorPaint textPaint, SolidColorPaint linePaint)
        {
            var yAxis = new Axis { LabelsPaint = textPaint, SeparatorsPaint = linePaint };

            if (useCustomScaling)
            {
                double min = double.MaxValue;
                double max = double.MinValue;

                void CheckMinMax(SPCPoint[] arr)
                {
                    foreach (var pt in arr)
                    {
                        if (pt != null && pt.Y.HasValue && !double.IsNaN(pt.Y.Value))
                        {
                            if (pt.Y.Value > max) max = pt.Y.Value;
                            if (pt.Y.Value < min) min = pt.Y.Value;
                        }
                    }
                }

                CheckMinMax(buffer.Values); CheckMinMax(buffer.UpperLimits); CheckMinMax(buffer.LowerLimits); CheckMinMax(buffer.Mids);

                if (min == double.MaxValue) min = 0;
                if (max == double.MinValue) max = 1;
                if (min == max) { min -= 1; max += 1; }

                double range = max - min;
                double roughStep = range / 5.0;
                double magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
                double normalizedStep = roughStep / magnitude;

                double niceStep;
                if (normalizedStep < 1.5) niceStep = 1 * magnitude;
                else if (normalizedStep < 3) niceStep = 2 * magnitude;
                else if (normalizedStep < 7) niceStep = 5 * magnitude;
                else niceStep = 10 * magnitude;

                double niceMin = Math.Floor(min / niceStep) * niceStep;
                double niceMax = Math.Ceiling(max / niceStep) * niceStep;

                yAxis.MinLimit = niceMin - (niceStep * 0.5);
                yAxis.MaxLimit = niceMax + (niceStep * 0.5);
            }
            else
            {
                yAxis.MinLimit = null;
                yAxis.MaxLimit = null;
            }

            return new Axis[] { yAxis };
        }

        [RelayCommand]
        private async Task ChartDoubleClickedAsync(object args)
        {
            if (args is ChartPoint point)
            {
                if (point.Context.DataSource is SPCPoint spcPoint && !string.IsNullOrEmpty(spcPoint.LotNo))
                {
                    var record = FirDataList.FirstOrDefault(r => r.LotNo == spcPoint.LotNo);
                    if (record != null)
                    {
                        await ShowDetailAsync(record);
                    }
                }
            }
        }

        public class SPCPoint : ObservablePoint
        {
            public string LotNo { get; set; }
            public DateTime OriginalTime { get; set; }
            public double? OriginalValue { get; set; }

            public SPCPoint(DateTime dateTime, double? value, string lotNo)
                : base(dateTime.ToOADate(), value)
            {
                LotNo = lotNo;
                OriginalTime = dateTime;
                OriginalValue = value;
            }
        }

        private class ChartDataBuffer
        {
            public SPCPoint[] Values { get; set; }
            public SPCPoint[] UpperLimits { get; set; }
            public SPCPoint[] UpperWarnings { get; set; }
            public SPCPoint[] Mids { get; set; }
            public SPCPoint[] LowerWarnings { get; set; }
            public SPCPoint[] LowerLimits { get; set; }

            public ChartDataBuffer(int size)
            {
                Values = new SPCPoint[size];
                UpperLimits = new SPCPoint[size];
                UpperWarnings = new SPCPoint[size];
                Mids = new SPCPoint[size];
                LowerWarnings = new SPCPoint[size];
                LowerLimits = new SPCPoint[size];
            }
        }

        // 🌟 擴增 DTO，加入 eq_no 以利精準匹配
        private class WsTimeData
        {
            public string work_station { get; set; }
            public string eq_no { get; set; }
            public string Work_time { get; set; }
        }

        [RelayCommand]
        private async Task ShowDetailAsync(FirRecordModel selectedItem)
        {
            if (selectedItem == null || string.IsNullOrWhiteSpace(SearchProductName)) return;

            var newDetailVM = _serviceProvider.GetRequiredService<VM_LotDetail>();
            await newDetailVM.LoadDetailAsync(selectedItem.LotNo, SearchProductName);
            _windowManager.ShowWindow(newDetailVM);
        }
    }
}