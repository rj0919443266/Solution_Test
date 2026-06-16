using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WpfControlLibrary1.Mode;
using WpfControlLibrary1.Services;

namespace WpfControlLibrary1.ViewModels
{
    public partial class VM_WorkPageDataEdit : ObservableObject, IBarcodeReceiver
    {
        private readonly ApiService _apiService;
        private readonly ISnackbarService _snackbarService;
        private readonly SystemConfig _config;
        private readonly IMessenger _messenger;

        // ==========================================
        // 畫面上半部：工單與基礎資訊綁定
        // ==========================================
        [ObservableProperty] private string _lotNo = string.Empty;
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private string _parentLotNo = string.Empty;

        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private DateTime _workTime = DateTime.Now;
        [ObservableProperty] private string _remarks = string.Empty;

        // ==========================================
        // 時間編輯控制開關
        // ==========================================
        [ObservableProperty] private bool _isWorkTimeManual = false;
        [ObservableProperty] private bool _canEditWorkTimeToggle = true;

        // ==========================================
        // 條碼刷入提示 (UI 鑲邊框高亮狀態)
        // ==========================================
        [ObservableProperty] private bool _flashLotNo;
        [ObservableProperty] private bool _flashUser;
        [ObservableProperty] private bool _flashWorkStation;
        [ObservableProperty] private bool _flashEqNo;
        [ObservableProperty] private bool _flashQuantity;
        [ObservableProperty] private bool _flashRemarks;
        [ObservableProperty] private bool _flashWorkTime;

        // ==========================================
        // 下拉選單資料源
        // ==========================================
        [ObservableProperty] private ObservableCollection<ListUserModel> _userList = new();
        [ObservableProperty] private ObservableCollection<ListWorkStationModel> _workStationList = new();
        [ObservableProperty] private ObservableCollection<EqNoModel> _eqNoList = new();
        [ObservableProperty] private ObservableCollection<InstructionSetRemarkModel> _remarkInstructionList = new();

        [ObservableProperty] private ListUserModel _selectedUser;
        [ObservableProperty] private ListWorkStationModel _selectedWorkStation;
        [ObservableProperty] private EqNoModel _selectedEqNo;

        // ==========================================
        // 畫面下半部：雙表格資料源
        // ==========================================
        public partial class ActualWorkItemModel : ObservableObject
        {
            [ObservableProperty] private DateTime _workTime;
            [ObservableProperty] private string _workStation;
            [ObservableProperty] private string _eqNo;
            [ObservableProperty] private int _quantity;
            [ObservableProperty] private string _user;
            [ObservableProperty] private string _remarks;
            [ObservableProperty] private bool _delete;
            [ObservableProperty] private bool _isExtra;
        }

        public partial class SOPFlowItemModel : ObservableObject
        {
            [ObservableProperty] private int _no;
            [ObservableProperty] private bool _canPass;
            [ObservableProperty] private string _workStation;
            [ObservableProperty] private bool _isCompleted;
        }

        public ObservableCollection<ActualWorkItemModel> ActualWorkHistory { get; set; } = new();
        public ObservableCollection<SOPFlowItemModel> StandardWorkFlow { get; set; } = new();

