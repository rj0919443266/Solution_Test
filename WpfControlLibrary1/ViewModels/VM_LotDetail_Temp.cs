using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfControlLibrary1.Services;
namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_LotDetail_Temp : ObservableObject
    {
        private readonly ApiService _apiService;
        private bool _hasLoaded = false;

        // 儲存從父 VM 傳遞過來的查詢條件
        private string _currentLotNo;
        private string _currentProductName;

        [ObservableProperty]
        private bool _isLoading = false;

        // 假設你的資料模型
        [ObservableProperty]
        private object _workDataDetail;

        public VM_LotDetail_Temp(ApiService apiService)
        {
            _apiService = apiService;
        }
        public void SetContext(string lotNo, string productName)
        {
            _currentLotNo = lotNo;
            _currentProductName = productName;
            _hasLoaded = false; // 如果工單改變，重置讀取狀態
        }

        // 2. 實際觸發查詢 API 的方法
        public async Task LoadDataAsync()
        {
            // 防呆：如果已經載入過，或是正在載入中，直接跳出 (這就是 Lazy Loading 的精髓)
            if (_hasLoaded || IsLoading || string.IsNullOrEmpty(_currentLotNo)) return;

            IsLoading = true;
            try
            {
                // 這裡放你原本的 API 請求邏輯
                // var result = await _apiService.PostAsync<...>("get_work_data", new { ... });
                // WorkDataDetail = result.Data;

                // 模擬 API 延遲
                await Task.Delay(500);

                _hasLoaded = true; // 標記為已載入成功
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
