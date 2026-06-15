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
    public partial class VM_System_SetDetail_QualityAbnormalAction : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<QualityAbnormalActionModel> _qualityAbnormalActionList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_QualityAbnormalAction(ApiService apiService)
        {
            _apiService = apiService;
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return; // 防連點
            IsLoading = true; HasNoData = false;
            try
            {
                var result = await _apiService.PostAsync<List<QualityAbnormalActionModel>>("get_QualityAbnormalActions", new { });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    QualityAbnormalActionList.Clear();
                    if (result.IsSuccess && result.Data != null)
                        foreach (var item in result.Data) QualityAbnormalActionList.Add(item);
                    HasNoData = QualityAbnormalActionList.Count == 0;
                });
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"讀取異常: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task AddAnomalyAsync()
        {
            var dialogVM = new VM_System_SetDetail_QualityAbnormalActionEditDialog();
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is QualityAbnormalActionModel newItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_QualityAbnormalAction", newItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "新增失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EditAnomalyAsync(QualityAbnormalActionModel target)
        {
            if (target == null) return;
            var dialogVM = new VM_System_SetDetail_QualityAbnormalActionEditDialog(target.Clone());
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is QualityAbnormalActionModel updatedItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_QualityAbnormalAction", updatedItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "修改失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAnomalyAsync(QualityAbnormalActionModel target)
        {
            if (target == null) return;
            var confirm = await MaterialMessageBox.ShowAsync($"確定刪除異常代碼 [{target.Definition}]？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_QualityAbnormalAction", new { BarCode = target.BarCode });
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "刪除失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }
    }
}