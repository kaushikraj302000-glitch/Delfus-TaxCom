CREATE TABLE [dbo].[LienMailGroups]
(
	[LienDataId] INT NOT NULL, 
    [MailGroupId] INT NOT NULL,      
    CONSTRAINT [FK_LienMailGroups_Lien] FOREIGN KEY (LienDataId) REFERENCES [LienData](Id), 
    CONSTRAINT [FK_LienMailGroups_MailGroups] FOREIGN KEY (MailGroupId) REFERENCES [MailGroups](Id), 
    CONSTRAINT [PK_LienMailGroups] PRIMARY KEY ([LienDataId], [MailGroupId]) 
)
