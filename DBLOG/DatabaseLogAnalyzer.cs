using DBLOG.Common;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG
{
    /// <summary>
    /// Summary: SQL Server Database Log Analyzer. Author: AP0405140
    /// <para></para> 
    /// <para>History:</para> 
    /// <para>2020/03/08 AP0405140 create.</para> 
    /// </summary>
    [Serializable]
    public class DatabaseLogAnalyzer
    {
        private static DatabaseOperation DB, DB_DAC;
        private static List<string> DDLTranName, ExceptTranName;
        private string _objectname,
                       _starttime, _endtime,
                       _MinLSN,
                       _tsql;
        public int ReadPercent;       // 读取进度百分比 1->100
        public static Logger NLogger;

        /// <summary>
        /// Initializes a new instance of the DBLOG.DatabaseLogAnalyzer class.
        /// </summary>
        /// <param name="pservername">The name or network address of the instance of SQL Server to which to connect.</param>
        /// <param name="pdatabasename">The name of the database.</param>
        /// <param name="plogin">The SQL Server login account.</param>
        /// <param name="ppassword">The password for the SQL Server account logging on.</param>
        public DatabaseLogAnalyzer(string pservername, string pdatabasename, string plogin, string ppassword)
        {
            DB = new DatabaseOperation(pservername, pdatabasename, plogin, ppassword);

            Init();
        }

        /// <summary>
        /// Initializes a new instance of the DBLOG.DatabaseLogAnalyzer class.
        /// </summary>
        /// <param name="pconnectstring">The string used to open a SQL Server database.</param>
        public DatabaseLogAnalyzer(string pconnectstring)
        {
            DB = new DatabaseOperation(pconnectstring);

            Init();
        }

        private void Init()
        {
            FileTarget LogFile, LogFile_Exception;
            LoggingConfiguration LogConfig;

            // Init DAC connect
            DB.RefreshConnect();
            if (DB_DAC != null)
            {
                DB_DAC.Dispose();
            }
            DB_DAC = new DatabaseOperation($"ADMIN:{DB.ServerName}", DB.DatabaseName, DB.LoginName, DB.Password);

            // Init DDLTranName and ExceptTranName
            DDLTranName = new List<string>() { "CREATE TABLE", "DROPOBJ", "create-schema", "DROP SCHEMA", "CREATE INDEX", "DROP INDEX", "user_transaction", 
                                               "ALTER TABLE", "TRUNCATE TABLE", "SELECT INTO", "CREATE/ALTER VIEW", "CREATE/ALTER PROCEDURE", "CREATE/ALTER FUNCTION", 
                                               "CREATE/ALTER TRIGGER", "CREATE SYNONYM", "CREATE TYPE", "DROP TYPE" };
            ExceptTranName = new List<string>() { "AllocHeapPageSimpleXactDML", "AllocFirstPage" };

            // Init NLog
            LogFile = new NLog.Targets.FileTarget("logfile");
            LogFile.FileName = $"logs/AnalysisLog_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.txt";
            LogFile.Layout = "${date:format=yyyy/MM/dd HH\\:mm\\:ss.fff} ${level:uppercase=true} \r\n${message}\r\n";

            LogFile_Exception = new NLog.Targets.FileTarget("logfile_exception");
            LogFile_Exception.FileName = $"logs/error_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.txt";
            LogFile_Exception.Layout = "${date:format=yyyy/MM/dd HH\\:mm\\:ss.fff} ${level:uppercase=true} \r\n${message}\r\n";

            LogConfig = new LoggingConfiguration();
            LogConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Info, LogFile);
            LogConfig.AddRule(NLog.LogLevel.Error, NLog.LogLevel.Error, LogFile_Exception);
            LogManager.Configuration = LogConfig;
            NLogger = LogManager.GetCurrentClassLogger();

        }

        /// <summary>
        /// Read database logs.
        /// </summary>
        /// <param name="pStartTime">Start Time</param>
        /// <param name="pEndTime">End Time</param>
        /// <param name="pObjectName">Table Name, Blank for query all objects.</param>
        /// <returns>DatabaseLog array.</returns>
        public DatabaseLog[] ReadLog(string pStartTime, string pEndTime, string pObjectName)
        {
            List<DatabaseLog> logs, tmplog, ddllogs;
            int i;
            string databasename, schemaname, tablename, maxlsn;
            long partitionid,allocunitid;
            DataTable dtTemp;
            DBLOG_DML_DDL[] tablelist;
            List<FLOG> Loglist, Loglist_DDL;
            List<(string TableName, string SchemaName, long PartitionId, long AllocUnitId, string MaxLSN)> tables, tablesUK;

            _objectname = pObjectName ?? string.Empty;
            _objectname = (_objectname.Length > 0 && _objectname.Contains(".") == false ? "dbo." : "") + _objectname;
            _starttime = pStartTime;
            _endtime = pEndTime;

            logs = new List<DatabaseLog>();
            ReadPercent = 0;

            databasename = DB.DatabaseName;
            schemaname = "";
            tablename = "";
            if (_objectname.Length > 0)
            {
                schemaname = _objectname.Substring(0, _objectname.IndexOf(".", 0));
                tablename = _objectname.Substring(_objectname.IndexOf(".", 0) + 1, _objectname.Length - _objectname.IndexOf(".", 0) - 1);
            }

            // transaction list
            _tsql = "if object_id('tempdb..#TransactionList') is not null drop table #TransactionList; ";
            DB.ExecuteSQL(_tsql, false);

            _tsql = "set transaction isolation level read uncommitted; "
                    + "select 'TransactionID'=a.[Transaction ID], "
                    + "       'BeginTime'=isnull(min(a.[Begin Time]),max(a.[End Time])), "
                    + "       'EndTime'=isnull(max(a.[End Time]),min(a.[Begin Time])), "
                    + "       'BeginLSN'=min([Current LSN]), "
                    + "       'EndLSN'=max([Current LSN]) "
                    + " into #TransactionList "
                    + " from sys.fn_dblog(null,null) a "
                    + " where a.[Transaction ID]<>N'0000:00000000' "
                    + " and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=a.[Transaction ID] and b.Operation=N'LOP_COMMIT_XACT') "
                    + " group by a.[Transaction ID] "
                    + " having cast(min(a.[Begin Time]) as datetime) between '" + _starttime + "' and '" + _endtime + "' "
                    + "        or cast(max(a.[End Time]) as datetime) between '" + _starttime + "' and '" + _endtime + "' ";
            DB.ExecuteSQL(_tsql, false);
            ReadPercent = ReadPercent + 5;

            // get StartLSN
            _tsql = "select 'MinLSN'=cast(min(BeginLSN) as varchar) from #TransactionList; ";
            dtTemp = DB.Query(_tsql, false);

            if (dtTemp != null && dtTemp.Rows.Count > 0)
            {
                _MinLSN = dtTemp.Rows[0]["MinLSN"].ToString();
            }
            else
            {
                _MinLSN = "";
            }

            // get original logs
            _tsql = "if object_id('tempdb..#LogList') is not null drop table #LogList; ";
            DB.ExecuteSQL(_tsql, false);

            _tsql = "select *,IsVirtual=cast(0 as bit),LogType=cast(N'' as nvarchar(10)) "
                  + " into #LogList "
                  + " from sys.fn_dblog(null,null) t "
                  + " where 1=2; ";
            DB.ExecuteSQL(_tsql, false);

            _tsql = $"alter table #LogList add constraint pk#LogList{Guid.NewGuid().ToString().Replace("-", "")} primary key clustered ([Current LSN]); ";
            DB.ExecuteSQL(_tsql, false);
            
            _tsql = "set transaction isolation level read uncommitted; "
                    + "insert into #LogList "
                    + "output inserted.* "
                    + "select *,IsVirtual=cast(0 as bit),LogType=N'DML' "
                    + "  from sys.fn_dblog(null,null) t "
                    + $" where [Current LSN]>=N'{_MinLSN}' "
                    + "  and [Context] in(N'LCX_HEAP',N'LCX_CLUSTERED',N'LCX_MARK_AS_GHOST',N'LCX_TEXT_TREE',N'LCX_TEXT_MIX') "
                    + "  and [Operation] in(N'LOP_INSERT_ROWS',N'LOP_DELETE_ROWS',N'LOP_MODIFY_ROW',N'LOP_MODIFY_COLUMNS',N'LOP_FORMAT_PAGE') "
                    + "  and [AllocUnitName] not like N'sys.%' "
                    + "  and [AllocUnitName] is not null "
                    + $" and not exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.Operation=N'LOP_BEGIN_XACT' and b.[Transaction Name] in({string.Join(",", DDLTranName.Select(n => $"N'{n}'"))})) "
                    + $" and not exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.Operation=N'LOP_BEGIN_XACT' and b.[Transaction Name] in({string.Join(",", ExceptTranName.Select(n => $"N'{n}'"))})) "
                    + "  [FDMLFILTER]; ";

            if (_objectname.Length > 0)
            {
                _tsql = _tsql.Replace("[FDMLFILTER]",
                                      "and case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],2) else parsename([AllocUnitName],1) end=N'" + tablename + "' "
                                      + "and case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],3) else parsename([AllocUnitName],2) end=N'" + schemaname + "' ");
            }
            else
            {
                _tsql = _tsql.Replace("[FDMLFILTER]", "");
            }
            Loglist = DB.Query<FLOG>(_tsql, false);
            
            Loglist_DDL = GetDDLLog(_MinLSN);

            // get dml table list
            _tsql = "select TableName=case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],2) else parsename([AllocUnitName],1) end, "
                    + "     SchemaName=case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],3) else parsename([AllocUnitName],2) end, "
                    + "     PartitionId=0,"
                    + "     AllocUnitId=0, "
                    + "     MaxLSN=max([Current LSN]) "
                    + " from #LogList "
                    + " where [Transaction ID] in(select TransactionID from #TransactionList) "
                    + " and LogType=N'DML' "
                    + " and [AllocUnitName]<>N'Unknown Alloc Unit' "
                    + " group by case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],2) else parsename([AllocUnitName],1) end, "
                    + "          case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],3) else parsename([AllocUnitName],2) end "
                    + " order by max([Current LSN]) desc; ";
            tables = DB.Query<(string TableName, string SchemaName, long PartitionId, long AllocUnitId, string MaxLSN)>(_tsql, false).ToList();

            _tsql = "select TableName=N'', "
                    + "     SchemaName=N'', "
                    + "     PartitionId=[PartitionId], "
                    + "     AllocUnitId=[AllocUnitId], "
                    + "     MaxLSN=max([Current LSN]) "
                    + " from #LogList "
                    + " where [Transaction ID] in(select TransactionID from #TransactionList) "
                    + " and LogType=N'DML' "
                    + " and [AllocUnitName]=N'Unknown Alloc Unit' "
                    + " and [AllocUnitId] is not null "
                    + " group by [PartitionId],[AllocUnitId] "
                    + " order by max([Current LSN]) desc; ";
            tablesUK = DB.Query<(string TableName, string SchemaName, long PartitionId, long AllocUnitId, string MaxLSN)>(_tsql, false).ToList();
            tables.AddRange(tablesUK);

            ReadPercent = ReadPercent + 5;

            DBLOG_DML_DDL.Init(databasename, DB, DB_DAC, NLogger);
            DBLOG_DML_DDL.DDLLogs = Loglist_DDL;

            if (tables.Count > 0)
            {
                tablelist = new DBLOG_DML_DDL[tables.Count];
                i = 0;
                foreach ((string TableName, string SchemaName, long PartitionId, long AllocUnitId, string MaxLSN) dr in tables)
                {
                    tablename = dr.TableName;
                    schemaname = dr.SchemaName;
                    partitionid = dr.PartitionId;
                    allocunitid = dr.AllocUnitId;
                    maxlsn = dr.MaxLSN;

                    if (string.IsNullOrEmpty(tablename) == false && string.IsNullOrEmpty(schemaname) == false)
                    {
                        tablelist[i] = new DBLOG_DML_DDL(schemaname, tablename);
                        tablelist[i].DTLogs = Loglist.Where(p => p.AllocUnitName == $"{schemaname}.{tablename}"
                                                                 || p.AllocUnitName.StartsWith($"{schemaname}.{tablename}."))
                                                     .ToList();
                    }
                    else
                    {
                        tablelist[i] = new DBLOG_DML_DDL(partitionid, allocunitid, maxlsn);
                        tablelist[i].DTLogs = Loglist.Where(p => p.PartitionId == partitionid
                                                                 && p.AllocUnitId == allocunitid)
                                                     .ToList();
                    }

#if DEBUG
                    NLogger.Info($"Begin Analysis Log for [{schemaname}].[{tablename}]. (partitionid={partitionid.ToString()},allocunitid={allocunitid.ToString()},maxlsn={maxlsn})");
#endif

                    tmplog = tablelist[i].AnalyzeLog()
                                         .Where(p => IsInTimeRange(p.BeginTime, p.EndTime) == true)
                                         .ToList();
                    logs.AddRange(tmplog);
                    ReadPercent = ReadPercent + Convert.ToInt32(Math.Floor((tablelist[i].DTLogs.Count * 1.0) / (Loglist.Count * 1.0) * 50.0));

#if DEBUG
                    NLogger.Info($"End Analysis Log for [{schemaname}].[{tablename}]. (partitionid={partitionid.ToString()},allocunitid={allocunitid.ToString()},maxlsn={maxlsn})");
#endif

                    i = i + 1;
                }

            }

            DBLOG_DML_DDL ddl = new DBLOG_DML_DDL();
            ddllogs = ddl.AnalyzeDDLLog()
                         .Where(p => IsInTimeRange(p.BeginTime, p.EndTime) == true)
                         .ToList();
            logs.AddRange(ddllogs);

            logs = logs.OrderBy(p => p.TransactionID).ToList();

            ReadPercent = 100;

            return logs.ToArray();
        }

        public static List<FLOG> GetDDLLog(string minlsn = "", string maxlsn = "")
        {
            string _tsql;
            List<FLOG> dt;

            _tsql = "set transaction isolation level read uncommitted; "
                    + "select *,IsVirtual=cast(0 as bit),LogType=N'DDL' "
                    + "  from sys.fn_dblog(null,null) t "
                    + $" where [Transaction ID]<>N'0000:00000000' "
                    + $" and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.Operation=N'LOP_BEGIN_XACT' and b.[Transaction Name] in({string.Join(",", DDLTranName.Select(n => $"N'{n}'"))})) "
                    + "  and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.Operation=N'LOP_COMMIT_XACT') "
                    //+ "  and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.AllocUnitName is not null) "
                    + (string.IsNullOrEmpty(minlsn) == false ? $"and [Current LSN]>=N'{minlsn}' " : "")
                    + (string.IsNullOrEmpty(maxlsn) == false ? $"and [Current LSN]<=N'{maxlsn}' " : "");
            dt = DB.Query<FLOG>(_tsql, false);

            return dt;
        }

        private bool IsInTimeRange(DateTime begintime, DateTime endtime)
        {
            bool r;

            if ((begintime >= Convert.ToDateTime(_starttime) && begintime <= Convert.ToDateTime(_endtime))
                || (endtime >= Convert.ToDateTime(_starttime) && endtime <= Convert.ToDateTime(_endtime)))
            {
                r = true;
            }
            else
            {
                r = false;
            }

            return r;
        }

    }
}
