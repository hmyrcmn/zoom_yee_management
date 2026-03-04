using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
using Toplanti.Entities.DTOs.ZoomProvisioning;
using Toplanti.Entities.Enums;

namespace Toplanti.Business.Concrete
{
    public class ZoomProvisioningService : IZoomProvisioningService
    {
        private const string SourceSystem = "SYSTEM";
        private const string SourceWebhook = "WEBHOOK";

        private const string ActionProvision = "PROVISION";
        private const string ActionRetryProvision = "RETRY_PROVISION";
        private const string ActionApiAccepted = "API_ACCEPTED";
        private const string ActionApiConflict = "API_CONFLICT";
        private const string ActionApiRateLimit = "API_RATE_LIMIT";
        private const string ActionApiError = "API_ERROR";
        private const string ActionWebhookActivated = "WEBHOOK_USER_ACTIVATED";
        private const string ActionSyncExternalLookup = "SYNC_EXTERNAL_LOOKUP";
        private const string ActionWebhookDiscovery = "WEBHOOK_DISCOVERY";

        private readonly ToplantiContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITokenHelper _tokenHelper;
        private readonly ILogger<ZoomProvisioningService> _logger;
        private readonly string _zoomBaseApiUrl;
        private readonly string _webhookSecretToken;
        private readonly int _webhookTimestampToleranceSeconds;
        private readonly bool _skipWebhookSignatureValidation;

        public ZoomProvisioningService(
            ToplantiContext context,
            IHttpClientFactory httpClientFactory,
            ITokenHelper tokenHelper,
            IConfiguration configuration,
            ILogger<ZoomProvisioningService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _tokenHelper = tokenHelper;
            _logger = logger;

            _zoomBaseApiUrl = (configuration["ZoomApi:BaseUrl"] ?? "https://api.zoom.us/v2/").TrimEnd('/') + "/";
            _webhookSecretToken = configuration["ZoomWebhook:SecretToken"] ?? string.Empty;
            _webhookTimestampToleranceSeconds = Math.Max(
                0,
                configuration.GetValue<int?>("ZoomWebhook:TimestampToleranceSeconds") ?? 300);
            _skipWebhookSignatureValidation = configuration.GetValue<bool>("ZoomWebhook:SkipSignatureValidation");
        }

        public async Task<ZoomAccountStatusResult> CheckAccountStatusAsync(
            CheckZoomAccountStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                return AccountStatusFailure(
                    ZoomProvisioningResultCodes.InvalidRequest,
                    "A valid email is required.");
            }

            var email = NormalizeEmail(request.Email);
            var emailKey = NormalizeEmailKey(email);
            var actorUserId = request.ActorUserId;
            var ipAddress = NormalizeIp(request.IpAddress);

            await EnsureStatusCatalogAsync(cancellationToken);

            var existingProvisioning = await _context.ZoomUserProvisionings
                .AsNoTracking()
                .Include(x => x.ZoomStatus)
                .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);

            if (existingProvisioning != null)
            {
                _logger.LogInformation("Zoom provisioning status fetched from local db for {Email}", email);
                return new ZoomAccountStatusResult
                {
                    Success = true,
                    Code = ZoomProvisioningResultCodes.StatusFetched,
                    Message = "Provisioning status fetched from local storage.",
                    UserProvisioningId = existingProvisioning.UserProvisioningId,
                    Email = existingProvisioning.Email,
                    StatusName = existingProvisioning.ZoomStatus?.Name ?? ResolveStatusName(existingProvisioning.ZoomStatusId),
                    ExistsInLocalProvisioning = true,
                    ExistsInZoomWorkspace = !string.IsNullOrWhiteSpace(existingProvisioning.ZoomUserId),
                    ZoomUserId = existingProvisioning.ZoomUserId ?? string.Empty,
                    LastSyncedAtUtc = existingProvisioning.LastSyncedAt
                };
            }

            var now = DateTime.UtcNow;
            var zoomLookup = await GetZoomUserByEmailAsync(email, cancellationToken);
            var mappedStatus = MapLookupToStatus(zoomLookup);
            var authUserId = await _context.AuthUsers
                .Where(x => x.EmailNormalized == emailKey)
                .Select(x => (Guid?)x.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            var provisioning = new ZoomUserProvisioning
            {
                UserProvisioningId = Guid.NewGuid(),
                UserId = authUserId,
                Email = email,
                EmailNormalized = emailKey,
                ZoomUserId = zoomLookup.ZoomUserId,
                ZoomStatusId = (byte)mappedStatus,
                LastErrorCode = zoomLookup.ErrorCode,
                LastErrorMessage = zoomLookup.ErrorMessage,
                LastSyncedAt = now,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = now
            };

            await _context.ZoomUserProvisionings.AddAsync(provisioning, cancellationToken);
            AddHistory(
                provisioning,
                fromStatusId: null,
                toStatusId: (byte)mappedStatus,
                actionType: ActionSyncExternalLookup,
                actorUserId: actorUserId,
                source: SourceSystem,
                httpStatusCode: zoomLookup.HttpStatusCode,
                message: zoomLookup.ErrorMessage,
                rawResponse: zoomLookup.RawResponse,
                requestIpAddress: ipAddress);

            AddAuditLog(
                actorUserId,
                "CHECK_ACCOUNT_STATUS",
                email,
                null,
                ZoomProvisioningResultCodes.StatusFetched,
                mappedStatus == ZoomProvisioningStatus.Active
                    ? "External Zoom account found and synced as Active."
                    : "Local status initialized from external lookup.",
                ipAddress);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Zoom account status synced for {Email}. LocalStatus={Status}, Http={Http}",
                email,
                ResolveStatusName((byte)mappedStatus),
                zoomLookup.HttpStatusCode);

