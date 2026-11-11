CREATE TABLE [Schema].[Request] (
    [Id]                   INT             NOT NULL,
    [FieldName]            NVARCHAR (255)  NULL,
    [TemplateFieldMapName] NVARCHAR (255)  NULL,
    [RecordType]           NCHAR (10)      NULL,
    [StartPosition]        INT             NOT NULL,
    [Length]               INT             NOT NULL,
    [IsActive]             BIT             NOT NULL,
    [Description]          NVARCHAR (2000) NULL,
    [DataType]             NVARCHAR (255)  NULL,
    [DataFormatTemplate]   NVARCHAR (255)  NULL,
    CONSTRAINT [PK_SchemaRequest] PRIMARY KEY CLUSTERED ([Id] ASC)
);

