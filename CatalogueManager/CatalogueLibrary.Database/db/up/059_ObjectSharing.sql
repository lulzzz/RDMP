--Version:2.6.0.1
--Description: Creates the RemoteRDMP Table and Adds a Name to the AutomationServiceSlot
if not exists (select 1 from sys.tables where name = 'ObjectImport')
begin

CREATE TABLE [dbo].[ObjectImport](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[SharingUID] [varchar](36) NOT NULL,
	[LocalObjectID] [int] NOT NULL,
	[LocalTypeName] [varchar](500) NOT NULL,
	[RepositoryTypeName] [varchar](500) NOT NULL,
	INDEX [ix_YouCanImportEachObjectOnlyOnce] UNIQUE NONCLUSTERED 
(
	[SharingUID] ASC
),
 CONSTRAINT [PK_ObjectImports] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]


CREATE TABLE [dbo].[ObjectExport](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[ObjectTypeName] [varchar](500) NOT NULL,
	[ObjectID] [int] NOT NULL,
	[SharingUID] [varchar](36) NOT NULL,
	[RepositoryTypeName] [varchar](500) NOT NULL,
	INDEX [ix_YouCanExportEachObjectOnlyOnce]  UNIQUE NONCLUSTERED 
(
	[ObjectTypeName] ASC,
	[ObjectID] ASC
),
 CONSTRAINT [PK_ObjectShares] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

end