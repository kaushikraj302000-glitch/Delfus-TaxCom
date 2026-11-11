CREATE TABLE [dbo].[DocumentTypes] (
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [DocumentName]  NVARCHAR (255) NULL,
    [OuterEnvelope] NVARCHAR (255) NULL,
    [BREEnvelope]   NVARCHAR (255) NULL,
    [IsColor]       BIT            NULL,
    [IsFedEx] BIT NULL, 
    CONSTRAINT [PK_DocumentTypes] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_OuterEnvelope_BREEnvelope_IsColor_IsFedEx]
    ON [dbo].[DocumentTypes]([OuterEnvelope] ASC, [BREEnvelope] ASC, [IsColor] ASC,[IsFedEx] ASC) WITH (FILLFACTOR = 90);

