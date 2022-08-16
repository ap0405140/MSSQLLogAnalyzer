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
        private string _objectname,    // 对象名
                       _starttime, _endtime, // 开始时间 结束时间
                       _MinLSN, // 开始LSN
                       _tsql;
        private DatabaseOperation DB;  // 数据库操作对象
        /// <summary>
        /// Readed Percent (0-100).
        /// </summary>
        public int ReadPercent;       // 读取进度百分比 1-100
        public string LogFile = "AnalysisLog.txt";

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
            DB.RefreshConnect();
        }

        /// <summary>
        /// Initializes a new instance of the DBLOG.DatabaseLogAnalyzer class.
        /// </summary>
        /// <param name="pconnectstring">The string used to open a SQL Server database.</param>
        public DatabaseLogAnalyzer(string pconnectstring)
        {
            DB = new DatabaseOperation(pconnectstring);
            DB.RefreshConnect();
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
            List<DatabaseLog> logs, dmllog, ddllog;

            _objectname = pObjectName ?? string.Empty;
            _objectname = (_objectname.Length > 0 && _objectname.Contains(".") == false ? "dbo." : "") + _objectname;
            _starttime = pStartTime;
            _endtime = pEndTime;

            if (File.Exists(LogFile) == true)
            {
                File.Delete(LogFile);
            }

            logs = new List<DatabaseLog>();
            ReadPercent = 0;

            dmllog = ReadLogDML();
            logs.AddRange(dmllog);

            ReadPercent = 100;

            return logs.ToArray();
        }

        private List<DatabaseLog> ReadLogDML()
        {
            List<DatabaseLog> dmllog, tmplog;
            int i;
            string databasename, schemaname, tablename;
            DataTable dtTemp;
            DBLOG_DML[] tablelist;
            List<FLOG> Loglist;
            List<(string tablename, string schemaname)> tables;

            databasename = DB.DatabaseName;
            schemaname = "";
            tablename = "";
            if (_objectname.Length > 0)
            {
                schemaname = _objectname.Substring(0, _objectname.IndexOf(".", 0));
                tablename = _objectname.Substring(_objectname.IndexOf(".", 0) + 1, _objectname.Length - _objectname.IndexOf(".", 0) - 1);
            }

            // get DML Transaction list
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

            // get StartLSN and EndLSN
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

            // get DML original log list
            _tsql = "if object_id('tempdb..#LogList') is not null drop table #LogList; ";
            DB.ExecuteSQL(_tsql, false);

            _tsql = "select * "
                  + " into #LogList "
                  + " from sys.fn_dblog(null,null) t "
                  + " where 1=2; ";
            DB.ExecuteSQL(_tsql, false);

            _tsql = "set transaction isolation level read uncommitted; "
                    + "insert into #LogList "
                    + "output inserted.* "
                    + "select * "
                    + "  from sys.fn_dblog(null,null) t "
                    + $" where [Current LSN]>='{_MinLSN}' "
                    + "  and [Context] in('LCX_HEAP','LCX_CLUSTERED','LCX_MARK_AS_GHOST','LCX_TEXT_TREE','LCX_TEXT_MIX') "
                    + "  and [Operation] in('LOP_INSERT_ROWS','LOP_DELETE_ROWS','LOP_MODIFY_ROW','LOP_MODIFY_COLUMNS','LOP_FORMAT_PAGE') "
                    + "  and [AllocUnitName]<>'Unknown Alloc Unit' "
                    + "  and [AllocUnitName] not like 'sys.%' "
                    + "  and [AllocUnitName] is not null ";

            if (_objectname.Length > 0)
            {
                _tsql = _tsql 
                        + " and case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],2) else parsename([AllocUnitName],1) end='" + tablename + "' "
                        + " and case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],3) else parsename([AllocUnitName],2) end='" + schemaname + "' ";
            }
            Loglist = DB.Query<FLOG>(_tsql, false);

            _tsql = $"alter table #LogList add constraint pk#LogList{Guid.NewGuid().ToString().Replace("-", "")} primary key clustered ([Current LSN]); ";
            DB.ExecuteSQL(_tsql, false);

            // get table list
            _tsql = "select 'TableName'=case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],2) else parsename([AllocUnitName],1) end, "
                    + "     'SchemaName'=case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],3) else parsename([AllocUnitName],2) end "
                    + " from #LogList "
                    + " where [Transaction ID] in(select TransactionID from #TransactionList) "
                    + " group by case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],2) else parsename([AllocUnitName],1) end, "
                    + "          case when parsename([AllocUnitName],3) is not null then parsename([AllocUnitName],3) else parsename([AllocUnitName],2) end "
                    + " order by max([Current LSN]) desc; ";
            tables = DB.Query<(string tablename,string schemaname)>(_tsql, false).ToList();

            ReadPercent = ReadPercent + 5;

            tablelist = new DBLOG_DML[tables.Count];
            dmllog = new List<DatabaseLog>();
            i = 0;
            foreach (var dr in tables)
            {
                tablename = dr.tablename;
                schemaname = dr.schemaname;
                tablelist[i] = new DBLOG_DML(databasename, schemaname, tablename, DB, LogFile);
                tablelist[i].DTLogs = Loglist.Where(p => p.AllocUnitName == $"{schemaname}.{tablename}"
                                                         || p.AllocUnitName.StartsWith($"{schemaname}.{tablename}.")).ToList();

#if DEBUG
                FCommon.WriteTextFile(LogFile, $"Start Analysis Log for [{schemaname}].[{tablename}]. ");
#endif

                tmplog = tablelist[i].AnalyzeLog();
                dmllog.AddRange(tmplog);
                ReadPercent = ReadPercent + Convert.ToInt32(Math.Floor((tablelist[i].DTLogs.Count * 1.0) / (Loglist.Count * 1.0) * 85.0));

#if DEBUG
                FCommon.WriteTextFile(LogFile, $"End Analysis Log for [{schemaname}].[{tablename}]. ");
#endif

                i = i + 1;
            }

            dmllog = dmllog.OrderBy(p => p.TransactionID).ToList();

            ReadPercent = 95;
            return dmllog;
        }
      
    }

    [Serializable]
    public class DatabaseLog
    {
        private string _redosql,
                       _undosql;
        private byte[] _redosqlfile,
                       _undosqlfile;

        public string LSN { get; set; }
        public string Type { get; set; } // DML / DDL / DCL
        public string TransactionID { get; set; }
        public string BeginTime { get; set; }
        public string EndTime { get; set; }
        public string ObjectName { get; set; }
        public string Operation { get; set; }

        public string RedoSQL 
        { 
            get
            {
                return (_redosql.Length <= 1000 ? _redosql : _redosql.Substring(0, 1000) + "...");
            }
            set
            {
                _redosql = value;
            }
        }
        public byte[] RedoSQLFile 
        { 
            get
            {
                if (_redosqlfile == null)
                {
                    _redosqlfile = _redosql.ToFileByteArray();
                }

                return _redosqlfile;
            }
        }

        public string UndoSQL
        {
            get
            {
                return (_undosql.Length <= 1000 ? _undosql : _undosql.Substring(0, 1000) + "...");
            }
            set
            {
                _undosql = value;
            }
        }
        public byte[] UndoSQLFile
        {
            get
            {
                if (_undosqlfile == null)
                {
                    _undosqlfile = _undosql.ToFileByteArray();
                }

                return _undosqlfile;
            }
        }

        public string Message { get; set; }
    }
}
