CREATE PROCEDURE [dbo].[SelectTotalDailyDownloadCountsPerPackageVersion]
	@Date DATE
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	P.[PackageId],
			P.[PackageVersion],
			SUM(ISNULL(F.[DownloadCount], 0)) AS [TotalDownloadCount]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F

	INNER JOIN	[dbo].[Dimension_Package] AS P (NOLOCK)
	ON		P.[Id] = F.[Dimension_Package_Id]

	INNER JOIN	Dimension_Client AS C (NOLOCK)
	ON			C.[Id] = F.[Dimension_Client_Id]

    INNER JOIN Dimension_Date AS D (NOLOCK)
    ON         D.[Id] = F.[Dimension_Date_Id]

	WHERE		(D.[Date] = @Date)
			AND C.ClientCategory NOT IN ('Crawler', 'Unknown')
			AND NOT (C.ClientCategory = 'NuGet' AND CAST(ISNULL(C.[Major], '0') AS INT) > 10)

	GROUP BY	P.[PackageId],
				P.[PackageVersion]

END