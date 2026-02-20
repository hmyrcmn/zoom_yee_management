using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        private static readonly DateTime InviteDebugLogUntilUtc = DateTime.UtcNow.AddMinutes(5);
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
                            $"Zoom user list could not be retrieved: {(int)response.StatusCode} - {errorBody}");
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
                    .Select(g =>
                    {
                        var user = g.First();
                        return new ZoomUsers
                        {
                            id = user.id,
                            first_name = user.first_name ?? string.Empty,
                            last_name = user.last_name ?? string.Empty,
                            email = user.email ?? string.Empty,
                            type = user.type,
                            pmi = user.pmi,
                            timezone = user.timezone ?? string.Empty,
                            verified = user.verified,
                            dept = user.dept ?? string.Empty,
                            created_at = user.created_at,
                            last_login_time = user.last_login_time,
                            last_client_version = user.last_client_version ?? string.Empty,
                            language = user.language ?? string.Empty,
                            phone_number = user.phone_number ?? string.Empty,
                            status = user.status ?? string.Empty,
                            role_id = user.role_id ?? string.Empty
                        };
                    })
                    .ToList();

                return new SuccessDataResult<List<ZoomUsers>>(workspaceUsers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZoomService:GetWorkspaceUsers] Exception: {ex.Message}");
                return new ErrorDataResult<List<ZoomUsers>>($"Zoom user list failed: {ex.Message}");
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

            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var responseEmail = root.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String
                    ? emailEl.GetString()
                    : string.Empty;
                var status = root.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String
                    ? statusEl.GetString()
                    : string.Empty;

                return string.Equals(responseEmail, email, StringComparison.OrdinalIgnoreCase) && IsStatus(status, "active");
            }
            catch
            {
                return false;
            }
        }

        [SecuredOperation("Admin")]
        public async Task<IResult> AddUserToZoom(ZoomUserCreatedResponse request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.email))
                {
                    return new ErrorResult("Invalid user payload.");
                }

                var client = await CreateZoomClient();
                var normalizedUser = new ZoomInviteUserInfo
                {
                    email = request.email?.Trim() ?? string.Empty,
                    first_name = request.first_name?.Trim() ?? string.Empty,
                    last_name = request.last_name?.Trim() ?? string.Empty,
                    type = request.type.GetValueOrDefault(1) <= 0 ? 1 : request.type.GetValueOrDefault(1)
                };

                if (string.IsNullOrWhiteSpace(normalizedUser.email)
                    || string.IsNullOrWhiteSpace(normalizedUser.first_name)
                    || string.IsNullOrWhiteSpace(normalizedUser.last_name))
                {
                    return new ErrorResult("email, first_name ve last_name alanları zorunludur.");
                }

                var payload = new ZoomCreateUserRequest
                {
                    action = "invite",
                    user_info = normalizedUser
                };

                if (ShouldLogInviteDebug())
                {
                    var outboundJson = JsonSerializer.Serialize(payload);
                    Console.WriteLine($"[ZoomService:AddUserToZoom] Outbound JSON => {outboundJson}");
                }

                var response = await client.PostAsJsonAsync($"{BaseApiUrl}users", payload);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (ShouldLogInviteDebug())
                {
                    Console.WriteLine($"[ZoomService:AddUserToZoom] Inbound JSON => {responseBody}");
                }
                Console.WriteLine(
                    $"[ZoomService:AddUserToZoom] Zoom response {(int)response.StatusCode} {response.StatusCode}: {responseBody}");

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && IsInviteActionNotSupported(responseBody))
                {
                    payload.action = "create";

                    if (ShouldLogInviteDebug())
                    {
                        var fallbackOutboundJson = JsonSerializer.Serialize(payload);
                        Console.WriteLine($"[ZoomService:AddUserToZoom] Fallback Outbound JSON => {fallbackOutboundJson}");
                    }

                    response = await client.PostAsJsonAsync($"{BaseApiUrl}users", payload);
                    responseBody = await response.Content.ReadAsStringAsync();
                    if (ShouldLogInviteDebug())
                    {
                        Console.WriteLine($"[ZoomService:AddUserToZoom] Fallback Inbound JSON => {responseBody}");
                    }
                    Console.WriteLine(
                        $"[ZoomService:AddUserToZoom] Zoom fallback response {(int)response.StatusCode} {response.StatusCode}: {responseBody}");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    return new SuccessResult("Zoom user added.");
                }

                var zoomMessage = ExtractZoomErrorMessage(responseBody);
                return new ErrorResult(string.IsNullOrWhiteSpace(zoomMessage)
                    ? $"Zoom user add failed: {(int)response.StatusCode}"
                    : zoomMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZoomService:AddUserToZoom] Exception: {ex.Message}");
                return new ErrorResult($"Zoom user add exception: {ex.Message}");
            }
        }

        [SecuredOperation("Admin")]
        public async Task<IResult> DeleteUserFromZoom(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new ErrorResult("Email cannot be empty.");
            }

            var client = await CreateZoomClient();
            var response = await client.DeleteAsync($"{BaseApiUrl}users/{Uri.EscapeDataString(email)}");

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new SuccessResult("Zoom user deleted.");
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            return new ErrorResult($"Zoom user delete failed: {(int)response.StatusCode} - {errorBody}");
        }

        [SecuredOperation("Admin")]
        public async Task<IResult> DeleteUsersFromZoom(List<string> emails)
        {
            if (emails == null || emails.Count == 0)
            {
                return new ErrorResult("At least one email is required.");
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
                return new ErrorResult($"Some users could not be deleted: {string.Join(", ", failures)}");
            }

            return new SuccessResult("Bulk delete completed.");
        }

        private async Task<HttpClient> CreateZoomClient()
        {
            var accessToken = await _tokenHelper.CreateAccessToken();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Zoom OAuth returned empty access token.");
            }

            Console.WriteLine($"[ZoomService:CreateZoomClient] Access token acquired. Length: {accessToken.Length}");
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static bool IsStatus(string status, string expected)
        {
            return string.Equals((status ?? string.Empty).Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldLogInviteDebug()
        {
            return DateTime.UtcNow <= InviteDebugLogUntilUtc;
        }

        private static string ExtractZoomErrorMessage(string errorBody)
        {
            if (string.IsNullOrWhiteSpace(errorBody))
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(errorBody);
                if (document.RootElement.TryGetProperty("message", out var messageElement)
                    && messageElement.ValueKind == JsonValueKind.String)
                {
                    return messageElement.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // Try XML next.
            }

            try
            {
                var xml = XDocument.Parse(errorBody);
                var messages = xml.Descendants("message")
                    .Select(x => (x.Value ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                var field = xml.Descendants("field")
                    .Select(x => (x.Value ?? string.Empty).Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (messages.Count >= 2 && !string.IsNullOrWhiteSpace(field))
                {
                    return $"{messages[0]} ({field}: {messages[1]})";
                }

                if (messages.Count > 0)
                {
                    return messages[0];
                }
            }
            catch
            {
                // fallback below
            }

            return errorBody;
        }

        private static bool IsInviteActionNotSupported(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            var compact = responseBody.ToLowerInvariant();
            if (compact.Contains("<field>action</field>") && compact.Contains("invalid field"))
            {
                return true;
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;
                if (root.TryGetProperty("errors", out var errorsElement)
                    && errorsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in errorsElement.EnumerateArray())
                    {
                        var field = item.TryGetProperty("field", out var fieldEl) && fieldEl.ValueKind == JsonValueKind.String
                            ? fieldEl.GetString()
                            : string.Empty;
                        var message = item.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String
                            ? messageEl.GetString()
                            : string.Empty;

                        if (string.Equals(field, "action", StringComparison.OrdinalIgnoreCase)
                            && (message ?? string.Empty).Contains("invalid", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // XML / plain-text responses are handled above.
            }

            return false;
        }
    }
}
