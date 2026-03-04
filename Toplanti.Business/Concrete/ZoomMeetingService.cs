using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Toplanti.Business.Abstract;
using Toplanti.Business.Constants;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.ZoomMeetings;
using Toplanti.Entities.Enums;

namespace Toplanti.Business.Concrete
{
    public class ZoomMeetingService : IZoomMeetingService
    {
        private readonly ToplantiContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITokenHelper _tokenHelper;
        private readonly ILogger<ZoomMeetingService> _logger;
        private readonly string _zoomBaseApiUrl;

        public ZoomMeetingService(
            ToplantiContext context,
            IHttpClientFactory httpClientFactory,
            ITokenHelper tokenHelper,
            IConfiguration configuration,
            ILogger<ZoomMeetingService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _tokenHelper = tokenHelper;
            _logger = logger;
            _zoomBaseApiUrl = (configuration["ZoomApi:BaseUrl"] ?? "https://api.zoom.us/v2/").TrimEnd('/') + "/";
        }

        public async Task<ZoomMeetingOperationResult> CreateMeetingAsync(
            Guid actorUserId,
            CreateZoomMeetingRequest request,
            CancellationToken cancellationToken = default)
        {
            if (actorUserId == Guid.Empty
                || request == null
                || string.IsNullOrWhiteSpace(request.Topic)
                || request.DurationMinutes <= 0)
            {
                return MeetingFailure(
                    ZoomMeetingResultCodes.InvalidRequest,
                    "Actor, topic and positive duration are required.");
            }

            var provisioning = await _context.ZoomUserProvisionings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == actorUserId
                         && x.ZoomStatusId == (byte)ZoomProvisioningStatus.Active,
                    cancellationToken);

            if (provisioning == null)
            {
                return MeetingFailure(
                    ZoomMeetingResultCodes.InvalidRequest,
                    "Actor has no active Zoom provisioning.");
            }

            var hostIdentifier = !string.IsNullOrWhiteSpace(provisioning.ZoomUserId)
                ? provisioning.ZoomUserId
                : provisioning.Email;

            var createApiResponse = await CreateZoomMeetingApiAsync(hostIdentifier, request, cancellationToken);
            if (!createApiResponse.Success)
            {
                AddAuditLog(
                    actorUserId,
                    "CREATE_MEETING",
                    provisioning.Email,
                    null,
                    ZoomMeetingResultCodes.MeetingCreateFailed,
                    createApiResponse.ErrorMessage);

                await _context.SaveChangesAsync(cancellationToken);
                return MeetingFailure(
                    ZoomMeetingResultCodes.MeetingCreateFailed,
                    createApiResponse.ErrorMessage);
            }

            var meeting = new ZoomMeeting
            {
                MeetingId = Guid.NewGuid(),
                OwnerUserId = actorUserId,
                UserProvisioningId = provisioning.UserProvisioningId,
                ZoomMeetingId = createApiResponse.ZoomMeetingId,
                Topic = createApiResponse.Topic,
                Agenda = createApiResponse.Agenda,
                StartTimeUtc = createApiResponse.StartTimeUtc,
                DurationMinutes = createApiResponse.DurationMinutes,
                Timezone = createApiResponse.Timezone,
                JoinUrl = createApiResponse.JoinUrl,
                StartUrl = createApiResponse.StartUrl,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.ZoomMeetings.AddAsync(meeting, cancellationToken);
            AddAuditLog(
                actorUserId,
                "CREATE_MEETING",
                provisioning.Email,
                meeting.ZoomMeetingId,
                ZoomMeetingResultCodes.MeetingCreated,
                "Zoom meeting created successfully.");

            await _context.SaveChangesAsync(cancellationToken);

            return new ZoomMeetingOperationResult
            {
                Success = true,
                Code = ZoomMeetingResultCodes.MeetingCreated,
                Message = "Meeting created.",
                Meeting = ToSummary(meeting)
            };
        }

        public async Task<ZoomMeetingHistoryResult> GetHistoryAsync(
            Guid actorUserId,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (actorUserId == Guid.Empty)
            {
                return new ZoomMeetingHistoryResult
                {
                    Success = false,
                    Code = ZoomMeetingResultCodes.InvalidRequest,
                    Message = "Actor user id is required."
                };
            }

            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 200);

            // Object-level authorization: only actor-owned meetings are queryable.
            var baseQuery = _context.ZoomMeetings
                .AsNoTracking()
                .Where(x => x.OwnerUserId == actorUserId && !x.IsDeleted);

