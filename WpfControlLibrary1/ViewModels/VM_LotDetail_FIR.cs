using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_LotDetail_FIR : ObservableObject
    {
        private readonly ApiService _apiService;
        private bool _hasLoaded = false;
        private string _currentLotNo;
        private string _currentProductName;

        [ObservableProperty] private bool _hasNoData = false;// 用來控制是否顯示「無資料提示」的屬性
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private ObservableCollection<FirRecordModel> _firRecords = new();
        [ObservableProperty] private FirRecordModel _selectedFirRecord;

        // 🌟 改用新的 Display Model
        [ObservableProperty] private ObservableCollection<FirDisplayDetailModel> _selectedFirDataDetails = new();

        public VM_LotDetail_FIR(ApiService apiService) { _apiService = apiService; }

        public void SetContext(string lotNo, string productName)
        {
            _currentLotNo = lotNo;
            _currentProductName = productName;
            _hasLoaded = false;
            
            HasNoData = false; // 重置無資料的狀態

            FirRecords.Clear();
            SelectedFirDataDetails.Clear();
        }

        public async Task LoadDataAsync()
        {
            if (_hasLoaded || IsLoading || string.IsNullOrEmpty(_currentLotNo)) return;
            IsLoading = true;
            HasNoData = false; // 🌟 3. 載入前先隱藏提示

            try
            {
                var result = await _apiService.PostAsync<ObservableCollection<FirRecordModel>>(
                    "fir_data_detail",
                    new { lot_no = _currentLotNo, product_name = _currentProductName }
                );

                if (result.IsSuccess && result.Data != null)
                {
                    foreach (var record in result.Data)
                    {
                        CalculateRecordIssues(record);
                    }

                    FirRecords = result.Data;

                    if (FirRecords.Count > 0)
                    {
                        SelectedFirRecord = FirRecords[0];
                    }
                    else
                    {
                        // 🌟 4. 如果資料庫回傳成功，但是筆數為 0，則顯示無資料畫面
                        HasNoData = true;
                    }
                }
                else
                {
                    // 若 API 發生錯誤或無回傳，也可以視為無資料 (或未來可擴充 HasError)
                    HasNoData = true;
                }
                _hasLoaded = true;
            }
            finally { IsLoading = false; }
        }

        // 預先解析 JSON 並統計所有狀態數量
        private void CalculateRecordIssues(FirRecordModel record)
        {
            record.AlarmCount = 0;
            record.WarningCount = 0;
            record.NormalCount = 0;
            record.UnknownCount = 0; // 🌟 負責統計 ? 的數量

            if (string.IsNullOrWhiteSpace(record.DataJsonString) || string.IsNullOrWhiteSpace(record.SetDataJsonString))
                return;

            try
            {
                var rawData = JsonSerializer.Deserialize<FirRawDataModel[]>(record.DataJsonString);
                var setData = JsonSerializer.Deserialize<FirSetDataModel[]>(record.SetDataJsonString);

                if (rawData == null || setData == null) return;

                foreach (var raw in rawData)
                {
                    var setting = setData.FirstOrDefault(s => s.Name == raw.Name);

                    // 🌟 1. 判斷是否為「無法判定」(找不到設定、空值、格式無法轉為數字)
                    if (setting == null || string.IsNullOrWhiteSpace(raw.Value) || !double.TryParse(raw.Value, out double actualVal))
                    {
                        record.UnknownCount++;
                        continue; // 直接換下一筆，不往下判斷
                    }

                    // 2. 判斷 Alarm (超限)
                    if ((setting.LimitsUpper.HasValue && actualVal >= setting.LimitsUpper.Value) ||
                        (setting.LimitsLower.HasValue && actualVal <= setting.LimitsLower.Value))
                    {
                        record.AlarmCount++;
                    }
                    // 3. 判斷 Warning (預警)
                    else if ((setting.LimitsUpperWarning.HasValue && actualVal >= setting.LimitsUpperWarning.Value) ||
                             (setting.LimitsLowerWarning.HasValue && actualVal <= setting.LimitsLowerWarning.Value))
                    {
                        record.WarningCount++;
                    }
                    //  4. 如果都有設定，且沒有超限，就是正常！
                    else
                    {
                        record.NormalCount++;
                    }
                }

                // 5. 進階防呆：統計「有設定值，但在實際測量資料中整筆遺失」的項目，算入無法判定
                var missingMeasurementsCount = setData.Count(s => !rawData.Any(r => r.Name == s.Name));
                record.UnknownCount += missingMeasurementsCount;
            }
            catch { /* 忽略解析錯誤，維持數量預設值 */ }
        }

        // 🌟 核心解析與判定邏輯
        partial void OnSelectedFirRecordChanged(FirRecordModel value)
        {
            SelectedFirDataDetails.Clear();
            if (value == null) return;

            try
            {
                // 1. 反序列化量測資料與設定資料 (若無設定檔則給空陣列防呆)
                var rawData = string.IsNullOrWhiteSpace(value.DataJsonString)
                    ? Array.Empty<FirRawDataModel>()
                    : JsonSerializer.Deserialize<FirRawDataModel[]>(value.DataJsonString);

                var setData = string.IsNullOrWhiteSpace(value.SetDataJsonString)
                    ? Array.Empty<FirSetDataModel>()
                    : JsonSerializer.Deserialize<FirSetDataModel[]>(value.SetDataJsonString);

                // 2. 進行關聯比對
                foreach (var raw in rawData)
                {
                    // 找出對應的設定
                    var setting = setData.FirstOrDefault(s => s.Name == raw.Name);

                    var displayItem = new FirDisplayDetailModel
                    {
                        Name = raw.Name,
                        ActualData = raw.Value,
                        Target = setting?.LimitsMid?.ToString() ?? "-",
                        LowerLimit = setting?.LimitsLower?.ToString() ?? "-",
                        LowerWarning = setting?.LimitsLowerWarning?.ToString() ?? "-",
                        UpperWarning = setting?.LimitsUpperWarning?.ToString() ?? "-",
                        UpperLimit = setting?.LimitsUpper?.ToString() ?? "-",
                        Note = "Normal",
                        StatusLevel = "Normal"
                    };

                    // 3. 判斷邏輯 (將實際字串轉為 double 進行比較)
                    if (setting != null && double.TryParse(raw.Value, out double actualVal))
                    {
                        if (setting.LimitsUpper.HasValue && actualVal >= setting.LimitsUpper.Value)
                        {
                            displayItem.Note = "Upper Alarm";
                            displayItem.StatusLevel = "Alarm";
                        }
                        else if (setting.LimitsLower.HasValue && actualVal <= setting.LimitsLower.Value)
                        {
                            displayItem.Note = "Lower Alarm";
                            displayItem.StatusLevel = "Alarm";
                        }
                        else if (setting.LimitsUpperWarning.HasValue && actualVal >= setting.LimitsUpperWarning.Value)
                        {
                            displayItem.Note = "Upper Warning";
                            displayItem.StatusLevel = "Warning";
                        }
                        else if (setting.LimitsLowerWarning.HasValue && actualVal <= setting.LimitsLowerWarning.Value)
                        {
                            displayItem.Note = "Lower Warning";
                            displayItem.StatusLevel = "Warning";
                        }
                    }
                    else if (setting == null)
                    {
                        displayItem.Note = "未設定規格";
                        displayItem.StatusLevel = "Unknown";
                    }

                    SelectedFirDataDetails.Add(displayItem);
                }
            }
            catch (Exception) { /* JSON 解析錯誤處理 */ }
        }
    }
}