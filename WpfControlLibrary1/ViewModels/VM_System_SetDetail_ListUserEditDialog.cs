using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_ListUserEditDialog : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private string _dialogTitle = "新增 生產者人員";
        [ObservableProperty] private string _barCode = string.Empty;
        [ObservableProperty] private string _definition = string.Empty;
        [ObservableProperty] private string _department = string.Empty;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isEditMode = false;

        // 存放從資料庫撈取的部門清單
        [ObservableProperty] private ObservableCollection<ProcessDepartmentModel> _departmentList = new();

        // 🌟 建構子：接收 ApiService 去撈部門清單
        public VM_System_SetDetail_ListUserEditDialog(ApiService apiService, ListUserModel? targetUser = null)
        {
            _apiService = apiService;

            if (targetUser != null)
            {
                DialogTitle = "修改 生產者資料";
                BarCode = targetUser.BarCode;
                Definition = targetUser.Definition;
                Department = targetUser.Department;
                IsActive = targetUser.IsActive;
                IsEditMode = true;
            }

            // 啟動時自動撈取部門清單供下拉選擇
            LoadDepartmentsAsync();
        }

        private async Task LoadDepartmentsAsync()
        {
            var result = await _apiService.PostAsync<System.Collections.Generic.List<ProcessDepartmentModel>>("get_departments", new { });
            if (result.IsSuccess && result.Data != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    DepartmentList.Clear();
                    foreach (var dept in result.Data)
                    {
                        DepartmentList.Add(dept);
                    }
                });
            }
        }

        // 🌟 這裡只負責把資料打包傳出去，不做資料庫儲存
        [RelayCommand]
        private void ConfirmSave()
        {
            if (string.IsNullOrWhiteSpace(BarCode) || string.IsNullOrWhiteSpace(Definition))
                return;

            var resultModel = new ListUserModel
            {
                BarCode = BarCode.Trim(),
                Definition = Definition.Trim(),
                Department = Department?.Trim() ?? "",
                IsActive = IsActive
            };

            // 關閉對話框，將結果丟回給呼叫它的地方 (ListUser)
            DialogHost.Close("RootDialog", resultModel);
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogHost.Close("RootDialog", null);
        }
    }
}