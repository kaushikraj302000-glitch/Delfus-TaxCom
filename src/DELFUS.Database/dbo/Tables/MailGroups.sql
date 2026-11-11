CREATE TABLE [dbo].[MailGroups]
(
	[Id] INT NOT NULL IDENTITY (1, 1) PRIMARY KEY,     
    [BatchId] INT NULL,
    [LoanId] NVARCHAR(255) NULL,
    [CustomerId] NVARCHAR(255) NULL,
    [DocumentType] nvarchar(255) NULL,
    [PdfFileName] nvarchar(255) NULL,
    [LogoFileName] nvarchar(255) NULL,
    [FirstName] NVARCHAR(255) NULL, 
    [LastName] NVARCHAR(255) NULL, 
    [AddressLine1] NVARCHAR(255) NULL, 
    [AddressLine2] NVARCHAR(255) NULL, 
    [City] NVARCHAR(255) NULL, 
    [State] NVARCHAR(255) NULL, 
    [Zip] NVARCHAR(255) NULL, 
    [Country] NVARCHAR(255) NULL,
    [GroupType] VARCHAR(255) NOT NULL
)