            return new ZoomAccountStatusResult
            {
                Success = true,
                Code = ZoomProvisioningResultCodes.StatusFetched,
                Message = "Provisioning status synchronized.",
                UserProvisioningId = provisioning.UserProvisioningId,
                Email = provisioning.Email,
                StatusName = ResolveStatusName(provisioning.ZoomStatusId),
                ExistsInLocalProvisioning = true,
                ExistsInZoomWorkspace = zoomLookup.Exists,
                ZoomUserId = provisioning.ZoomUserId ?? string.Empty,
                LastSyncedAtUtc = provisioning.LastSyncedAt
            };
        }

        public async Task<ZoomProvisionUserResult> ProvisionUserAsync(
            ProvisionZoomUserRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.Email)
                || !IsValidEmail(request.Email)
                || string.IsNullOrWhiteSpace(request.FirstName)
                || string.IsNullOrWhiteSpace(request.LastName))
            {
                return ProvisionFailure(
                    ZoomProvisioningResultCodes.InvalidRequest,
                    "Email, first name and last name are required.");
            }

            var email = NormalizeEmail(request.Email);
            var emailKey = NormalizeEmailKey(email);
            var actorUserId = request.ActorUserId;
            var ipAddress = NormalizeIp(request.IpAddress);
            var now = DateTime.UtcNow;

            await EnsureStatusCatalogAsync(cancellationToken);

            var provisioning = await _context.ZoomUserProvisionings
                .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);

            if (provisioning == null)
            {
                var authUserId = await _context.AuthUsers
                    .Where(x => x.EmailNormalized == emailKey)
                    .Select(x => (Guid?)x.UserId)
                    .FirstOrDefaultAsync(cancellationToken);

                provisioning = new ZoomUserProvisioning
                {
                    UserProvisioningId = Guid.NewGuid(),
                    UserId = authUserId,
                    Email = email,
                    EmailNormalized = emailKey,
                    ZoomStatusId = (byte)ZoomProvisioningStatus.None,
                    CreatedAt = now,
                    CorrelationId = Guid.NewGuid()
                };

                await _context.ZoomUserProvisionings.AddAsync(provisioning, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (provisioning.ZoomStatusId == (byte)ZoomProvisioningStatus.Active)
            {
                return new ZoomProvisionUserResult
                {
                    Success = true,
                    Code = ZoomProvisioningResultCodes.ProvisioningConflictActivated,
                    Message = "User is already active in Zoom.",
                    UserProvisioningId = provisioning.UserProvisioningId,
                    Email = provisioning.Email,
                    StatusName = ResolveStatusName(provisioning.ZoomStatusId),
                    ZoomUserId = provisioning.ZoomUserId ?? string.Empty
                };
            }

            var pendingAction = provisioning.ZoomStatusId == (byte)ZoomProvisioningStatus.Failed
                ? ActionRetryProvision
                : ActionProvision;

            var pendingStatusId = (byte)ZoomProvisioningStatus.ProvisioningPending;
            if (!await IsTransitionAllowedAsync(provisioning.ZoomStatusId, pendingStatusId, pendingAction, cancellationToken))
            {
                return ProvisionFailure(
                    ZoomProvisioningResultCodes.TransitionNotAllowed,
                    $"Transition {ResolveStatusName(provisioning.ZoomStatusId)} -> ProvisioningPending is not allowed.");
            }

            var previousStatusId = provisioning.ZoomStatusId;
            provisioning.ZoomStatusId = pendingStatusId;
            provisioning.LastErrorCode = null;
            provisioning.LastErrorMessage = null;
            provisioning.UpdatedAt = now;
            provisioning.LastSyncedAt = now;

            AddHistory(
                provisioning,
                previousStatusId,
                pendingStatusId,
                pendingAction,
                actorUserId,
                SourceSystem,
                null,
                "Provisioning request sent to Zoom.",
                null,
                ipAddress);

            await _context.SaveChangesAsync(cancellationToken);

            var createResponse = await CreateZoomUserAutoCreateAsync(
                email,
                request.FirstName.Trim(),
                request.LastName.Trim(),
                request.UserType <= 0 ? 1 : request.UserType,
                cancellationToken);

            if (createResponse.IsConflict)
            {
                return await HandleConflictResponseAsync(provisioning, createResponse, actorUserId, ipAddress, cancellationToken);
            }

            if (createResponse.Success)
            {
                return await HandleAcceptedResponseAsync(provisioning, createResponse, actorUserId, ipAddress, cancellationToken);
            }

            return await HandleFailedResponseAsync(provisioning, createResponse, actorUserId, ipAddress, cancellationToken);
        }

        public async Task<ZoomCallbackResult> HandleCallbackAsync(
            ZoomCallbackRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.PayloadJson)
                || string.IsNullOrWhiteSpace(request.Signature)
                || string.IsNullOrWhiteSpace(request.Timestamp))
            {
                return CallbackFailure(
                    ZoomProvisioningResultCodes.InvalidRequest,
                    "Callback payload, signature and timestamp are required.");
            }

            await EnsureStatusCatalogAsync(cancellationToken);

            var callbackData = ParseWebhookPayload(request.PayloadJson);
            var eventType = string.IsNullOrWhiteSpace(request.EventType) ? callbackData.EventType : request.EventType.Trim();
            var eventId = string.IsNullOrWhiteSpace(request.EventId) ? callbackData.EventId : request.EventId.Trim();
            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventId = ComputeSha256Hex(request.PayloadJson);
            }

            if (!IsWebhookSignatureValid(request.Timestamp, request.PayloadJson, request.Signature))
            {
                AddAuditLog(
                    null,
                    "WEBHOOK_CALLBACK",
                    callbackData.Email,
                    null,
                    ZoomProvisioningResultCodes.CallbackInvalidSignature,
                    "Zoom webhook signature validation failed.",
                    NormalizeIp(request.IpAddress));

                await _context.SaveChangesAsync(cancellationToken);

                return CallbackFailure(
                    ZoomProvisioningResultCodes.CallbackInvalidSignature,
                    "Invalid webhook signature.");
            }

            var existingInbox = await _context.ZoomWebhookInboxes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

            if (existingInbox != null && existingInbox.ProcessedAt != null)
            {
                return new ZoomCallbackResult
                {
                    Success = true,
                    Code = ZoomProvisioningResultCodes.CallbackAlreadyProcessed,
                    Message = "Webhook event is already processed.",
                    WebhookInboxId = existingInbox.WebhookInboxId,
                    AlreadyProcessed = true,
                    EventType = eventType
                };
            }

            var now = DateTime.UtcNow;
            var inbox = existingInbox == null
                ? new ZoomWebhookInbox
                {
                    WebhookInboxId = Guid.NewGuid(),
                    EventId = eventId,
                    EventType = eventType,
                    Signature = request.Signature.Trim(),
                    Timestamp = request.Timestamp.Trim(),
                    PayloadHash = ComputeSha256Hex(request.PayloadJson),
                    PayloadJson = request.PayloadJson,
                    RequestIpAddress = NormalizeIp(request.IpAddress),
                    ReceivedAt = now,
                    CorrelationId = Guid.NewGuid()
                }
                : await _context.ZoomWebhookInboxes.FirstAsync(x => x.WebhookInboxId == existingInbox.WebhookInboxId, cancellationToken);

            if (existingInbox == null)
            {
                await _context.ZoomWebhookInboxes.AddAsync(inbox, cancellationToken);
            }

            try
            {
                if (string.Equals(eventType, "user.activated", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleUserActivatedCallbackAsync(callbackData, inbox, cancellationToken);
                }
                else
                {
                    inbox.ProcessingResult = "IGNORED";
                }

                inbox.ProcessedAt = DateTime.UtcNow;
                AddAuditLog(
                    null,
                    "WEBHOOK_CALLBACK",
                    callbackData.Email,
                    null,
                    ZoomProvisioningResultCodes.CallbackProcessed,
                    $"Webhook processed for event {eventType}.",
                    NormalizeIp(request.IpAddress));

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException dbUpdateException) when (IsUniqueEventConstraintViolation(dbUpdateException))
            {
                _logger.LogWarning("Duplicate webhook event detected for EventId: {EventId}", eventId);
                var dupInbox = await _context.ZoomWebhookInboxes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

                return new ZoomCallbackResult
                {
                    Success = true,
                    Code = ZoomProvisioningResultCodes.CallbackAlreadyProcessed,
                    Message = "Webhook event is already recorded.",
                    WebhookInboxId = dupInbox?.WebhookInboxId,
                    AlreadyProcessed = true,
                    EventType = eventType
                };
            }

            return new ZoomCallbackResult
            {
                Success = true,
                Code = ZoomProvisioningResultCodes.CallbackProcessed,
                Message = "Webhook callback processed.",
                WebhookInboxId = inbox.WebhookInboxId,
                AlreadyProcessed = false,
                EventType = eventType
            };
        }

        private async Task<ZoomProvisionUserResult> HandleConflictResponseAsync(
            ZoomUserProvisioning provisioning,
            ZoomCreateUserResponse createResponse,
            Guid? actorUserId,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            var pendingStatusId = (byte)ZoomProvisioningStatus.ProvisioningPending;
            var activeStatusId = (byte)ZoomProvisioningStatus.Active;

            if (!await IsTransitionAllowedAsync(pendingStatusId, activeStatusId, ActionApiConflict, cancellationToken))
            {
                return ProvisionFailure(
                    ZoomProvisioningResultCodes.TransitionNotAllowed,
                    "Provisioning conflict handled but transition rule to Active is missing.");
            }

            provisioning.ZoomStatusId = activeStatusId;
            provisioning.ZoomUserId = createResponse.ZoomUserId;
            provisioning.LastErrorCode = null;
            provisioning.LastErrorMessage = null;
            provisioning.LastSyncedAt = DateTime.UtcNow;
            provisioning.UpdatedAt = DateTime.UtcNow;

            AddHistory(
                provisioning,
                pendingStatusId,
                activeStatusId,
                ActionApiConflict,
                actorUserId,
                SourceSystem,
                createResponse.HttpStatusCode,
                "Zoom returned conflict; user treated as active.",
                createResponse.RawResponse,
                ipAddress);

            AddAuditLog(
                actorUserId,
                "PROVISION_USER",
                provisioning.Email,
                null,
                ZoomProvisioningResultCodes.ProvisioningConflictActivated,
                "Zoom conflict handled as active user.",
                ipAddress);

            await _context.SaveChangesAsync(cancellationToken);

            return new ZoomProvisionUserResult
            {
                Success = true,
                Code = ZoomProvisioningResultCodes.ProvisioningConflictActivated,
                Message = "User already exists in Zoom. Local status set to Active.",
                UserProvisioningId = provisioning.UserProvisioningId,
                Email = provisioning.Email,
                StatusName = ResolveStatusName(provisioning.ZoomStatusId),
                ZoomUserId = provisioning.ZoomUserId ?? string.Empty
            };
        }

        private async Task<ZoomProvisionUserResult> HandleAcceptedResponseAsync(
            ZoomUserProvisioning provisioning,
            ZoomCreateUserResponse createResponse,
            Guid? actorUserId,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            var pendingStatusId = (byte)ZoomProvisioningStatus.ProvisioningPending;
            var activationPendingStatusId = (byte)ZoomProvisioningStatus.ActivationPending;

            if (!await IsTransitionAllowedAsync(pendingStatusId, activationPendingStatusId, ActionApiAccepted, cancellationToken))
            {
                return ProvisionFailure(
                    ZoomProvisioningResultCodes.TransitionNotAllowed,
                    "Provisioning accepted by Zoom but transition rule to ActivationPending is missing.");
            }

            provisioning.ZoomStatusId = activationPendingStatusId;
            provisioning.ZoomUserId = createResponse.ZoomUserId;
            provisioning.LastErrorCode = null;
            provisioning.LastErrorMessage = null;
            provisioning.LastSyncedAt = DateTime.UtcNow;
            provisioning.UpdatedAt = DateTime.UtcNow;

            AddHistory(
                provisioning,
                pendingStatusId,
                activationPendingStatusId,
                ActionApiAccepted,
                actorUserId,
                SourceSystem,
                createResponse.HttpStatusCode,
                "Zoom autoCreate accepted. Waiting for activation.",
                createResponse.RawResponse,
                ipAddress);

            AddAuditLog(
                actorUserId,
                "PROVISION_USER",
                provisioning.Email,
                null,
                ZoomProvisioningResultCodes.ProvisioningStarted,
                "Zoom provisioning accepted.",
                ipAddress);

            await _context.SaveChangesAsync(cancellationToken);

            return new ZoomProvisionUserResult
            {
                Success = true,
                Code = ZoomProvisioningResultCodes.ProvisioningStarted,
                Message = "Provisioning accepted. Waiting for activation event.",
                UserProvisioningId = provisioning.UserProvisioningId,
                Email = provisioning.Email,
                StatusName = ResolveStatusName(provisioning.ZoomStatusId),
                ZoomUserId = provisioning.ZoomUserId ?? string.Empty
            };
        }

        private async Task<ZoomProvisionUserResult> HandleFailedResponseAsync(
            ZoomUserProvisioning provisioning,
            ZoomCreateUserResponse createResponse,
            Guid? actorUserId,
            string ipAddress,
            CancellationToken cancellationToken)
        {
            var pendingStatusId = (byte)ZoomProvisioningStatus.ProvisioningPending;
            var failedStatusId = (byte)ZoomProvisioningStatus.Failed;
            var failAction = createResponse.IsRateLimited ? ActionApiRateLimit : ActionApiError;

            if (!await IsTransitionAllowedAsync(pendingStatusId, failedStatusId, failAction, cancellationToken))
            {
                return ProvisionFailure(
                    ZoomProvisioningResultCodes.TransitionNotAllowed,
                    "Provisioning failed but transition rule to Failed is missing.");
            }

            provisioning.ZoomStatusId = failedStatusId;
            provisioning.LastErrorCode = createResponse.ErrorCode;
            provisioning.LastErrorMessage = createResponse.ErrorMessage;
            provisioning.LastSyncedAt = DateTime.UtcNow;
            provisioning.UpdatedAt = DateTime.UtcNow;

            AddHistory(
                provisioning,
                pendingStatusId,
                failedStatusId,
                failAction,
                actorUserId,
                SourceSystem,
                createResponse.HttpStatusCode,
                createResponse.ErrorMessage,
                createResponse.RawResponse,
                ipAddress);

            AddAuditLog(
                actorUserId,
                "PROVISION_USER",
                provisioning.Email,
                null,
                createResponse.IsRateLimited
                    ? ZoomProvisioningResultCodes.ProvisioningRateLimited
                    : ZoomProvisioningResultCodes.ProvisioningFailed,
                createResponse.ErrorMessage,
                ipAddress);

            await _context.SaveChangesAsync(cancellationToken);

            if (createResponse.IsRateLimited)
            {
                return new ZoomProvisionUserResult
                {
                    Success = false,
                    Code = ZoomProvisioningResultCodes.ProvisioningRateLimited,
                    Message = "Zoom rate limit reached. Retry later.",
                    UserProvisioningId = provisioning.UserProvisioningId,
                    Email = provisioning.Email,
                    StatusName = ResolveStatusName(provisioning.ZoomStatusId),
                    ZoomUserId = provisioning.ZoomUserId ?? string.Empty,
                    RetryAfterSeconds = createResponse.RetryAfterSeconds
                };
            }

            return ProvisionFailure(
                ZoomProvisioningResultCodes.ProvisioningFailed,
                string.IsNullOrWhiteSpace(createResponse.ErrorMessage)
                    ? "Zoom provisioning failed."
                    : createResponse.ErrorMessage,
                provisioning.UserProvisioningId,
                provisioning.Email,
                ResolveStatusName(provisioning.ZoomStatusId));
        }

        private async Task HandleUserActivatedCallbackAsync(
            ParsedWebhookPayload callbackData,
            ZoomWebhookInbox inbox,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(callbackData.Email))
            {
                inbox.ProcessingResult = "FAILED";
                return;
            }

            var email = NormalizeEmail(callbackData.Email);
            var emailKey = NormalizeEmailKey(email);
            var now = DateTime.UtcNow;

            var provisioning = await _context.ZoomUserProvisionings
                .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);

            if (provisioning == null)
            {
                var authUserId = await _context.AuthUsers
                    .Where(x => x.EmailNormalized == emailKey)
                    .Select(x => (Guid?)x.UserId)
                    .FirstOrDefaultAsync(cancellationToken);

                provisioning = new ZoomUserProvisioning
                {
                    UserProvisioningId = Guid.NewGuid(),
                    UserId = authUserId,
                    Email = email,
                    EmailNormalized = emailKey,
                    ZoomStatusId = (byte)ZoomProvisioningStatus.Active,
                    ZoomUserId = callbackData.ZoomUserId ?? string.Empty,
                    LastSyncedAt = now,
                    CreatedAt = now
                };

                await _context.ZoomUserProvisionings.AddAsync(provisioning, cancellationToken);

                AddHistory(
                    provisioning,
                    null,
                    (byte)ZoomProvisioningStatus.Active,
                    ActionWebhookDiscovery,
                    null,
                    SourceWebhook,
                    null,
                    "Provisioning record created from webhook callback.",
                    inbox.PayloadJson,
                    inbox.RequestIpAddress);

                inbox.ProcessingResult = "CREATED_ACTIVE";
                return;
            }

            if (provisioning.ZoomStatusId == (byte)ZoomProvisioningStatus.Active)
            {
                provisioning.ZoomUserId = string.IsNullOrWhiteSpace(callbackData.ZoomUserId)
                    ? provisioning.ZoomUserId
                    : callbackData.ZoomUserId;
                provisioning.LastSyncedAt = now;
                provisioning.UpdatedAt = now;
                inbox.ProcessingResult = "ALREADY_ACTIVE";
                return;
            }

            if (provisioning.ZoomStatusId == (byte)ZoomProvisioningStatus.ActivationPending
                && await IsTransitionAllowedAsync(
                    (byte)ZoomProvisioningStatus.ActivationPending,
                    (byte)ZoomProvisioningStatus.Active,
                    ActionWebhookActivated,
                    cancellationToken))
            {
                var previous = provisioning.ZoomStatusId;
                provisioning.ZoomStatusId = (byte)ZoomProvisioningStatus.Active;
                provisioning.ZoomUserId = string.IsNullOrWhiteSpace(callbackData.ZoomUserId)
                    ? provisioning.ZoomUserId
                    : callbackData.ZoomUserId;
                provisioning.LastErrorCode = null;
                provisioning.LastErrorMessage = null;
                provisioning.LastSyncedAt = now;
                provisioning.UpdatedAt = now;

                AddHistory(
                    provisioning,
                    previous,
                    provisioning.ZoomStatusId,
                    ActionWebhookActivated,
                    null,
                    SourceWebhook,
                    null,
                    "Activation callback received from Zoom.",
                    inbox.PayloadJson,
                    inbox.RequestIpAddress);

                inbox.ProcessingResult = "ACTIVATED";
                return;
            }

            inbox.ProcessingResult = "IGNORED_STATUS";
        }

        private void AddHistory(
            ZoomUserProvisioning provisioning,
            byte? fromStatusId,
            byte toStatusId,
            string actionType,
            Guid? actorUserId,
            string source,
            int? httpStatusCode,
            string? message,
            string? rawResponse,
            string? requestIpAddress)
        {
            _context.ZoomUserProvisioningHistories.Add(new ZoomUserProvisioningHistory
            {
                UserProvisioningHistoryId = Guid.NewGuid(),
                UserProvisioningId = provisioning.UserProvisioningId,
                FromStatusId = fromStatusId,
                ToStatusId = toStatusId,
                ActionType = actionType,
                ActorUserId = actorUserId,
                Source = source,
                HttpStatusCode = httpStatusCode,
                Message = Truncate(message, 2000),
                RawResponse = Truncate(rawResponse, 4000),
                RequestIpAddress = NormalizeIp(requestIpAddress),
                CorrelationId = provisioning.CorrelationId ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            });
        }

        private void AddAuditLog(
            Guid? actorUserId,
            string actionType,
            string? targetEmail,
            string? targetMeetingId,
            string resultCode,
            string? message,
            string? ipAddress)
        {
            _context.AuditZoomActionLogs.Add(new AuditZoomActionLog
            {
                ActorUserId = actorUserId,
                ActionType = actionType,
                TargetEmail = targetEmail,
                TargetMeetingId = targetMeetingId,
                ResultCode = resultCode,
                Message = Truncate(message, 2000),
                RequestIpAddress = NormalizeIp(ipAddress),
                CreatedAt = DateTime.UtcNow
            });
        }

        private async Task EnsureStatusCatalogAsync(CancellationToken cancellationToken)
        {
            var requiredStatuses = new Dictionary<byte, (string Name, string DisplayName, bool IsTerminal)>
            {
                { (byte)ZoomProvisioningStatus.None, ("None", "Not Provisioned", false) },
                { (byte)ZoomProvisioningStatus.ProvisioningPending, ("ProvisioningPending", "Provisioning Pending", false) },
                { (byte)ZoomProvisioningStatus.ActivationPending, ("ActivationPending", "Activation Pending", false) },
                { (byte)ZoomProvisioningStatus.Active, ("Active", "Active", true) },
                { (byte)ZoomProvisioningStatus.Failed, ("Failed", "Failed", true) },
                { (byte)ZoomProvisioningStatus.ManualSupportRequired, ("ManualSupportRequired", "Manual Support Required", true) }
            };

            var statusChanged = false;
            var existingStatuses = await _context.ZoomStatuses
                .ToDictionaryAsync(x => x.ZoomStatusId, cancellationToken);

            foreach (var kvp in requiredStatuses)
            {
                if (existingStatuses.ContainsKey(kvp.Key))
                {
                    continue;
                }

                _context.ZoomStatuses.Add(new ZoomStatus
                {
                    ZoomStatusId = kvp.Key,
                    Name = kvp.Value.Name,
                    DisplayName = kvp.Value.DisplayName,
                    IsTerminal = kvp.Value.IsTerminal,
                    IsActive = true
                });
                statusChanged = true;
            }

            if (statusChanged)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            var requiredRules = new[]
            {
                Rule((byte)ZoomProvisioningStatus.None, (byte)ZoomProvisioningStatus.ProvisioningPending, ActionProvision, "Initial provisioning"),
                Rule((byte)ZoomProvisioningStatus.Failed, (byte)ZoomProvisioningStatus.ProvisioningPending, ActionRetryProvision, "Retry provisioning"),
                Rule((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.ActivationPending, ActionApiAccepted, "Zoom accepted request"),
                Rule((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.Active, ActionApiConflict, "Conflict means existing active user"),
                Rule((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.Failed, ActionApiRateLimit, "Rate limit failure"),
                Rule((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.Failed, ActionApiError, "API failure"),
                Rule((byte)ZoomProvisioningStatus.ActivationPending, (byte)ZoomProvisioningStatus.Active, ActionWebhookActivated, "Activation webhook"),
                Rule((byte)ZoomProvisioningStatus.None, (byte)ZoomProvisioningStatus.Active, ActionSyncExternalLookup, "External discovery in active state"),
                Rule((byte)ZoomProvisioningStatus.None, (byte)ZoomProvisioningStatus.Active, ActionWebhookDiscovery, "Webhook discovered active account")
            };

            var existingRuleKeys = await _context.ZoomStatusTransitionRules
                .Select(x => new
                {
                    x.FromStatusId,
                    x.ToStatusId,
                    ActionType = x.ActionType.ToUpper()
                })
                .ToListAsync(cancellationToken);

            var ruleChanged = false;
            foreach (var rule in requiredRules)
            {
                var exists = existingRuleKeys.Any(x =>
                    x.FromStatusId == rule.FromStatusId
                    && x.ToStatusId == rule.ToStatusId
                    && x.ActionType == rule.ActionType);
                if (exists)
                {
                    continue;
                }

                _context.ZoomStatusTransitionRules.Add(new ZoomStatusTransitionRule
                {
                    FromStatusId = rule.FromStatusId,
                    ToStatusId = rule.ToStatusId,
                    ActionType = rule.ActionType,
                    Description = rule.Description,
                    IsEnabled = true
                });
                ruleChanged = true;
            }

            if (ruleChanged)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<bool> IsTransitionAllowedAsync(
            byte fromStatusId,
            byte toStatusId,
            string actionType,
            CancellationToken cancellationToken)
        {
            if (fromStatusId == toStatusId)
            {
                return true;
            }

            var normalizedAction = (actionType ?? string.Empty).Trim().ToUpperInvariant();
            return await _context.ZoomStatusTransitionRules.AnyAsync(
                x => x.FromStatusId == fromStatusId
                    && x.ToStatusId == toStatusId
                    && x.IsEnabled
                    && x.ActionType.ToUpper() == normalizedAction,
                cancellationToken);
        }

        private async Task<ZoomUserLookupResponse> GetZoomUserByEmailAsync(string email, CancellationToken cancellationToken)
        {
            using var client = await CreateZoomHttpClientAsync(cancellationToken);
            using var response = await client.GetAsync($"users/{Uri.EscapeDataString(email)}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ZoomUserLookupResponse
                {
                    Exists = false,
                    HttpStatusCode = (int)response.StatusCode,
                    RawResponse = body
                };
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new ZoomUserLookupResponse
                {
                    Exists = false,
                    IsRateLimited = true,
                    HttpStatusCode = (int)response.StatusCode,
                    RetryAfterSeconds = ParseRetryAfterSeconds(response),
                    ErrorCode = "429",
                    ErrorMessage = ExtractZoomErrorMessage(body, "Zoom rate limit reached during lookup."),
                    RawResponse = body
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ZoomUserLookupResponse
                {
                    Exists = false,
                    HttpStatusCode = (int)response.StatusCode,
                    ErrorCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                    ErrorMessage = ExtractZoomErrorMessage(body, "Zoom lookup failed."),
                    RawResponse = body
                };
            }

            return new ZoomUserLookupResponse
            {
                Exists = true,
                HttpStatusCode = (int)response.StatusCode,
                ZoomUserId = TryExtractJsonString(body, "id"),
                ZoomStatus = TryExtractJsonString(body, "status"),
                RawResponse = body
            };
        }

        private async Task<ZoomCreateUserResponse> CreateZoomUserAutoCreateAsync(
            string email,
            string firstName,
            string lastName,
            int userType,
            CancellationToken cancellationToken)
        {
            using var client = await CreateZoomHttpClientAsync(cancellationToken);
            var payload = new
            {
                action = "autoCreate",
                user_info = new
                {
                    email,
                    first_name = firstName,
                    last_name = lastName,
                    type = userType
                }
            };

            using var response = await client.PostAsJsonAsync("users", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new ZoomCreateUserResponse
                {
                    Success = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ZoomUserId = TryExtractJsonString(body, "id"),
                    RawResponse = body
                };
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new ZoomCreateUserResponse
                {
                    IsConflict = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ZoomUserId = TryExtractJsonString(body, "id"),
                    ErrorCode = "409",
                    ErrorMessage = ExtractZoomErrorMessage(body, "Zoom user already exists."),
                    RawResponse = body
                };
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new ZoomCreateUserResponse
                {
                    IsRateLimited = true,
                    HttpStatusCode = (int)response.StatusCode,
                    RetryAfterSeconds = ParseRetryAfterSeconds(response),
                    ErrorCode = "429",
                    ErrorMessage = ExtractZoomErrorMessage(body, "Zoom rate limit reached."),
                    RawResponse = body
                };
            }

            return new ZoomCreateUserResponse
            {
                HttpStatusCode = (int)response.StatusCode,
                ErrorCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                ErrorMessage = ExtractZoomErrorMessage(body, "Zoom user creation failed."),
                RawResponse = body
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

        private bool IsWebhookSignatureValid(string timestamp, string payloadJson, string signature)
        {
            if (_skipWebhookSignatureValidation)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_webhookSecretToken))
            {
                return false;
            }

            if (!IsTimestampValid(timestamp))
            {
                return false;
            }

            var canonical = $"v0:{timestamp}:{payloadJson}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecretToken));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var expected = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

            return FixedTimeEquals(expected, signature.Trim());
        }

        private bool IsTimestampValid(string timestamp)
        {
            if (_webhookTimestampToleranceSeconds <= 0)
            {
                return true;
            }

            if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            {
                return false;
            }

            var eventTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            var delta = DateTimeOffset.UtcNow - eventTime;
            return Math.Abs(delta.TotalSeconds) <= _webhookTimestampToleranceSeconds;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static ParsedWebhookPayload ParseWebhookPayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return ParsedWebhookPayload.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                var root = document.RootElement;

                var eventType = root.TryGetProperty("event", out var eventEl) && eventEl.ValueKind == JsonValueKind.String
                    ? eventEl.GetString() ?? string.Empty
                    : string.Empty;
                var eventId = root.TryGetProperty("event_id", out var eventIdEl) && eventIdEl.ValueKind == JsonValueKind.String
                    ? eventIdEl.GetString() ?? string.Empty
                    : string.Empty;

                var email = string.Empty;
                var zoomUserId = string.Empty;
                if (root.TryGetProperty("payload", out var payloadEl)
                    && payloadEl.ValueKind == JsonValueKind.Object
                    && payloadEl.TryGetProperty("object", out var objectEl)
                    && objectEl.ValueKind == JsonValueKind.Object)
                {
                    email = objectEl.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String
                        ? emailEl.GetString() ?? string.Empty
                        : string.Empty;
                    zoomUserId = objectEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                        ? idEl.GetString() ?? string.Empty
                        : string.Empty;
                }

                return new ParsedWebhookPayload
                {
                    EventType = eventType,
                    EventId = eventId,
                    Email = email,
                    ZoomUserId = zoomUserId
                };
            }
            catch
            {
                return ParsedWebhookPayload.Empty;
            }
        }

        private static ZoomProvisioningStatus MapLookupToStatus(ZoomUserLookupResponse response)
        {
            if (response.Exists)
            {
                if (string.Equals(response.ZoomStatus, "active", StringComparison.OrdinalIgnoreCase))
                {
                    return ZoomProvisioningStatus.Active;
                }

                return ZoomProvisioningStatus.ActivationPending;
            }

            if (response.IsRateLimited || !string.IsNullOrWhiteSpace(response.ErrorCode))
            {
                return ZoomProvisioningStatus.Failed;
            }

            return ZoomProvisioningStatus.None;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                _ = new System.Net.Mail.MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeEmailKey(string email)
        {
            return NormalizeEmail(email).ToUpperInvariant();
        }

        private static string NormalizeIp(string? ipAddress)
        {
            return (ipAddress ?? string.Empty).Trim();
        }

        private static string ResolveStatusName(byte statusId)
        {
            return Enum.IsDefined(typeof(ZoomProvisioningStatus), statusId)
                ? Enum.GetName(typeof(ZoomProvisioningStatus), statusId) ?? "Unknown"
                : "Unknown";
        }

        private static string Truncate(string? value, int maxLength)
        {
            var normalized = value ?? string.Empty;
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength);
        }

        private static int ParseRetryAfterSeconds(HttpResponseMessage response)
        {
            var header = response.Headers.TryGetValues("Retry-After", out var values)
                ? values.FirstOrDefault()
                : null;
            return int.TryParse(header, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? Math.Max(0, seconds)
                : 0;
        }

        private static string TryExtractJsonString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(propertyName, out var el))
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        return el.GetString() ?? string.Empty;
                    }

                    return el.ToString();
                }
            }
            catch
            {
                // ignored intentionally
            }

            return string.Empty;
        }

        private static string ExtractZoomErrorMessage(string body, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return fallbackMessage;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var messageElement)
                    && messageElement.ValueKind == JsonValueKind.String)
                {
                    var message = messageElement.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message.Trim();
                    }
                }
            }
            catch
            {
                // ignored intentionally
            }

            return fallbackMessage;
        }

        private static string ComputeSha256Hex(string payload)
        {
            var content = payload ?? string.Empty;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool IsUniqueEventConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException?.Message?.Contains("UX_zoom_WebhookInbox_EventId", StringComparison.OrdinalIgnoreCase) == true
                   || exception.Message.Contains("UX_zoom_WebhookInbox_EventId", StringComparison.OrdinalIgnoreCase);
        }

        private static ZoomAccountStatusResult AccountStatusFailure(string code, string message)
        {
            return new ZoomAccountStatusResult
            {
                Success = false,
                Code = code,
                Message = message
            };
        }

        private static ZoomProvisionUserResult ProvisionFailure(
            string code,
            string message,
            Guid? userProvisioningId = null,
            string email = "",
            string statusName = "")
        {
            return new ZoomProvisionUserResult
            {
                Success = false,
                Code = code,
                Message = message,
                UserProvisioningId = userProvisioningId,
                Email = email ?? string.Empty,
                StatusName = statusName ?? string.Empty
            };
        }

        private static ZoomCallbackResult CallbackFailure(string code, string message)
        {
            return new ZoomCallbackResult
            {
                Success = false,
                Code = code,
                Message = message
            };
        }

        private static (byte FromStatusId, byte ToStatusId, string ActionType, string Description) Rule(
            byte from,
            byte to,
            string actionType,
            string description)
        {
            return (from, to, actionType.ToUpperInvariant(), description);
        }

        private sealed class ZoomUserLookupResponse
        {
            public bool Exists { get; init; }
            public bool IsRateLimited { get; init; }
            public int HttpStatusCode { get; init; }
            public int RetryAfterSeconds { get; init; }
            public string ZoomUserId { get; init; } = string.Empty;
            public string ZoomStatus { get; init; } = string.Empty;
            public string ErrorCode { get; init; } = string.Empty;
            public string ErrorMessage { get; init; } = string.Empty;
            public string RawResponse { get; init; } = string.Empty;
        }

        private sealed class ZoomCreateUserResponse
        {
            public bool Success { get; init; }
            public bool IsConflict { get; init; }
            public bool IsRateLimited { get; init; }
            public int HttpStatusCode { get; init; }
            public int RetryAfterSeconds { get; init; }
            public string ZoomUserId { get; init; } = string.Empty;
            public string ErrorCode { get; init; } = string.Empty;
            public string ErrorMessage { get; init; } = string.Empty;
            public string RawResponse { get; init; } = string.Empty;
        }

        private sealed class ParsedWebhookPayload
        {
            public static ParsedWebhookPayload Empty { get; } = new ParsedWebhookPayload();

            public string EventId { get; init; } = string.Empty;
            public string EventType { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public string ZoomUserId { get; init; } = string.Empty;
        }
    }
}
