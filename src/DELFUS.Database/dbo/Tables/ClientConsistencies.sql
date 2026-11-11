CREATE TABLE [dbo].[ClientConsistencies] (
    [Id] INT NOT NULL IDENTITY, 
   [CustId]   NVARCHAR (255) NOT NULL,
    [FieldName]    NVARCHAR (255) NULL,
    [SearchText] NVARCHAR (255) NULL,
    [ReplaceText] NVARCHAR (255) NULL, 
   
    CONSTRAINT [PK_ClientConsistencies] PRIMARY KEY ([Id]) 
);

