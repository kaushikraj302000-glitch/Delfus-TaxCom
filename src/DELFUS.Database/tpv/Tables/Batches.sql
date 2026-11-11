CREATE TABLE [tpv].[Batches] (
    [Id]                     INT            IDENTITY (1, 1) NOT NULL,
    [BatchName]              NVARCHAR (255) NULL,
    [ValidationStatus]       VARCHAR (255)  NULL,
    [ValidationErrors]       VARCHAR (MAX)  NULL,
    [PipelineStatus]         VARCHAR (255)  NULL,
    [IsDownloaded]           AS             (case when [DownloadedOn] IS NULL then (0) else (1) end),
    [DownloadedOn]           DATETIME       NULL,
    [IsParsed]               AS             (case when [ParsedOn] IS NULL then (0) else (1) end),
    [ParsedOn]               DATETIME       NULL,
    [IsValidated]            AS             (case when [ValidatedOn] IS NULL then (0) else (1) end),
    [ValidatedOn]            DATETIME       NULL,
    [IsAcknowledged]         AS             (case when [AcknowledgedOn] IS NULL then (0) else (1) end),
    [AcknowledgedOn]         DATETIME       NULL,
    [IsLetterGenerated]      AS             (case when [LetterGeneratedOn] IS NULL then (0) else (1) end),
    [LetterGeneratedOn]      DATETIME       NULL,
    [IsSubmittedToAuthoMail] AS             (case when [SubmittedToAuthoMailOn] IS NULL then (0) else (1) end),
    [SubmittedToAuthoMailOn] DATETIME       NULL,
    [IsRejected]             AS             (case when [RejectedOn] IS NULL then (0) else (1) end),
    [RejectedOn]             DATETIME       NULL,
    CONSTRAINT [PK_TPVBatches] PRIMARY KEY CLUSTERED ([Id] ASC)
);

