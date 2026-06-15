using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_ListWorkStationTwinsEditDialog : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty] private string _dialogTitle = "新增 關聯工作站";
        [ObservableProperty] private string _workStationName = string.Empty;
        [ObservableProperty] private string _workStationStart = string.Empty;
        [ObservableProperty] private string _workStationEnd = string.Empty;
        [ObservableProperty] private bool _isEditMode = false;

        public VM_System_SetDetail_ListWorkStationTwinsEditDialog(ApiService apiService, ListWorkStationTwinsModel? targetTwin = null)
        {
            _apiService = apiService;

            if (targetTwin != null)
            {
                DialogTitle = "修改 關聯工作站";
                WorkStationName = targetTwin.WorkStationName;
                WorkStationStart = targetTwin.WorkStationStart;
                WorkStationEnd = targetTwin.WorkStationEnd;
                IsEditMode = true;
            }
        }

        [RelayCommand]
        private void ConfirmSave()
        {
            if (string.IsNullOrWhiteSpace(WorkStationName)) return;

            var resultModel = new ListWorkStationTwinsModel
            {
                WorkStationName = WorkStationName.Trim(),
                WorkStationStart = WorkStationStart?.Trim() ?? "",
                WorkStationEnd = WorkStationEnd?.Trim() ?? ""
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