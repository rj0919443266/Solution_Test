using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_Login : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty]
        private string _account;

        [ObservableProperty]
        private string _password;

        [ObservableProperty]
        private string _errorMessage;


        public VM_Login(ApiService apiService)
        {
            _apiService = apiService;
        }

        [RelayCommand]
        private async Task ExecuteLogin()
        {
            ErrorMessage = "登入中，請稍候...";

            var result = await _apiService.LoginAsync(Account, Password);

            if (result.IsSuccess)
            {
                // 🌟 告訴 DialogHost 關閉對話框，並回傳 true (代表登入成功)
                DialogHost.Close("RootDialog", result.Data);
            }
            else
            {
                // 顯示 API 回傳的真實錯誤訊息 (例如：密碼錯誤、無此帳號)
                ErrorMessage = result.ErrorMessage ?? "登入失敗！";
            }
        }


        // 取消按鈕
        [RelayCommand]
        private void Cancel()
        {
            DialogHost.Close("RootDialog", false);
        }

    }
}
