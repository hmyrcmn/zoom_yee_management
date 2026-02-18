using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.Core.Utilities.Results;
using Toplanti.Entities.Zoom;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MeetingsController : ControllerBase
    {
        private readonly ToplantiContext _context;

        public MeetingsController(ToplantiContext context)
        {
            _context = context;
        }

        [HttpPost]
        public ActionResult CreateMeeting([FromBody] ZoomCreateRequest meetingRequest)
        {
            if (meetingRequest == null)
            {
                return BadRequest("Meeting request is required.");
            }

            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                return Unauthorized("Geçerli kullanıcı bulunamadı.");
            }

            EnsureMeetingLogTable();
            var responseModel = BuildMeetingResponse(meetingRequest);
            LogMeeting(userId, meetingRequest, responseModel);

            return Ok(new SuccessDataResult<ZoomCreatedResponse>(responseModel, "Toplantı kaydedildi."));
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

        private ZoomCreatedResponse BuildMeetingResponse(ZoomCreateRequest request)
        {
            var now = DateTime.UtcNow;
            return new ZoomCreatedResponse
            {
                id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                topic = request.topic ?? string.Empty,
                agenda = request.agenda ?? string.Empty,
                duration = request.duration,
                timezone = request.timezone ?? "UTC",
                start_time = request.start_time == default ? now : request.start_time,
                created_at = now,
                join_url = string.Empty,
                start_url = string.Empty,
                type = request.type,
                status = "scheduled"
            };
        }
    }
}
