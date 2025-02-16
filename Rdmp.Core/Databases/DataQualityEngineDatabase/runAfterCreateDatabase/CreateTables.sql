CREATE TABLE [dbo].[ColumnState](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Evaluation_ID] [int] NOT NULL,
	[TargetProperty] [varchar](500) NOT NULL,
	[DataLoadRunID] [int] NOT NULL,
	[CountCorrect] [int] NOT NULL,
	[CountDBNull] [int] NOT NULL,
	[ItemValidatorXML] [varchar](MAX) NULL,
	[CountMissing] [int] NOT NULL,
	[CountWrong] [int] NOT NULL,
	[CountInvalidatesRow] [int] NOT NULL,
 CONSTRAINT [PK_ColumnState] PRIMARY KEY CLUSTERED 
(
	[Evaluation_ID] DESC,
	[TargetProperty] ASC,
	[DataLoadRunID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
CREATE TABLE [dbo].[Evaluation](
	[DateOfEvaluation] [datetime] NOT NULL,
	[CatalogueID] [int] NOT NULL,
	[ID] [int] IDENTITY(1,1) NOT NULL,
 CONSTRAINT [PK_Evaluation] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
CREATE TABLE [dbo].[RowState](
	[Evaluation_ID] [int] NOT NULL,
	[Correct] [int] NOT NULL,
	[Missing] [int] NOT NULL,
	[Wrong] [int] NOT NULL,
	[Invalid] [int] NOT NULL,
	[DataLoadRunID] [int] NOT NULL,
	[ValidatorXML] [varchar](MAX) NOT NULL,
 CONSTRAINT [PK_RowState] PRIMARY KEY CLUSTERED 
(
	[Evaluation_ID] ASC,
	[DataLoadRunID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO


CREATE TABLE [dbo].[PeriodicityState](
	[Evaluation_ID] [int] NOT NULL,
	[Year] [int] NOT NULL,
	[Month] [int] NOT NULL,
	[CountOfRecords] [int] NOT NULL,
	[RowEvaluation] [varchar](50) NOT NULL,
 CONSTRAINT [PK_PeriodicityState] PRIMARY KEY CLUSTERED 
(
	[Evaluation_ID] ASC,
	[Year] ASC,
	[Month] ASC,
	[RowEvaluation] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

/****** Object:  Index [ixColumnStateID]    Script Date: 04/11/2015 11:04:32 ******/
CREATE UNIQUE NONCLUSTERED INDEX [ixColumnStateID] ON [dbo].[ColumnState]
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ColumnState]  WITH CHECK ADD  CONSTRAINT [FK_ColumnState_Evaluation] FOREIGN KEY([Evaluation_ID])
REFERENCES [dbo].[Evaluation] ([ID])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RowState]  WITH CHECK ADD  CONSTRAINT [FK_RowState_Evaluation] FOREIGN KEY([Evaluation_ID])
REFERENCES [dbo].[Evaluation] ([ID])
ON DELETE CASCADE

ALTER TABLE [dbo].[PeriodicityState]  WITH CHECK ADD  CONSTRAINT [FK_PeriodicityState_Evaluation] FOREIGN KEY([Evaluation_ID])
REFERENCES [dbo].[Evaluation] ([ID])
GO

CREATE FUNCTION [dbo].[GetSoftwareVersion]()
RETURNS nvarchar(50)
AS
BEGIN
	-- Return the result of the function
	RETURN (SELECT top 1 version from RoundhousE.Version order by version desc)
END