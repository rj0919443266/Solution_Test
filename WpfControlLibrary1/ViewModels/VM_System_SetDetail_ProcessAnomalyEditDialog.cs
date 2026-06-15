using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using WpfControlLibrary1.Mode;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_System_SetDetail_ProcessAnomalyEditDialog : ObservableObject
    {
        [ObservableProperty] private string _dialogTitle = "新增 製程異常別 項目";
        [ObservableProperty] private string _barCode = string.Empty;
        [ObservableProperty] private string _definition = string.Empty;
        [ObservableProperty] private bool _isEditMode = false;

        public VM_System_SetDetail_ProcessAnomalyEditDialog(ProcessAnomalyModel? target = null)
        {
            if (target != null)
            {
                DialogTitle = "修改 製程異常別項目";
                BarCode = target.BarCode;
                Definition = target.Definition;
                IsEditMode = true;
            }
        }

        [RelayCommand]
        private void ConfirmSave()
        {
            if (string.IsNullOrWhiteSpace(BarCode) || string.IsNullOrWhiteSpace(Definition)) return;

            var resultModel = new ProcessAnomalyModel
            {
                BarCode = BarCode.Trim(),
                Definition = Definition.Trim()
            };

            DialogHost.Close("RootDialog", resultModel);
        }

        [RelayCommand]
        private void Cancel() => DialogHost.Close("RootDialog", null);
    }
}