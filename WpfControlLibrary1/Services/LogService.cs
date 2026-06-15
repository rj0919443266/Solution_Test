using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfControlLibrary1.Services
{
    // 定義 Log 層級
    public enum LogLevel
    {
        Information,
        Success,
        Warning,
        Error
    }

    public interface ILogService
    {
        void Log(string message, LogLevel level = LogLevel.Information);
        void LogError(string message, Exception ex = null);
    }

    public class LogService : ILogService
    {
        private readonly string _logDirectory;
        private static readonly object _lock = new object();

        // 🌟 新增：記錄上次執行清理的日期，避免每次寫 Log 都去掃描資料夾耗費效能
        private DateTime _lastCleanupDate = DateTime.MinValue;

        public LogService()
        {
            // 自動取得執行檔所在目錄，並在其下建立 "log" 資料夾
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 🌟 程式剛啟動時，先在背景清理一次舊檔案
            CleanUpOldLogs();
        }

        public void Log(string message, LogLevel level = LogLevel.Information)
        {
            WriteToFile(level.ToString(), message);
        }

        public void LogError(string message, Exception ex = null)
        {
            string fullMessage = ex == null
                ? message
                : $"{message}\r\nException: {ex.Message}\r\nStackTrace: {ex.StackTrace}";

            WriteToFile("Error", fullMessage);
        }

        private void WriteToFile(string level, string message)
        {
            // 丟到背景執行緒去寫入檔案，絕對不阻塞 UI
            Task.Run(() =>
            {
                // 每天自動換一個檔案 (例如：2023-11-20.log)
                string fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
                string filePath = Path.Combine(_logDirectory, fileName);

                // 格式：[時間] [層級] 訊息
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

                // 多執行緒鎖定 (lock)，確保多個硬體 (如條碼槍) 同時觸發事件時，寫入檔案不會發生衝突
                lock (_lock)
                {
                    try
                    {
                        File.AppendAllText(filePath, logLine);
                    }
                    catch
                    {
                        // 忽略檔案被其他外部程式咬死的情況，防止導致系統崩潰
                    }
                }

                // 🌟 自動清理機制：如果今天還沒檢查過，就觸發檢查
                if (DateTime.Now.Date > _lastCleanupDate)
                {
                    CleanUpOldLogs();
                }
            });
        }

        /// <summary>
        /// 🌟 清理超過 90 天的舊 Log 檔案
        /// </summary>
        private void CleanUpOldLogs()
        {
            // 先更新檢查日期，防止重複觸發
            _lastCleanupDate = DateTime.Now.Date;

            // 丟入背景執行，絕不拖慢主程式
            Task.Run(() =>
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(_logDirectory);

                    // 只抓取 .log 結尾的檔案
                    var files = directoryInfo.GetFiles("*.log");

                    // 計算出 90 天前的日期死線
                    var retentionDate = DateTime.Now.AddDays(-90);

                    foreach (var file in files)
                    {
                        // 根據檔案的「最後修改時間」來判斷，若早於 90 天前就刪除
                        if (file.LastWriteTime < retentionDate)
                        {
                            file.Delete();
                        }
                    }
                }
                catch
                {
                    // 忽略刪除失敗的狀況（例如某個舊檔案剛好被防毒軟體或使用者打開鎖定了）
                }
            });
        }
    }
}