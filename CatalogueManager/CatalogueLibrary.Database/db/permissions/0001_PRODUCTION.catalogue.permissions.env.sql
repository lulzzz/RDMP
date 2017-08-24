/****** Object:  User [linkuser]    Script Date: 26/01/2015 09:50:17 ******/
IF NOT EXISTS (SELECT name FROM master.sys.server_principals WHERE name = 'linkuser')
BEGIN
	CREATE LOGIN [linkuser] WITH PASSWORD = 'linkuserasdjasdkljaskldjasldka18SDSD83'
	CREATE USER [linkuser] FOR LOGIN [linkuser] WITH DEFAULT_SCHEMA=[dbo]
	ALTER ROLE [db_datareader] ADD MEMBER [linkuser]
END
GO

/****** Object:  User [catalogueuser]    Script Date: 26/01/2015 09:50:17 ******/
IF NOT EXISTS (SELECT name FROM master.sys.server_principals WHERE name = 'catalogueuser')
BEGIN
	CREATE LOGIN [catalogueuser] WITH PASSWORD = 'catalogueuserasdq3rea123fefASD'
	CREATE USER [catalogueuser] FOR LOGIN [catalogueuser] WITH DEFAULT_SCHEMA=[dbo]
	ALTER ROLE [db_datareader] ADD MEMBER [catalogueuser]
	ALTER ROLE [db_datawriter] ADD MEMBER [catalogueuser]
END
GO

/****** Object:  User [cataloguereader]    Script Date: 26/01/2015 09:50:17 ******/
IF NOT EXISTS (SELECT name FROM master.sys.server_principals WHERE name = 'cataloguereader')
BEGIN
	CREATE LOGIN [cataloguereader] WITH PASSWORD = 'cataloguereADaderasdWaw31r32rw'
	CREATE USER [cataloguereader] FOR LOGIN [cataloguereader] WITH DEFAULT_SCHEMA=[dbo]
	ALTER ROLE [db_datareader] ADD MEMBER [cataloguereader]
END
GO