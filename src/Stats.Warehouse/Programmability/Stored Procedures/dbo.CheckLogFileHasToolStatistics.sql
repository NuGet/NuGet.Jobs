CREATE PROCEDURE [dbo].[CheckLogFileHasToolStatistics]
    @logFileName nvarchar(255)
AS
BEGIN
	SET NOCOUNT ON;

    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    SELECT	CASE
			WHEN EXISTS (
				SELECT TOP 1 D.[Fact_LogFileName_Id]
				FROM [dbo].[Fact_Dist_Download] AS D
				INNER JOIN [dbo].[Fact_LogFileName] AS L
				ON D.[Fact_LogFileName_Id] = L.[Id]
				WHERE L.[LogFileName] IS NOT NULL AND L.[LogFileName] = @logFileName
				)
			THEN CAST(1 AS BIT)
			ELSE CAST(0 AS BIT)
		END AS 'exists'
END