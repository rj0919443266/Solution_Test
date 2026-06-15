using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_LotDetail_Quality_Nonconformity_Report : ObservableObject
    {
        private readonly ApiService _apiService;
        private bool _hasLoaded = false;
        private string _currentLotNo;
        private string _currentProductName;

        [ObservableProperty] private bool _hasNoData = false;
        [ObservableProperty] private bool _isLoading = false;

        [ObservableProperty] private ObservableCollection<QualityNoticeModel> _noticeRecords = new();
        [ObservableProperty] private QualityNoticeModel _selectedNotice;

        // 🌟 改為兩個獨立的清單
        [ObservableProperty] private ObservableCollection<StationDisplayModel> _oldStationList = new();
        [ObservableProperty] private ObservableCollection<StationDisplayModel> _newStationList = new();

        public VM_LotDetail_Quality_Nonconformity_Report(ApiService apiService)
        {
            _apiService = apiService;
        }

        public void SetContext(string lotNo, string productName)
        {
            _currentLotNo = lotNo;
            _currentProductName = productName;
            _hasLoaded = false;
            HasNoData = false;
            NoticeRecords.Clear();
            OldStationList.Clear();
            NewStationList.Clear();
            SelectedNotice = null;
        }

        public async Task LoadDataAsync()
        {
            if (_hasLoaded || IsLoading || string.IsNullOrEmpty(_currentLotNo)) return;

            IsLoading = true;
            HasNoData = false;

            try
            {
                var result = await _apiService.PostAsync<ObservableCollection<QualityNoticeModel>>(
                    "quality_abnormal_notice",
                    new { lot_no = _currentLotNo }
                );

                if (result.IsSuccess && result.Data != null)
                {
                    NoticeRecords = result.Data;
                    if (NoticeRecords.Count > 0)
                        SelectedNotice = NoticeRecords[0];
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

        // 🌟 獨立解析與比對邏輯
        partial void OnSelectedNoticeChanged(QualityNoticeModel value)
        {
            OldStationList.Clear();
            NewStationList.Clear();
            if (value == null) return;

            try
            {
                var oldList = string.IsNullOrWhiteSpace(value.ListWorkStationOld)
                    ? new List<StationNodeModel>()
                    : JsonSerializer.Deserialize<List<StationNodeModel>>(value.ListWorkStationOld);

                var newList = string.IsNullOrWhiteSpace(value.ListWorkStationNew)
                    ? new List<StationNodeModel>()
                    : JsonSerializer.Deserialize<List<StationNodeModel>>(value.ListWorkStationNew);

                // 建立 HashSet 加快比對速度
                var oldNames = oldList.Select(x => x.WorkStation).ToHashSet();
                var newNames = newList.Select(x => x.WorkStation).ToHashSet();

                // 填入舊工序 (如果在新的找不到，就是被刪除)
                foreach (var oldItem in oldList)
                {
                    string status = newNames.Contains(oldItem.WorkStation) ? "Unchanged" : "Removed";
                    OldStationList.Add(new StationDisplayModel
                    {
                        No = oldItem.No,
                        CanPass = oldItem.CanPass,
                        WorkStation = oldItem.WorkStation,
                        DiffStatus = status
                    });
                }

                // 填入新工序 (如果在舊的找不到，就是新增)
                foreach (var newItem in newList)
                {
                    string status = oldNames.Contains(newItem.WorkStation) ? "Unchanged" : "Added";
                    NewStationList.Add(new StationDisplayModel
                    {
                        No = newItem.No,
                        CanPass = newItem.CanPass,
                        WorkStation = newItem.WorkStation,
                        DiffStatus = status
                    });
                }
            }
            catch (Exception) { /* 忽略解析錯誤 */ }
        }
    }
}