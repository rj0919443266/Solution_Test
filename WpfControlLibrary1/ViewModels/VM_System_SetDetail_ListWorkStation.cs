using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_ListWorkStation : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<ListWorkStationModel> _workStationList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_ListWorkStation(ApiService apiService)
        {
            _apiService = apiService;
            // 進入畫面時自動刷洗資料
            LoadDataCommand.Execute(null);
        }

        /// <summary>
        /// [R] 查詢讀取：非同步載入工作站清單
        /// </summary>
        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return; // 防連點機制

            IsLoading = true;
            HasNoData = false;
            try
            {
                var result = await _apiService.PostAsync<List<ListWorkStationModel>>("get_work_stations", new { });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    WorkStationList.Clear();
                    if (result.IsSuccess && result.Data != null)
                    {
                        foreach (var station in result.Data)
                        {
                            WorkStationList.Add(station);
                        }
                    }
                    HasNoData = WorkStationList.Count == 0;
                });
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"資料讀取異常: {ex.Message}", "系統錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// [C] 新增工作站
        /// </summary>
        [RelayCommand]
        private async Task AddWorkStationAsync()
        {
            // 需事先建置對應的 Dialog ViewModel: VM_System_SetDetail_ListWorkStationEditDialog
            var dialogVM = new VM_System_SetDetail_ListWorkStationEditDialog(_apiService);

            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ListWorkStationModel newStation)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_work_station", newStation);
                if (apiResult.IsSuccess)
                {
                    await LoadDataAsync();
                }
                else
                {
                    await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "資料庫新增失敗", "API 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                IsLoading = false;
            }
        }

        /// <summary>
        /// [U] 修改工作站
        /// </summary>
        [RelayCommand]
        private async Task EditWorkStationAsync(ListWorkStationModel targetStation)
        {
            if (targetStation == null) return;

            var dialogVM = new VM_System_SetDetail_ListWorkStationEditDialog(_apiService, targetStation.Clone());

            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ListWorkStationModel updatedStation)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_work_station", updatedStation);
                if (apiResult.IsSuccess)
                {
                    await LoadDataAsync();
                }
                else
                {
                    await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "資料庫更新失敗", "API 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                IsLoading = false;
            }
        }

        /// <summary>
        /// [D] 刪除工作站
        /// </summary>
        [RelayCommand]
        private async Task DeleteWorkStationAsync(ListWorkStationModel targetStation)
        {
            if (targetStation == null) return;

            var confirm = await MaterialMessageBox.ShowAsync(
                $"您確定要刪除該工作站紀錄嗎？\n\n站點條碼：{targetStation.BarCode}\n站點名稱：{targetStation.Definition}",
                "刪除確認警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_work_station", new { BarCode = targetStation.BarCode });
                if (apiResult.IsSuccess)
                {
                    await LoadDataAsync();
                }
                else
                {
                    await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "資料庫刪除失敗", "API 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                IsLoading = false;
            }
        }
    }
}