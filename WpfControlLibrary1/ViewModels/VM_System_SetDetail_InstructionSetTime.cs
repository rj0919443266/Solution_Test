using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_InstructionSetTime : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<ProcessAnomalyModel> _objectList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_InstructionSetTime(ApiService apiService)
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
                var result = await _apiService.PostAsync<List<ProcessAnomalyModel>>("get_InstructionSetTimes", new { });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ObjectList.Clear();
                    if (result.IsSuccess && result.Data != null)
                        foreach (var item in result.Data) ObjectList.Add(item);
                    HasNoData = ObjectList.Count == 0;
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
            var dialogVM = new VM_System_SetDetail_ProcessAnomalyEditDialog();
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ProcessAnomalyModel newItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_InstructionSetTime", newItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "新增失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EditAnomalyAsync(ProcessAnomalyModel target)
        {
            if (target == null) return;
            var dialogVM = new VM_System_SetDetail_ProcessAnomalyEditDialog(target.Clone());
            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ProcessAnomalyModel updatedItem)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_InstructionSetTime", updatedItem);
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "修改失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAnomalyAsync(ProcessAnomalyModel target)
        {
            if (target == null) return;
            var confirm = await MaterialMessageBox.ShowAsync($"確定刪除異常代碼 [{target.Definition}]？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_InstructionSetTime", new { BarCode = target.BarCode });
                if (apiResult.IsSuccess) await LoadDataAsync();
                else await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "刪除失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                IsLoading = false;
            }
        }
    }
}
