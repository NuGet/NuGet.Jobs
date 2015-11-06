CREATE TABLE [dbo].[Fact_Dnx_Download]
(
	[Id]							UNIQUEIDENTIFIER NOT NULL DEFAULT newid(),
	[Dimension_Date_Id]				INT NOT NULL,
    [Dimension_Time_Id]				INT NOT NULL,
    [Dimension_Dnx_Id]				INT NOT NULL,
    [Dimension_Platform_Id]			INT NOT NULL,
	[DownloadCount]					INT NULL,
    [Fact_UserAgent_Id]				INT NOT NULL,
    [Fact_LogFileName_Id]			INT NOT NULL,
    [Fact_EdgeServer_IpAddress_Id]	INT NOT NULL,
    [Timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_Fact_Dnx_Download] PRIMARY KEY CLUSTERED ([Id]) WITH (STATISTICS_NORECOMPUTE = ON)
);
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_TimestampDesc]
    ON [dbo].[Fact_Dnx_Download]([Timestamp] DESC)
    INCLUDE([Dimension_Date_Id], [Dimension_Dnx_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_DownloadCount]
    ON [dbo].[Fact_Dnx_Download]([DownloadCount] ASC) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_Dnx_Id]
    ON [dbo].[Fact_Dnx_Download]([Dimension_Dnx_Id] ASC)
    INCLUDE([Dimension_Date_Id], [DownloadCount], [Timestamp]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_Date_Time]
    ON [dbo].[Fact_Dnx_Download]([Dimension_Date_Id] ASC, [Timestamp])
    INCLUDE([Dimension_Dnx_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_UserAgent]
    ON [dbo].[Fact_Dnx_Download] ([Fact_UserAgent_Id])
	INCLUDE ([DownloadCount]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_LogFileName]
    ON [dbo].[Fact_Download] ([Fact_LogFileName_Id])
	INCLUDE ([DownloadCount]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dnx_Download_NCI_EdgeServer_IpAddress]
    ON [dbo].[Fact_Dnx_Download] ([Fact_EdgeServer_IpAddress_Id])
	INCLUDE ([DownloadCount]) WITH (ONLINE = ON)
GO