        public VM_WorkPageDataEdit(ApiService apiService, ISnackbarService snackbarService, SystemConfig config, IMessenger messenger)
        {
            _apiService = apiService;
            _snackbarService = snackbarService;
            _config = config;
            _messenger = messenger;
            _ = LoadBaseDataAsync();

            // 當收到「系統設定已變更」的廣播時，觸發重新整理
            _messenger.Register<VM_WorkPageDataEdit,SystemConfig_Change_Message>(this, (recipient, message) =>
            {
                // 確保在 UI 執行緒上執行更新
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 1. 備份當前畫面上的選擇 (防呆，避免整理後使用者剛剛選的東西不見)
                    string backupUserCode = recipient.SelectedUser?.BarCode;
                    string backupWsCode = recipient.SelectedWorkStation?.BarCode;
                    string backupEqCode = recipient.SelectedEqNo?.BarCode;

                    // 2. 重新向 API 獲取資料，此時 LoadBaseDataAsync 內部的 SortByPriority 會吃到最新的 config
                    await recipient.LoadBaseDataAsync();

                    // 3. 將剛剛備份的選擇還原回去
                    if (backupUserCode != null)
                        recipient.SelectedUser = recipient.UserList.FirstOrDefault(x => x.BarCode == backupUserCode);

                    if (backupWsCode != null)
                        recipient.SelectedWorkStation = recipient.WorkStationList.FirstOrDefault(x => x.BarCode == backupWsCode);

                    if (backupEqCode != null)
                        recipient.SelectedEqNo = recipient.EqNoList.FirstOrDefault(x => x.BarCode == backupEqCode);
                });
            });
        }

        public void Dispose()
        {
            // ViewModel 被釋放時呼叫
            _messenger.UnregisterAll(this);
        }

        /// <summary>
        /// 依照本機 SystemConfig 設定的優先關鍵字，將符合的項目置頂
        /// </summary>
        private ObservableCollection<T> SortByPriority<T>(IEnumerable<T> source, Func<T, string> textSelector)
        {
            if (source == null) return new ObservableCollection<T>();

            // 若設定檔中未開啟此功能，直接回傳原陣列順序，不執行置頂優化！
            if (!_config.IsPrioritySortingEnabled)
                return new ObservableCollection<T>(source);

            // 若設定檔沒有設定優先關鍵字，直接回傳原陣列
            if (_config.DepartmentPriorityKeywords == null || !_config.DepartmentPriorityKeywords.Any())
                return new ObservableCollection<T>(source);

            var sortedList = source.OrderByDescending(item =>
            {
                string text = textSelector(item) ?? string.Empty;
                return _config.DepartmentPriorityKeywords.Any(keyword =>
                    text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            })
            .ThenBy(item => textSelector(item))
            .ToList();

            return new ObservableCollection<T>(sortedList);
        }

        /// <summary>
        /// 輔助方法：將更新 UI 的動作非同步丟回主執行緒執行，保護執行緒安全
        /// </summary>
        private void UpdateUI(Action action)
        {
            Application.Current.Dispatcher.InvokeAsync(action);
        }

        private async Task LoadBaseDataAsync()
        {
            // 🌟 取得資料後，呼叫 SortByPriority，並指定要拿來比對的文字 (條碼 + 定義名稱)

            var userRes = await _apiService.PostAsync<ObservableCollection<ListUserModel>>("get_users", new { });
            if (userRes.IsSuccess)
                UserList = SortByPriority(userRes.Data, x => $"{x.BarCode} {x.Definition}");

            var wsRes = await _apiService.PostAsync<ObservableCollection<ListWorkStationModel>>("get_work_stations", new { });
            if (wsRes.IsSuccess)
                WorkStationList = SortByPriority(wsRes.Data, x => $"{x.BarCode} {x.Definition}");

            var eqRes = await _apiService.PostAsync<ObservableCollection<EqNoModel>>("get_eq_nos", new { });
            if (eqRes.IsSuccess)
                EqNoList = SortByPriority(eqRes.Data, x => $"{x.BarCode} {x.Definition}");

            // 備註選項通常不用依照部門排序，維持原樣即可
            var remarkRes = await _apiService.PostAsync<ObservableCollection<InstructionSetRemarkModel>>("get_InstructionSetRemarks", new { });
            if (remarkRes.IsSuccess)
                RemarkInstructionList = remarkRes.Data;
        }

        private async void TriggerHighlight(Action<bool> setter)
        {
            setter(true);
            await Task.Delay(2500);
            setter(false);
        }

        // 💡 優化：全背景處理條碼邏輯，不卡畫面
        public void ReceiveBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            Task.Run(() =>
            {
                if (barcode.StartsWith("TIME+"))
                {
                    if (int.TryParse(barcode.Substring(5), out int addMin)) { UpdateUI(() => { WorkTime = WorkTime.AddMinutes(addMin); TriggerHighlight(v => FlashWorkTime = v); }); return; }
                }
                else if (barcode.StartsWith("TIME-"))
                {
                    if (int.TryParse(barcode.Substring(5), out int subMin)) { UpdateUI(() => { WorkTime = WorkTime.AddMinutes(-subMin); TriggerHighlight(v => FlashWorkTime = v); }); return; }
                }
                else if (barcode.StartsWith("+") || barcode.StartsWith("-"))
                {
                    if (int.TryParse(barcode, out int numChange))
                    {
                        UpdateUI(() => { Quantity = Math.Max(0, Quantity + numChange); TriggerHighlight(v => FlashQuantity = v); });
                        return;
                    }
                }

                var matchUser = UserList.FirstOrDefault(x => x.BarCode == barcode);
                if (matchUser != null) { UpdateUI(() => { SelectedUser = matchUser; TriggerHighlight(v => FlashUser = v); }); return; }

                var matchStation = WorkStationList.FirstOrDefault(x => x.BarCode == barcode);
                if (matchStation != null) { UpdateUI(() => { SelectedWorkStation = matchStation; TriggerHighlight(v => FlashWorkStation = v); }); return; }

                var matchEq = EqNoList.FirstOrDefault(x => x.BarCode == barcode);
                if (matchEq != null) { UpdateUI(() => { SelectedEqNo = matchEq; TriggerHighlight(v => FlashEqNo = v); }); return; }

                var matchRemark = RemarkInstructionList.FirstOrDefault(x => x.BarCode == barcode);
                if (matchRemark != null)
                {
                    UpdateUI(() =>
                    {
                        Remarks = string.IsNullOrEmpty(Remarks) ? matchRemark.Definition : $"{Remarks}, {matchRemark.Definition}";
                        TriggerHighlight(v => FlashRemarks = v);
                    });
                    return;
                }

                // 皆不符合則視為工單號
                UpdateUI(() =>
                {
                    LotNo = barcode;
                    TriggerHighlight(v => FlashLotNo = v);
                    _ = SearchLotNoAsync();
                });
            });
        }

        [RelayCommand]
        private async Task SearchLotNoAsync()
        {
            if (string.IsNullOrWhiteSpace(LotNo)) return;
            await FetchWorkPageDataAsync(LotNo);
        }

        private async Task FetchWorkPageDataAsync(string lotNo)
        {
            var result = await _apiService.PostAsync<WipDetailRawModel>("work_page_data_detail", new { lot_no = lotNo });

            if (result.IsSuccess && result.Data != null)
            {
                // 💡 優化：在背景建立暫存清單，避免在 UI 執行緒做大量 JSON 解析導致卡頓
                var tempSOPFlow = new List<SOPFlowItemModel>();
                var tempHistory = new List<ActualWorkItemModel>();
                ActualWorkItemModel lastValidWork = null;
                int currentMaxNo = 0;

                try
                {
                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; // 💡 容錯機制
                    var standardStations = new HashSet<string>();
                    var executedStations = new HashSet<string>();

                    if (!string.IsNullOrWhiteSpace(result.Data.list_Work_station))
                    {
                        var flow = JsonSerializer.Deserialize<WorkStationDef[]>(result.Data.list_Work_station, jsonOptions);
                        if (flow != null)
                        {
                            foreach (var item in flow)
                            {
                                tempSOPFlow.Add(new SOPFlowItemModel
                                {
                                    No = item.NO,
                                    CanPass = item.Can_Pass,
                                    WorkStation = item.work_station,
                                    IsCompleted = false
                                });
                                standardStations.Add(item.work_station);
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(result.Data.list_work_Data))
                    {
                        var history = JsonSerializer.Deserialize<WorkDataDef[]>(result.Data.list_work_Data, jsonOptions);
                        if (history != null)
                        {
                            foreach (var h in history)
                            {
                                var actualItem = new ActualWorkItemModel
                                {
                                    WorkTime = h.Work_time,
                                    WorkStation = h.work_station,
                                    EqNo = h.EQ_no,
                                    Quantity = h.Quantity,
                                    User = h.User,
                                    Remarks = h.Remarks,
                                    Delete = h.delete,
                                    IsExtra = !standardStations.Contains(h.work_station)
                                };
                                tempHistory.Add(actualItem);

                                if (!h.delete)
                                {
                                    executedStations.Add(h.work_station);
                                    lastValidWork = actualItem;
                                }
                            }
                        }
                    }

                    // 計算 SOP 完成狀態
                    foreach (var item in tempSOPFlow)
                    {
                        item.IsCompleted = executedStations.Contains(item.WorkStation);
                        if (item.IsCompleted && item.No > currentMaxNo)
                        {
                            currentMaxNo = item.No;
                        }
                    }
                }
                catch (Exception ex)
                {
                    await MaterialMessageBox.ShowAsync($"解析 JSON 發生錯誤: {ex.Message}", "錯誤");
                    return;
                }

                // 💡 優化：資料算完後，一次性切回 UI 執行緒更新畫面
                UpdateUI(() =>
                {
                    ParentLotNo = result.Data.Lot_no_Parenet ?? "";
                    ProductName = result.Data.Product_name ?? "";

                    WorkTime = DateTime.Now;
                    IsWorkTimeManual = false;
                    CanEditWorkTimeToggle = false;
                    TriggerHighlight(v => FlashWorkTime = v);

                    ActualWorkHistory.Clear();
                    foreach (var h in tempHistory) ActualWorkHistory.Add(h);

                    StandardWorkFlow.Clear();
                    foreach (var s in tempSOPFlow) StandardWorkFlow.Add(s);

                    // 自動帶入選單資料 (機台與人員)
                    if (lastValidWork != null)
                    {
                        SelectedUser = UserList.FirstOrDefault(u => u.Definition == lastValidWork.User || u.BarCode == lastValidWork.User);
                        SelectedEqNo = EqNoList.FirstOrDefault(e => e.Definition == lastValidWork.EqNo || e.BarCode == lastValidWork.EqNo);
                    }

                    // 自動帶入下一站
                    if (StandardWorkFlow.Any())
                    {
                        var nextStation = StandardWorkFlow.Where(s => s.No > currentMaxNo && !s.CanPass).OrderBy(s => s.No).FirstOrDefault()
                                          ?? StandardWorkFlow.OrderByDescending(s => s.No).FirstOrDefault();

                        if (nextStation != null)
                        {
                            SelectedWorkStation = WorkStationList.FirstOrDefault(w => w.Definition == nextStation.WorkStation || w.BarCode == nextStation.WorkStation);
                        }
                    }

                    WeakReferenceMessenger.Default.Send(new ScrollToLastRowMessage());
                });
            }
            else
            {
                await MaterialMessageBox.ShowAsync($"找不到工單 ({lotNo}) 或是網路異常。\n錯誤: {result.ErrorMessage}", "查詢失敗");
                UpdateUI(() =>
                {
                    ParentLotNo = string.Empty;
                    ProductName = string.Empty;
                    ActualWorkHistory.Clear();
                    StandardWorkFlow.Clear();
                });
            }
        }

        [RelayCommand]
        private void MarkAsDeleted(ActualWorkItemModel selectedItem)
        {
            if (selectedItem != null)
            {
                selectedItem.Delete = !selectedItem.Delete;
            }
        }

        [RelayCommand]
        private void ClearData()
        {
            LotNo = string.Empty;
            ProductName = string.Empty;
            ParentLotNo = string.Empty;
            SelectedUser = null;
            SelectedWorkStation = null;
            SelectedEqNo = null;
            Quantity = 1;
            Remarks = string.Empty;
            WorkTime = DateTime.Now;

            IsWorkTimeManual = false;
            CanEditWorkTimeToggle = true;

            ActualWorkHistory.Clear();
            StandardWorkFlow.Clear();
        }

        [RelayCommand]
        private async Task SaveDataAsync()
        {
            if (string.IsNullOrWhiteSpace(LotNo))
            {
                await MaterialMessageBox.ShowAsync("請先刷入或輸入工單號碼！", "錯誤");
                return;
            }

            // 判斷是否有填寫「新的過站紀錄」
            if (SelectedWorkStation != null || SelectedUser != null || SelectedEqNo != null)
            {
                // 💡 防呆：更明確的邏輯，避免使用者漏選
                if (SelectedWorkStation == null || SelectedUser == null || SelectedEqNo == null)
                {
                    await MaterialMessageBox.ShowAsync("欲新增紀錄，請確認【生產者】、【機台】與【工程站別】皆已完整選取！", "提示");
                    return;
                }

                bool isExtra = !StandardWorkFlow.Any(s => s.WorkStation == SelectedWorkStation.Definition);

                // 寫入到暫存清單 (UI 綁定)
                ActualWorkHistory.Add(new ActualWorkItemModel
                {
                    WorkTime = WorkTime,
                    WorkStation = SelectedWorkStation.Definition,
                    EqNo = SelectedEqNo.Definition,
                    Quantity = Quantity,
                    User = SelectedUser.Definition,
                    Remarks = Remarks ?? "",
                    Delete = false,
                    IsExtra = isExtra
                });
            }

            // 將整個 ActualWorkHistory 轉為 API 需要的 JSON 格式
            var workDataList = ActualWorkHistory.Select(h => new
            {
                Work_time = h.WorkTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                work_station = h.WorkStation,
                EQ_no = h.EqNo,
                Quantity = h.Quantity,
                User = h.User,
                Remarks = h.Remarks ?? "",
                delete = h.Delete
            }).ToList();

            string jsonWorkData = JsonSerializer.Serialize(workDataList);

            var payload = new
            {
                lot_no = LotNo,
                list_work_Data = jsonWorkData
            };

            var result = await _apiService.PostAsync<object>("save_work_data", payload);

            if (result.IsSuccess)
            {
                _snackbarService.ShowSnackbar($"資料寫入成功！", SnackbarMessageType.Success);

                await FetchWorkPageDataAsync(LotNo);

                // 清空輸入區塊 (保留機台與人員方便連續刷)
                SelectedWorkStation = null;
                Quantity = 1;
                Remarks = string.Empty;
                WorkTime = DateTime.Now;

                IsWorkTimeManual = false;
                CanEditWorkTimeToggle = true;
            }
            else
            {
                await MaterialMessageBox.ShowAsync($"寫入失敗: {result.ErrorMessage}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                _snackbarService.ShowSnackbar($"寫入失敗", SnackbarMessageType.Error);
            }
        }
    }
}