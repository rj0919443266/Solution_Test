using CommunityToolkit.Mvvm.Messaging;
using Notifications.Wpf;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WpfControlLibrary1.Services
{
    /// <summary>
    /// 負責處理條碼槍通訊的服務
    /// </summary>
    public class BarcodeScannerService
    {
        private SerialPort? _serialPort;
        private readonly IMessenger _messenger;
        private readonly ISnackbarService _snackbarService;
        private readonly INotificationService _notificationService;

        //斷線偵測雷達 (Timer)
        private System.Timers.Timer? _monitorTimer;

        // 記錄當前使用的 Port 號碼
        public string CurrentPort { get; private set; } = "未設定";

        // 提供外部查詢當前是否處於連線狀態
        public bool IsConnected => _serialPort?.IsOpen ?? false;


        // 建構子：透過 DI 注入 IMessenger
        public BarcodeScannerService(IMessenger messenger, ISnackbarService snackbarService, INotificationService notificationService)
        {
            _messenger = messenger;
            _snackbarService = snackbarService;
            _notificationService = notificationService;
        }

        // 啟動連線
        public bool Start(string portName)
        {
            // 1. 先安全關閉舊的連線
            Stop();

            CurrentPort = portName;

            // 2. 重新配置新的 SerialPort
            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                NewLine = "\r"
            };

            _serialPort.DataReceived += SerialPort_DataReceived;

            try
            {
                _serialPort.Open();

                //連線成功後，啟動斷線偵測雷達 (每 2000 毫秒掃描一次)
                _monitorTimer = new System.Timers.Timer(2000);
                _monitorTimer.Elapsed += MonitorTimer_Elapsed;
                _monitorTimer.Start();

                return true; // 連線成功
            }
            catch (Exception)
            {
                return false; // 連線失敗（例如被佔用、找不到裝置）
            }
        }

        // 關閉連線 (在程式關閉時呼叫，釋放硬體資源)
        // 檔案：BarcodeScannerService.cs
        public void Stop()
        {
            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
                _monitorTimer = null;
            }
            if (_serialPort != null)
            {
                // 立刻安全解綁事件，防止在關閉過程中執行緒繼續打架
                _serialPort.DataReceived -= SerialPort_DataReceived;

                var portToClose = _serialPort;
                _serialPort = null; // 斷開引用

                // 將 Close 丟至背景執行緒，不卡死 UI 執行緒，徹底粉碎死鎖條件
                Task.Run(() =>
                {
                    try
                    {
                        if (portToClose.IsOpen)
                        {
                            portToClose.DiscardInBuffer();
                            portToClose.DiscardOutBuffer();
                            portToClose.Close();
                        }
                        portToClose.Dispose();
                    }
                    catch (Exception)
                    {
                        // 吸收硬體中斷時的驅動層異常
                    }
                });
            }
        }

        private void MonitorTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_serialPort == null) return;

            bool isHardwareDisconnected = false;

            // 死法 1：驅動程式很盡責，直接把 IsOpen 變成 false 了
            if (!_serialPort.IsOpen)
            {
                isHardwareDisconnected = true;
            }
            else
            {
                // 死法 2：幽靈連線 (IsOpen 還是 true，但硬體已經不在了)
                try
                {
                    // 故意去觸碰底層的緩衝區屬性，拔除時會觸發 IOException
                    int testPing = _serialPort.BytesToRead;
                }
                catch (Exception)
                {
                    isHardwareDisconnected = true; // 抓到幽靈！
                }
            }

            // 只要符合任何一種死法，立刻啟動斷線處置
            if (isHardwareDisconnected)
            {
                Stop(); // 確實釋放資源，讓 Windows 清除登錄檔

                // 向全系統發送緊急斷線廣播
                _messenger.Send(new DeviceDisconnectedMessage(CurrentPort));
            }
        }

        // 當硬體傳來資料時觸發
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null) return;
            try
            {
                // 讀取直到遇到結尾符號 (\r)
                string barcode = _serialPort.ReadLine().Trim();

                if (!string.IsNullOrEmpty(barcode))
                {
                    //透過廣播中心，把掃到的條碼發送給全系統！
                    _messenger.Send(new BarcodeScannedMessage(barcode));
                    _snackbarService.ShowSnackbar($"已讀取條碼: {barcode}", SnackbarMessageType.Information);
                    //_notificationService.Show("掃描槍資訊", $"已讀取條碼: {barcode}", NotificationType.Information);

                }
            }
            catch (Exception ex)
            {
                // 處理讀取錯誤（例如中途拔線）
            }
        }
    }
    //定義介面：負責接收條碼的合約
    public interface IBarcodeReceiver
    {
        void ReceiveBarcode(string barcode);
    }

    // 用於 條碼機  Messenger 的通訊 Class
    public class BarcodeScannedMessage
    {
        public string Barcode { get; }
        public BarcodeScannedMessage(string barcode) => Barcode = barcode;
    }

    // 當ComPort硬體實體斷線時發送的訊息
    public class DeviceDisconnectedMessage
    {
        public string PortName { get; }

        public DeviceDisconnectedMessage(string portName)
        {
            PortName = portName;
        }
    }


    /// <summary>
    /// 掃描槍連線狀態列舉
    /// </summary>
    public enum ScannerConnectionState
    {
        NotSet,       // 未設定 / 初始狀態
        Connected,    // 已成功連線
        Failed,       // 連線失敗
        Disconnected  // 異常斷線
    }

}
