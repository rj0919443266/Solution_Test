using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;

namespace WpfControlLibrary1.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly SystemConfig _config;
        private readonly string _apiKey = "YOUR_SECURE_TOKEN_12345"; // 舊有金鑰

        // 透過 DI 注入 HttpClient 與全域設定檔
        public ApiService(HttpClient httpClient, SystemConfig config)
        {
            _httpClient = httpClient;
            _config = config;

            // 設定 HttpClient 預設逾時時間 (取代舊的 TimeoutWebClient)
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        /// <summary>
        /// 🌟 現代化非同步 POST 請求
        /// </summary>
        public async Task<ApiResult<T>> PostAsync<T>(string resource, object payload)
        {
            var result = new ApiResult<T>();

            try
            {
                // 1. 組合 URL (從 SystemConfig 取得最新網址)
                string url = $"http://{_config.PhpServerUrl.TrimEnd('/')}/IPIapi/index.php?resource={resource}";

                // 2. 準備 Request (包含 Header 與 JSON Body)
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-API-KEY", _apiKey);

                string jsonContent = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 3. 🌟 非同步發送請求 (絕不卡死 UI)
                var response = await _httpClient.SendAsync(request);

                // 4. 讀取並解析回傳內容
                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"HTTP 錯誤: {response.StatusCode}\n{responseJson}";
                    return result;
                }

                var apiResponse = JsonSerializer.Deserialize<JsonResponse<T>>(responseJson);

                if (apiResponse != null)
                {
                    result.Message = apiResponse.Message;
                    if (apiResponse.Status == "success")
                    {
                        result.IsSuccess = true;
                        result.Data = apiResponse.Data;
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = apiResponse.Message;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                result.ErrorMessage = "連線逾時，請檢查伺服器是否正常運作。";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"系統異常: {ex.Message}";
            }

            return result;
        }

        // ==========================================
        // 具體 API 功能實作
        // ==========================================

        public async Task<ApiResult<LoginResponseModel>> LoginAsync(string empId, string password)
        {
            var payload = new { employee_id = empId, password = password };
            return await PostAsync<LoginResponseModel>("login", payload);
        }

        public async Task<ApiResult<string>> ChangePasswordAsync(string empId, string oldPwd, string newPwd)
        {
            var payload = new { employee_id = empId, old_password = oldPwd, new_password = newPwd };
            return await PostAsync<string>("change_password", payload);
        }
    }
}
