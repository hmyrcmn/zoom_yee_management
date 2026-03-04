IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'zoom')
BEGIN
    EXEC('CREATE SCHEMA zoom');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit')
BEGIN
    EXEC('CREATE SCHEMA audit');
END
GO

IF OBJECT_ID('zoom.ZoomStatus', 'U') IS NULL
BEGIN
    CREATE TABLE zoom.ZoomStatus
    (
        ZoomStatusId TINYINT NOT NULL CONSTRAINT PK_zoom_ZoomStatus PRIMARY KEY,
        Name NVARCHAR(64) NOT NULL,
        DisplayName NVARCHAR(128) NOT NULL,
        IsTerminal BIT NOT NULL CONSTRAINT DF_zoom_ZoomStatus_IsTerminal DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_zoom_ZoomStatus_IsActive DEFAULT (1)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_zoom_ZoomStatus_Name'
      AND object_id = OBJECT_ID('zoom.ZoomStatus')
)
BEGIN
    CREATE UNIQUE INDEX UX_zoom_ZoomStatus_Name
    ON zoom.ZoomStatus(Name);
END
GO

IF OBJECT_ID('zoom.ZoomStatusTransitionRule', 'U') IS NULL
BEGIN
    CREATE TABLE zoom.ZoomStatusTransitionRule
    (
        ZoomStatusTransitionRuleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_zoom_TransitionRule PRIMARY KEY,
        FromStatusId TINYINT NOT NULL,
        ToStatusId TINYINT NOT NULL,
        ActionType NVARCHAR(64) NOT NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_zoom_TransitionRule_IsEnabled DEFAULT (1),
        Description NVARCHAR(500) NULL,
        CONSTRAINT FK_zoom_TransitionRule_FromStatus FOREIGN KEY (FromStatusId) REFERENCES zoom.ZoomStatus(ZoomStatusId),
        CONSTRAINT FK_zoom_TransitionRule_ToStatus FOREIGN KEY (ToStatusId) REFERENCES zoom.ZoomStatus(ZoomStatusId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_zoom_TransitionRule'
      AND object_id = OBJECT_ID('zoom.ZoomStatusTransitionRule')
)
BEGIN
    CREATE UNIQUE INDEX UX_zoom_TransitionRule
    ON zoom.ZoomStatusTransitionRule(FromStatusId, ToStatusId, ActionType);
END
GO

IF OBJECT_ID('zoom.UserProvisioning', 'U') IS NULL
BEGIN
    CREATE TABLE zoom.UserProvisioning
    (
        UserProvisioningId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_zoom_UserProvisioning PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NULL,
        Email NVARCHAR(320) NOT NULL,
        EmailNormalized NVARCHAR(320) NOT NULL,
        ZoomUserId NVARCHAR(64) NOT NULL CONSTRAINT DF_zoom_UserProvisioning_ZoomUserId DEFAULT (N''),
        ZoomStatusId TINYINT NOT NULL,
        LastErrorCode NVARCHAR(128) NULL,
        LastErrorMessage NVARCHAR(2000) NULL,
        LastSyncedAt DATETIME2(3) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_zoom_UserProvisioning_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_zoom_UserProvisioning_AuthUsers FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
        CONSTRAINT FK_zoom_UserProvisioning_Status FOREIGN KEY (ZoomStatusId) REFERENCES zoom.ZoomStatus(ZoomStatusId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_zoom_UserProvisioning_EmailNormalized'
      AND object_id = OBJECT_ID('zoom.UserProvisioning')
)
BEGIN
    CREATE UNIQUE INDEX UX_zoom_UserProvisioning_EmailNormalized
    ON zoom.UserProvisioning(EmailNormalized);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_zoom_UserProvisioning_UserId'
      AND object_id = OBJECT_ID('zoom.UserProvisioning')
)
BEGIN
    CREATE INDEX IX_zoom_UserProvisioning_UserId
    ON zoom.UserProvisioning(UserId);
END
GO

IF OBJECT_ID('zoom.UserProvisioningHistory', 'U') IS NULL
BEGIN
    CREATE TABLE zoom.UserProvisioningHistory
    (
        UserProvisioningHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_zoom_UserProvisioningHistory PRIMARY KEY,
        UserProvisioningId UNIQUEIDENTIFIER NOT NULL,
        FromStatusId TINYINT NULL,
        ToStatusId TINYINT NOT NULL,
        ActionType NVARCHAR(64) NOT NULL,
        ActorUserId UNIQUEIDENTIFIER NULL,
        Source NVARCHAR(64) NOT NULL,
        HttpStatusCode INT NULL,
        Message NVARCHAR(2000) NULL,
        RawResponse NVARCHAR(4000) NULL,
        RequestIpAddress NVARCHAR(45) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_zoom_UserProvisioningHistory_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_zoom_UserProvisioningHistory_Provisioning FOREIGN KEY (UserProvisioningId) REFERENCES zoom.UserProvisioning(UserProvisioningId),
        CONSTRAINT FK_zoom_UserProvisioningHistory_FromStatus FOREIGN KEY (FromStatusId) REFERENCES zoom.ZoomStatus(ZoomStatusId),
        CONSTRAINT FK_zoom_UserProvisioningHistory_ToStatus FOREIGN KEY (ToStatusId) REFERENCES zoom.ZoomStatus(ZoomStatusId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_zoom_UserProvisioningHistory_ProvisioningCreatedAt'
      AND object_id = OBJECT_ID('zoom.UserProvisioningHistory')
)
BEGIN
    CREATE INDEX IX_zoom_UserProvisioningHistory_ProvisioningCreatedAt
    ON zoom.UserProvisioningHistory(UserProvisioningId, CreatedAt DESC);
END
GO

IF OBJECT_ID('zoom.WebhookInbox', 'U') IS NULL
BEGIN
    CREATE TABLE zoom.WebhookInbox
    (
        WebhookInboxId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_zoom_WebhookInbox PRIMARY KEY,
        EventId NVARCHAR(128) NOT NULL,
        EventType NVARCHAR(128) NOT NULL,
        Signature NVARCHAR(300) NOT NULL,
        [Timestamp] NVARCHAR(64) NOT NULL,
        PayloadHash NVARCHAR(128) NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        RequestIpAddress NVARCHAR(45) NULL,
        ProcessingResult NVARCHAR(64) NULL,
        ReceivedAt DATETIME2(3) NOT NULL CONSTRAINT DF_zoom_WebhookInbox_ReceivedAt DEFAULT SYSUTCDATETIME(),
        ProcessedAt DATETIME2(3) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        RowVersion ROWVERSION NOT NULL
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_zoom_WebhookInbox_EventId'
      AND object_id = OBJECT_ID('zoom.WebhookInbox')
)
BEGIN
    CREATE UNIQUE INDEX UX_zoom_WebhookInbox_EventId
    ON zoom.WebhookInbox(EventId);
END
GO

IF OBJECT_ID('zoom.Meeting', 'U') IS NULL
BEGIN
    CREATE TABLE zoom.Meeting
    (
        MeetingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_zoom_Meeting PRIMARY KEY,
        OwnerUserId UNIQUEIDENTIFIER NOT NULL,
        UserProvisioningId UNIQUEIDENTIFIER NULL,
        ZoomMeetingId NVARCHAR(64) NOT NULL,
        Topic NVARCHAR(255) NOT NULL,
        Agenda NVARCHAR(2000) NOT NULL,
        StartTimeUtc DATETIME2(3) NULL,
        DurationMinutes INT NULL,
        Timezone NVARCHAR(64) NULL,
        JoinUrl NVARCHAR(1000) NOT NULL,
        StartUrl NVARCHAR(1000) NOT NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_zoom_Meeting_IsDeleted DEFAULT (0),
        DeletedAt DATETIME2(3) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_zoom_Meeting_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_zoom_Meeting_Owner FOREIGN KEY (OwnerUserId) REFERENCES auth.Users(UserId),
        CONSTRAINT FK_zoom_Meeting_Provisioning FOREIGN KEY (UserProvisioningId) REFERENCES zoom.UserProvisioning(UserProvisioningId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_zoom_Meeting_Owner_CreatedAt'
      AND object_id = OBJECT_ID('zoom.Meeting')
)
BEGIN
    CREATE INDEX IX_zoom_Meeting_Owner_CreatedAt
    ON zoom.Meeting(OwnerUserId, CreatedAt DESC);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_zoom_Meeting_ZoomMeetingId'
      AND object_id = OBJECT_ID('zoom.Meeting')
)
BEGIN
    CREATE INDEX IX_zoom_Meeting_ZoomMeetingId
    ON zoom.Meeting(ZoomMeetingId);
END
GO

IF OBJECT_ID('audit.ZoomActionLog', 'U') IS NULL
BEGIN
    CREATE TABLE audit.ZoomActionLog
    (
        AuditZoomActionLogId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_audit_ZoomActionLog PRIMARY KEY,
        ActorUserId UNIQUEIDENTIFIER NULL,
        ActionType NVARCHAR(64) NOT NULL,
        TargetEmail NVARCHAR(320) NULL,
        TargetMeetingId NVARCHAR(64) NULL,
        RequestIpAddress NVARCHAR(45) NULL,
        ResultCode NVARCHAR(64) NOT NULL,
        Message NVARCHAR(2000) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_audit_ZoomActionLog_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_audit_ZoomActionLog_CreatedAt'
      AND object_id = OBJECT_ID('audit.ZoomActionLog')
)
BEGIN
    CREATE INDEX IX_audit_ZoomActionLog_CreatedAt
    ON audit.ZoomActionLog(CreatedAt DESC);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_audit_ZoomActionLog_ActorUserId'
      AND object_id = OBJECT_ID('audit.ZoomActionLog')
)
BEGIN
    CREATE INDEX IX_audit_ZoomActionLog_ActorUserId
    ON audit.ZoomActionLog(ActorUserId);
END
GO

MERGE zoom.ZoomStatus AS target
USING (VALUES
    (CAST(0 AS TINYINT), N'None', N'Not Provisioned', CAST(0 AS BIT), CAST(1 AS BIT)),
    (CAST(1 AS TINYINT), N'ProvisioningPending', N'Provisioning Pending', CAST(0 AS BIT), CAST(1 AS BIT)),
    (CAST(2 AS TINYINT), N'ActivationPending', N'Activation Pending', CAST(0 AS BIT), CAST(1 AS BIT)),
    (CAST(3 AS TINYINT), N'Active', N'Active', CAST(1 AS BIT), CAST(1 AS BIT)),
    (CAST(4 AS TINYINT), N'Failed', N'Failed', CAST(1 AS BIT), CAST(1 AS BIT)),
    (CAST(5 AS TINYINT), N'ManualSupportRequired', N'Manual Support Required', CAST(1 AS BIT), CAST(1 AS BIT))
) AS source (ZoomStatusId, Name, DisplayName, IsTerminal, IsActive)
ON target.ZoomStatusId = source.ZoomStatusId
WHEN MATCHED THEN
    UPDATE SET Name = source.Name, DisplayName = source.DisplayName, IsTerminal = source.IsTerminal, IsActive = source.IsActive
WHEN NOT MATCHED THEN
    INSERT (ZoomStatusId, Name, DisplayName, IsTerminal, IsActive)
    VALUES (source.ZoomStatusId, source.Name, source.DisplayName, source.IsTerminal, source.IsActive);
GO

MERGE zoom.ZoomStatusTransitionRule AS target
USING (VALUES
    (CAST(0 AS TINYINT), CAST(1 AS TINYINT), N'PROVISION', N'Initial provisioning'),
    (CAST(4 AS TINYINT), CAST(1 AS TINYINT), N'RETRY_PROVISION', N'Retry provisioning'),
    (CAST(1 AS TINYINT), CAST(2 AS TINYINT), N'API_ACCEPTED', N'Zoom accepted request'),
    (CAST(1 AS TINYINT), CAST(3 AS TINYINT), N'API_CONFLICT', N'Conflict means existing active user'),
    (CAST(1 AS TINYINT), CAST(4 AS TINYINT), N'API_RATE_LIMIT', N'Rate limit failure'),
    (CAST(1 AS TINYINT), CAST(4 AS TINYINT), N'API_ERROR', N'API failure'),
    (CAST(2 AS TINYINT), CAST(3 AS TINYINT), N'WEBHOOK_USER_ACTIVATED', N'Activation webhook'),
    (CAST(0 AS TINYINT), CAST(3 AS TINYINT), N'SYNC_EXTERNAL_LOOKUP', N'External discovery in active state'),
    (CAST(0 AS TINYINT), CAST(3 AS TINYINT), N'WEBHOOK_DISCOVERY', N'Webhook discovered active account')
) AS source (FromStatusId, ToStatusId, ActionType, Description)
ON target.FromStatusId = source.FromStatusId
   AND target.ToStatusId = source.ToStatusId
   AND target.ActionType = source.ActionType
WHEN MATCHED THEN
    UPDATE SET Description = source.Description, IsEnabled = 1
WHEN NOT MATCHED THEN
    INSERT (FromStatusId, ToStatusId, ActionType, IsEnabled, Description)
    VALUES (source.FromStatusId, source.ToStatusId, source.ActionType, 1, source.Description);
GO
