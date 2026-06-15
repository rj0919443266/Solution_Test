using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfControlLibrary1.Services
{
    #region "Snackbar"

    public enum SnackbarMessageType
    {
        Information,
        Success,
        Warning,
        Error    // 異常訊息
    }

    // 用來把文字和狀態打包在一起，丟進 Snackbar 排隊的類別
    public class CustomSnackbarModel
    {
        public string Text { get; set; }

        // 從 SnackbarMessageType 改為 string
        public string MessageType { get; set; }
    }

    /// <summary>
    /// 定義 Snackbar 的廣播訊息
    /// </summary>
    public class SnackbarRequestMessage
    {
        public string Message { get; }
        public SnackbarMessageType Type { get; }
        public string ActionContent { get; }
        public Action ActionHandler { get; }

        public bool ClearQueue { get; } //是否要在顯示前清空佇列 (預設為 true)

        public SnackbarRequestMessage(string message, SnackbarMessageType type = SnackbarMessageType.Information, string actionContent = null, Action actionHandler = null, bool clearQueue = true)
        {
            Message = message;
            Type = type;
            ActionContent = actionContent;
            ActionHandler = actionHandler;
            ClearQueue = clearQueue; // 指派變數
        }
    }

    public interface ISnackbarService
    {
        // 加上 bool clearQueue = true
        void ShowSnackbar(string message, SnackbarMessageType type = SnackbarMessageType.Information, bool clearQueue = true);
        void ShowSnackbar(string message, SnackbarMessageType type, string actionContent, Action actionHandler, bool clearQueue = true);
    }

    public class SnackbarService : ISnackbarService
    {
        private readonly IMessenger _messenger;

        public SnackbarService(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public void ShowSnackbar(string message, SnackbarMessageType type = SnackbarMessageType.Information, bool clearQueue = true)
        {
            // 將 clearQueue 傳進去
            _messenger.Send(new SnackbarRequestMessage(message, type, null, null, clearQueue));
        }

        public void ShowSnackbar(string message, SnackbarMessageType type, string actionContent, Action actionHandler, bool clearQueue = true)
        {
            // 將 clearQueue 傳進去
            _messenger.Send(new SnackbarRequestMessage(message, type, actionContent, actionHandler, clearQueue));
        }
    }
    #endregion
}
