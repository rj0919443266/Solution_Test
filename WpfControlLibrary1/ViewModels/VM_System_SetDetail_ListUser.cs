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
    public partial class VM_System_SetDetail_ListUser : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private ObservableCollection<ListUserModel> _userList = new();
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _hasNoData = true;

        public VM_System_SetDetail_ListUser(ApiService apiService)
        {
            _apiService = apiService;
            // 進入畫面時自動刷洗資料
            LoadDataCommand.Execute(null);
        }

        /// <summary>
        /// [R] 查詢讀取：非同步載入
        /// </summary>
        [RelayCommand]
        public async Task LoadDataAsync()
        {
           
            if (IsLoading) return; // 防連點機制：如果正在讀取中，直接退回，不允許重複觸發

            IsLoading = true;
            HasNoData = false;
            try
            {
                // 傳入資源名稱 "get_users" 進行查詢
                var result = await _apiService.PostAsync<List<ListUserModel>>("get_users", new { });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UserList.Clear();
                    if (result.IsSuccess && result.Data != null)
                    {
                        foreach (var user in result.Data)
                        {
                            UserList.Add(user);
                        }
                    }
                    HasNoData = UserList.Count == 0;
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
        /// [C] 新增生產者：呼叫 DialogHost 並經由 API 寫入
        /// </summary>
        [RelayCommand]
        private async Task AddUserAsync()
        {
            // 必須將 _apiService 作為參數傳給對話框
            var dialogVM = new VM_System_SetDetail_ListUserEditDialog(_apiService);

            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ListUserModel newUser)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("add_user", newUser);
                if (apiResult.IsSuccess)
                {
                    await LoadDataAsync(); // 重新整理
                }
                else
                {
                    await MaterialMessageBox.ShowAsync(apiResult.ErrorMessage ?? "資料庫新增失敗", "API 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                IsLoading = false;
            }
        }
        /// <summary>
        /// [U] 修改生產者：帶入現有模型副本至 DialogHost 後更新
        /// </summary>
        [RelayCommand]
        private async Task EditUserAsync(ListUserModel targetUser)
        {
            if (targetUser == null) return;

            // 第一個參數必須傳入 _apiService，第二個參數才是 targetUser.Clone()
            var dialogVM = new VM_System_SetDetail_ListUserEditDialog(_apiService, targetUser.Clone());

            var result = await DialogHost.Show(dialogVM, "RootDialog");

            if (result is ListUserModel updatedUser)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("update_user", updatedUser);
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
        /// [D] 刪除生產者：拋出警告視窗確認後執行 API 刪除
        /// </summary>
        [RelayCommand]
        private async Task DeleteUserAsync(ListUserModel targetUser)
        {
            if (targetUser == null) return;

            var confirm = await MaterialMessageBox.ShowAsync(
                $"您確定要刪除該生產者人員嗎？\n\n工號：{targetUser.BarCode}\n姓名：{targetUser.Definition}",
                "刪除確認警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsLoading = true;
                var apiResult = await _apiService.PostAsync<string>("delete_user", new { BarCode = targetUser.BarCode });
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