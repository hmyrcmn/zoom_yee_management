IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'auth')
BEGIN
    EXEC('CREATE SCHEMA auth');
END
GO

IF OBJECT_ID('auth.Users', 'U') IS NULL
BEGIN
    CREATE TABLE auth.Users
    (
        UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_auth_Users PRIMARY KEY,
        Email NVARCHAR(320) NOT NULL,
        EmailNormalized NVARCHAR(320) NOT NULL,
        DisplayName NVARCHAR(200) NULL,
        Department NVARCHAR(128) NULL,
        IsInternal BIT NOT NULL CONSTRAINT DF_auth_Users_IsInternal DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_auth_Users_IsActive DEFAULT (1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_auth_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NULL,
        RowVersion ROWVERSION NOT NULL
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_auth_Users_EmailNormalized'
      AND object_id = OBJECT_ID('auth.Users')
)
BEGIN
    CREATE UNIQUE INDEX UX_auth_Users_EmailNormalized
    ON auth.Users(EmailNormalized);
END
GO

IF OBJECT_ID('auth.OtpChallenge', 'U') IS NULL
BEGIN
    CREATE TABLE auth.OtpChallenge
    (
        OtpChallengeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_auth_OtpChallenge PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NULL,
        EmailNormalized NVARCHAR(320) NOT NULL,
        Purpose TINYINT NOT NULL,
        OtpCodeHash VARBINARY(64) NOT NULL,
        OtpCodeSalt VARBINARY(64) NOT NULL,
        AttemptCount SMALLINT NOT NULL CONSTRAINT DF_auth_OtpChallenge_AttemptCount DEFAULT (0),
        MaxAttempts SMALLINT NOT NULL CONSTRAINT DF_auth_OtpChallenge_MaxAttempts DEFAULT (5),
        ExpiresAt DATETIME2(3) NOT NULL,
        ConsumedAt DATETIME2(3) NULL,
        DeliveryChannel TINYINT NOT NULL CONSTRAINT DF_auth_OtpChallenge_DeliveryChannel DEFAULT (1),
        RequestIpAddress NVARCHAR(45) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_auth_OtpChallenge_CreatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_auth_OtpChallenge_Users
            FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_auth_OtpChallenge_EmailPurposeCreated'
      AND object_id = OBJECT_ID('auth.OtpChallenge')
)
BEGIN
    CREATE INDEX IX_auth_OtpChallenge_EmailPurposeCreated
    ON auth.OtpChallenge(EmailNormalized, Purpose, CreatedAt DESC);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_auth_OtpChallenge_Active'
      AND object_id = OBJECT_ID('auth.OtpChallenge')
)
BEGIN
    CREATE INDEX IX_auth_OtpChallenge_Active
    ON auth.OtpChallenge(EmailNormalized, Purpose, ExpiresAt)
    WHERE ConsumedAt IS NULL;
END
GO
