CREATE TYPE [dbo].[DnxDimensionTableType] AS TABLE
(
	[DnxVersion]			NVARCHAR(255)	NOT NULL,
	[OperatingSystem]		NVARCHAR(128)	NOT NULL,
	[FileName]			NVARCHAR(128)	NOT NULL,
	UNIQUE NONCLUSTERED ([DnxVersion], [OperatingSystem], [FileName])
)