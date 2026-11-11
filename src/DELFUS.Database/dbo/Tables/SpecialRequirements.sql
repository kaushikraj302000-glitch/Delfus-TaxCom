CREATE TABLE [dbo].[SpecialRequirements]
(
    [Id] int NOT NULL IDENTITY, 
    [CustId] VARCHAR(255) NOT NULL, 
    [TemplateCode] VARCHAR(255) NOT NULL, 
    [SpecialReq] VARCHAR(255) NOT NULL, 
    CONSTRAINT [PK_SpecialRequirements] PRIMARY KEY ([Id])
)
