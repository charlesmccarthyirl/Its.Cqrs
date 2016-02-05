IF NOT EXISTS (SELECT Id FROM [Scheduler].[Clock] 
			   WHERE Name LIKE 'default')
  BEGIN
	INSERT INTO [Scheduler].[Clock] 
	(Name, StartTime, UtcNow)
	VALUES 
	('default', GetDate(), GetDate())
  END