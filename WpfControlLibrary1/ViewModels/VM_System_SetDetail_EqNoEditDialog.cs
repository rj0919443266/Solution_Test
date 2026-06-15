using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_EqNoEditDialog : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private string _dialogTitle = "新增 機台";
        [ObservableProperty] private string _barCode = string.Empty;
        [ObservableProperty] private string _definition = string.Empty;
        [ObservableProperty] private string _department = string.Empty;
        [ObservableProperty] private bool _isEditMode = false;

        // 存放從資料庫撈取的部門清單 (與人員共用同一個 API)
        [ObservableProperty] private ObservableCollection<ProcessDepartmentModel> _departmentList = new();

        public VM_System_SetDetail_EqNoEditDialog(ApiService apiService, EqNoModel? target = null)
        {
            _apiService = apiService;

            if (target != null)
            {
                DialogTitle = "修改 機台資料";
                BarCode = target.BarCode;
                Definition = target.Definition;
                Department = target.Department;
                IsEditMode = true;
            }

            // 啟動時自動撈取部門清單
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
                    foreach (var dept in result.Data) DepartmentList.Add(dept);
                });
            }
        }

        [RelayCommand]
        private void ConfirmSave()
        {
            if (string.IsNullOrWhiteSpace(BarCode) || string.IsNullOrWhiteSpace(Definition)) return;

            var resultModel = new EqNoModel
            {
                BarCode = BarCode.Trim(),
                Definition = Definition.Trim(),
                Department = Department?.Trim() ?? ""
            };

            DialogHost.Close("RootDialog", resultModel);
        }

        [RelayCommand]
        private void Cancel() => DialogHost.Close("RootDialog", null);
    }
}