﻿CREATE TABLE [dbo].[Surveys] (
    [Id] INT NOT NULL IDENTITY(1,1),
    [Name] NVARCHAR(256) NOT NULL,
	[Url] NVARCHAR(512) NULL,
	[Description] NVARCHAR(MAX) NULL,
    [Version] NVARCHAR(64) NOT NULL,
    [IsActive] [bit] NOT NULL CONSTRAINT [DF_Surveys_IsActive] DEFAULT 1,
    [Created] DATETIME NOT NULL CONSTRAINT [DF_Surveys_Created] DEFAULT GETUTCDATE(),
    [Modified] DATETIME NULL,
    [Deleted] DATETIME NULL,
	[Discriminator] NVARCHAR(128) NULL,

    CONSTRAINT [PK_Surveys_Id] PRIMARY KEY ([Id])
)