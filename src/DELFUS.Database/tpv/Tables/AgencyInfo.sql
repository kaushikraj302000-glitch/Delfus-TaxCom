CREATE TABLE [tpv].[AgencyInfo] (
    [Id]                  INT            IDENTITY (1, 1) NOT NULL,
    [BatchId]             INT            NULL,
    [RecordStatus]        NVARCHAR (255) NULL,
    [DisqualifyReason]    NVARCHAR (MAX) NULL,
    [PdfFileName]         NVARCHAR (255) NULL,
    [AgencyNumber]        NVARCHAR (255) NULL,
    [AgencyName]          NVARCHAR (512) NULL,
    [TaxCollectorName]    NVARCHAR (255) NULL,
    [TaxCollectorAddress] NVARCHAR (255) NULL,
    [TaxCollectorCity]    NVARCHAR (255) NULL,
    [TaxCollectorState]   NVARCHAR (255) NULL,
    [TaxCollectorZip]     NVARCHAR (255) NULL,
    [ELD]                 NVARCHAR (255) NULL,
    [DueDate]             NVARCHAR (255) NULL,
    CONSTRAINT [PK_AgencyInfo] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AgencyInfo_Batches] FOREIGN KEY ([BatchId]) REFERENCES [tpv].[Batches] ([Id])
);



