using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfControlLibrary1.Mode
{
    internal class C_API
    {
    }
    #region "通用"
    // 專門用來教 C# 如何看懂 PHP 時間格式的轉換器
    public class PhpDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dateString = reader.GetString();
            if (string.IsNullOrWhiteSpace(dateString)) return null;

            // 用 C# 最寬容的 TryParse 來解析字串
            if (DateTime.TryParse(dateString, out DateTime parsedDate))
                return parsedDate;

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            else
                writer.WriteNullValue();
        }
    }

    // PHP JSend 格式對應
    public class JsonResponse<T>
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public T Data { get; set; }
    }

    // 封裝給 ViewModel 使用的回傳結果
    public class ApiResult<T>
    {
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }
    #endregion
    //=============================================
    #region "登入資訊"
    public class LoginResponseModel
    {
        [JsonPropertyName("EmployeeID")]
        public string User_ID { get; set; }

        [JsonPropertyName("UserName")]
        public string UserName { get; set; }

        // 接收伺服器傳來的 Level 權限等級
        [JsonPropertyName("Level")]
        public int Level { get; set; }
    }
    #endregion
    //=============================================
    #region "過站狀態"
    //====================================================================
    /// <summary>
    /// 過站狀態 (WIP Status)
    /// </summary>
    public class WipStatusModel
    {
        public string Lot_no { get; set; }
        public string Product_name { get; set; }

        public string current_station { get; set; }
        public int current_station_no { get; set; }

        public string next_station { get; set; }
        public int next_station_no { get; set; }
        public string last_update_time { get; set; }
    }


    #endregion
    //=============================================WorkPageData 相關資訊
    #region "WorkData 相關資訊"

    // API 回傳的最外層結構
    public class WipDetailRawModel
    {
        [JsonConverter(typeof(PhpDateTimeConverter))]
        public DateTime? c_time { get; set; }
        public string Product_name { get; set; }
        public string list_Work_station { get; set; }
        public string list_work_Data { get; set; }
        public string Lot_no_Parenet { get; set; }
        public string create_user { get; set; }

    }
    //
    //JSON 解析用：標準工序(list_Work_station)
    public class WorkStationDef
    {
        public int NO { get; set; }
        public bool Can_Pass { get; set; }
        public string work_station { get; set; }
    }

    // JSON 解析用：實際歷程 (list_work_Data)
    public class WorkDataDef
    {
        public DateTime Work_time { get; set; }
        public string work_station { get; set; }
        public string EQ_no { get; set; }
        public int Quantity { get; set; }
        public string User { get; set; }
        public string Remarks { get; set; }
        public bool delete { get; set; }
    }


    #endregion
    //=============================================FIR 資訊
    #region "FIR 資訊"
    // 對應資料庫的一筆 FIR 紀錄
    public class FirRecordModel
    {
        [JsonPropertyName("c_time")]
        public string CTime { get; set; }

        [JsonPropertyName("Lot_no")]
        public string LotNo { get; set; }

        [JsonPropertyName("User")]
        public string User { get; set; }

        [JsonPropertyName("Remarks")]
        public string Remarks { get; set; }

        //  JSON 字串
        [JsonPropertyName("Data")]
        public string DataJsonString { get; set; }

        [JsonPropertyName("Set_Data")]
        public string SetDataJsonString { get; set; }

        public int AlarmCount { get; set; }
        public int WarningCount { get; set; }

        public int NormalCount { get; set; }
        public int UnknownCount { get; set; }
    }

    // 對應 Data 欄位解析出來的每一筆明細數值
    public class FirRawDataModel
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Value")]
        public string Value { get; set; }
    }

    //  用來解析 Set_Data 陣列內部的物件結構
    public class FirSetDataModel
    {
        [JsonPropertyName("Name")] public string Name { get; set; }
        [JsonPropertyName("Limits_upper")] public double? LimitsUpper { get; set; }
        [JsonPropertyName("Limits_upper_warning")] public double? LimitsUpperWarning { get; set; }
        [JsonPropertyName("Limits_mid")] public double? LimitsMid { get; set; }
        [JsonPropertyName("Limits_lower_warning")] public double? LimitsLowerWarning { get; set; }
        [JsonPropertyName("Limits_lower")] public double? LimitsLower { get; set; }
    }

    //  最終要綁定到 DataGrid 的「合併後明細模型」
    public class FirDisplayDetailModel
    {
        public string Name { get; set; } // 項目名稱
        public string Target { get; set; } // Limits_mid
        public string ActualData { get; set; } // 實際測量值
        public string LowerLimit { get; set; }
        public string LowerWarning { get; set; }
        public string UpperWarning { get; set; }
        public string UpperLimit { get; set; }

        public string Note { get; set; } // 顯示文字 (如 Upper Alarm)
        public string StatusLevel { get; set; } // 用於 XAML 顏色判斷 (Normal, Warning, Alarm)
    }


    /// <summary>
    /// 提供給 UserControl_Chart 綁定用的單一圖表容器
    /// </summary>
    public class ChartContainerModel
    {
        public string ChartTitle { get; set; }
        public ISeries[] Series { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }
    }
    #endregion

    #region "品質異常單"
    public class QualityNoticeModel
    {
        [JsonPropertyName("c_time")] public string CTime { get; set; }
        [JsonPropertyName("process_department")] public string ProcessDepartment { get; set; }
        [JsonPropertyName("OP")] public string Op { get; set; }
        [JsonPropertyName("QC")] public string Qc { get; set; }
        [JsonPropertyName("process_anomaly")] public string ProcessAnomaly { get; set; }
        [JsonPropertyName("problem_classification")] public string ProblemClassification { get; set; }
        [JsonPropertyName("problem_classification_txt")] public string ProblemClassificationTxt { get; set; }
        [JsonPropertyName("Root_Cause_Analysis_txt")] public string RootCauseAnalysisTxt { get; set; }
        [JsonPropertyName("quality_abnormal_action")] public string QualityAbnormalAction { get; set; }
        [JsonPropertyName("quality_abnormal_action_txt")] public string QualityAbnormalActionTxt { get; set; }

        [JsonPropertyName("list_Work_station_old")] public string ListWorkStationOld { get; set; }
        [JsonPropertyName("list_Work_station_new")] public string ListWorkStationNew { get; set; }
    }

    // 用於解析 JSON 陣列內部的站點格式
    public class StationNodeModel
    {
        [JsonPropertyName("NO")] public int No { get; set; }
        [JsonPropertyName("Can_Pass")] public bool CanPass { get; set; }
        [JsonPropertyName("work_station")] public string WorkStation { get; set; }
    }

    //用於左右獨立表格綁定的 Model
    public class StationDisplayModel
    {
        public int No { get; set; }
        public bool CanPass { get; set; }
        public string WorkStation { get; set; }
        public string DiffStatus { get; set; } // 狀態：Unchanged, Added, Removed
    }
    #endregion

    #region "IPI"
    // 對應 ipi_data 資料表結構 (主表)
    public class IpiRecordModel
    {
        [JsonPropertyName("c_time")]
        public string CTime { get; set; }

        [JsonPropertyName("Lot_no")]
        public string LotNo { get; set; }

        [JsonPropertyName("Data")]
        public string DataJsonString { get; set; }

        [JsonPropertyName("Remarks")]
        public string Remarks { get; set; }

        // 加上允許字串轉數字的屬性，防呆 PHP 傳回 "0" 或 "1"
        [JsonPropertyName("judge_measurement")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int JudgeMeasurement { get; set; }

        [JsonPropertyName("judge_missing_data")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int JudgeMissingData { get; set; }

        [JsonPropertyName("JudegEndWork")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int JudgeEndWork { get; set; }

        // 允許字串 "0.0400" 轉為 double
        [JsonPropertyName("max_roundness")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double MaxRoundness { get; set; }
    }

    // 針對 ipi_data 裡的 Data JSON 結構撰寫的強型別
    public class IpiMeasurementItemModel
    {
        public string StationName { get; set; }
        public string TargetName { get; set; }
        public double? NominalValue { get; set; }
        public double? UpperLimit { get; set; }
        public double? LowerLimit { get; set; }
        public double? ActualMeasurement { get; set; }
        public double? ActualRoundness { get; set; }
        public string OperatorBarcode { get; set; }
        public string OperatorName { get; set; }
        public DateTime? MeasureTime { get; set; }
        public bool CheckRoundness { get; set; }

        // ================= UI 專用擴充屬性 (不參與 JSON 反序列化) =================
        [JsonIgnore] public string StatusLevel { get; set; } // 用於觸發顏色 (Normal, Alarm, Unknown)
        [JsonIgnore] public string Note { get; set; }        // 介面顯示的文字說明
    }

    #endregion

    #region "List User"
    public partial class ListUserModel : ObservableObject
    {
        // 加上 property: 讓標籤正確掛載到生成的公開屬性 BarCode 上
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        [property: JsonPropertyName("department")]
        [ObservableProperty]
        private string _department = string.Empty;

        [property: JsonPropertyName("IsActive")]
        [ObservableProperty]
        private bool _isActive = true;

        /// <summary>
        /// 深度複製方法，防止編輯時尚未儲存就同步更動到 DataGrid 畫面
        /// </summary>
        public ListUserModel Clone()
        {
            return new ListUserModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition,
                Department = this.Department,
                IsActive = this.IsActive
            };
        }
    
    }
    #endregion

    #region"部門"
    public partial class ProcessDepartmentModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public ProcessDepartmentModel Clone()
        {
            return new ProcessDepartmentModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }

    #endregion

    #region "設備"
    public partial class EqNoModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        [property: JsonPropertyName("department")]
        [ObservableProperty]
        private string _department = string.Empty;

        public EqNoModel Clone()
        {
            return new EqNoModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition,
                Department = this.Department
            };
        }
    }
    #endregion

    #region "製程異常別"
    public partial class ProcessAnomalyModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public ProcessAnomalyModel Clone()
        {
            return new ProcessAnomalyModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }
    #endregion

    #region "異常處理動作"
    public partial class QualityAbnormalActionModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public QualityAbnormalActionModel Clone()
        {
            return new QualityAbnormalActionModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }
    #endregion 

    #region "異常問題分類"
    public partial class ProblemClassificationModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public ProblemClassificationModel Clone()
        {
            return new ProblemClassificationModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }
    #endregion 

    #region"數量"
    public partial class InstructionSetNumModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public InstructionSetNumModel Clone()
        {
            return new InstructionSetNumModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }

    #endregion

    #region"時間"
    public partial class InstructionSetTimeModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public InstructionSetTimeModel Clone()
        {
            return new InstructionSetTimeModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }

    #endregion

    #region"備註"
    public partial class InstructionSetRemarkModel : ObservableObject
    {
        [property: JsonPropertyName("BarCode")]
        [ObservableProperty]
        private string _barCode = string.Empty;

        [property: JsonPropertyName("definition")]
        [ObservableProperty]
        private string _definition = string.Empty;

        public InstructionSetRemarkModel Clone()
        {
            return new InstructionSetRemarkModel
            {
                BarCode = this.BarCode,
                Definition = this.Definition
            };
        }
    }

    #endregion

    #region" 對應 list_work_station 資料表"
    public class ListWorkStationModel
    {
        public string BarCode { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string Definition2 { get; set; } = string.Empty;
        public string Department { get; set; } = "AG";

        public ListWorkStationModel Clone()
        {
            return (ListWorkStationModel)this.MemberwiseClone();
        }
    }
    #endregion
    #region" 對應 list_work_station_twins 資料表"
    public class ListWorkStationTwinsModel
    {
        public string WorkStationName { get; set; } = string.Empty;
        public string WorkStationStart { get; set; } = string.Empty;
        public string WorkStationEnd { get; set; } = string.Empty;

        public ListWorkStationTwinsModel Clone()
        {
            return (ListWorkStationTwinsModel)this.MemberwiseClone();
        }
    }
    #endregion

}
