CREATE PROCEDURE [dbo].[DownloadReportByWeeks]
	@weeks int=6
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @Cursor DATETIME = (SELECT MAX([Position]) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')
	DECLARE @CheckTime DATETIME = getutcdate()

	SELECT	Min(D.[Date]) 'FirstDayOfWeek',
			SUM(ISNULL(Facts.[DownloadCount], 0)) 'Downloads'
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Date] AS D (NOLOCK)
	ON			D.[Id] = Facts.[Dimension_Date_Id]

	WHERE	D.[Date] IS NOT NULL
			AND DATEDIFF(week, D.[Date] , @CheckTime) <= @weeks
			AND DATEDIFF(week, D.[Date] , @CheckTime) > 0 
			AND Facts.[Timestamp] <= @Cursor

	GROUP BY D.WeekOfYearNameInYear
	order by FirstDayOfWeek desc
END