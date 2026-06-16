using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Notifications.Wpf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfControlLibrary1
{
    public class C_Base
    {

    }

    //=============================================設定檔
    public class SystemConfig
    {
        // PHP 伺服器的位址 (預設值)
        public string PhpServerUrl { get; set; } = "http://192.168.1.100/api/";

        // 預設的條碼槍 COM Port (預設值)
        public string BarcodeComPort { get; set; } = "COM3";

      
        // 本機端專屬的優先排序關鍵字 (依照該機台所在部門/站別設定)
        // 如果選單項目的名稱包含這些字，就會自動被拉到下拉選單的最上面
        public List<string> DepartmentPriorityKeywords { get; set; } = new List<string> ();

        public bool IsPrioritySortingEnabled { get; set; } = false;
    }

    public class SystemConfig_Change_Message
    {
        // 這裡不需要放屬性，我們只需要它作為一個「觸發信號」
    }

    // 用來通知 UI 捲動到最後一列的訊息類別
    public class ScrollToLastRowMessage { }

}
