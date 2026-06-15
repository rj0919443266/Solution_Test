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
    public partial class VM_System_SetDetail_ProcessDepartment : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<ProcessDepartmentModel> _objectList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_ProcessDepartment(ApiService apiService)
        {
            _apiService = apiService;
            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            IsLoading = true; HasNoData = false;
            try
            {
                var result = await _apiService.PostAsync<List<ProcessDepartmentModel>>("get_departments", new { });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ObjectList.Clear();
                    if (result.IsSuccess && result.Data != null)
                        foreach (var item in result.Data) ObjectList.Add(item);
                    HasNoData = ObjectList.Count == 0;
                });
            }
            catch (Exception ex) { await MaterialMessageBox.ShowAsync($"讀取異常: {ex.Message}", "系統錯誤", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task AddDepartmentAsync()
        {
            var result = await DialogHost.Show(new VM_System_SetDetail_ProcessDepartmentEditDialog(), "RootDialog");
            if (result is ProcessDepartmentModel newObj)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_department", newObj);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "新增失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EditDepartmentAsync(ProcessDepartmentModel target)
        {
            if (target == null) return;
            var result = await DialogHost.Show(new VM_System_SetDetail_ProcessDepartmentEditDialog(target.Clone()), "RootDialog");
            if (result is ProcessDepartmentModel updatedObj)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_department", updatedObj);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "修改失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteDepartmentAsync(ProcessDepartmentModel target)
        {
            if (target == null) return;
            if (await MaterialMessageBox.ShowAsync($"確定刪除部門 [{target.Definition}]？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_department", new { BarCode = target.BarCode });
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "刪除失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }
    }
}