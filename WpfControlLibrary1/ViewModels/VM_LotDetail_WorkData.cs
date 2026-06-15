using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;
using static WpfControlLibrary1.ViewModels.VM_LotDetail;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_LotDetail_WorkData : ObservableObject
    {
        private readonly ApiService _apiService;
        private bool _hasLoaded = false;

        // 儲存從父 VM 傳遞過來的查詢條件
        private string _currentLotNo;
        private string _currentProductName;

        [ObservableProperty]
        private bool _isLoading = false;

        //// 假設你的資料模型
        //[ObservableProperty]
        //private object _workDataDetail;

        // 定義右側：標準工序 Model
        public partial class RouteItemModel : ObservableObject
        {
            [ObservableProperty] private int _no;
            [ObservableProperty] private bool _canPass;
            [ObservableProperty] private string _stationName;
            [ObservableProperty] private bool _isCompleted;
        }

        // 定義左側：實際歷程 Model
        public partial class HistoryItemModel : ObservableObject
        {
            [ObservableProperty] private string _exeTime;
            [ObservableProperty] private string _stationName;
            [ObservableProperty] private string _eqNo;
            [ObservableProperty] private string _user;
            [ObservableProperty] private string _remarks;
            [ObservableProperty] private bool _isExtra;
            [ObservableProperty] private bool _delete;
        }


        // 左/右表格清單
        [ObservableProperty] private ObservableCollection<HistoryItemModel> _historyList = new();
        [ObservableProperty] private ObservableCollection<RouteItemModel> _routeList = new();

        // 圖表綁定屬性 (使用 ObservableCollection 確保 UI 同步刷新)
        [ObservableProperty] private ObservableCollection<ISeries> _durationChartSeries = new();
        [ObservableProperty] private ObservableCollection<Axis> _durationXAxes = new();
        [ObservableProperty] private ObservableCollection<Axis> _durationYAxes = new();



        public VM_LotDetail_WorkData(ApiService apiService)
        {
            _apiService = apiService;
        }

        // 1. 接收查詢條件 (但不馬上查 API)
        public void SetContext(string lotNo, string productName)
        {
            _currentLotNo = lotNo;
            _currentProductName = productName;

            HistoryList.Clear();
            RouteList.Clear();

            _hasLoaded = false; // 如果工單改變，重置讀取狀態
        }

        // 2. 實際觸發查詢 API 的方法
        public async Task LoadDataAsync()
        {
            // 防呆：如果已經載入過，或是正在載入中，直接跳出 (這就是 Lazy Loading 的精髓)
            if (_hasLoaded || IsLoading || string.IsNullOrEmpty(_currentLotNo)) return;

            IsLoading = true;
            try
            {
                // 2. 呼叫 API 取得資料
                var result = await _apiService.Get_work_page_data_Detail_Async(_currentLotNo);

                if (result.IsSuccess && result.Data != null)
                {
                    // 3. 解析 JSON 資料
                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var routeData = JsonSerializer.Deserialize<List<WorkStationDef>>(result.Data.list_Work_station ?? "[]", jsonOptions) ?? new();
                    //var historyData = JsonSerializer.Deserialize<List<WorkDataDef>>(result.Data.list_work_Data ?? "[]", jsonOptions)?.Where(x => !x.delete).ToList() ?? new();
                    var historyData = JsonSerializer.Deserialize<List<WorkDataDef>>(result.Data.list_work_Data ?? "[]", jsonOptions) ?? new();

                    var standardStations = routeData.Select(r => r.work_station).ToHashSet();
                    //var executedStations = historyData.Select(h => h.work_station).ToHashSet();
            
                    var executedStations = historyData.Where(h => !h.delete).Select(h => h.work_station).ToHashSet();
                    // 4. 依序執行各區塊的資料綁定 (模組化呼叫)

                    BuildRouteList(routeData, executedStations);
                    BuildHistoryList(historyData, standardStations);
                    BuildGanttChart(historyData);
                }

                _hasLoaded = true; // 標記為已載入成功
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ========================================================
        // 子功能 1：更新上方工單基本資訊
        // ========================================================
       

        // ========================================================
        // 子功能 2：建立右側 DataGrid (作業參考工序)
        // ========================================================
        private void BuildRouteList(List<WorkStationDef> routeData, HashSet<string> executedStations)
        {
            foreach (var r in routeData)
            {
                RouteList.Add(new RouteItemModel
                {
                    No = r.NO,
                    CanPass = r.Can_Pass,
                    StationName = r.work_station,
                    IsCompleted = executedStations.Contains(r.work_station) // 若歷程有做過，就亮綠色
                });
            }
        }

        // ========================================================
        // 子功能 3：建立左側 DataGrid (已執行歷程)
        // ========================================================
        private void BuildHistoryList(List<WorkDataDef> historyData, HashSet<string> standardStations)
        {
            foreach (var h in historyData)
            {
                HistoryList.Add(new HistoryItemModel
                {
                    ExeTime = h.Work_time != DateTime.MinValue ? h.Work_time.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") : "",
                    StationName = h.work_station,
                    EqNo = h.EQ_no,
                    User = h.User,
                    Remarks = h.Remarks,
                    IsExtra = !standardStations.Contains(h.work_station),
                    Delete = h.delete // 💡 綁定刪除狀態給 XAML 使用
                });
            }
        }

        // ========================================================
        // 子功能 4：計算時間並繪製甘特圖 (Gantt Chart)
        // ========================================================
        private void BuildGanttChart(List<WorkDataDef> historyData)
        {
            // 🌟 關鍵修正：在函式一開始，先無條件清空圖表與座標軸！
            // 這樣就算載入沒有歷程的新工單，畫面也會立刻清空舊資料。
            DurationChartSeries.Clear();
            DurationXAxes.Clear();
            DurationYAxes.Clear();

            var chartLabels = new List<string>();
            var offsetValues = new List<double>();   // 第一段：透明推移區段
            var durationValues = new List<double>(); // 第二段：實際耗時區段

            // 確保歷史資料照時間排序，並轉換為 DateTime 格式
            //var sortedHistory = historyData
            //    .Select(h => new { Data = h, Time = h.Work_time != DateTime.MinValue ? h.Work_time.ToLocalTime() : DateTime.MinValue })
            //    .Where(x => x.Time != DateTime.MinValue)
            //    .OrderBy(x => x.Time)
            //    .ToList();
            var sortedHistory = historyData
                .Where(h => !h.delete) // 💡 圖表只需要正常工時，過濾掉作廢的！
                .Select(h => new { Data = h, Time = h.Work_time != DateTime.MinValue ? h.Work_time.ToLocalTime() : DateTime.MinValue })
                .Where(x => x.Time != DateTime.MinValue)
                .OrderBy(x => x.Time)
                .ToList();

            // 如果排序後沒有任何有效資料，清空完就可以直接結束了
            if (!sortedHistory.Any()) return;

            var minDate = sortedHistory.First().Time.Date;

            for (int i = 0; i < sortedHistory.Count; i++)
            {
                var curr = sortedHistory[i];
                var nextTime = (i < sortedHistory.Count - 1) ? sortedHistory[i + 1].Time : curr.Time;

                chartLabels.Add(curr.Data.work_station);
                offsetValues.Add((curr.Time - minDate).TotalHours);

                // 直接在這裡四捨五入，方便後續第三段標籤抓取
                durationValues.Add(Math.Round((nextTime - curr.Time).TotalHours, 1));
            }

            var textPaint = new SolidColorPaint(SKColor.Parse("#E0E0E0"));
            var linePaint = new SolidColorPaint(SKColor.Parse("#40FFFFFF")) { StrokeThickness = 1 };

            // 1. 第一段：隱藏的推移長條
            var offsetSeries = new StackedRowSeries<double>
            {
                Values = offsetValues,
                Fill = new SolidColorPaint(SKColors.Transparent),
                Stroke = null,
                DataLabelsPaint = null,
                IsHoverable = false
            };

            // 2. 第二段：實際看得到耗時長條
            var durationSeries = new StackedRowSeries<double>
            {
                Values = durationValues,

                // 🌟 將 Name 設為空字串，可以隱藏預設的標題，讓排版更乾淨像您的截圖一樣
                Name = "",

                DataLabelsPaint = null,
                MaxBarWidth = 14,
                Rx = 2,
                Ry = 2,

                // 自訂 Tooltip 的格式
                XToolTipLabelFormatter = point =>
                {
                    // 透過 Index 抓取該長條對應的「站點名稱」
                    int index = point.Context.Entity.MetaData.EntityIndex;
                    string stationName = chartLabels[index];

                    // 回傳組合字串 (\n 會讓文字換行，達到截圖中上下兩排的效果)
                    return $"{stationName}  經過耗時: {point.Model} (h)";
                }
            };

            var palette = new[]
            {
                SKColor.Parse("#EF5350"), SKColor.Parse("#66BB6A"), SKColor.Parse("#42A5F5"),
                SKColor.Parse("#AB47BC"), SKColor.Parse("#26C6DA"), SKColor.Parse("#FFA726"),
                SKColor.Parse("#8D6E63"), SKColor.Parse("#78909C")
            };

            durationSeries.PointMeasured += point =>
            {
                if (point.Visual is null) return;
                int index = point.Context.Entity.MetaData.EntityIndex;
                point.Visual.Fill = new SolidColorPaint(palette[index % palette.Length]);
            };

            // 3. 第三段：專門用來把文字推到線條右側的「透明標籤段」
            var labelSeries = new StackedRowSeries<double>
            {
                Values = durationValues.Select(x => 0.001).ToList(),
                Fill = new SolidColorPaint(SKColors.Transparent),
                Stroke = null,
                DataLabelsPaint = textPaint,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right,
                DataLabelsFormatter = point => $"  {durationValues[point.Context.Entity.MetaData.EntityIndex]} (h)",
                IsHoverable = false
            };

            // 依序加入三段系列 (因為一開始已經 Clear 過了，這裡直接 Add 即可)
            DurationChartSeries.Add(offsetSeries);
            DurationChartSeries.Add(durationSeries);
            DurationChartSeries.Add(labelSeries);

            // Y軸設定
            DurationYAxes.Add(new Axis
            {
                Labels = chartLabels,
                LabelsPaint = textPaint,
                TextSize = 13,
                SeparatorsPaint = linePaint,
                IsInverted = true
            });

            // X軸設定
            DurationXAxes.Add(new Axis
            {
                NamePaint = textPaint,
                LabelsPaint = textPaint,
                MinLimit = 0,
                SeparatorsPaint = linePaint,
                Labeler = value => minDate.AddHours(value).ToString("MM/dd HH:mm")
            });
        }
    }
}