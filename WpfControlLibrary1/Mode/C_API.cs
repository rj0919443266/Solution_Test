using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfControlLibrary1.Mode
{
    internal class C_API
    {
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
    //====================================================================
    public class LoginResponseModel
    {
        [JsonPropertyName("UserName")]
        public string UserName { get; set; }

        // 接收伺服器傳來的 Level 權限等級
        [JsonPropertyName("Level")]
        public int Level { get; set; }
    }
}
