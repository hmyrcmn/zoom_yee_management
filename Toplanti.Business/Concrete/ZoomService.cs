using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Business.BusinessAspects.Autofac;
using Toplanti.Core.Utilities.Results;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.Entities.DTOs;
using Toplanti.Entities.Zoom;

namespace Toplanti.Business.Concrete
{
    public class ZoomService : IZoomService
    {
        private const string BaseApiUrl = "https://api.zoom.us/v2/";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITokenHelper _tokenHelper;

        public ZoomService(IHttpClientFactory httpClientFactory, ITokenHelper tokenHelper)
        {
            _httpClientFactory = httpClientFactory;
            _tokenHelper = tokenHelper;
        }

        [SecuredOperation("Admin")]
        public async Task<IDataResult<List<ZoomUsers>>> GetWorkspaceUsers()
        {
            try
            {
                var client = await CreateZoomClient();
                var allUsers = new List<ZoomUsers>();
                var nextPageToken = string.Empty;

                do
                {
                    var endpoint = $"{BaseApiUrl}users?page_size=300";
                    if (!string.IsNullOrWhiteSpace(nextPageToken))
                    {
                        endpoint += $"&next_page_token={Uri.EscapeDataString(nextPageToken)}";
                    }

                    var response = await client.GetAsync(endpoint);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        return new ErrorDataResult<List<ZoomUsers>>(
                            $"Zoom kullanıcı listesi alınamadı: {(int)response.StatusCode} - {errorBody}");
                    }

                    var page = await response.Content.ReadFromJsonAsync<ZoomUserList>();
                    if (page?.users != null && page.users.Count > 0)
                    {
                        allUsers.AddRange(page.users);
                    }

                    nextPageToken = page?.next_page_token ?? string.Empty;
                } while (!string.IsNullOrWhiteSpace(nextPageToken));

                var workspaceUsers = allUsers
                    .Where(u => u != null && (IsStatus(u.status, "active") || IsStatus(u.status, "pending")))
                    .GroupBy(u => (u.email ?? string.Empty).Trim().ToLowerInvariant())
                    .Select(g => g.First())
                    .ToList();

                return new SuccessDataResult<List<ZoomUsers>>(workspaceUsers);
            }
            catch (Exception ex)
            {
                return new ErrorDataResult<List<ZoomUsers>>($"Zoom kullanıcı listesi alınırken hata oluştu: {ex.Message}");
            }
        }

        public async Task<bool> IsUserActiveInZoom(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var client = await CreateZoomClient();
            var response = await client.GetAsync($"{BaseApiUrl}users/{Uri.EscapeDataString(email)}");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var user = await response.Content.ReadFromJsonAsync<ZoomUserCreatedResponse>();
            return user != null && string.Equals(user.email, email, StringComparison.OrdinalIgnoreCase);
        }

        [SecuredOperation("Admin")]
        public async Task<IResult> AddUserToZoom(ZoomUserCreatedResponse request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.email))
            {
                return new ErrorResult("Geçersiz kullanıcı bilgisi.");
            }

            var client = await CreateZoomClient();
            var payload = new ZoomCreateUserRequest
            {
                action = "create",
                user_info = request
            };

            var response = await client.PostAsJsonAsync($"{BaseApiUrl}users", payload);
            if (response.IsSuccessStatusCode)
            {
                return new SuccessResult("Zoom kullanıcısı başarıyla eklendi.");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            return new ErrorResult($"Zoom kullanıcı ekleme başarısız: {(int)response.StatusCode} - {errorBody}");
        }

        [SecuredOperation("Admin")]
        public async Task<IResult> DeleteUserFromZoom(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new ErrorResult("Silinecek kullanıcı e-posta bilgisi boş olamaz.");
            }

            var client = await CreateZoomClient();
            var response = await client.DeleteAsync($"{BaseApiUrl}users/{Uri.EscapeDataString(email)}");

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new SuccessResult("Zoom kullanıcısı silindi.");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            return new ErrorResult($"Zoom kullanıcı silme başarısız: {(int)response.StatusCode} - {errorBody}");
        }

        [SecuredOperation("Admin")]
        public async Task<IResult> DeleteUsersFromZoom(List<string> emails)
        {
            if (emails == null || emails.Count == 0)
            {
                return new ErrorResult("Toplu silme için en az bir e-posta gönderilmelidir.");
            }

            var failures = new List<string>();
            foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var deleteResult = await DeleteUserFromZoom(email);
                if (!deleteResult.Success)
                {
                    failures.Add(email);
                }
            }

            if (failures.Count > 0)
            {
                return new ErrorResult($"Bazı kullanıcılar silinemedi: {string.Join(", ", failures)}");
            }

            return new SuccessResult("Toplu kullanıcı silme işlemi tamamlandı.");
        }

        private async Task<HttpClient> CreateZoomClient()
        {
            var accessToken = await _tokenHelper.CreateAccessToken();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        private static bool IsStatus(string status, string expected)
        {
            return string.Equals((status ?? string.Empty).Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
