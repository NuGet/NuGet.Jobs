CREATE TABLE [dbo].[Fact_UserAgent]
(
	[Id]                INT				IDENTITY (1, 1) NOT NULL,
    [UserAgent]         NVARCHAR(2048)	NULL
    CONSTRAINT [UserAgent] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Fact_UserAgent_UniqueIndex] ON [dbo].[Fact_UserAgent] ([UserAgent] ASC) INCLUDE ([Id])
GO