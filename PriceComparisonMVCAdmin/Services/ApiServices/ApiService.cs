﻿using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Net;
using PriceComparisonMVCAdmin.Models.DTOs.Response;

namespace PriceComparisonMVCAdmin.Services.ApiServices
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly TokenManager _tokenManager;

        public ApiService(HttpClient httpClient, TokenManager tokenManager)
        {
            _httpClient = httpClient;
            _tokenManager = tokenManager;
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            return await ExecuteRequestAsync<T>(() => _httpClient.GetAsync(endpoint));
        }


        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest requestData)
        {
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            return await ExecuteRequestAsync<TResponse>(() => _httpClient.PostAsync(endpoint, content));
        }

        public async Task<TResponse> DeleteAsync<TRequest, TResponse>(string endpoint, TRequest? requestData = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);

            if (requestData != null)
            {
                var json = JsonSerializer.Serialize(requestData);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await ExecuteRequestAsync<TResponse>(() => _httpClient.SendAsync(request));
        }

        private async Task<T> ExecuteRequestAsync<T>(Func<Task<HttpResponseMessage>> requestFunc)
        {
            try
            {
                SetAuthorizationHeader();
                var response = await requestFunc();


                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var newToken = await _tokenManager.RefreshTokenAsync();
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        SetAuthorizationHeader();
                        response = await requestFunc();
                    }
                }

                //response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var validationModel = JsonSerializer.Deserialize<ApiValidationErrorResponseModel>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var result = new GeneralApiResponseModel
                    {
                        ReturnCode = "ValidationError",
                        Message = validationModel?.Title ?? "Bad Request",
                        Data = validationModel
                    };

                    return (T)(object)result!;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API error {response.StatusCode}: {json}");
                }

                if (string.IsNullOrWhiteSpace(json))
                    throw new Exception("Порожня відповідь від API.");

                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"Error during API request: {ex.Message}", ex);
            }
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(
                HttpMethod method,
                string endpoint,
                TRequest? requestData = default,
                bool useMultipartFormData = false)
        {
            // 1. Формуємо HttpRequestMessage
            var request = new HttpRequestMessage(method, endpoint);

            // 2. Якщо є requestData, дивимось, у якому форматі його відправляти
            if (requestData != null)
            {
                if (useMultipartFormData)
                {
                    // Використовуємо multipart/form-data
                    request.Content = BuildMultipartContent(requestData);
                }
                else
                {
                    // Використовуємо application/json
                    var json = JsonSerializer.Serialize(requestData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
            }

            // 3. Відправляємо запит через допоміжний метод
            return await SendHttpRequestAsync<TResponse>(request);
        }

        private static MultipartFormDataContent BuildMultipartContent<TRequest>(TRequest data)
        {
            var formData = new MultipartFormDataContent();

            // Reflection: TRequest
            var properties = typeof(TRequest).GetProperties();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(data);
                if (value == null)
                    continue;

                if (value is IFormFile singleFile && singleFile.Length > 0)
                {
                    var fileContent = new StreamContent(singleFile.OpenReadStream());
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(singleFile.ContentType);
                    formData.Add(fileContent, prop.Name, singleFile.FileName);
                }
                else if (value is IEnumerable<IFormFile> multipleFiles)
                {
                    foreach (var file in multipleFiles)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var fileContent = new StreamContent(file.OpenReadStream());
                            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                            formData.Add(fileContent, prop.Name, file.FileName);
                        }
                    }
                }
                else
                {
                    formData.Add(new StringContent(value.ToString()!), prop.Name);
                }
            }

            return formData;
        }

        private async Task<TResponse> SendHttpRequestAsync<TResponse>(HttpRequestMessage request)
        {
            try
            {
                // Додаємо заголовок авторизації (Bearer), якщо є токен
                SetAuthorizationHeader();

                // Виконуємо запит
                var response = await _httpClient.SendAsync(request);

                // Перевірка на 401 Unauthorized + рефреш токена
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var newToken = await _tokenManager.RefreshTokenAsync();
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        SetAuthorizationHeader();
                        response = await _httpClient.SendAsync(request);
                    }
                }

                // Кидає виняток, якщо код статусу не 2xx
                //response.EnsureSuccessStatusCode();

                // Читаємо відповідь як JSON
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var validationModel = JsonSerializer.Deserialize<ApiValidationErrorResponseModel>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var result = new GeneralApiResponseModel
                    {
                        ReturnCode = "ValidationError",
                        Message = validationModel?.Title ?? "Bad Request",
                        Data = validationModel
                    };

                    return (TResponse)(object)result!;
                }

                return JsonSerializer.Deserialize<TResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })!;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during API request: {ex.Message}");
            }
        }


        private void SetAuthorizationHeader()
        {
            var token = _tokenManager.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}