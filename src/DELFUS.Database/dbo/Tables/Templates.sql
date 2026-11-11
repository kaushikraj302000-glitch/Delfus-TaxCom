CREATE TABLE [dbo].[Templates]
(
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FileName] [varchar](255) NOT NULL,
	[FilePath] [varchar](255) NOT NULL,
	[ModifiedBy] [nvarchar](500) NOT NULL,
	[ModifiedOn] [datetime] NOT NULL,
	[Status] [nvarchar](250) NOT NULL,
	[Reason] [nvarchar](1024) NULL,
 CONSTRAINT [PK_Templates] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 90) ON [PRIMARY]
) ON [PRIMARY]
