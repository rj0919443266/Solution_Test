using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_LotDetail_IPI : ObservableObject
    {
        private readonly ApiService _apiService;
        private bool _hasLoaded = false;
        private string _currentLotNo;

        [ObservableProperty] private bool _hasNoData = false;
        [ObservableProperty] private bool _isLoading = false;

        [ObservableProperty] private ObservableCollection<IpiRecordModel> _ipiRecords = new();
        [ObservableProperty] private IpiRecordModel _selectedIpiRecord;

        // 🌟 改用專屬的 JSON 結構 Model 來綁定下方 DataGrid
        [ObservableProperty] private ObservableCollection<IpiMeasurementItemModel> _selectedIpiDataDetails = new();

        public VM_LotDetail_IPI(ApiService apiService) { _apiService = apiService; }

        public void SetContext(string lotNo, string productName)
        {
            _currentLotNo = lotNo;
            _hasLoaded = false;
            HasNoData = false;
            IpiRecords.Clear();
            SelectedIpiDataDetails.Clear();
        }

        public async Task LoadDataAsync()
        {
            if (_hasLoaded || IsLoading || string.IsNullOrEmpty(_currentLotNo)) return;
            IsLoading = true;
            HasNoData = false;

            try
            {
                var result = await _apiService.PostAsync<ObservableCollection<IpiRecordModel>>(
                    "ipi_data_detail",
                    new { lot_no = _currentLotNo }
                );

                if (result.IsSuccess && result.Data != null)
                {
                    IpiRecords = result.Data;
                    if (IpiRecords.Count > 0)
                        SelectedIpiRecord = IpiRecords[0];
                    else
                        HasNoData = true;
                }
                else
                {
                    HasNoData = true;
                }
                _hasLoaded = true;
            }
            finally { IsLoading = false; }
        }

        // 🌟 核心：解析 JSON 並加入自動判定邏輯
        partial void OnSelectedIpiRecordChanged(IpiRecordModel value)
        {
            SelectedIpiDataDetails.Clear();
            if (value == null || string.IsNullOrWhiteSpace(value.DataJsonString)) return;

            try
            {
                var parsedData = JsonSerializer.Deserialize<IpiMeasurementItemModel[]>(value.DataJsonString);

                if (parsedData != null)
                {
                    foreach (var item in parsedData)
                    {
                        // --- 邏輯判定 ---
                        if (item.ActualMeasurement == null)
                        {
                            item.Note = "未量測 (漏填)";
                            item.StatusLevel = "Unknown";
                        }
                        else
                        {
                            if (item.UpperLimit.HasValue && item.ActualMeasurement.Value > item.UpperLimit.Value)
                            {
                                item.Note = "超出上限";
                                item.StatusLevel = "Alarm";
                            }
                            else if (item.LowerLimit.HasValue && item.ActualMeasurement.Value < item.LowerLimit.Value)
                            {
                                item.Note = "低於下限";
                                item.StatusLevel = "Alarm";
                            }
                            else
                            {
                                item.Note = "正常";
                                item.StatusLevel = "Normal";
                            }
                        }

                        SelectedIpiDataDetails.Add(item);
                    }
                }
            }
            catch (Exception) { /* JSON 解析錯誤防呆 */ }
        }
    }
}