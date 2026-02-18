using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Results;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.Entities.Zoom;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MeetingsController : ControllerBase
    {
        private readonly ToplantiContext _context;
        private readonly IZoom _zoomApi;

        public MeetingsController(ToplantiContext context, IZoom zoomApi)
        {
            _context = context;
            _zoomApi = zoomApi;
        }

        [HttpPost]
        public async Task<ActionResult> CreateMeeting([FromBody] ZoomCreateRequest meetingRequest)
        {
            try
            {
                if (meetingRequest == null)
                {
                    return BadRequest("Meeting request is required.");
                }

                var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
                {
                    return Unauthorized("Gecerli kullanici bulunamadi.");
                }

                EnsureMeetingLogTable();

                var zoomResult = await _zoomApi.CreateZoomMeetingNew(new ZoomAuthRequest(), meetingRequest);
                if (zoomResult == null || !zoomResult.Success || zoomResult.Data == null)
                {
                    return BadRequest(new ErrorDataResult<object>(null, zoomResult?.Message ?? "Zoom toplantisi olusturulamadi."));
                }

                var responseModel = zoomResult.Data;
                LogMeeting(userId, meetingRequest, responseModel);

                return Ok(new SuccessDataResult<ZoomCreatedResponse>(responseModel, "Toplanti kaydedildi."));
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorDataResult<object>(null, $"Toplanti olusturma hatasi: {ex.Message}"));
            }
        }

        private void EnsureMeetingLogTable()
        {
            const string sql = @"
IF OBJECT_ID('dbo.MeetingLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MeetingLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        ZoomMeetingId BIGINT NULL,
        Topic NVARCHAR(500) NULL,
        Agenda NVARCHAR(MAX) NULL,
        StartTime DATETIME2 NULL,
        Duration INT NULL,
        Timezone NVARCHAR(100) NULL,
        JoinUrl NVARCHAR(1000) NULL,
        StartUrl NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END";
            _context.Database.ExecuteSqlRaw(sql);

            const string ensureJoinUrlColumnSql = @"
IF COL_LENGTH('dbo.MeetingLogs', 'JoinUrl') IS NULL
BEGIN
    ALTER TABLE dbo.MeetingLogs ADD JoinUrl NVARCHAR(1000) NULL;
END";
            _context.Database.ExecuteSqlRaw(ensureJoinUrlColumnSql);

            const string ensureStartUrlColumnSql = @"
IF COL_LENGTH('dbo.MeetingLogs', 'StartUrl') IS NULL
BEGIN
    ALTER TABLE dbo.MeetingLogs ADD StartUrl NVARCHAR(1000) NULL;
END";
            _context.Database.ExecuteSqlRaw(ensureStartUrlColumnSql);
        }

        private void LogMeeting(int userId, ZoomCreateRequest request, ZoomCreatedResponse response)
        {
            const string sql = @"
INSERT INTO dbo.MeetingLogs
    (UserId, ZoomMeetingId, Topic, Agenda, StartTime, Duration, Timezone, JoinUrl, StartUrl, CreatedAt)
VALUES
    (@UserId, @ZoomMeetingId, @Topic, @Agenda, @StartTime, @Duration, @Timezone, @JoinUrl, @StartUrl, @CreatedAt)";

            var parameters = new[]
            {
                new SqlParameter("@UserId", userId),
                new SqlParameter("@ZoomMeetingId", response.id > 0 ? response.id : DBNull.Value),
                new SqlParameter("@Topic", (object?)request.topic ?? DBNull.Value),
                new SqlParameter("@Agenda", (object?)request.agenda ?? DBNull.Value),
                new SqlParameter("@StartTime", request.start_time == default ? DBNull.Value : request.start_time),
                new SqlParameter("@Duration", request.duration),
                new SqlParameter("@Timezone", (object?)request.timezone ?? DBNull.Value),
                new SqlParameter("@JoinUrl", (object?)response.join_url ?? DBNull.Value),
                new SqlParameter("@StartUrl", (object?)response.start_url ?? DBNull.Value),
                new SqlParameter("@CreatedAt", DateTime.UtcNow),
            };

            _context.Database.ExecuteSqlRaw(sql, parameters);
        }
    }
}
