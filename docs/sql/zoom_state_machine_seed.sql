/*
  Zoom provisioning state machine seed script
  Target DB objects:
    - [zoom].[ZoomStatus]
    - [zoom].[ZoomStatusTransitionRule]
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF SCHEMA_ID(N'zoom') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [zoom]');
END;

IF OBJECT_ID(N'[zoom].[ZoomStatus]', N'U') IS NULL
BEGIN
    THROW 50001, 'Missing table [zoom].[ZoomStatus]. Run migrations before seeding.', 1;
END;

IF OBJECT_ID(N'[zoom].[ZoomStatusTransitionRule]', N'U') IS NULL
BEGIN
    THROW 50002, 'Missing table [zoom].[ZoomStatusTransitionRule]. Run migrations before seeding.', 1;
END;

BEGIN TRANSACTION;

MERGE [zoom].[ZoomStatus] AS target
USING
(
    VALUES
        (CAST(0 AS tinyint), N'None',                 N'None',                    CAST(0 AS bit), CAST(1 AS bit)),
        (CAST(1 AS tinyint), N'ProvisioningPending',  N'Provisioning Pending',    CAST(0 AS bit), CAST(1 AS bit)),
        (CAST(2 AS tinyint), N'ActivationPending',    N'Activation Pending',      CAST(0 AS bit), CAST(1 AS bit)),
        (CAST(3 AS tinyint), N'Active',               N'Active',                  CAST(0 AS bit), CAST(1 AS bit)),
        (CAST(4 AS tinyint), N'Failed',               N'Failed',                  CAST(0 AS bit), CAST(1 AS bit)),
        (CAST(5 AS tinyint), N'ManualSupportRequired',N'Manual Support Required', CAST(1 AS bit), CAST(1 AS bit))
) AS src ([ZoomStatusId], [Name], [DisplayName], [IsTerminal], [IsActive])
ON target.[ZoomStatusId] = src.[ZoomStatusId]
WHEN MATCHED THEN
    UPDATE SET
        target.[Name] = src.[Name],
        target.[DisplayName] = src.[DisplayName],
        target.[IsTerminal] = src.[IsTerminal],
        target.[IsActive] = src.[IsActive]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ZoomStatusId], [Name], [DisplayName], [IsTerminal], [IsActive])
    VALUES (src.[ZoomStatusId], src.[Name], src.[DisplayName], src.[IsTerminal], src.[IsActive]);

;WITH RequiredRules AS
(
    SELECT CAST(0 AS tinyint) AS FromStatusId, CAST(1 AS tinyint) AS ToStatusId, N'PROVISION'               AS ActionType, N'Initial provisioning request.' AS Description, CAST(1 AS bit) AS IsEnabled
    UNION ALL SELECT CAST(4 AS tinyint), CAST(1 AS tinyint), N'RETRY_PROVISION',        N'Retry after failure.', CAST(1 AS bit)
    UNION ALL SELECT CAST(1 AS tinyint), CAST(2 AS tinyint), N'API_ACCEPTED',           N'Zoom accepted autoCreate.', CAST(1 AS bit)
    UNION ALL SELECT CAST(1 AS tinyint), CAST(3 AS tinyint), N'API_CONFLICT',           N'Zoom returned conflict; account exists.', CAST(1 AS bit)
    UNION ALL SELECT CAST(1 AS tinyint), CAST(4 AS tinyint), N'API_RATE_LIMIT',         N'Zoom rate limit hit.', CAST(1 AS bit)
    UNION ALL SELECT CAST(1 AS tinyint), CAST(4 AS tinyint), N'API_ERROR',              N'Zoom API error.', CAST(1 AS bit)
    UNION ALL SELECT CAST(2 AS tinyint), CAST(3 AS tinyint), N'WEBHOOK_USER_ACTIVATED', N'Webhook confirms activation.', CAST(1 AS bit)
    UNION ALL SELECT CAST(0 AS tinyint), CAST(3 AS tinyint), N'SYNC_EXTERNAL_LOOKUP',   N'External lookup found active account.', CAST(1 AS bit)
    UNION ALL SELECT CAST(0 AS tinyint), CAST(3 AS tinyint), N'WEBHOOK_DISCOVERY',      N'Webhook discovered active account.', CAST(1 AS bit)
    UNION ALL SELECT CAST(4 AS tinyint), CAST(5 AS tinyint), N'MANUAL_SUPPORT_ESCALATE',N'Escalate failed provisioning to IT.', CAST(1 AS bit)
    UNION ALL SELECT CAST(2 AS tinyint), CAST(5 AS tinyint), N'MANUAL_SUPPORT_ESCALATE',N'Activation timeout escalation.', CAST(1 AS bit)
)
MERGE [zoom].[ZoomStatusTransitionRule] AS target
USING RequiredRules AS src
ON target.[FromStatusId] = src.[FromStatusId]
   AND target.[ToStatusId] = src.[ToStatusId]
   AND UPPER(target.[ActionType]) = UPPER(src.[ActionType])
WHEN MATCHED THEN
    UPDATE SET
        target.[Description] = src.[Description],
        target.[IsEnabled] = src.[IsEnabled]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([FromStatusId], [ToStatusId], [ActionType], [IsEnabled], [Description])
    VALUES (src.[FromStatusId], src.[ToStatusId], src.[ActionType], src.[IsEnabled], src.[Description]);

COMMIT TRANSACTION;
