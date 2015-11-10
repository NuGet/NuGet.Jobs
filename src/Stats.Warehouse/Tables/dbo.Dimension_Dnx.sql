CREATE TABLE [dbo].[Dimension_Dnx]
(
	[Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [DnxVersion]          NVARCHAR (255) NOT NULL,
    [OperatingSystem]     NVARCHAR (128)  NOT NULL,
    [FileName]     NVARCHAR (128)  NOT NULL,
    [LowercasedDnxVersion] AS LOWER([DnxVersion]) PERSISTED,
	[LowercasedOperatingSystem] AS LOWER([OperatingSystem]) PERSISTED,
	[LowercasedFileName] AS LOWER([FileName]) PERSISTED,
    CONSTRAINT [PK_Dimension_Dnx] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Dnx_NCI_DnxVersion_OperatingSystem_FileName]
    ON [dbo].[Dimension_Dnx]([DnxVersion] ASC, [OperatingSystem] ASC, [FileName] ASC) WITH (STATISTICS_NORECOMPUTE = ON);
GO

CREATE NONCLUSTERED INDEX [Dimension_Dnx_Lowercased]
	ON [dbo].[Dimension_Dnx]([LowercasedDnxVersion] ASC, [LowercasedOperatingSystem] ASC, [LowercasedFileName] ASC) INCLUDE ([Id], [DnxVersion], [OperatingSystem], [FileName]) WITH (STATISTICS_NORECOMPUTE = ON);
GO