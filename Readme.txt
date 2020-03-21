DBLOG.DatabaseLogAnalyzer can read the SQL Server transaction logs online, and return RedoSQL and UndoSQL for every transactions.


Some Tips:
1. The SQL Server to be analyzed needs 2008 or later version.
2. Target Database Recovery model must be 'Full'.
3. This module only analyze for DML transaction.
4. For develop, please install Visual Studio 2017 or later version and .NET Framework 4.8.
5. Please contact me when have any question: ap0405140@163.com


MSSQLLogAnalyzer.exe use example:
step1: Execute ExecutableFiles\MSSQLLogAnalyzer.exe.
step2: Modify [ConnectionString], change it for your environment.
       Modify [StartTime] and [EndTime] to what time range need to read logs.
       Modify [TableName], It can  be blank, when blank means read all table logs.
step3: Click [Readlog] button, wait for analysis results.


DBLOG.dll use example:
step1: Start Visual Studio 2017, create a new project.
step2: Add reference DBLOG.dll to current project.
step3: At cs file header, add "using DBLOG;"
step4: Call DatabaseLogAnalyzer.ReadLog() for read logs, like below:
   string ConnectionString, StartTime, EndTime, TableName;
   DatabaseLogAnalyzer dbla;
   DatabaseLog[] logs;

   //connection string: Please change below connection string for your environment.
   ConnectionString = "server=[ServerName];database=[DatabaseName];uid=[LoginName];pwd=[Password];Connection Timeout=5;Integrated Security=false;";
   //start time for analyze, format: yyyy-MM-dd HH:mm:ss
   StartTime = "2020-03-18 10:01:02"; 
   //end time for analyze, format: yyyy-MM-dd HH:mm:ss
   EndTime = "2020-03-18 10:02:02"; 
   //table name: Need include schema name(like dbo.Table1), When blank means query all tables 's logs, you can change it for need.
   TableName = ""; 

    // Initializes a new instance of the DBLOG.DatabaseLogAnalyzer class.
    dbla = new DatabaseLogAnalyzer(ConnectionString);  
    // read logs, return a DatabaseLog array, include below properties:LSN,TransactionID,BeginTime,EndTime,ObjectName,Operation,RedoSQL,UndoSQL.
    logs = dbla.ReadLog(StartTime, EndTime, TableName); 

