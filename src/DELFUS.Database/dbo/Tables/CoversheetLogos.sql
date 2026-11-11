CREATE TABLE [dbo].[CoversheetLogos] (
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [OuterEnvelope] NVARCHAR (255) NULL,
    [CustomerId]    NVARCHAR (255) NULL,
    [TemplateCode]  NVARCHAR (255) NULL,
    [FileName]      NVARCHAR (255) NULL,
    CONSTRAINT [PK_CoversheetLogos] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_OuterEnvelope_CustomerId_TemplateCode]
    ON [dbo].[CoversheetLogos]([OuterEnvelope] ASC, [CustomerId] ASC, [TemplateCode] ASC) WITH (FILLFACTOR = 90);

