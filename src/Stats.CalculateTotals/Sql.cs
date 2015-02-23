namespace Stats.CalculateTotals
{
    internal static class Sql
    {
        // Note the NOLOCK hints here!
        internal const string SqlGetStatistics = @"SELECT 
                    (SELECT COUNT([Key]) FROM PackageRegistrations pr WITH (NOLOCK)
                            WHERE EXISTS (SELECT 1 FROM Packages p WITH (NOLOCK) WHERE p.PackageRegistrationKey = pr.[Key] AND p.Listed = 1)) AS UniquePackages,
                    (SELECT COUNT([Key]) FROM Packages WITH (NOLOCK) WHERE Listed = 1) AS TotalPackages,
                    (SELECT TotalDownloadCount FROM GallerySettings WITH (NOLOCK)) AS Downloads";

        internal const string SqlGetOperationsStatistics = @"DECLARE @LastDownloadDate DATE
DECLARE @LastDownloadHourOfDay INT
DECLARE @LastDownloadDateTime DATETIME
DECLARE @MinDate DATETIME

SELECT		TOP 1 @LastDownloadDate = D.Date, @LastDownloadHourOfDay = MAX(T.HourOfDay)
FROM		dbo.Fact_Download AS F WITH (NOLOCK)
INNER JOIN	dbo.Dimension_Date AS D WITH (NOLOCK)
ON			F.Dimension_Date_Id = D.Id
INNER JOIN	dbo.Dimension_Time AS T WITH (NOLOCK)
ON			F.Dimension_Time_Id = T.Id
GROUP BY	D.Date, T.HourOfDay
ORDER BY	D.Date DESC, T.HourOfDay DESC

SELECT @LastDownloadDateTime = CAST('' + CAST(@LastDownloadDate AS VARCHAR(10)) + ' ' + CAST(@LastDownloadHourOfDay AS VARCHAR(2)) + ':00' AS DATETIME)
SELECT @MinDate = DATEADD(HOUR, -25, @LastDownloadDateTime)

SELECT	Operation, 
		SUM(DownloadCount) AS Total,
		CAST('' + CAST(D.[Date] AS VARCHAR(10)) + ' ' + CAST(T.HourOfDay AS VARCHAR(2)) + ':00' AS DATETIME) AS [HourOfOperation]
FROM	Dimension_Operation AS O
INNER JOIN Fact_Download AS F WITH (NOLOCK) ON F.Dimension_Operation_Id = O.Id
INNER JOIN Dimension_Date AS D WITH (NOLOCK) ON D.Id = F.Dimension_Date_Id
INNER JOIN Dimension_Time AS T WITH (NOLOCK) ON T.Id = F.Dimension_Time_Id
WHERE	D.[Date] BETWEEN CAST(@MinDate AS DATE) AND @LastDownloadDateTime
		AND T.HourOfDay >=	(
								CASE
									WHEN DATEDIFF(DAY, @MinDate, D.[Date]) = 0 
									THEN DATEPART(HOUR, @MinDate)
									ELSE 0
								END
							)
		AND T.HourOfDay <	(
								CASE
									WHEN DATEDIFF(DAY, @MinDate, D.[Date]) = 0 THEN 24
									ELSE DATEPART(HOUR, @LastDownloadDateTime)
								END
							)
GROUP BY Operation, D.[Date], T.HourOfDay
ORDER BY Operation, D.[Date], T.HourOfDay";
    }
}