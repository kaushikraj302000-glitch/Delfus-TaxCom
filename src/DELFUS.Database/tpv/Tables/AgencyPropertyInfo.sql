CREATE TABLE [tpv].[AgencyPropertyInfo] (
    [Id]                    INT            IDENTITY (1, 1) NOT NULL,
    [BatchId]               INT            NULL,
    [AgencyId]              INT            NULL,
    [AgencyNumber]          NVARCHAR (255) NULL,
    [AgencyName]            NVARCHAR (255) NULL,
    [ParcelNumber]          NVARCHAR (255) NULL,
    [PropertyName]          NVARCHAR (255) NULL,
    [PropertyStreetAddress] NVARCHAR (512) NULL,
    [KeyNo]                 NVARCHAR (255) NULL,
    CONSTRAINT [PK_AgencyPropertyInfo] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AgencyPropertyInfo_AgencyInfo] FOREIGN KEY ([AgencyId]) REFERENCES [tpv].[AgencyInfo] ([Id]),
    CONSTRAINT [FK_AgencyPropertyInfo_Batches] FOREIGN KEY ([BatchId]) REFERENCES [tpv].[Batches] ([Id])
);