            var totalCount = await baseQuery.CountAsync(cancellationToken);
            var meetings = await baseQuery
                .OrderByDescending(x => x.StartTimeUtc ?? x.CreatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new ZoomMeetingHistoryResult
            {
                Success = true,
                Code = ZoomMeetingResultCodes.MeetingHistoryFetched,
                Message = "Meeting history fetched.",
                Meetings = meetings.Select(ToSummary).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<ZoomMeetingOperationResult> GetMeetingByIdAsync(
            Guid actorUserId,
            Guid meetingId,
            CancellationToken cancellationToken = default)
        {
            if (actorUserId == Guid.Empty || meetingId == Guid.Empty)
            {
                return MeetingFailure(
                    ZoomMeetingResultCodes.InvalidRequest,
                    "Actor user id and meeting id are required.");
            }

            // Object-level authorization: ownership check is part of the query.
            var meeting = await _context.ZoomMeetings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.MeetingId == meetingId
                         && x.OwnerUserId == actorUserId
                         && !x.IsDeleted,
                    cancellationToken);

            if (meeting == null)
            {
                return MeetingFailure(
                    ZoomMeetingResultCodes.MeetingNotFoundOrForbidden,
                    "Meeting not found or not owned by actor.");
            }

            return new ZoomMeetingOperationResult
            {
                Success = true,
                Code = ZoomMeetingResultCodes.MeetingFetched,
                Message = "Meeting fetched.",
                Meeting = ToSummary(meeting)
            };
        }

        public async Task<ZoomMeetingOperationResult> DeleteMeetingAsync(
            Guid actorUserId,
            Guid meetingId,
            CancellationToken cancellationToken = default)
        {
            if (actorUserId == Guid.Empty || meetingId == Guid.Empty)
            {
                return MeetingFailure(
                    ZoomMeetingResultCodes.InvalidRequest,
                    "Actor user id and meeting id are required.");
            }

            // Never trust meeting id alone; enforce ownership at query level.
            var meeting = await _context.ZoomMeetings
                .FirstOrDefaultAsync(
                    x => x.MeetingId == meetingId
                         && x.OwnerUserId == actorUserId
                         && !x.IsDeleted,
                    cancellationToken);

            if (meeting == null)
            {
                return MeetingFailure(
                    ZoomMeetingResultCodes.MeetingNotFoundOrForbidden,
                    "Meeting not found or not owned by actor.");
            }

            var deleteResponse = await DeleteZoomMeetingApiAsync(meeting.ZoomMeetingId, cancellationToken);
            if (!deleteResponse.Success)
            {
                AddAuditLog(
                    actorUserId,
                    "DELETE_MEETING",
                    null,
                    meeting.ZoomMeetingId,
                    ZoomMeetingResultCodes.MeetingDeleteFailed,
                    deleteResponse.ErrorMessage);

                await _context.SaveChangesAsync(cancellationToken);
                return MeetingFailure(
                    ZoomMeetingResultCodes.MeetingDeleteFailed,
                    deleteResponse.ErrorMessage);
            }

            meeting.IsDeleted = true;
            meeting.DeletedAt = DateTime.UtcNow;
            meeting.UpdatedAt = DateTime.UtcNow;

            AddAuditLog(
                actorUserId,
                "DELETE_MEETING",
                null,
                meeting.ZoomMeetingId,
                ZoomMeetingResultCodes.MeetingDeleted,
                "Zoom meeting deleted.");

            await _context.SaveChangesAsync(cancellationToken);

            return new ZoomMeetingOperationResult
            {
                Success = true,
                Code = ZoomMeetingResultCodes.MeetingDeleted,
                Message = "Meeting deleted.",
                Meeting = ToSummary(meeting)
            };
        }

        private async Task<ZoomCreateMeetingApiResponse> CreateZoomMeetingApiAsync(
            string hostIdentifier,
            CreateZoomMeetingRequest request,
            CancellationToken cancellationToken)
        {
            using var client = await CreateZoomHttpClientAsync(cancellationToken);
            var payload = new
            {
                topic = request.Topic.Trim(),
                agenda = (request.Agenda ?? string.Empty).Trim(),
                type = 2,
                start_time = request.StartTimeUtc == default
                    ? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    : request.StartTimeUtc.ToString("O", CultureInfo.InvariantCulture),
                duration = request.DurationMinutes,
                timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim()
            };

            using var response = await client.PostAsJsonAsync(
                $"users/{Uri.EscapeDataString(hostIdentifier)}/meetings",
                payload,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ZoomCreateMeetingApiResponse
                {
                    ErrorMessage = ExtractZoomErrorMessage(body, $"Zoom create meeting failed: {(int)response.StatusCode}")
                };
            }

            return ParseCreateMeetingResponse(body);
        }

        private async Task<ZoomDeleteMeetingApiResponse> DeleteZoomMeetingApiAsync(
            string zoomMeetingId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(zoomMeetingId))
            {
                return new ZoomDeleteMeetingApiResponse
                {
                    ErrorMessage = "Zoom meeting id is empty."
                };
            }

            using var client = await CreateZoomHttpClientAsync(cancellationToken);
            using var response = await client.DeleteAsync(
                $"meetings/{Uri.EscapeDataString(zoomMeetingId)}",
                cancellationToken);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ZoomDeleteMeetingApiResponse { Success = true };
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ZoomDeleteMeetingApiResponse
            {
                ErrorMessage = ExtractZoomErrorMessage(body, $"Zoom delete meeting failed: {(int)response.StatusCode}")
            };
        }

