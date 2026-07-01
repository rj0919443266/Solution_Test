using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WpfControlLibrary1.Mode;

namespace WpfControlLibrary1.Services
{
    public enum PhpServerState
    {
        Testing,   // 測試連線中
        Online,    // 已連線
        Offline    // 斷線 / 伺服器異常
    }

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
            // 設定 HttpClient 預設逾時時間
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        // ==========================================
        // 核心共用引擎：負責所有請求的發送、解析與錯誤攔截
        // ==========================================
        private async Task<ApiResult<T>> SendRequestAsync<T>(HttpRequestMessage request)
        {
            var result = new ApiResult<T>();

            try
            {
                // 1. 統一加上安全性 Header
                request.Headers.Add("X-API-KEY", _apiKey);

                // 2. 非同步發送請求
                var response = await _httpClient.SendAsync(request);
                string responseJson = await response.Content.ReadAsStringAsync();

                // 3. 嘗試解析 JSON (包含忽略大小寫的設定)
                JsonResponse<T> apiResponse = null;
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    apiResponse = JsonSerializer.Deserialize<JsonResponse<T>>(responseJson, options);
                }
                catch
                {
                    // 若伺服器掛掉回傳 HTML 網頁等非 JSON 格式，忽略並交由下方處理
                }

                // 4. 判斷 HTTP 狀態碼與 API 回傳狀態
                if (response.IsSuccessStatusCode) // HTTP 200 ~ 299
                {
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
                    else
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "伺服器回傳格式無法解析";
                    }
                }
                else // HTTP 400, 404, 409, 500 等錯誤區間
                {
                    result.IsSuccess = false;

                    // 如果 PHP 有傳回我們規範的 JSON 格式 (例如: 該異常代碼已經存在)，優先顯示中文提示！
                    if (apiResponse != null && !string.IsNullOrWhiteSpace(apiResponse.Message))
                    {
                        result.ErrorMessage = apiResponse.Message;
                    }
                    else
                    {
                        // 伺服器嚴重當機或未預期錯誤，顯示原始 HTTP 錯誤
                        result.ErrorMessage = $"伺服器連線異常 ({response.StatusCode}):\n{responseJson}";
                    }
                }
            }
            catch (TaskCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "連線逾時，請檢查伺服器是否正常運作。";
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"系統異常: {ex.Message}";
            }

            return result;
        }

        // ==========================================
        // HTTP 基礎方法 (精簡後只剩下打包功能)
        // ==========================================

        /// <summary>
        /// 現代化非同步 POST 請求
        /// </summary>
        public async Task<ApiResult<T>> PostAsync<T>(string resource, object payload)
        {
            string url = $"http://{_config.ServerUrl.TrimEnd('/')}/WorkPage/index.php?resource={resource}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            string jsonContent = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // 呼叫共用引擎
            return await SendRequestAsync<T>(request);
        }

        /// <summary>
        /// 現代化非同步 GET 請求共用方法
        /// </summary>
        public async Task<ApiResult<T>> GetAsync<T>(string resource)
        {
            string url = $"http://{_config.ServerUrl.TrimEnd('/')}/WorkPage/index.php?resource={resource}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // 呼叫共用引擎
            return await SendRequestAsync<T>(request);
        }


        // ==========================================
        // 具體 API 功能實作
        // ==========================================

        /// <summary>
        /// server 心跳測試 (靜態檔案法，不經過 PHP 引擎，極致省效能)
        /// </summary>
        public async Task<ApiResult<string>> PingServerAsync()
        {
            var result = new ApiResult<string>();
            try
            {
                // 加入時間戳記 (Cache-Buster)，防止 HttpClient 讀取到本機的靜態檔案快取，確保每一次都是真實的網路探測
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string url = $"http://{_config.ServerUrl.TrimEnd('/')}/WorkPage/ping.txt?t={timestamp}";

                // 既然只是簡單的 GET，可以直接使用 GetAsync 讓程式碼更簡潔
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    result.IsSuccess = true;
                    result.Data = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"HTTP 錯誤: {response.StatusCode}";
                }
            }
            catch (TaskCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "連線逾時";
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"連線異常: {ex.Message}";
            }

            return result;
        }

        #region "登入與密碼管理"
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
        #endregion

        #region "過站狀態"
        public async Task<ApiResult<List<WipStatusModel>>> GetWipStatusAsync(string startDate, string endDate, string lotNo, string productName, string parentLotNo, bool includeCompleted)
        {
            var payload = new
            {
                start_date = startDate,
                end_date = endDate,
                lot_no = lotNo,
                product_name = productName,
                parent_lot_no = parentLotNo,
                include_completed = includeCompleted
            };

            return await PostAsync<List<WipStatusModel>>("wip_status", payload);
        }
        #endregion

        #region "lot 明細"
        public async Task<ApiResult<WipDetailRawModel>> Get_work_page_data_Detail_Async(string lotNo)
        {
            var payload = new { lot_no = lotNo };
            return await PostAsync<WipDetailRawModel>("work_page_data_detail", payload);
        }
        #endregion

        #region "下拉選單輔助資料"
        public async Task<ApiResult<List<string>>> GetProductNamesAsync()
        {
            return await GetAsync<List<string>>("product_names");
        }
        #endregion

        #region "新增工單與紀錄查詢"

        /// <summary>
        /// 獲取最新建立的 50 筆工單資訊 (排除 b_delete = 1)
        /// </summary>
        public async Task<ApiResult<ObservableCollection<WorkPageDataSummaryModel>>> GetLatestWorkOrdersAsync()
        {
            return await PostAsync<ObservableCollection<WorkPageDataSummaryModel>>("get_latest_work_page_data", new { limit = 50 });
        }

        /// <summary>
        /// 軟刪除工單 (更新 b_delete = 1, d_time = NOW(), delete_user = 當前使用者)
        /// </summary>
        public async Task<ApiResult<object>> SoftDeleteWorkOrderAsync(string lotNo, string productName, string deleteUser)
        {
            var payload = new
            {
                Lot_no = lotNo,
                Product_name = productName,
                delete_user = deleteUser
            };
            return await PostAsync<object>("delete_work_page_data", payload);
        }

        #endregion


    }
}