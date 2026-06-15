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
    public partial class VM_System_SetDetail_ListWorkStationTwins : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<ListWorkStationTwinsModel> _twinsList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_ListWorkStationTwins(ApiService apiService)
        {
            _apiService = apiService;
            LoadDataCommand.Execute(null);
        }

        /// <summary>
        /// [R] 查詢讀取：非同步載入配對工作站清單
        /// </summary>
        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            HasNoData = false;
            try
            {
                var result = await _apiService.PostAsync<List<ListWorkStationTwinsModel>>("get_work_station_twins", new { });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TwinsList.Clear();
                    if (result.IsSuccess && result.Data != null)
                    {
                        foreach (var twin in result.Data)
                        {
                            TwinsList.Add(twin);
                        }
                    }
                    HasNoData = TwinsList.Count == 0;
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
        /// [C] 新增配對工作站
        /// </summary>
        [RelayCommand]
        private async Task AddTwinAsync()
        {
            // 需事先建置對應的 Dialog ViewModel: VM_System_SetDetail_ListWorkStationTwinsEditDialog
            var dialogVM = new VM_System_SetDetail_ListWorkStationTwinsEditDialog(_apiService);

            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ListWorkStationTwinsModel newTwin)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_work_station_twin", newTwin);
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
        /// [U] 修改配對工作站
        /// </summary>
        [RelayCommand]
        private async Task EditTwinAsync(ListWorkStationTwinsModel targetTwin)
        {
            if (targetTwin == null) return;

            var dialogVM = new VM_System_SetDetail_ListWorkStationTwinsEditDialog(_apiService, targetTwin.Clone());

            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ListWorkStationTwinsModel updatedTwin)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_work_station_twin", updatedTwin);
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
        /// [D] 刪除配對工作站
        /// </summary>
        [RelayCommand]
        private async Task DeleteTwinAsync(ListWorkStationTwinsModel targetTwin)
        {
            if (targetTwin == null) return;

            var confirm = await MaterialMessageBox.ShowAsync(
                $"您確定要刪除該配對紀錄嗎？\n\n主名稱：{targetTwin.WorkStationName}\n開始站點：{targetTwin.WorkStationStart}\n結束站點：{targetTwin.WorkStationEnd}",
                "刪除確認警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                // 以 Primary Key (WorkStationName) 作為刪除條件
                var apiResult = await _apiService.PostAsync<string>("delete_work_station_twin", new { WorkStationName = targetTwin.WorkStationName });
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