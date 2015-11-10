CREATE PROCEDURE [dbo].[EnsureDnxDimensionsExist]
	@dnxs [dbo].[DnxDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL PRIMARY KEY,
		[DnxVersion]			NVARCHAR(255)	NOT NULL,
		[OperatingSystem]		NVARCHAR(128)	NOT NULL,
		[FileName]			NVARCHAR(128)	NOT NULL,
		INDEX IX_Results NONCLUSTERED ([DnxVersion], [OperatingSystem], [FileName])
	)

	-- Select existing packages and insert them into the results table
	INSERT INTO @results ([Id], [DnxVersion], [OperatingSystem], [FileName])
		SELECT	T.[Id], T.[DnxVersion], T.[OperatingSystem], T.[FileName]
		FROM	[dbo].[Dimension_Dnx] AS T (NOLOCK)
		INNER JOIN	@dnxs AS I
		ON	T.[LowercasedDnxVersion] = LOWER(I.DnxVersion)
			AND T.[LowercasedOperatingSystem] = LOWER(I.OperatingSystem)
			AND T.[LowercasedFileName] = LOWER(I.FileName)

	-- Insert new packages
	BEGIN TRY
		SET TRANSACTION ISOLATION LEVEL READ COMMITTED
		BEGIN TRANSACTION

			INSERT INTO [Dimension_Dnx] ([DnxVersion], [OperatingSystem], [FileName])
				OUTPUT inserted.Id, inserted.DnxVersion, inserted.OperatingSystem, inserted.FileName INTO @results
			SELECT	[DnxVersion], [OperatingSystem], [FileName]
				FROM	@dnxs
			EXCEPT
			SELECT	[DnxVersion], [OperatingSystem], [FileName]
				FROM	@results

		COMMIT

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

	-- Select all matching dimensions
	SELECT		[Id], [DnxVersion], [OperatingSystem], [FileName]
	FROM		@results

END