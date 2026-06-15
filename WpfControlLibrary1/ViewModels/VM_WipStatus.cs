using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts; // 為了使用無頭渲染器匯出圖片
using MaterialDesignThemes.Wpf; //使用 DialogHost
using Microsoft.Win32; // 為了使用 SaveFileDialog
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;
using Microsoft.Extensions.DependencyInjection; //  加入這行來啟用 GetRequiredService 擴充方法



namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_WipStatus : ObservableObject
    {


        private readonly ApiService _apiService;
        private readonly IThemeService _themeService; // 注入主題服務
        private readonly IServiceProvider _serviceProvider;// 宣告 DI 容器
        private readonly IWindowManager _windowManager; // 注入視窗服務

        // 通用明細 ViewModel
        //public VM_LotDetail LotDetailVM { get; }

        [ObservableProperty]
        private ObservableCollection<string> _productNameList = new();
   
        // ======== 查詢條件綁定 ========
        [ObservableProperty]
        private string _searchLotNo = "";

        [ObservableProperty]
        private string _searchParentLotNo;

        [ObservableProperty]
        private string _searchProductName = "";

        [ObservableProperty]
        private DateTime _searchStartDate ;

        [ObservableProperty]
        private DateTime _searchEndDate ;

        [ObservableProperty]
        private bool _isSearching = false;

        [ObservableProperty]
        private bool _includeCompleted = false;

        // ======== 查詢結果綁定 ========
        [ObservableProperty]
        private ObservableCollection<WipStatusModel> _wipDataList = new();


        // 用於顯示總筆數與限制提示的文字
        [ObservableProperty]
        private string _totalCountMessage = "總筆數: 0 (系統最多顯示前 10000 筆)";

        // 用於判斷是否達到 10000 筆上限 (達到時可將字體變紅警告)
        [ObservableProperty]
        private bool _isLimitReached = false;
        // ======== 直方圖綁定屬性 ========
        [ObservableProperty]
        private ISeries[] _stationChartSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _stationXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _stationYAxes = Array.Empty<Axis>();

        // 切換 X 軸模式 (false = 當前站點, true = 下一站)
        [ObservableProperty]
        private bool _isGroupedByNextStation = false;

        //=================================
        // ======== 雙擊列選取綁定 ========
        [ObservableProperty]
        private WipStatusModel _selectedWipItem;

        //=================================


        #region "通用明細"
        //==========================================================通用明細



        //[ObservableProperty]
        //private bool _isDrawerOpen = false;

        // 修改建構子，注入 VM_LotDetail
        public VM_WipStatus(ApiService apiService, IThemeService themeService, IServiceProvider serviceProvider , IWindowManager windowManager)  // , VM_LotDetail lotDetailVM
        {
            _apiService = apiService;
            _themeService = themeService;
            _serviceProvider = serviceProvider;
            _windowManager = windowManager;
           

            DateTime today = DateTime.Today;
            SearchEndDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            DateTime threeMonthsAgo = today.AddMonths(-3);// 預設起始日期為三個月前的第一天
            SearchStartDate = new DateTime(threeMonthsAgo.Year, threeMonthsAgo.Month, 1);

            InitEmptyChartAxes();

            _ = LoadProductNamesAsync();
        }

        //public VM_WipStatus(ApiService apiService, IThemeService themeService)
        //{
        //    _apiService = apiService;
        //    _themeService = themeService;

        //    // 初始狀態下先配置空的 Y 軸，維持樣式一致
        //    InitEmptyChartAxes();
        //}

        // 從 API 非同步載入產品名稱列表，並更新 ProductNameList 屬性
        private async Task LoadProductNamesAsync()
        {
            var result = await _apiService.GetProductNamesAsync();
            if (result.IsSuccess && result.Data != null)
            {
                ProductNameList.Clear();
                foreach (var name in result.Data)
                {
                    ProductNameList.Add(name);
                }
            }
        }

        //[RelayCommand]
        //private async Task ShowDetailAsync(WipStatusModel selectedItem)
        //{
        //    if (selectedItem == null) return;

        //    // 1. 先請通用 ViewModel 去 API 撈取並解析資料
        //    await LotDetailVM.LoadDetailAsync(selectedItem.Lot_no, selectedItem.Product_name);

        //    // 2. 將撈好資料的 ViewModel，丟給主視窗的 RootDialog 彈出來！
        //    await DialogHost.Show(LotDetailVM, "RootDialog");
        //}

        //[RelayCommand]
        //private async Task ShowDetailAsync(WipStatusModel selectedItem)
        //{
        //    if (selectedItem == null) return;

        //    // 1. 先請通用 ViewModel 去 API 撈取並解析資料
        //    await LotDetailVM.LoadDetailAsync(selectedItem.Lot_no, selectedItem.Product_name);

        //    // 2. 改用獨立的 MetroWindow 顯示
        //    // 注意：開啟視窗屬於 UI 操作，必須確保在 UI 執行緒 (Dispatcher) 執行
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        var detailWindow = new Window_LotDetail
        //        {
        //            DataContext = LotDetailVM // 綁定已經載入好資料的 ViewModel
        //        };
        //        detailWindow.Show();
        //    });
        //}

        //[RelayCommand]
        //private async Task ShowDetailAsync(WipStatusModel selectedItem)
        //{
        //    if (selectedItem == null) return;

        //    // 1. 先請通用 ViewModel 去 API 撈取並解析資料
        //    await LotDetailVM.LoadDetailAsync(selectedItem.Lot_no, selectedItem.Product_name);

        //    // 2. 確保視窗的建立與顯示在 UI 執行緒 (Dispatcher) 上執行，符合 MVVM 嚴格規範
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        // 實例化剛寫好的 Window_LotDetail，並綁定資料已經就緒的 LotDetailVM
        //        var detailWindow = new Window_LotDetail
        //        {
        //            DataContext = LotDetailVM,

        //            // 可以選擇是否在關閉後釋放 ViewModel 的資料，若 ViewModel 是 Singleton 則保留
        //            // 若為 Transient，Window 關閉時資源會自動釋放。
        //        };

        //        // 開啟新視窗 (.Show() 支援多開，若希望卡死主畫面可改用 .ShowDialog())
        //        detailWindow.Show();
        //    });

        //    //=============================
        //    // 如果要支援多開，標準做法是向 DI 容器要一個全新的 ViewModel 實例
        //    var newDetailVM = App.Current.Services.GetRequiredService<VM_LotDetail>();

        //    // 新的 VM 自己去載入資料
        //    await newDetailVM.LoadDetailAsync(selectedItem.Lot_no, selectedItem.Product_name);

        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        var detailWindow = new Window_LotDetail
        //        {
        //            DataContext = newDetailVM // 綁定全新的 VM，視窗互不干擾
        //        };
        //        detailWindow.Show();
        //    });

        //}

        //[RelayCommand]
        //private async Task ShowDetailAsync(WipStatusModel selectedItem)
        //{
        //    if (selectedItem == null) return;

        //    // 🌟 1. 向 DI 容器要求一個「全新」的 ViewModel 實例
        //    // 每個視窗都有自己的 VM，資料絕對不會互相打架！
        //    var newDetailVM = _serviceProvider.GetRequiredService<VM_LotDetail>();

        //    // 2. 請這個「專屬的 VM」去 API 撈取並解析資料
        //    await newDetailVM.LoadDetailAsync(selectedItem.Lot_no, selectedItem.Product_name);

        //    // 3. 確保視窗的建立與顯示在 UI 執行緒
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        // 🌟 4. 向 DI 容器要求一個「全新」的 Window (如果前面有 AddTransient 的話)
        //        // 或者是直接 new Window_LotDetail() 也可以
        //        var detailWindow = _serviceProvider.GetRequiredService<Window_LotDetail>();

        //        // 5. 將剛剛撈好資料的「專屬 VM」綁定給這個視窗
        //        detailWindow.DataContext = newDetailVM;

        //        // 6. 設定視窗為「非強制回應對話框」
        //        // 使用 .Show() 而不是 .ShowDialog()，這樣主畫面才不會被卡死，才能繼續點擊下一張工單
        //        detailWindow.Show();
        //    });
        //}

        [RelayCommand]
        private async Task ShowDetailAsync(WipStatusModel selectedItem)
        {
            if (selectedItem == null) return;

            // 1. 從 DI 要一個全新的子 ViewModel
            var newDetailVM = _serviceProvider.GetRequiredService<VM_LotDetail>();

            // 2. 讓子 ViewModel 準備資料
            await newDetailVM.LoadDetailAsync(selectedItem.Lot_no, selectedItem.Product_name);

            //3. 呼叫服務開啟視窗！
            _windowManager.ShowWindow(newDetailVM);
        }

        #endregion

        // ======== 另存圖表功能 ========
        //[RelayCommand]
        //private void SaveChartImage()
        //{
        //    if (StationChartSeries == null || !StationChartSeries.Any())
        //    {
        //        MessageBox.Show("目前沒有圖表資料可供儲存，請先執行查詢。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        //        return;
        //    }

        //    // 1. 彈出系統存檔對話框
        //    var dialog = new SaveFileDialog
        //    {
        //        Filter = "PNG 圖片 (*.png)|*.png",
        //        Title = "另存產能圖表",
        //        FileName = $"產能過站明細_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        //    };

        //    if (dialog.ShowDialog() == true)
        //    {
        //        try
        //        {
        //            // 2. 從主題服務拿目前的卡片背景色，這個回傳的型別就是 SKColor
        //            var bgColor = _themeService.GetSkiaColor("MaterialDesignCardBackground", "#2D2D30");

        //            // 3. 建立記憶體中的圖表 (Headless Chart)
        //            var skChart = new SKCartesianChart
        //            {
        //                Width = 1600,
        //                Height = 800,
        //                Series = StationChartSeries,
        //                XAxes = StationXAxes,
        //                YAxes = StationYAxes,

        //                // 🌟 修正這裡：直接給 SKColor，不需要再包裝成 SolidColorPaint
        //                Background = bgColor
        //            };

        //            // 4. 存檔
        //            skChart.SaveImage(dialog.FileName);

        //            MessageBox.Show("圖表已成功儲存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    }
        //}


        /// <summary>
        /// 當沒資料或初始化時，讓軸線依然保有主題線條色，視覺上比較美觀
        /// </summary>
        private void InitEmptyChartAxes()
        {
            var themeTextPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
            var themeLinePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };

            StationXAxes = new Axis[] { new Axis { LabelsPaint = themeTextPaint, SeparatorsPaint = themeLinePaint } };
            StationYAxes = new Axis[] { new Axis { MinLimit = 0, MinStep = 1, LabelsPaint = themeTextPaint, SeparatorsPaint = themeLinePaint } };
        }

        /// <summary>
        /// 當使用者點擊切換開關時，自動觸發此方法重新繪製圖表
        /// </summary>
        partial void OnIsGroupedByNextStationChanged(bool oldValue, bool newValue)
        {
            SortWipDataList();
            UpdateChart();
        }

        [RelayCommand]
        private async Task SearchWipAsync()
        {
            if (IsSearching) return; // 防連點機制

            IsSearching = true;

            // 每次查詢前先重置提示訊息
            TotalCountMessage = "查詢中...";
            IsLimitReached = false;

            try
            {
                WipDataList.Clear();
                var result = await _apiService.GetWipStatusAsync(
                    SearchStartDate.ToString("yyyy-MM-dd 00:00:00"),
                    SearchEndDate.ToString("yyyy-MM-dd 23:59:59"),
                    SearchLotNo,
                    SearchProductName,
                    SearchParentLotNo,
                    IncludeCompleted
                );

                if (result.IsSuccess)
                {
                    // 先用 List 收集資料，不要在迴圈內直接操作 ObservableCollection
                    var tempBuffer = new List<WipStatusModel>();

                    foreach (var item in result.Data)
                    {
                        if (DateTime.TryParse(item.last_update_time, out DateTime dt))
                        {
                            item.last_update_time = dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
                        }

                        tempBuffer.Add(item); // 先加到暫存區
                    }

                    //  一次性將資料丟給 ObservableCollection
                    WipDataList = new ObservableCollection<WipStatusModel>(tempBuffer);

                    // 執行剛寫好的自動排序
                    SortWipDataList();

                    // 更新總筆數與上限判斷
                    int currentCount = WipDataList.Count;
                    IsLimitReached = currentCount >= 1000;
                    TotalCountMessage = $"總筆數: {currentCount} (系統最多顯示前 1000 筆)";

                    // 資料載入完成後，更新圖表與主題色彩
                    UpdateChart();
                }
                else
                {
                    TotalCountMessage = "查詢失敗";
                    await MaterialMessageBox.ShowAsync($"查詢失敗：{result.ErrorMessage}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                TotalCountMessage = "查詢異常";
                await MaterialMessageBox.ShowAsync($"發生異常：{ex.Message}", "系統異常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
            }
        }

        /// <summary>
        /// 根據目前的資料與主題色彩設定，重新計算並更新直方圖
        /// </summary>
        private void UpdateChart()
        {
            // 1. 從主題服務撈出符合當前系統（暗黑/白天模式）的 Skia 畫筆
            var themeTextPaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignBody", "#E0E0E0"));
            var themeLinePaint = new SolidColorPaint(_themeService.GetSkiaColor("MaterialDesignDivider", "#424242")) { StrokeThickness = 1 };
            var themeAccentPaint = new SolidColorPaint(_themeService.GetSkiaColor("SecondaryHueMidBrush", "#CDDC39"));

            if (WipDataList == null || !WipDataList.Any())
            {
                StationChartSeries = Array.Empty<ISeries>();
                InitEmptyChartAxes();
                return;
            }

            // 2. 根據 IsGroupedByNextStation 決定要群組的欄位
            var groupedData = IsGroupedByNextStation
                ? WipDataList.GroupBy(x => string.IsNullOrWhiteSpace(x.next_station) ? "無資料" : x.next_station)
                             .OrderBy(g => g.Min(x => x.next_station_no)) //  依照群組內最小的下一站工序號碼排序
                : WipDataList.GroupBy(x => string.IsNullOrWhiteSpace(x.current_station) ? "尚未開工" : x.current_station)
                             .OrderBy(g => g.Min(x => x.current_station_no)); //  依照群組內最小的當前站點工序號碼排序

            // 3. 擷取 X 軸標籤與 Y 軸數量
            var labels = groupedData.Select(g => g.Key).ToArray();
            var values = groupedData.Select(g => (double)g.Count()).ToArray();

            // 4. 更新圖表 Series (套用主題色、上方數字提示)
            StationChartSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = IsGroupedByNextStation ? "下一站數量" : "當前站點數量",
                    Values = values,
                    MaxBarWidth = 40,
                    Fill = themeAccentPaint,                     // 柱體顏色 (Lime 強調色)
                    DataLabelsPaint = themeTextPaint,            // 柱體上方數字顏色
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsSize = 12
                }
            };

            // 5. 更新 X 軸樣式 (套用主題文字、格線顏色)
            StationXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = 20,                          // 稍微旋轉避免文字重疊
                    LabelsPaint = themeTextPaint,                 // X 軸標籤文字顏色
                    SeparatorsPaint = themeLinePaint              // X 軸垂直分割線
                }
            };

            // 6. 更新 Y 軸樣式 (套用主題文字、格線顏色)
            StationYAxes = new Axis[]
            {
                new Axis
                {
                    Name = "工單數量",
                    LabelsRotation = 20,                          // 稍微旋轉避免文字重疊
                    MinLimit = 0,
                    MinStep = 1,                                  // 強制以整數跳格
                    NamePaint = themeTextPaint,                   // Y 軸標題顏色
                    LabelsPaint = themeTextPaint,                 // Y 軸標籤文字顏色
                    SeparatorsPaint = themeLinePaint              // Y 軸水平網格線
                }
            };
        }

        /// <summary>
        /// 根據目前的「當前站點 / 下一站」模式，自動將 DataGrid 的資料進行正確的邏輯排序
        /// </summary>
        private void SortWipDataList()
        {
            if (WipDataList == null || !WipDataList.Any()) return;

            //使用「工序號碼 (_no)」排序，保證順序與直方圖 X 軸的順序完全一致！
            // 當站點相同時，再利用工單號 (Lot_no) 進行次要排序，畫面會更整齊
            var sortedData = IsGroupedByNextStation
                ? WipDataList.OrderBy(x => x.next_station_no).ThenBy(x => x.Lot_no)
                : WipDataList.OrderBy(x => x.current_station_no).ThenBy(x => x.Lot_no);

            // 直接替換整個集合，WPF 只會重繪 DataGrid 一次！
            WipDataList = new ObservableCollection<WipStatusModel>(sortedData);
        }

        /// <summary>
        /// 當使用者在直方圖上雙擊某一根柱子時觸發 (反向操作)
        /// </summary>
        /// <param name="point">由圖表控制項傳回的完整資料點資訊</param>
        [RelayCommand]
        private void ChartDoubleClick(ChartPoint point)
        {
            // 防呆：確保參數與基礎資料存在
            if (point == null || StationXAxes == null || StationXAxes.Length == 0 || WipDataList == null || !WipDataList.Any()) return;

            // 泛用化取值示範：
            // 取得 X 軸的索引值 (即原本的 xIndex)
            int xIndex = (int)point.Coordinate.SecondaryValue;

            // 取得 Y 軸的數值 (如果您未來的雙軸圖表需要 Y 值，就是用這個)
            // double yValue = point.Coordinate.PrimaryValue;

            // 取得被點擊的是哪一組 Series (如果您有雙線圖，可以藉此判斷點了哪一條線)
            // var seriesName = point.Context.Series.Name;

            //point.Context.DataSource (能直接拿到當初塞入 Values 的底層原始 Model 物件)
            var xAxis = StationXAxes[0];
            if (xAxis.Labels == null || xIndex < 0 || xIndex >= xAxis.Labels.Count) return;

            // 1. 根據雙擊的 Index，換算出實際的站點文字名稱
            string targetStationName = xAxis.Labels[xIndex];

            // 2. 根據目前系統分組模式，去 DataList 尋找第一筆吻合的工單
            WipStatusModel matchedItem = null;

            if (IsGroupedByNextStation)
            {
                matchedItem = WipDataList.FirstOrDefault(x =>
                    (string.IsNullOrWhiteSpace(x.next_station) ? "無資料" : x.next_station) == targetStationName);
            }
            else
            {
                matchedItem = WipDataList.FirstOrDefault(x =>
                    (string.IsNullOrWhiteSpace(x.current_station) ? "尚未開工" : x.current_station) == targetStationName);
            }

            // 3. 如果找到了，直接將其設定為選取項，觸發 DataGrid 滾動！
            if (matchedItem != null)
            {
                SelectedWipItem = matchedItem;
            }
        }
    }
}
