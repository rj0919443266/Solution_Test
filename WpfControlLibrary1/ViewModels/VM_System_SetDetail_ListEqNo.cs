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
    public partial class VM_System_SetDetail_ListEqNo : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<EqNoModel> _eqNoList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_ListEqNo(ApiService apiService)
        {
            _apiService = apiService;
            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (IsLoading) return; // 防連點
            IsLoading = true; HasNoData = false;
            try
            {
                var result = await _apiService.PostAsync<List<EqNoModel>>("get_eq_nos", new { });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    EqNoList.Clear();
                    if (result.IsSuccess && result.Data != null)
                        foreach (var item in result.Data) EqNoList.Add(item);
                    HasNoData = EqNoList.Count == 0;
                });
            }
            catch (Exception ex)
            {
                await MaterialMessageBox.ShowAsync($"讀取異常: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task AddEqNoAsync()
        {
            var dialogVM = new VM_System_SetDetail_EqNoEditDialog(_apiService);
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is EqNoModel newItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_eq_no", newItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "新增失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EditEqNoAsync(EqNoModel target)
        {
            if (target == null) return;
            var dialogVM = new VM_System_SetDetail_EqNoEditDialog(_apiService, target.Clone());
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is EqNoModel updatedItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_eq_no", updatedItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "修改失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteEqNoAsync(EqNoModel target)
        {
            if (target == null) return;
            var confirm = await MaterialMessageBox.ShowAsync($"確定刪除機台 [{target.Definition}]？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_eq_no", new { BarCode = target.BarCode });
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "刪除失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }
    }
}