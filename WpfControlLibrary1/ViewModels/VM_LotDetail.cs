using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_LotDetail : ObservableObject
    {
        // 綁定 XAML 的 TabControl 所選取的 Index
        [ObservableProperty]
        private int _selectedTabIndex = 0;
        //==============================
        // 將 4 個子 ViewModel 開放給 XAML 綁定
        public VM_LotDetail_WorkData WorkDataVM { get; }
        public VM_LotDetail_FIR FirVM { get; }
        public VM_LotDetail_IPI IpiVM { get; }
        public VM_LotDetail_Quality_Nonconformity_Report QualityNonconformityReportVM { get; }

        //==============================


        private readonly ApiService _apiService;

        // 這裡是工單的基本資訊，從外部 (VM_WipStatus) 傳入後，會顯示在畫面上方
        [ObservableProperty] private string _lotNo = "載入中...";
        [ObservableProperty] private string _productName = "";

        [ObservableProperty] private string _parentLotNo = "";
        [ObservableProperty] private string _create_user = "";
        [ObservableProperty] private DateTime? _c_time ;


        public VM_LotDetail(
            ApiService apiService , 
            VM_LotDetail_WorkData workDataVM,
            VM_LotDetail_FIR firVM,
            VM_LotDetail_IPI ipiVM , 
            VM_LotDetail_Quality_Nonconformity_Report qualityNonconformityReportVM)
        {
            _apiService = apiService;

            //  透過建構子注入的方式，將子 ViewModel 傳入並儲存起來，讓 XAML 可以綁定
            WorkDataVM = workDataVM;
            FirVM = firVM;
            IpiVM = ipiVM;
            QualityNonconformityReportVM = qualityNonconformityReportVM;

        }
        //  由外部 (VM_WipStatus) 呼叫，用來初始化整張工單
        public async Task LoadDetailAsync(string lotNo, string productName)
        {
            // 1. 初始化畫面狀態
            LotNo = lotNo;
            ProductName = productName;
            ParentLotNo = "查詢中...";
            _parentLotNo = "";
            _create_user = "";
            _c_time = null;

            //==================
            // 1. 把參數發派給所有子 VM 讓他們準備好
            WorkDataVM.SetContext(lotNo, productName);
            FirVM.SetContext(lotNo, productName);
            IpiVM.SetContext(lotNo, productName);
            QualityNonconformityReportVM.SetContext(lotNo, productName);
            //==================
            var result = await _apiService.Get_work_page_data_Detail_Async(LotNo);
            if (result.IsSuccess && result.Data != null)
            {
                UpdateBasicInfo(result.Data);
            }

            // 2. 重置 Tab 回到第一頁
            SelectedTabIndex = 0;

            // 3. 強制先載入第一頁的資料 (因為視窗打開時預設看到第一頁)
            await LoadCurrentTabAsync(0);

        }

        private void UpdateBasicInfo(dynamic data)
        {
            ParentLotNo = data.Lot_no_Parenet ?? "無柱號";
            Create_user = data.create_user ?? "";
            C_time = data.c_time;
        }

        // 監聽使用者切換 Tab 的動作 (CommunityToolkit 的魔法方法)
        partial void OnSelectedTabIndexChanged(int value)
        {
            // 當 XAML 切換 Tab 時，非同步觸發該分頁的載入邏輯
            _ = LoadCurrentTabAsync(value);
        }

        private async Task LoadCurrentTabAsync(int tabIndex)
        {
            // 根據目前的 Index，叫對應的小孩去打 API
            switch (tabIndex)
            {
                case 0:
                    await WorkDataVM.LoadDataAsync(); 
                    break;
                case 1:
                    await IpiVM.LoadDataAsync();
                    break;
                case 2: 
                    await FirVM.LoadDataAsync(); 
                    break;

                case 3: 
                    await QualityNonconformityReportVM.LoadDataAsync(); 
                    break;

            }
        }


      
        
    }
}