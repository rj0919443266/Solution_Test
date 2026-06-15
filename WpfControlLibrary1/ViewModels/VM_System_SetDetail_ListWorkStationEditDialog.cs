using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_ListWorkStationEditDialog : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private string _dialogTitle = "新增 工作站資料";
        [ObservableProperty] private string _barCode = string.Empty;
        [ObservableProperty] private string _definition = string.Empty;
        [ObservableProperty] private string _definition2 = string.Empty;
        [ObservableProperty] private string _department = "AG";
        [ObservableProperty] private bool _isEditMode = false;

        public VM_System_SetDetail_ListWorkStationEditDialog(ApiService apiService, ListWorkStationModel? targetStation = null)
        {
            _apiService = apiService;

            if (targetStation != null)
            {
                DialogTitle = "修改 工作站資料";
                BarCode = targetStation.BarCode;
                Definition = targetStation.Definition;
                Definition2 = targetStation.Definition2;
                Department = targetStation.Department;
                IsEditMode = true;
            }
        }

        [RelayCommand]
        private void ConfirmSave()
        {
            if (string.IsNullOrWhiteSpace(BarCode) || string.IsNullOrWhiteSpace(Definition))
                return;

            var resultModel = new ListWorkStationModel
            {
                BarCode = BarCode.Trim(),
                Definition = Definition.Trim(),
                Definition2 = Definition2?.Trim() ?? "",
                Department = Department?.Trim() ?? "AG"
            };

            DialogHost.Close("RootDialog", resultModel);
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogHost.Close("RootDialog", null);
        }
    }
}