        private async Task<HttpClient> CreateZoomHttpClientAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = await _tokenHelper.CreateAccessToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Zoom OAuth token could not be created.");
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_zoomBaseApiUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static ZoomCreateMeetingApiResponse ParseCreateMeetingResponse(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new ZoomCreateMeetingApiResponse
                {
                    ErrorMessage = "Zoom create meeting returned empty response."
                };
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var zoomMeetingId = ReadJsonAsString(root, "id");
                if (string.IsNullOrWhiteSpace(zoomMeetingId))
                {
                    return new ZoomCreateMeetingApiResponse
                    {
                        ErrorMessage = "Zoom response does not contain meeting id."
                    };
                }

                return new ZoomCreateMeetingApiResponse
                {
                    Success = true,
                    ZoomMeetingId = zoomMeetingId,
                    Topic = ReadJsonAsString(root, "topic"),
                    Agenda = ReadJsonAsString(root, "agenda"),
                    StartTimeUtc = ReadJsonDateTime(root, "start_time"),
                    DurationMinutes = ReadJsonInt(root, "duration"),
                    Timezone = ReadJsonAsString(root, "timezone"),
                    JoinUrl = ReadJsonAsString(root, "join_url"),
                    StartUrl = ReadJsonAsString(root, "start_url")
                };
            }
            catch
            {
                return new ZoomCreateMeetingApiResponse
                {
                    ErrorMessage = "Zoom response could not be parsed."
                };
            }
        }

        private static string ReadJsonAsString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var child))
            {
                return string.Empty;
            }

            return child.ValueKind switch
            {
                JsonValueKind.String => child.GetString() ?? string.Empty,
                JsonValueKind.Number => child.ToString(),
                _ => child.ToString()
            };
        }

        private static DateTime? ReadJsonDateTime(JsonElement element, string propertyName)
        {
            var raw = ReadJsonAsString(element, propertyName);
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date)
                ? date
                : null;
        }

        private static int? ReadJsonInt(JsonElement element, string propertyName)
        {
            var raw = ReadJsonAsString(element, propertyName);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static string ExtractZoomErrorMessage(string body, string fallback)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return fallback;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    var value = message.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
            catch
            {
                // ignored intentionally
            }

            return fallback;
        }

        private static ZoomMeetingSummary ToSummary(ZoomMeeting meeting)
        {
            return new ZoomMeetingSummary
            {
                MeetingId = meeting.MeetingId,
                ZoomMeetingId = meeting.ZoomMeetingId,
                Topic = meeting.Topic,
                Agenda = meeting.Agenda,
                StartTimeUtc = meeting.StartTimeUtc,
                DurationMinutes = meeting.DurationMinutes,
                Timezone = meeting.Timezone ?? string.Empty,
                JoinUrl = meeting.JoinUrl,
                StartUrl = meeting.StartUrl,
                CreatedAtUtc = meeting.CreatedAt
            };
        }

        private void AddAuditLog(
            Guid? actorUserId,
            string actionType,
            string? targetEmail,
            string? targetMeetingId,
            string resultCode,
            string? message)
        {
            _context.AuditZoomActionLogs.Add(new AuditZoomActionLog
            {
                ActorUserId = actorUserId,
                ActionType = actionType,
                TargetEmail = targetEmail,
                TargetMeetingId = targetMeetingId,
                ResultCode = resultCode,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static ZoomMeetingOperationResult MeetingFailure(string code, string message)
        {
            return new ZoomMeetingOperationResult
            {
                Success = false,
                Code = code,
                Message = message
            };
        }

        private sealed class ZoomCreateMeetingApiResponse
        {
            public bool Success { get; init; }
            public string ZoomMeetingId { get; init; } = string.Empty;
            public string Topic { get; init; } = string.Empty;
            public string Agenda { get; init; } = string.Empty;
            public DateTime? StartTimeUtc { get; init; }
            public int? DurationMinutes { get; init; }
            public string Timezone { get; init; } = string.Empty;
            public string JoinUrl { get; init; } = string.Empty;
            public string StartUrl { get; init; } = string.Empty;
            public string ErrorMessage { get; init; } = string.Empty;
        }

        private sealed class ZoomDeleteMeetingApiResponse
        {
            public bool Success { get; init; }
            public string ErrorMessage { get; init; } = string.Empty;
        }
    }
}
