--Version:2.3.0.1
--Description: Changes column called [ResourceIdentifier] in LoadProgress to be called Name
  if exists (select 1 from sys.columns where name = 'ResourceIdentifier')
	EXEC sp_rename 'LoadProgress.ResourceIdentifier', 'Name', 'COLUMN'; 
