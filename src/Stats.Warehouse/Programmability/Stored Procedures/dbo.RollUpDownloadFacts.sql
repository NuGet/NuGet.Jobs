CREATE PROCEDURE [dbo].[RollUpDownloadFacts]
  @MinAgeInDays INT = 90
AS
BEGIN
  SET NOCOUNT ON;

  -- This procedure will roll-up the datapoints to only retain the specified T-@MinAgeInDays window of facts.
  -- Rolled-up datapoints are aggregated by the SUM of the downloadcount for a given package id and version,
  -- and decoupled from any dimensions we don't care about.

  -- For the most popular packages which are generating the most download facts, we'll do a roll-up to T-1 days.
  -- However, for the rolled-up datapoints for those facts in the window between T-@MinAgeInDays and T-1,
  -- we do want to retain the dimensions linked to the daily roll-ups, so we don't lose reporting capabilities on them.

  IF @MinAgeInDays IS NOT NULL
  BEGIN

    -- Ensure no leftovers are consumed when a previous run exited prematurely.
    DROP TABLE IF EXISTS [dbo].[Temp_RollUp_PackageIdTable];
    DROP TABLE IF EXISTS [dbo].[Temp_RollUp_T1RollUpTable];
    DROP TABLE IF EXISTS [dbo].[Temp_RollUp_LinkedFactIdTable];

    -- This threshold defines the number of records to remove we consider as being 'popular' enough
    -- to trigger a roll-up to a single day instead of retaining the configured T-@MinAgeInDays period.
    DECLARE @RecordsToRemoveThresholdForRollUpsToOneDay INT = 1000000

    DECLARE @MaxDimensionDateId INT = -1
    DECLARE @MaxDimensionDateIdForRollUpsToOneDay INT = -1
    DECLARE @Dimension_Package_Id INT
    DECLARE @DownloadCount INT = 0
    DECLARE @RecordCountInT1Window INT = 0
    DECLARE @RollUpToDimensionDateId INT = 0
    DECLARE @CursorPosition INT = 0
    DECLARE @TotalCursorPositions INT = 0
    DECLARE @Msg NVARCHAR(MAX) = ''

    -- Temporary table that tracks how many records can be rolled-up per package,
    -- and what target Date we should roll-up to.
    -- If the record count we can remove in this roll-up window is greater than or equal to the threshold
    -- (defined by @RecordsToRemoveThresholdForRollupsToOneDay),
    -- then the MaxDimensionDateId will aim to roll-up to T-1 day, instead of the default T-@MinAgeInDays period.
    CREATE TABLE [dbo].[Temp_RollUp_PackageIdTable]
    (
      [Id] INT NOT NULL PRIMARY KEY,
      [RecordCountInT1Window] INT NOT NULL,
      [MaxDimensionDateId] INT NOT NULL
    );

    DECLARE @NowUtc DATETIME = GETUTCDATE()

    -- Get the Dimension_Date_Id for the maximum date in this T-@MinAgeInDays period
    SELECT  @MaxDimensionDateId = MAX([Id])
    FROM    [dbo].[Dimension_Date] (NOLOCK)
    WHERE   [Date] IS NOT NULL
        AND [Date] < DATEADD(DAY, -@MinAgeInDays, @NowUtc)

    -- Get the Dimension_Date_Id for the maximum date in this T-1 period
    SELECT  @MaxDimensionDateIdForRollUpsToOneDay = MAX([Id])
    FROM    [dbo].[Dimension_Date] (NOLOCK)
    WHERE   [Date] IS NOT NULL
        AND [Date] < DATEADD(DAY, -1, @NowUtc)

    INSERT INTO [dbo].[Temp_RollUp_PackageIdTable]
    SELECT  DISTINCT p.[Id],
            'RecordCountInT1Window' = COUNT(f.[Id]),
            'MaxDimensionDateId' =
              CASE
                WHEN COUNT(f.[Id]) >= @RecordsToRemoveThresholdForRollUpsToOneDay
                THEN @MaxDimensionDateIdForRollUpsToOneDay
                ELSE @MaxDimensionDateId
              END
    FROM  [dbo].[Dimension_Package] AS p (NOLOCK)
    INNER JOIN  [dbo].[Fact_Download] AS f (NOLOCK)
    ON    f.[Dimension_Package_Id] = p.[Id]
    WHERE f.[Dimension_Date_Id] <> -1
      AND f.[Dimension_Date_Id] <= @MaxDimensionDateIdForRollUpsToOneDay
    GROUP BY  p.[Id]
    ORDER BY  RecordCountInT1Window DESC;

    SELECT  @TotalCursorPositions = COUNT([Id])
    FROM    [dbo].[Temp_RollUp_PackageIdTable] (NOLOCK);

    SET @Msg = 'Fetched ' + CAST(@TotalCursorPositions AS VARCHAR) + ' package dimension IDs.';
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;

    -- This cursor will run over the package ID's, sorted by number of records to remove.
    -- This is a good indicator of package ID popularity at a given point in time (since the previous roll-up).
    DECLARE PackageCursor CURSOR FOR
    SELECT  [Id],
            [RecordCountInT1Window],
            [MaxDimensionDateId]
    FROM    [dbo].[Temp_RollUp_PackageIdTable] (NOLOCK)
    -- Optimization: no need to roll-up if only a single download was recorded for a given package id and version
    WHERE   [RecordCountInT1Window] > 1
    ORDER BY  [RecordCountInT1Window] DESC;

    OPEN PackageCursor

    FETCH NEXT FROM PackageCursor
    INTO @Dimension_Package_Id, @RecordCountInT1Window, @RollUpToDimensionDateId

    WHILE @@FETCH_STATUS = 0
        BEGIN

          SET @CursorPosition = @CursorPosition + 1

          DECLARE @DeletedRecords INT = 0
          DECLARE @InsertedRecords INT = 0
          DECLARE @RecordCountToRemove INT = @RecordCountInT1Window
          DECLARE @ProgressPct FLOAT = ROUND((@CursorPosition / (@TotalCursorPositions * 1.0))* 100, 2)

          SET @Msg = 'Cursor: ' + CAST(@CursorPosition AS VARCHAR) + '/' + CAST(@TotalCursorPositions AS VARCHAR) + ' [' + CAST(@ProgressPct AS VARCHAR) + ' pct.]';
          RAISERROR(@Msg, 0, 1) WITH NOWAIT;

          IF @RollUpToDimensionDateId = @MaxDimensionDateIdForRollUpsToOneDay
            BEGIN
              -- This is a T-1 roll-up: keep track of linked dimensions
              CREATE TABLE [dbo].[Temp_RollUp_T1RollUpTable]
              (
                [Dimension_Package_Id] INT NOT NULL,
                [Dimension_Date_Id] INT NOT NULL,
                [Dimension_Operation_Id] INT NOT NULL,
                [Dimension_Client_Id] INT NOT NULL,
                [Dimension_Platform_Id] INT NOT NULL,
                [DownloadCount] INT NOT NULL,
                [RecordCountToRemove] INT NOT NULL
              );

              -- This is a T-1 roll-up: keep track of linked dimensions
              INSERT INTO [dbo].[Temp_RollUp_T1RollUpTable]
              SELECT  f.[Dimension_Package_Id],
                      f.[Dimension_Date_Id],
                      f.[Dimension_Operation_Id],
                      f.[Dimension_Client_Id],
                      f.[Dimension_Platform_Id],
                      'DownloadCount' = SUM(f.[DownloadCount]),
                      'RecordCountToRemove' = COUNT(f.[Id])
              FROM    [dbo].[Fact_Download] AS f (NOLOCK)
              WHERE   f.[Dimension_Date_Id] <> -1
                  AND f.[Dimension_Date_Id] <= @RollUpToDimensionDateId
                  AND f.[Dimension_Package_Id] = @Dimension_Package_Id
              GROUP BY  f.[Dimension_Package_Id],
                        f.[Dimension_Date_Id],
                        f.[Dimension_Operation_Id],
                        f.[Dimension_Client_Id],
                        f.[Dimension_Platform_Id]
              -- Optimization: no need to roll-up if only a single record matches these linked dimensions
              -- for a given package id download in this T-1 roll-up window
              HAVING  COUNT(f.[Id]) > 1

              -- This cursor will run over the package ID's with linked dimensions, sorted by number of records to remove.
              -- This is a good indicator of package ID popularity at a given point in time (since the previous roll-up).
              DECLARE @LinkedPackageId INT
              DECLARE @LinkedDateId INT
              DECLARE @LinkedOperationId INT
              DECLARE @LinkedClientId INT
              DECLARE @LinkedPlatformId INT
              DECLARE @LinkedDownloadCount INT
              DECLARE @LinkedRecordCountToRemove INT

              DECLARE LinkedPackageCursor CURSOR FOR
                SELECT  [Dimension_Package_Id],
                        [Dimension_Date_Id],
                        [Dimension_Operation_Id],
                        [Dimension_Client_Id],
                        [Dimension_Platform_Id],
                        [DownloadCount],
                        [RecordCountToRemove]
                FROM    [dbo].[Temp_RollUp_T1RollUpTable] (NOLOCK)
                ORDER BY [RecordCountToRemove] DESC;

              OPEN LinkedPackageCursor

              FETCH NEXT FROM LinkedPackageCursor
              INTO @LinkedPackageId, @LinkedDateId, @LinkedOperationId, @LinkedClientId, @LinkedPlatformId, @LinkedDownloadCount, @LinkedRecordCountToRemove

              WHILE @@FETCH_STATUS = 0
              BEGIN
                -- Roll-up operation for a single package ID linked to its dimensions within the T-1 roll-up window

                BEGIN TRANSACTION

                BEGIN TRY

                  SET @Msg = 'Package Dimension ID ' + CAST(@LinkedPackageId AS VARCHAR)
                              + ' (linked to date ' + CAST(@LinkedDateId AS VARCHAR)
                              + ', operation ' + CAST(@LinkedOperationId AS VARCHAR)
                              + ', client ' + CAST(@LinkedClientId AS VARCHAR)
                              + ', platform ' + CAST(@LinkedPlatformId AS VARCHAR)
                              + '): '
                              + CAST(@LinkedDownloadCount AS VARCHAR) + ' downloads, ' + CAST(@LinkedRecordCountToRemove AS VARCHAR) + ' records to be removed';
                  RAISERROR(@Msg, 0, 1) WITH NOWAIT

                  SET @DeletedRecords = 0
                  SET @InsertedRecords = 0

                  -- Fetch the Fact_Download Id's matching the records to be rolled-up
                  CREATE TABLE [dbo].[Temp_RollUp_LinkedFactIdTable]
                  (
                    [Id] UNIQUEIDENTIFIER NOT NULL
                  )

                  INSERT INTO [dbo].[Temp_RollUp_LinkedFactIdTable]
                  SELECT  [Id]
                  FROM    [dbo].[Fact_Download] (NOLOCK)
                  WHERE   [Dimension_Package_Id] = @LinkedPackageId
                      AND [Dimension_Date_Id] = @LinkedDateId
                      AND [Dimension_Operation_Id] = @LinkedOperationId
                      AND [Dimension_Client_Id] = @LinkedClientId
                      AND [Dimension_Platform_Id] = @LinkedPlatformId

                  -- No need to keep track of linked project-type dimensions
                  DELETE
                  FROM  [dbo].[Fact_Download_Dimension_ProjectType]
                  WHERE [Fact_Download_Id] IN (SELECT [Id] FROM [dbo].[Temp_RollUp_LinkedFactIdTable] (NOLOCK))

                  SET @DeletedRecords = @@rowcount

                  SET @Msg = 'Package Dimension ID ' + CAST(@LinkedPackageId AS VARCHAR) + ': Deleted ' + CAST(@DeletedRecords AS VARCHAR) + ' records from [dbo].[Fact_Download_Dimension_ProjectType]';
                  RAISERROR(@Msg, 0, 1) WITH NOWAIT

                  SET @DeletedRecords = 0

                  DELETE
                  FROM  [dbo].[Fact_Download]
                  WHERE [Id] IN (SELECT [Id] FROM [dbo].[Temp_RollUp_LinkedFactIdTable] (NOLOCK))

                  SET @DeletedRecords = @@rowcount

                  SET @Msg = 'Package Dimension ID ' + CAST(@LinkedPackageId AS VARCHAR) + ': Deleted ' + CAST(@DeletedRecords AS VARCHAR) + ' records from [dbo].[Fact_Download]';
                  RAISERROR(@Msg, 0, 1) WITH NOWAIT

                  INSERT INTO [dbo].[Fact_Download]
                  (
                    [Dimension_Package_Id],
                    [Dimension_Date_Id],
                    [Dimension_Time_Id],
                    [Dimension_Operation_Id],
                    [Dimension_Client_Id],
                    [Dimension_Platform_Id],
                    [Fact_UserAgent_Id],
                    [Fact_LogFileName_Id],
                    [Fact_EdgeServer_IpAddress_Id],
                    [DownloadCount],
                    [Timestamp]
                  )
                  VALUES
                  (
                    @LinkedPackageId,
                    @LinkedDateId,
                    0, -- no longer track the Time dimension on T-1 roll-ups
                    @LinkedOperationId,
                    @LinkedClientId,
                    @LinkedPlatformId,
                    1, -- no longer track the raw user agent on T-1 roll-ups
                    1, -- no longer track the log file name on T-1 roll-ups
                    1, -- no longer track the edge server IP on T-1 roll-ups
                    @LinkedDownloadCount,
                    GETUTCDATE()
                  )

                  SET @InsertedRecords = @@rowcount
                  SET @Msg = 'Package Dimension ID ' + CAST(@LinkedPackageId AS VARCHAR) + ': Inserted ' + CAST(@InsertedRecords AS VARCHAR) + ' record for ' + CAST(@LinkedDownloadCount AS VARCHAR) + ' downloads';
                  RAISERROR(@Msg, 0, 1) WITH NOWAIT

                  COMMIT TRANSACTION

                END TRY
                BEGIN CATCH

                  ROLLBACK TRANSACTION

                  PRINT 'Package Dimension ID ' + CAST(@LinkedPackageId AS VARCHAR)
                          + ' (linked to date ' + CAST(@LinkedDateId AS VARCHAR)
                          + ', operation ' + CAST(@LinkedOperationId AS VARCHAR)
                          + ', client ' + CAST(@LinkedClientId AS VARCHAR)
                          + ', platform ' + CAST(@LinkedPlatformId AS VARCHAR)
                          + '): Rolled back transaction - ' + ERROR_MESSAGE();

                END CATCH

                DROP TABLE IF EXISTS [dbo].[Temp_RollUp_LinkedFactIdTable];

                FETCH NEXT FROM LinkedPackageCursor
                INTO @LinkedPackageId, @LinkedDateId, @LinkedOperationId, @LinkedClientId, @LinkedPlatformId, @LinkedDownloadCount, @LinkedRecordCountToRemove
              END

              CLOSE LinkedPackageCursor;
              DEALLOCATE LinkedPackageCursor;

              DROP TABLE IF EXISTS [dbo].[Temp_RollUp_T1RollUpTable];

            END
          ELSE
            BEGIN

              -- This is a T-@MinAgeInDays roll-up: don't keep track of linked dimensions
              SELECT  @DownloadCount = SUM(f.[DownloadCount]),
                      @RecordCountToRemove = COUNT(f.[Id])
              FROM    [dbo].[Fact_Download] AS f (NOLOCK)
              WHERE   f.[Dimension_Date_Id] <= @MaxDimensionDateId
                  AND f.[Dimension_Package_Id] = @Dimension_Package_Id
              GROUP BY f.[Dimension_Package_Id]

              SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': ' + CAST(@DownloadCount AS VARCHAR) + ' downloads, ' + CAST(@RecordCountToRemove AS VARCHAR) + ' records to be removed';
              RAISERROR(@Msg, 0, 1) WITH NOWAIT

              BEGIN TRANSACTION

              BEGIN TRY

                SET @DeletedRecords = 0
                SET @InsertedRecords = 0

                DELETE
                FROM   [dbo].[Fact_Download_Dimension_ProjectType]
                WHERE  [Fact_Download_Id] IN  (
                                                SELECT  [Id]
                                                FROM  [dbo].[Fact_Download] (NOLOCK)
                                                WHERE  [Dimension_Package_Id] = @Dimension_Package_Id
                                                  AND  [Dimension_Date_Id] <= @MaxDimensionDateId
                                              )
                SET @DeletedRecords = @@rowcount

                SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Deleted ' + CAST(@DeletedRecords AS VARCHAR) + ' records from [dbo].[Fact_Download_Dimension_ProjectType]';
                RAISERROR(@Msg, 0, 1) WITH NOWAIT

                SET @DeletedRecords = 0

                DELETE
                FROM    [dbo].[Fact_Download]
                WHERE   [Dimension_Package_Id] = @Dimension_Package_Id
                    AND [Dimension_Date_Id] <= @MaxDimensionDateId

                SET @DeletedRecords = @@rowcount

                SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Deleted ' + CAST(@DeletedRecords AS VARCHAR) + ' records from [dbo].[Fact_Download]';
                RAISERROR(@Msg, 0, 1) WITH NOWAIT

                INSERT INTO [dbo].[Fact_Download]
                (
                  [Dimension_Package_Id],
                  [Dimension_Date_Id],
                  [Dimension_Time_Id],
                  [Dimension_Operation_Id],
                  [Dimension_Client_Id],
                  [Dimension_Platform_Id],
                  [Fact_UserAgent_Id],
                  [Fact_LogFileName_Id],
                  [Fact_EdgeServer_IpAddress_Id],
                  [DownloadCount],
                  [Timestamp]
                )
                VALUES
                (
                  @Dimension_Package_Id,
                  -1, -- no longer track the Date dimension on T-@MinAgeInDays roll-ups
                  0, -- no longer track the Time dimension on T-@MinAgeInDays roll-ups
                  1, -- no longer track the Operation dimension on T-@MinAgeInDays roll-ups
                  1, -- no longer track the Client dimension on T-@MinAgeInDays roll-ups
                  1, -- no longer track the Platform dimension on T-@MinAgeInDays roll-ups
                  1, -- no longer track the raw user agent on T-@MinAgeInDays roll-ups
                  1, -- no longer track the log file name on T-@MinAgeInDays roll-ups
                  1, -- no longer track the edge server IP on T-@MinAgeInDays roll-ups
                  @DownloadCount,
                  GETUTCDATE()
                )

                SET @InsertedRecords = @@rowcount
                SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Inserted ' + CAST(@InsertedRecords AS VARCHAR) + ' record for ' + CAST(@DownloadCount AS VARCHAR) + ' downloads';
                RAISERROR(@Msg, 0, 1) WITH NOWAIT

                COMMIT TRANSACTION
              END TRY
              BEGIN CATCH
                ROLLBACK TRANSACTION

                PRINT 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Rolled back transaction - ' + ERROR_MESSAGE();

              END CATCH
            END

      FETCH NEXT FROM PackageCursor
      INTO @Dimension_Package_Id, @RecordCountInT1Window, @RollUpToDimensionDateId
    END

    CLOSE PackageCursor;
    DEALLOCATE PackageCursor;

    DROP TABLE IF EXISTS [dbo].[Temp_RollUp_PackageIdTable];

    PRINT 'FINISHED!';
  END
END