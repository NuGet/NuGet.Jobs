CREATE PROCEDURE [dbo].[DownloadReportLast6Weeks]
	@ReportGenerationTime DATETIME
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @MinDate DATE
	DECLARE @MinWeekOfYear INT
	DECLARE @MinYear INT

	SELECT	@MinWeekOfYear = [WeekOfYear],
			@MinYear = [Year]
	FROM	[dbo].[Dimension_Date] (NOLOCK)
	WHERE	[Date] = CAST(DATEADD(day, -42, @ReportGenerationTime) AS DATE)

	SELECT	@MinDate = MIN([Date])
	FROM	[dbo].[Dimension_Date] (NOLOCK)
	WHERE	[WeekOfYear] = @MinWeekOfYear
		AND	[Year] = @MinYear

	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')
	DECLARE @MaxDate DATE = DATEADD(DAY, 42, @MinDate);

	WITH WeekLookup AS 
	(
	    -- Around new year we might have an issue where week start and week end have different
		-- [WeekOfYear] and [Year] values, which cause same week to be represented twice in
		-- the result set. This CTE makes sure that all days within one week have same
		-- [WeekOfYear] and [Year] values around new year (taken from the first day of that week).
		SELECT d.[Id], dd.[WeekOfYear], dd.[Year]
		FROM [dbo].[Dimension_Date] AS d WITH(NOLOCK)
		CROSS APPLY
		(
			SELECT TOP(1) [WeekOfYear], [Year]
			FROM [dbo].[Dimension_Date] AS d2 WITH(NOLOCK)
			WHERE d2.[Date] <= d.[Date] AND d2.[DayOfWeek] = 1
			ORDER BY d2.[Date] DESC
		) AS dd
		WHERE d.[Date] >= @MinDate AND d.[Date] < @MaxDate AND d.[Date] <= @Cursor
	)
	SELECT	D.[Year],
			D.[WeekOfYear],
			SUM(ISNULL(Facts.[DownloadCount], 0)) AS [Downloads]
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)
	INNER JOIN WeekLookup AS D ON D.Id = Facts.Dimension_Date_Id
	GROUP BY D.[Year], D.[WeekOfYear]
	ORDER BY [Year], [WeekOfYear]
END