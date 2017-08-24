--Version:1.1.0.0
--Description: Adds the fields Username and Password to ExternalCohortTable
if not exists (select 1 from sys.columns where name = 'Username' and object_id = OBJECT_ID('ExternalCohortTable'))
begin
ALTER TABLE dbo.ExternalCohortTable ADD
	Username varchar(500) NULL,
	Password varchar(500) NULL
END
