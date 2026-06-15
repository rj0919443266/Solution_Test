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
    public partial class VM_System_SetDetail_ProblemClassification : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<ProblemClassificationModel> _object_List = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_ProblemClassification(ApiService apiService)
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
                var result = await _apiService.PostAsync<List<ProblemClassificationModel>>("get_ProblemClassifications", new { });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Object_List.Clear();
                    if (result.IsSuccess && result.Data != null)
                        foreach (var item in result.Data) Object_List.Add(item);
                    HasNoData = Object_List.Count == 0;
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
            var dialogVM = new VM_System_SetDetail_ProblemClassificationEditDialog();
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ProblemClassificationModel newItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_ProblemClassification", newItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "新增失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EditAnomalyAsync(ProblemClassificationModel target)
        {
            if (target == null) return;
            var dialogVM = new VM_System_SetDetail_ProblemClassificationEditDialog(target.Clone());
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ProblemClassificationModel updatedItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_ProblemClassification", updatedItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "修改失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAnomalyAsync(ProblemClassificationModel target)
        {
            if (target == null) return;
            var confirm = await MaterialMessageBox.ShowAsync($"確定刪除異常代碼 [{target.Definition}]？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_ProblemClassification", new { BarCode = target.BarCode });
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "刪除失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }
    }
}