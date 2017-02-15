CREATE PROCEDURE [dbo].[CheckLogFileHasPackageStatistics]
    @logFileName varchar(200)
AS
BEGIN
	SET NOCOUNT ON;

    SELECT	CASE
			WHEN EXISTS (
				SELECT TOP 1 D.[Fact_LogFileName_Id]
				FROM [dbo].[Fact_Download] AS D (NOLOCK)
				INNER JOIN [dbo].[Fact_LogFileName] AS L (NOLOCK)
				ON D.[Fact_LogFileName_Id] = L.[Id]
				WHERE ISNULL(L.[LogFileName], '') = @logFileName
				)
			THEN CAST(1 AS BIT)
			ELSE CAST(0 AS BIT)
		END AS 'exists'
END