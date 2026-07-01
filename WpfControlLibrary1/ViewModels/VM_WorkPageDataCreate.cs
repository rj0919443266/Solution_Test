using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services; // 依賴注入的 ApiService 等

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_WorkPageDataCreate : ObservableObject , IUserLevelReceiver
    {
        private readonly ApiService _apiService;
        private readonly ISnackbarService _snackbarService;
        private readonly IServiceProvider _serviceProvider;// 宣告 DI 容器
        private readonly IWindowManager _windowManager; // 注入視窗服務

        [ObservableProperty] private string _lotNo;
        [ObservableProperty] private string _productName;
        [ObservableProperty] private int? _quantity = 1;
        [ObservableProperty] private string _lotNoParent;

        [ObservableProperty] private ObservableCollection<ListWorkStationModel> _workStationList = new();
        [ObservableProperty] private ObservableCollection<TempWorkStationModel> _currentRecipe = new();
        [ObservableProperty] private TempWorkStationModel _selectedStation;


        // 紀錄登入者資訊
        [ObservableProperty] private LoginResponseModel _loginInfo;
        //[ObservableProperty] private string _currentUserId;
        //[ObservableProperty] private string _currentUserName;
        //[ObservableProperty] private int _currentUserLevel;

        [ObservableProperty]
        private ObservableCollection<string> _productNameList = new();

        [ObservableProperty]
        private ObservableCollection<WorkPageDataSummaryModel> _latestRecords = new();

        [ObservableProperty]
        private bool _isAutoIncrementLotNo = true; // 控制是否自動 +1 的狀態

        public VM_WorkPageDataCreate(ApiService apiService, ISnackbarService snackbarService, IServiceProvider serviceProvider, IWindowManager windowManager)
        {
            _apiService = apiService;
            _snackbarService = snackbarService;
            _serviceProvider = serviceProvider;
            _windowManager = windowManager;

            // 初始化載入
            _ = LoadWorkStationsAsync();
            _ = LoadProductNamesAsync();
            _ = LoadLatestRecordsAsync(); // 載入右側最新 50 筆
            
        }

        /// <summary>
        /// 使用者登入
        /// </summary>
        /// <param name="loginInfo"></param>
        public void ReceiveUserLevel(LoginResponseModel loginInfo)
        {
            if (loginInfo != null)
            {
                LoginInfo = loginInfo;

                //CurrentUserId = loginInfo.User_ID;
                //CurrentUserName = loginInfo.UserName;
                //CurrentUserLevel = loginInfo.Level;

                // 若有需要，也可在這裡根據權限 (Level) 控制畫面按鈕的開放與否
            }
        }


        /// <summary>
        /// 從 API 非同步載入產品名稱列表，並更新 ProductNameList 屬性
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// 讀取右側最新紀錄
        /// </summary>
        [RelayCommand]
        private async Task LoadLatestRecordsAsync()
        {
            var res = await _apiService.GetLatestWorkOrdersAsync();
            if (res.IsSuccess && res.Data != null)
            {
                LatestRecords = res.Data;
            }
        }



        private async Task LoadWorkStationsAsync()
        {
            try
            {
                var wsRes = await _apiService.PostAsync<ObservableCollection<ListWorkStationModel>>("get_work_stations", new { });
                if (wsRes.IsSuccess && wsRes.Data != null)
                {
                    // 根據優先權(或自訂邏輯)排序
                    var sortedList = wsRes.Data.OrderBy(x => $"{x.BarCode} {x.Definition}");
                    WorkStationList = new ObservableCollection<ListWorkStationModel>(sortedList);
                }
            }
            catch (Exception ex)
            {
                _snackbarService.ShowSnackbar($"獲取工程站別失敗: {ex.Message}", SnackbarMessageType.Warning);
            }
        }

        /// <summary>
        /// 軟刪除指定紀錄
        /// </summary>
        [RelayCommand]
        private async Task DeleteRecordAsync(WorkPageDataSummaryModel record)
        {
            if (record == null) return;
            if (LoginInfo == null)
            {
                _snackbarService.ShowSnackbar($"未登入 使用者 :", SnackbarMessageType.Error);
                return;
            }

            // 再次確認對話框防呆 (需確認您有對應的 DialogHost 或 MessageBox 服務)
            var confirm = await MaterialMessageBox.ShowAsync($"確定要刪除批號 {record.LotNo} 嗎？", "刪除確認", MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var res = await _apiService.SoftDeleteWorkOrderAsync(record.LotNo, record.ProductName, LoginInfo.User_ID);
                if (res.IsSuccess)
                {
                    _snackbarService.ShowSnackbar($"工單 {record.LotNo} 已刪除", SnackbarMessageType.Success);
                    // 重新整理右側列表
                    await LoadLatestRecordsAsync();
                }
                else
                {
                    _snackbarService.ShowSnackbar($"刪除失敗: {res.ErrorMessage}", SnackbarMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _snackbarService.ShowSnackbar($"例外錯誤: {ex.Message}", SnackbarMessageType.Error);
            }
        }

        [RelayCommand]
        private void AddStation()
        {
            CurrentRecipe.Add(new TempWorkStationModel { Can_Pass = false, Work_station = "" });
            RefreshNumbers();
        }

        [RelayCommand]
        private void InsertStation()
        {
            if (SelectedStation != null)
            {
                int index = CurrentRecipe.IndexOf(SelectedStation);
                CurrentRecipe.Insert(index, new TempWorkStationModel { Can_Pass = false, Work_station = "" });
                RefreshNumbers();
            }
        }

        [RelayCommand]
        private void DeleteStation(TempWorkStationModel station)
        {
            if (station != null)
            {
                CurrentRecipe.Remove(station);
                RefreshNumbers(); // 重新計算 1, 2, 3... 排序
            }
        }

        private void RefreshNumbers()
        {
            for (int i = 0; i < CurrentRecipe.Count; i++)
            {
                CurrentRecipe[i].No = i + 1;
            }
        }

        [RelayCommand]
        private void LoadRecipe()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipe");
            Directory.CreateDirectory(dir); // 確保資料夾存在

            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = dir,
                Filter = "Recipe files (*.rcp)|*.rcp|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<TempWorkStationModel>), new XmlRootAttribute("ArrayOfTemp_work_station"));
                    using FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open);
                    var stations = (List<TempWorkStationModel>)serializer.Deserialize(fs);

                    CurrentRecipe = new ObservableCollection<TempWorkStationModel>(stations);
                    RefreshNumbers(); // 重新賦予 NO 順序
                    _snackbarService.ShowSnackbar("配方讀取成功", SnackbarMessageType.Success);

                }
                catch (Exception ex)
                {
                    _snackbarService.ShowSnackbar($"讀取配方失敗: {ex.Message}", SnackbarMessageType.Error);
                }
            }
        }

        [RelayCommand]
        private void SaveRecipe()
        {
            if (!CurrentRecipe.Any())
            {
                _snackbarService.ShowSnackbar("沒有可儲存的工序資料", SnackbarMessageType.Warning);
                return;
            }

            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipe");
            Directory.CreateDirectory(dir);

            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = dir,
                Filter = "Recipe files (*.rcp)|*.rcp",
                DefaultExt = ".rcp",
                FileName = string.IsNullOrWhiteSpace(LotNo) ? "NewRecipe" : LotNo
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<TempWorkStationModel>), new XmlRootAttribute("ArrayOfTemp_work_station"));
                    using FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create);
                    serializer.Serialize(fs, CurrentRecipe.ToList());
                    _snackbarService.ShowSnackbar("配方儲存成功", SnackbarMessageType.Success);
                }
                catch (Exception ex)
                {
                    _snackbarService.ShowSnackbar($"儲存配方失敗: {ex.Message}", SnackbarMessageType.Error);
                }
            }
        }

        /// <summary>
        /// 字串尾數自動加 1 (保留前綴與補零格式)
        /// 例: A202606070088 -> A202606070089
        /// </summary>
        private string GetIncrementedLotNo(string originalLotNo)
        {
            if (string.IsNullOrWhiteSpace(originalLotNo)) return originalLotNo;

            // 使用正則表達式，把字串拆成「前綴(Group 1)」與「結尾數字(Group 2)」
            var match = Regex.Match(originalLotNo, @"^(.+?)(\d+)$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string numberPart = match.Groups[2].Value;

                if (int.TryParse(numberPart, out int currentNumber))
                {
                    currentNumber++;
                    // ToString("D" + length) 確保 0088 加 1 後依然是 0089 (保持長度)
                    return prefix + currentNumber.ToString("D" + numberPart.Length);
                }
            }

            // 防呆：如果字串結尾完全沒有數字，就直接在後面補 1
            return originalLotNo + "1";
        }

        [RelayCommand]
        private async Task SaveToDatabaseAsync()
        {
            if (LoginInfo == null)
            {
                _snackbarService.ShowSnackbar("未登入", SnackbarMessageType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(LotNo) || string.IsNullOrWhiteSpace(ProductName))
            {
                _snackbarService.ShowSnackbar("請填寫必填欄位 (Lot_no, Product_name)", SnackbarMessageType.Warning);
                return;
            }

            // 檢查是否有設定工序 (防呆：不可建立沒有站點的空工單)
            if (!CurrentRecipe.Any())
            {
                _snackbarService.ShowSnackbar("請至少加入一筆工程站別", SnackbarMessageType.Warning);
                return;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), // 允許所有 Unicode 字元
                    WriteIndented = false // 設為 false 節省資料庫空間
                };
                string stationJson = JsonSerializer.Serialize(CurrentRecipe, options);

                var payload = new
                {
                    Lot_no = LotNo,
                    Product_name = ProductName,
                    Quantity = Quantity ?? 1,             
                    Lot_no_Parenet = LotNoParent ?? "",   //確保給空字串，防止 JSON 傳 null 造成 DB 錯誤
                    list_Work_station = stationJson,
                    list_work_Data = "[]",
                    c_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    create_user = LoginInfo.User_ID
                };

                var res = await _apiService.PostAsync<object>("create_work_page_data", payload);

                if (res.IsSuccess)
                {
                    _snackbarService.ShowSnackbar("工單資料建檔成功", SnackbarMessageType.Success);

                    // 重新讀取右側最新清單
                    await LoadLatestRecordsAsync();

                    // 🌟 修改：依據是否勾選自動 +1 決定 LotNo 的行為
                    if (IsAutoIncrementLotNo)
                    {
                        LotNo = GetIncrementedLotNo(LotNo);
                    }
                    else
                    {
                        LotNo = string.Empty;
                    }

                    //ProductName = string.Empty;
                    //LotNoParent = string.Empty; 
                    //Quantity = 1;

                    //CurrentRecipe.Clear();
                }
                else
                {
                    _snackbarService.ShowSnackbar($"建檔失敗: {res.ErrorMessage}", SnackbarMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _snackbarService.ShowSnackbar($"例外錯誤: {ex.Message}", SnackbarMessageType.Error);
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            // 重置所有欄位
            LotNo = string.Empty;
            ProductName = string.Empty;
            LotNoParent = string.Empty;
            Quantity = 1;

            // 清空配方列表
            CurrentRecipe.Clear();

        
        }


        [RelayCommand]
        private async Task ShowDetailAsync(WorkPageDataSummaryModel selectedItem)
        {
            if (selectedItem == null) return;

            // 1. 從 DI 要一個全新的子 ViewModel
            var newDetailVM = _serviceProvider.GetRequiredService<VM_LotDetail>();

            // 2. 讓子 ViewModel 準備資料
            await newDetailVM.LoadDetailAsync(selectedItem.LotNo, selectedItem.ProductName);

            //3. 呼叫服務開啟視窗！
            _windowManager.ShowWindow(newDetailVM);
        }
    }
}