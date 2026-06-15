using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail : ObservableObject
    {
        // 綁定 XAML 的 TabControl 所選取的 Index
        [ObservableProperty]
        private int _selectedTabIndex = 0;
        //==============================// 註冊子畫面的 ViewModel
        public VM_System_SetDetail_ListUser ListUserVM { get; }
        public VM_System_SetDetail_ProcessDepartment ProcessDepartmentVM { get; }

        public VM_System_SetDetail_ListEqNo ListEqNoVM { get; }

        public VM_System_SetDetail_ProcessAnomaly ProcessAnomalyVM { get; }

        public VM_System_SetDetail_QualityAbnormalAction QualityAbnormalActionVM { get; }
        public VM_System_SetDetail_ProblemClassification ProblemClassificationVM { get; }
        public VM_System_SetDetail_InstructionSetNum InstructionSetNumVM { get; }
        public VM_System_SetDetail_InstructionSetTime InstructionSetTimeVM { get; }
        public VM_System_SetDetail_InstructionSetRemark InstructionSetRemarkVM { get; }

        public VM_System_SetDetail_ListWorkStation ListWorkStationVM { get; }
        public VM_System_SetDetail_ListWorkStationTwins ListWorkStationTwinsVM { get; }

        //==============================
        private readonly ApiService _apiService;
        public VM_System_SetDetail()
        {
        }
        public VM_System_SetDetail(ApiService apiService)
        {
            _apiService = apiService;

            ListUserVM = new VM_System_SetDetail_ListUser(apiService);
            ProcessDepartmentVM = new VM_System_SetDetail_ProcessDepartment(apiService);
            ListEqNoVM = new VM_System_SetDetail_ListEqNo(apiService);

            ProcessAnomalyVM = new VM_System_SetDetail_ProcessAnomaly(apiService);
            QualityAbnormalActionVM = new VM_System_SetDetail_QualityAbnormalAction(apiService);
            ProblemClassificationVM = new VM_System_SetDetail_ProblemClassification(apiService);
            InstructionSetNumVM = new VM_System_SetDetail_InstructionSetNum(apiService);
            InstructionSetTimeVM = new VM_System_SetDetail_InstructionSetTime(apiService);
            InstructionSetRemarkVM = new VM_System_SetDetail_InstructionSetRemark(apiService);

            ListWorkStationVM = new VM_System_SetDetail_ListWorkStation(apiService);
            ListWorkStationTwinsVM = new VM_System_SetDetail_ListWorkStationTwins(apiService);
        }

        public async Task LoadDetailAsync()
        {

            // 3. 強制先載入第一頁的資料 (因為視窗打開時預設看到第一頁)
            await LoadCurrentTabAsync(0);
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
                    await ListUserVM.LoadDataAsync();
                    break;
                case 1:
                    await ListEqNoVM.LoadDataAsync();
                    break;
                case 2:
                    await ProcessDepartmentVM.LoadDataAsync();
                    break;

                case 3:
                    await ListWorkStationVM.LoadDataAsync();
                    break;

                case 4:
                    await ListWorkStationTwinsVM.LoadDataAsync();
                    break;


                case 5:
                    // 品質異常別
                    await ProcessAnomalyVM.LoadDataAsync();
                    break;

                
                case 6:
                    // 異常問題分類
                    await ProblemClassificationVM.LoadDataAsync();
                    break;
                case 7:
                    // 異常處理動作
                    await QualityAbnormalActionVM.LoadDataAsync();
                    break;

                case 8:
                    // 數量
                    await InstructionSetNumVM.LoadDataAsync();
                    break;
                case 9:
                    // 時間
                    await InstructionSetTimeVM.LoadDataAsync();
                    break;
                case 10:
                    // 備註
                    await InstructionSetRemarkVM.LoadDataAsync();
                    break;
            }
        }

    }
}
