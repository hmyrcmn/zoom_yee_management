

using Microsoft.EntityFrameworkCore;
using Toplanti.Core.Entities.Concrete;
using Microsoft.Extensions.Configuration;
using Toplanti.Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Contexts
{
    public class ToplantiContext:DbContext
    {
        private readonly IConfiguration? _configuration;

        public ToplantiContext()
        {
        }

        public ToplantiContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ToplantiContext(DbContextOptions<ToplantiContext> options, IConfiguration configuration) : base(options)
        {
            _configuration = configuration;
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<OperationClaim> OperationClaims { get; set; } = null!;
        public DbSet<UserOperationClaim> UserOperationClaims { get; set; } = null!;
        public DbSet<AuthUser> AuthUsers { get; set; } = null!;
        public DbSet<AuthOtpChallenge> AuthOtpChallenges { get; set; } = null!;
        public DbSet<ZoomStatus> ZoomStatuses { get; set; } = null!;
        public DbSet<ZoomStatusTransitionRule> ZoomStatusTransitionRules { get; set; } = null!;
        public DbSet<ZoomUserProvisioning> ZoomUserProvisionings { get; set; } = null!;
        public DbSet<ZoomUserProvisioningHistory> ZoomUserProvisioningHistories { get; set; } = null!;
        public DbSet<ZoomWebhookInbox> ZoomWebhookInboxes { get; set; } = null!;
        public DbSet<ZoomMeeting> ZoomMeetings { get; set; } = null!;
        public DbSet<AuditZoomActionLog> AuditZoomActionLogs { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var configuration = _configuration ?? ServiceTool.ServiceProvider?.GetService<IConfiguration>();
                if (configuration != null)
                {
                    optionsBuilder.UseSqlServer(configuration.GetConnectionString("ToplantiContext"));
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("UX_Users_Email")
                .HasFilter("[Email] IS NOT NULL");

            modelBuilder.Entity<AuthUser>(entity =>
            {
                entity.ToTable("Users", "auth");
                entity.HasKey(x => x.UserId);

                entity.Property(x => x.Email)
                    .HasMaxLength(320)
                    .IsRequired();

                entity.Property(x => x.EmailNormalized)
                    .HasMaxLength(320)
                    .IsRequired();

                entity.Property(x => x.DisplayName)
                    .HasMaxLength(200);

                entity.Property(x => x.Department)
                    .HasMaxLength(128);

                entity.Property(x => x.IsInternal)
                    .IsRequired();

                entity.Property(x => x.IsActive)
                    .IsRequired();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => x.EmailNormalized)
                    .IsUnique()
                    .HasDatabaseName("UX_auth_Users_EmailNormalized");
            });

            modelBuilder.Entity<AuthOtpChallenge>(entity =>
            {
                entity.ToTable("OtpChallenge", "auth");
                entity.HasKey(x => x.OtpChallengeId);

                entity.Property(x => x.EmailNormalized)
                    .HasMaxLength(320)
                    .IsRequired();

                entity.Property(x => x.Purpose)
                    .IsRequired();

                entity.Property(x => x.OtpCodeHash)
                    .IsRequired();

                entity.Property(x => x.OtpCodeSalt)
                    .IsRequired();

                entity.Property(x => x.AttemptCount)
                    .IsRequired();

                entity.Property(x => x.MaxAttempts)
                    .IsRequired();

                entity.Property(x => x.ExpiresAt)
                    .IsRequired();

                entity.Property(x => x.DeliveryChannel)
                    .IsRequired();

                entity.Property(x => x.RequestIpAddress)
                    .HasMaxLength(45);

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => new { x.EmailNormalized, x.Purpose, x.CreatedAt })
                    .HasDatabaseName("IX_auth_OtpChallenge_EmailPurposeCreated");

                entity.HasIndex(x => new { x.EmailNormalized, x.Purpose, x.ExpiresAt })
                    .HasDatabaseName("IX_auth_OtpChallenge_Active")
                    .HasFilter("[ConsumedAt] IS NULL");

                entity.HasOne(x => x.User)
                    .WithMany(x => x.OtpChallenges)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ZoomStatus>(entity =>
            {
                entity.ToTable("ZoomStatus", "zoom");
                entity.HasKey(x => x.ZoomStatusId);

                entity.Property(x => x.ZoomStatusId)
                    .ValueGeneratedNever();

                entity.Property(x => x.Name)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.DisplayName)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(x => x.IsTerminal)
                    .IsRequired();

                entity.Property(x => x.IsActive)
                    .IsRequired();

                entity.HasIndex(x => x.Name)
                    .IsUnique()
                    .HasDatabaseName("UX_zoom_ZoomStatus_Name");
            });

            modelBuilder.Entity<ZoomStatusTransitionRule>(entity =>
            {
                entity.ToTable("ZoomStatusTransitionRule", "zoom");
                entity.HasKey(x => x.ZoomStatusTransitionRuleId);

                entity.Property(x => x.ActionType)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(500);

                entity.Property(x => x.IsEnabled)
                    .IsRequired();

                entity.HasIndex(x => new { x.FromStatusId, x.ToStatusId, x.ActionType })
                    .IsUnique()
                    .HasDatabaseName("UX_zoom_TransitionRule");

                entity.HasOne(x => x.FromStatus)
                    .WithMany(x => x.OutgoingTransitions)
                    .HasForeignKey(x => x.FromStatusId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ToStatus)
                    .WithMany(x => x.IncomingTransitions)
                    .HasForeignKey(x => x.ToStatusId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ZoomUserProvisioning>(entity =>
            {
                entity.ToTable("UserProvisioning", "zoom");
                entity.HasKey(x => x.UserProvisioningId);

                entity.Property(x => x.Email)
                    .HasMaxLength(320)
                    .IsRequired();

                entity.Property(x => x.EmailNormalized)
                    .HasMaxLength(320)
                    .IsRequired();

                entity.Property(x => x.ZoomUserId)
                    .HasMaxLength(64);

                entity.Property(x => x.LastErrorCode)
                    .HasMaxLength(128);

                entity.Property(x => x.LastErrorMessage)
                    .HasMaxLength(2000);

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => x.EmailNormalized)
                    .IsUnique()
                    .HasDatabaseName("UX_zoom_UserProvisioning_EmailNormalized");

                entity.HasIndex(x => x.UserId)
                    .HasDatabaseName("IX_zoom_UserProvisioning_UserId");

                entity.HasIndex(x => x.ZoomStatusId)
                    .HasDatabaseName("IX_zoom_UserProvisioning_Status");

                entity.HasOne(x => x.User)
                    .WithMany(x => x.ZoomUserProvisionings)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ZoomStatus)
                    .WithMany(x => x.UserProvisionings)
                    .HasForeignKey(x => x.ZoomStatusId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ZoomUserProvisioningHistory>(entity =>
            {
                entity.ToTable("UserProvisioningHistory", "zoom");
                entity.HasKey(x => x.UserProvisioningHistoryId);

                entity.Property(x => x.ActionType)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.Source)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.Message)
                    .HasMaxLength(2000);

                entity.Property(x => x.RawResponse)
                    .HasMaxLength(4000);

                entity.Property(x => x.RequestIpAddress)
                    .HasMaxLength(45);

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.HasIndex(x => new { x.UserProvisioningId, x.CreatedAt })
                    .HasDatabaseName("IX_zoom_UserProvisioningHistory_ProvisioningCreatedAt");

                entity.HasOne(x => x.UserProvisioning)
                    .WithMany(x => x.History)
                    .HasForeignKey(x => x.UserProvisioningId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.FromStatus)
                    .WithMany()
                    .HasForeignKey(x => x.FromStatusId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ToStatus)
                    .WithMany()
                    .HasForeignKey(x => x.ToStatusId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ZoomWebhookInbox>(entity =>
            {
                entity.ToTable("WebhookInbox", "zoom");
                entity.HasKey(x => x.WebhookInboxId);

                entity.Property(x => x.EventId)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(x => x.EventType)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(x => x.Signature)
                    .HasMaxLength(300)
                    .IsRequired();

                entity.Property(x => x.Timestamp)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.PayloadHash)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(x => x.PayloadJson)
                    .HasColumnType("nvarchar(max)")
                    .IsRequired();

                entity.Property(x => x.RequestIpAddress)
                    .HasMaxLength(45);

                entity.Property(x => x.ProcessingResult)
                    .HasMaxLength(64);

                entity.Property(x => x.ReceivedAt)
                    .IsRequired();

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => x.EventId)
                    .IsUnique()
                    .HasDatabaseName("UX_zoom_WebhookInbox_EventId");
            });

            modelBuilder.Entity<ZoomMeeting>(entity =>
            {
                entity.ToTable("Meeting", "zoom");
                entity.HasKey(x => x.MeetingId);

                entity.Property(x => x.ZoomMeetingId)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.Topic)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(x => x.Agenda)
                    .HasMaxLength(2000)
                    .IsRequired();

                entity.Property(x => x.Timezone)
                    .HasMaxLength(64);

                entity.Property(x => x.JoinUrl)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(x => x.StartUrl)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.RowVersion)
                    .IsRowVersion();

                entity.HasIndex(x => new { x.OwnerUserId, x.CreatedAt })
                    .HasDatabaseName("IX_zoom_Meeting_Owner_CreatedAt");

                entity.HasIndex(x => x.ZoomMeetingId)
                    .HasDatabaseName("IX_zoom_Meeting_ZoomMeetingId");

                entity.HasOne(x => x.OwnerUser)
                    .WithMany(x => x.OwnedZoomMeetings)
                    .HasForeignKey(x => x.OwnerUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.UserProvisioning)
                    .WithMany()
                    .HasForeignKey(x => x.UserProvisioningId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<AuditZoomActionLog>(entity =>
            {
                entity.ToTable("ZoomActionLog", "audit");
                entity.HasKey(x => x.AuditZoomActionLogId);

                entity.Property(x => x.ActionType)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.TargetEmail)
                    .HasMaxLength(320);

                entity.Property(x => x.TargetMeetingId)
                    .HasMaxLength(64);

                entity.Property(x => x.RequestIpAddress)
                    .HasMaxLength(45);

                entity.Property(x => x.ResultCode)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(x => x.Message)
                    .HasMaxLength(2000);

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.HasIndex(x => x.CreatedAt)
                    .HasDatabaseName("IX_audit_ZoomActionLog_CreatedAt");

                entity.HasIndex(x => x.ActorUserId)
                    .HasDatabaseName("IX_audit_ZoomActionLog_ActorUserId");
            });

            base.OnModelCreating(modelBuilder);
        }

    }
}
