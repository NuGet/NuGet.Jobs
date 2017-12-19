CREATE PROCEDURE [dbo].[Procedure1]
AS
BEGIN
	SELECT [Position] FROM [dbo].[Cursors] WHERE [Name] = 'GetDirtyPackageId'
END