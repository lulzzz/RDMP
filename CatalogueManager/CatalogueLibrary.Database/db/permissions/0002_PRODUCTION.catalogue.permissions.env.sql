/****** Object:  User [DATAENTRY\wbonney]    Script Date: 26/01/2015 09:50:17 ******/
CREATE USER [DATAENTRY\wbonney] FOR LOGIN [DATAENTRY\wbonney] WITH DEFAULT_SCHEMA=[dbo]
GO
/****** Object:  User [DATAENTRY\sxdonaldsonbuist]    Script Date: 26/01/2015 09:50:17 ******/
CREATE USER [DATAENTRY\sxdonaldsonbuist] FOR LOGIN [DATAENTRY\sxdonaldsonbuist] WITH DEFAULT_SCHEMA=[dbo]
GO
/****** Object:  User [DATAENTRY\ltramma]    Script Date: 26/01/2015 09:50:17 ******/
CREATE USER [DATAENTRY\ltramma] FOR LOGIN [DATAENTRY\ltramma] WITH DEFAULT_SCHEMA=[dbo]
GO
/****** Object:  User [DATAENTRY\gimcallister]    Script Date: 26/01/2015 09:50:17 ******/
CREATE USER [DATAENTRY\gimcallister] FOR LOGIN [DATAENTRY\gimcallister] WITH DEFAULT_SCHEMA=[dbo]
GO
/****** Object:  User [DATAENTRY\dpaul]    Script Date: 26/01/2015 09:50:17 ******/
CREATE USER [DATAENTRY\dpaul] FOR LOGIN [DATAENTRY\dpaul] WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_owner] ADD MEMBER [DATAENTRY\wbonney]
GO
ALTER ROLE [db_datareader] ADD MEMBER [DATAENTRY\sxdonaldsonbuist]
GO
ALTER ROLE [db_ddladmin] ADD MEMBER [DATAENTRY\ltramma]
GO
ALTER ROLE [db_datareader] ADD MEMBER [DATAENTRY\ltramma]
GO
ALTER ROLE [db_datawriter] ADD MEMBER [DATAENTRY\ltramma]
GO
ALTER ROLE [db_datareader] ADD MEMBER [DATAENTRY\gimcallister]
GO
ALTER ROLE [db_datareader] ADD MEMBER [DATAENTRY\dpaul]
GO