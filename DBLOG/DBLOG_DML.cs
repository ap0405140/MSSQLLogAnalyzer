using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;

namespace DBLOG
{
    // DML log Analyzer for DML
    public partial class DBLOG_DML
    {
        private DatabaseOperation DB; // 数据库操作
        private string stsql,         // 动态SQL
                       DatabaseName,  // 数据库名
                       TableName,     // 表名
                       SchemaName,    // 架构名
                       LogFile;
        private TableColumn[] TableColumns;  // 表结构定义
        private TableInformation TableInfos;   // 表信息
        private Dictionary<string, FPageInfo> lobpagedata; // key:fileid+pageid value:FPageInfo
        private List<string> RowCompressionAffectsStorage;
        public static List<(string pageid, string lsn)> PrevPages; // fileid+pageid 
        public List<FLOG> DTLogs;     // 原始日志信息

        public DBLOG_DML(string pDatabasename, string pSchemaName, string pTableName, DatabaseOperation poDB, string pLogFile)
        {
            DB = poDB;
            DatabaseName = pDatabasename;
            TableName = pTableName;
            SchemaName = pSchemaName;
            LogFile = pLogFile;

            (TableInfos, TableColumns) = GetTableInfo(SchemaName, TableName);

            RowCompressionAffectsStorage = new List<string>();
            RowCompressionAffectsStorage.Add("smallint"); // If the value fits in 1 byte, only 1 byte will be used.
            RowCompressionAffectsStorage.Add("int"); // Uses only the bytes that are needed. For example, if a value can be stored in 1 byte, storage will take only 1 byte.
            RowCompressionAffectsStorage.Add("bigint"); // Uses only the bytes that are needed. For example, if a value can be stored in 1 byte, storage will take only 1 byte.
            RowCompressionAffectsStorage.Add("decimal"); // Uses only the bytes that are needed, regardless of the precision specified. For example, if a value can be stored in 3 bytes, storage will take only 3 bytes. The storage footprint is exactly the same as the vardecimal storage format.
            RowCompressionAffectsStorage.Add("numeric"); // Uses only the bytes that are needed, regardless of the precision specified. For example, if a value can be stored in 3 bytes, storage will take only 3 bytes. The storage footprint is exactly the same as the vardecimal storage format.
            RowCompressionAffectsStorage.Add("bit"); // The metadata overhead brings this to 4 bits.
            RowCompressionAffectsStorage.Add("smallmoney"); // Uses the integer data representation by using a 4-byte integer. Currency value is multiplied by 10000 and the resulting integer value is stored by removing any digits after the decimal point. This type has a storage optimization similar to that for integer types.
            RowCompressionAffectsStorage.Add("money"); // Uses the integer data representation by using an 8-byte integer. Currency value is multiplied by 10000 and the resulting integer value is stored by removing any digits after the decimal point. This type has a larger range than smallmoney. This type has a storage optimization similar to that for integer types.
            RowCompressionAffectsStorage.Add("float"); // Least significant bytes with zeros are not stored. float compression is applicable mostly for nonfractional values in mantissa.
            RowCompressionAffectsStorage.Add("real"); // Least significant bytes with zeros are not stored. real compression is applicable mostly for nonfractional values in mantissa.
            RowCompressionAffectsStorage.Add("datetime"); // Uses the integer data representation by using two 4-byte integers. The integer value represents the number of days with base date of 1/1/1900. The first 2 bytes can represent up to the year 2079. Compression can always save 2 bytes here until that point. Each integer value represents 3.33 milliseconds. Compression exhausts the first 2 bytes in first five minutes and needs the fourth byte after 4PM. Therefore, compression can save only 1 byte after 4PM. When datetime is compressed like any other integer, compression saves 2 bytes in the date.
            RowCompressionAffectsStorage.Add("datetime2"); // Uses the integer data representation by using 6 to 9 bytes. The first 4 bytes represent the date. The bytes taken by the time will depend on the precision of the time that is specified. The integer value represents the number of days since 1 / 1 / 0001 with an upper bound of 12 / 31 / 9999.To represent a date in year 2005, compression takes 3 bytes. There are no savings on time because it allows for 2 to 4 bytes for various time precisions.Therefore, for one - second time precision, compression uses 2 bytes for time, which takes the second byte after 255 seconds.
            RowCompressionAffectsStorage.Add("datetimeoffset"); // Resembles datetime2, except that there are 2 bytes of time zone of the format (HH:MM). Like datetime2, compression can save 2 bytes. For time zone values, MM value might be 0 for most cases. Therefore, compression can possibly save 1 byte. There are no changes in storage for row compression.
            RowCompressionAffectsStorage.Add("char"); // Trailing padding characters are removed. Note that the Database Engine inserts the same padding character regardless of the collation that is used.
            RowCompressionAffectsStorage.Add("nchar"); // Trailing padding characters are removed. Note that the Database Engine inserts the same padding character regardless of the collation that is used.
            RowCompressionAffectsStorage.Add("binary"); // Trailing zeros are removed.
            RowCompressionAffectsStorage.Add("timestamp"); // Uses the integer data representation by using 8 bytes. There is a timestamp counter that is maintained for each database, and its value starts from 0. This can be compressed like any other integer value.

        }

        // 解析日志
        public List<DatabaseLog> AnalyzeLog()
        {
            List<DatabaseLog> logs;
            DatabaseLog tmplog;
            int j, MinimumLength;
            string BeginTime = string.Empty, // 事务开始时间
                   EndTime = string.Empty,   // 事务结束时间
                   REDOSQL = string.Empty,   // redo sql
                   UNDOSQL = string.Empty,   // undo sql
                   stemp, ColumnList, ValueList1, ValueList0, Value, WhereList1, WhereList0, PrimaryKeyValue, SlotID = "";
            byte[] MR0 = null,
                   MR1 = null;
            DataRow Mrtemp;
            DataTable DTMRlist;  // 行数据前版本
            bool isfound;
            DataRow[] DRTemp;
            SQLGraphNode tj;
            FPageInfo tpageinfo;
            CompressionType compressiontype;

            logs = new List<DatabaseLog>();
            ColumnList = string.Join(",", TableColumns
                                          .Where(p => p.PhysicalStorageType != SqlDbType.Timestamp 
                                                      && p.IsComputed == false
                                                      && p.IsHidden == false)
                                          .Select(p => $"[{p.ColumnName}]"));

            DTMRlist = new DataTable();
            DTMRlist.Columns.Add("PAGEID", typeof(string));
            DTMRlist.Columns.Add("SlotID", typeof(string));
            DTMRlist.Columns.Add("AllocUnitId", typeof(string));
            DTMRlist.Columns.Add("MR1", typeof(byte[]));
            DTMRlist.Columns.Add("MR1TEXT", typeof(string));

            stsql = @"if object_id('tempdb..#temppagedata') is not null drop table #temppagedata; 
                        create table #temppagedata(LSN nvarchar(1000),ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
            DB.ExecuteSQL(stsql, false);

            stsql = "create index ix_#temppagedata on #temppagedata(LSN); ";
            DB.ExecuteSQL(stsql, false);

            stsql = @"if object_id('tempdb..#temppagedatalob') is not null drop table #temppagedatalob; 
                        create table #temppagedatalob(ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
            DB.ExecuteSQL(stsql, false);

            stsql = @"if object_id('tempdb..#ModifiedRawData') is not null drop table #ModifiedRawData; 
                        create table #ModifiedRawData([SlotID] int,[RowLog Contents 0_var] nvarchar(max),[RowLog Contents 0] varbinary(max)); ";
            DB.ExecuteSQL(stsql, false);

            lobpagedata = new Dictionary<string, FPageInfo>();

            stemp = $"{SchemaName}.{TableName}{(TableInfos.AllocUnitName.Length == 0 ? "" : "." + TableInfos.AllocUnitName)}";
            foreach (FLOG log in DTLogs.Where(p => (
                                                    (TableInfos.IsColumnStore == false && p.AllocUnitName == stemp)
                                                    ||
                                                    (TableInfos.IsColumnStore == true && p.AllocUnitName.StartsWith(stemp) == true)
                                                   )
                                             )
                                       .OrderByDescending(p => p.Transaction_ID)
                                       .OrderBy(p => (IsLCXTEXT(p) ? 1 : 2))
                                       .OrderByDescending(p => p.Current_LSN)
                    )
            {
                try
                {
#if DEBUG
                    FCommon.WriteTextFile(LogFile, $"TRANID={log.Transaction_ID} LSN={log.Current_LSN},Operation={log.Operation} ");
#endif

                    if (IsLCXTEXT(log) == false)
                    {
                        stsql = $"select top 1 BeginTime=substring(BeginTime,1,19),EndTime=substring(EndTime,1,19) from #TransactionList where TransactionID='{log.Transaction_ID}'; ";
                        (BeginTime, EndTime) = DB.Query<(string BeginTime, string EndTime)>(stsql, false).FirstOrDefault();

                        compressiontype = TableInfos.GetCompressionType(log.PartitionId);

                        if (log.Operation == "LOP_MODIFY_ROW" || log.Operation == "LOP_MODIFY_COLUMNS")
                        {
                            isfound = false;
                            PrimaryKeyValue = "";

                            DRTemp = DTMRlist.Select("PAGEID='" + log.Page_ID + "' and SlotID='" + log.Slot_ID.ToString() + "' and AllocUnitId='" + log.AllocUnitId.ToString() + "' ");
                            if (DRTemp.Length > 0
                                && (
                                    (log.Operation == "LOP_MODIFY_COLUMNS")
                                    ||
                                    (
                                     log.Operation == "LOP_MODIFY_ROW"
                                     && DRTemp[0]["MR1TEXT"].ToString().Contains(log.RowLog_Contents_1.ToText()) == true
                                    )
                                    //||
                                    //(compressiontype != CompressionType.NONE)
                                   )
                               )
                            {
                                isfound = true;
                            }

                            if (isfound == false && log.Operation == "LOP_MODIFY_ROW")
                            {
                                stemp = log.RowLog_Contents_2.ToText();
                                if (stemp.Length >= 2)
                                {
                                    switch (stemp.Substring(0, 2))
                                    {
                                        case "16":
                                            PrimaryKeyValue = stemp.Substring(2, stemp.Length - 4 * 2);
                                            break;
                                        case "36":
                                            PrimaryKeyValue = stemp.Substring(16);
                                            break;
                                        default:
                                            PrimaryKeyValue = "";
                                            break;
                                    }
                                }
                                else
                                {
                                    PrimaryKeyValue = "";
                                }

                                DRTemp = DTMRlist.Select("PAGEID='" + log.Page_ID + "' and MR1TEXT like '%" + log.RowLog_Contents_1.ToText() + "%' and MR1TEXT like '%" + PrimaryKeyValue + "%' ");
                                isfound = (DRTemp.Length > 0 ? true : false);
                            }

                            if (isfound == false)
                            {
                                MR1 = GetMR1(log, PrimaryKeyValue);
                                SlotID = log.Slot_ID.ToString();

                                if (MR1 != null)
                                {
                                    if (DRTemp.Length > 0)
                                    {
                                        DTMRlist.Rows.Remove(DRTemp[0]);
                                    }

                                    Mrtemp = DTMRlist.NewRow();
                                    Mrtemp["PAGEID"] = log.Page_ID;
                                    Mrtemp["SlotID"] = log.Slot_ID;
                                    Mrtemp["AllocUnitId"] = log.AllocUnitId;
                                    Mrtemp["MR1"] = MR1;
                                    Mrtemp["MR1TEXT"] = MR1.ToText();

                                    DTMRlist.Rows.Add(Mrtemp);
                                }
                            }
                            else
                            {
                                MR1 = (byte[])DRTemp[0]["MR1"];
                                SlotID = DRTemp[0]["SlotID"].ToString();
                            }
                        }

                        stemp = string.Empty;
                        REDOSQL = string.Empty;
                        UNDOSQL = string.Empty;
                        ValueList1 = string.Empty;
                        ValueList0 = string.Empty;
                        WhereList1 = string.Empty;
                        WhereList0 = string.Empty;
                        MR0 = new byte[1];
                        
                        switch (log.Operation)
                        {
                            // Insert / Delete
                            case "LOP_INSERT_ROWS":
                            case "LOP_DELETE_ROWS":
                                switch (compressiontype)
                                {
                                    case CompressionType.NONE:
                                    case CompressionType.COLUMNSTORE:
                                        SlotID = log.Slot_ID.ToString();
                                        MinimumLength = 2 + TableColumns.Where(p => p.IsVarLenDataType == false).Sum(p => p.Length) + 2;

                                        if (log.RowLog_Contents_0.Length >= MinimumLength)
                                        {
                                            try
                                            {
                                                TranslateData(log.RowLog_Contents_0, TableColumns);
                                                MR0 = new byte[log.RowLog_Contents_0.Length];
                                                MR0 = log.RowLog_Contents_0;
                                            }
                                            catch (Exception ex)
                                            {
                                                DRTemp = DTMRlist.Select("PAGEID='" + log.Page_ID + "' and SlotID='" + log.Slot_ID.ToString() + "' and AllocUnitId='" + log.AllocUnitId.ToString() + "' ");
                                                if (DRTemp.Length > 0)
                                                {
                                                    MR0 = (byte[])DRTemp[0]["MR1"];
                                                }
                                                else
                                                {
                                                    MR0 = GetMR1(log, "");
                                                }

                                                if (MR0.Length < MinimumLength) { continue; }
                                                TranslateData(MR0, TableColumns);
                                            }
                                        }
                                        else
                                        {
                                            MR0 = GetMR1(log, "");
                                            if (MR0.Length < MinimumLength) { continue; }
                                            TranslateData(MR0, TableColumns);
                                        }
                                        break;
                                    case CompressionType.ROW:
                                        MR0 = log.RowLog_Contents_0;
                                        TranslateData_CompressionROW(MR0, TableColumns, log);
                                        break;
                                    case CompressionType.PAGE:
                                        
                                        break;
                                }

                                for (j = 0; j <= TableColumns.Length - 1; j++)
                                {
                                    if (TableColumns[j].PhysicalStorageType == SqlDbType.Timestamp
                                        || TableColumns[j].IsComputed == true
                                        || TableColumns[j].IsHidden == true)
                                    {
                                        continue;
                                    }

                                    if ((TableInfos.IsNodeTable == true && j < 2)
                                        || (TableInfos.IsEdgeTable == true && j < 8))

                                    {
                                        tj = new SQLGraphNode { };

                                        if (TableInfos.IsNodeTable == true)
                                        {   // NodeTable
                                            tj.type = "node";
                                            tj.schema = SchemaName;
                                            tj.table = TableName;
                                            tj.id = Convert.ToInt32(TableColumns.FirstOrDefault(p => p.ColumnName.StartsWith("graph_id") == true).Value);
                                        }
                                        else
                                        {   // EdgeTable
                                            if (TableColumns[j].ColumnName.StartsWith("$edge_id") == true)
                                            {
                                                tj.type = "edge";
                                                tj.schema = SchemaName;
                                                tj.table = TableName;
                                                tj.id = Convert.ToInt32(TableColumns.FirstOrDefault(p => p.ColumnName.StartsWith("graph_id") == true).Value);
                                            }
                                            if (TableColumns[j].ColumnName.StartsWith("$from_id") == true)
                                            {
                                                tj.type = "node";
                                                stsql = "select schemaname=s.name,tablename=a.name "
                                                        + " from sys.tables a "
                                                        + " join sys.schemas s on a.schema_id=s.schema_id "
                                                        + $" where a.object_id={TableColumns.FirstOrDefault(p => p.ColumnName.StartsWith("from_obj_id") == true).Value}; ";
                                                (tj.schema, tj.table) = DB.Query<(string, string)>(stsql, false).FirstOrDefault();
                                                tj.id = Convert.ToInt32(TableColumns.FirstOrDefault(p => p.ColumnName.StartsWith("from_id") == true).Value);
                                            }
                                            if (TableColumns[j].ColumnName.StartsWith("$to_id") == true)
                                            {
                                                tj.type = "node";
                                                stsql = "select schemaname=s.name,tablename=a.name "
                                                        + " from sys.tables a "
                                                        + " join sys.schemas s on a.schema_id=s.schema_id "
                                                        + $" where a.object_id={TableColumns.FirstOrDefault(p => p.ColumnName.StartsWith("to_obj_id") == true).Value}; ";
                                                (tj.schema, tj.table) = DB.Query<(string, string)>(stsql, false).FirstOrDefault();
                                                tj.id = Convert.ToInt32(TableColumns.FirstOrDefault(p => p.ColumnName.StartsWith("to_id") == true).Value);
                                            }
                                        }

                                        TableColumns[j].IsNull = false;
                                        TableColumns[j].Value = JsonConvert.SerializeObject(tj);
                                    }

                                    Value = ColumnValue2SQLValue(TableColumns[j]);
                                    ValueList1 = ValueList1 + (ValueList1.Length > 0 ? "," : "") + Value;

                                    if (TableInfos.PrimaryKeyColumns.Count == 0
                                        || TableInfos.PrimaryKeyColumns.Contains(TableColumns[j].ColumnName))
                                    {
                                        WhereList0 = WhereList0
                                                     + (WhereList0.Length > 0 ? " and " : "")
                                                     + ColumnName2SQLName(TableColumns[j])
                                                     + (TableColumns[j].IsNull ? " is " : "=")
                                                     + Value;
                                    }
                                }

                                // 产生redo sql和undo sql -- Insert
                                if (log.Operation == "LOP_INSERT_ROWS")
                                {
                                    REDOSQL = $"insert into [{SchemaName}].[{TableName}]({ColumnList}) values({ValueList1}); ";
                                    UNDOSQL = $"delete top(1) from [{SchemaName}].[{TableName}] where {WhereList0}; ";

                                    if (TableColumns.Any(p => p.IsIdentity) == true)
                                    {
                                        REDOSQL = $"set identity_insert [{SchemaName}].[{TableName}] on; " + "\r\n"
                                                  + REDOSQL + "\r\n"
                                                  + $"set identity_insert [{SchemaName}].[{TableName}] off; " + "\r\n";
                                    }
                                }
                                // 产生redo sql和undo sql -- Delete
                                if (log.Operation == "LOP_DELETE_ROWS")
                                {
                                    REDOSQL = $"delete top(1) from [{SchemaName}].[{TableName}] where {WhereList0}; ";
                                    UNDOSQL = $"insert into [{SchemaName}].[{TableName}]({ColumnList}) values({ValueList1}); ";

                                    if (TableColumns.Any(p => p.IsIdentity) == true)
                                    {
                                        UNDOSQL = $"set identity_insert [{SchemaName}].[{TableName}] on; " + "\r\n"
                                                  + UNDOSQL + "\r\n"
                                                  + $"set identity_insert [{SchemaName}].[{TableName}] off; " + "\r\n";
                                    }
                                }

                                break;
                            // Update
                            case "LOP_MODIFY_ROW":
                            case "LOP_MODIFY_COLUMNS":
                                if (MR1 != null)
                                {
                                    AnalyzeUpdate(log, MR1, ref ValueList1, ref ValueList0, ref WhereList1, ref WhereList0, ref MR0);
                                    if (ValueList1.Length > 0)
                                    {
                                        REDOSQL = $"update top(1) [{SchemaName}].[{TableName}] set {ValueList1} where {WhereList1}; ";
                                        UNDOSQL = $"update top(1) [{SchemaName}].[{TableName}] set {ValueList0} where {WhereList0}; ";
                                    }
                                    stemp = "debug info: "
                                                + " sValueList1=" + ValueList1
                                                + " MR1=" + MR1.ToText() + ", "
                                                + " MR0=" + MR0.ToText() + ", "
                                                + " R1=" + log.RowLog_Contents_1.ToText() + ", "
                                                + " R0=" + log.RowLog_Contents_0.ToText() + ". ";
                                }
                                else
                                {
                                    stemp = "MR1=null";
                                }
                                break;
                        }

                        if (log.Operation == "LOP_MODIFY_ROW" || log.Operation == "LOP_MODIFY_COLUMNS" || log.Operation == "LOP_DELETE_ROWS")
                        {
                            DRTemp = DTMRlist.Select("PAGEID='" + log.Page_ID + "' and SlotID='" + SlotID + "' and AllocUnitId='" + log.AllocUnitId + "' ");
                            if (DRTemp.Length > 0)
                            {
                                DTMRlist.Rows.Remove(DRTemp[0]);
                            }

                            Mrtemp = DTMRlist.NewRow();
                            Mrtemp["PAGEID"] = log.Page_ID;
                            Mrtemp["SlotID"] = log.Slot_ID;
                            Mrtemp["AllocUnitId"] = log.AllocUnitId;
                            Mrtemp["MR1"] = MR0;
                            Mrtemp["MR1TEXT"] = MR0.ToText();

                            DTMRlist.Rows.Add(Mrtemp);
                        }

#if DEBUG
                        FCommon.WriteTextFile(LogFile, $"LSN={log.Current_LSN},Operation={log.Operation},REDOSQL={REDOSQL} ");
#endif

                        if (string.IsNullOrEmpty(BeginTime) == false)
                        {
                            tmplog = new DatabaseLog();
                            tmplog.LSN = log.Current_LSN;
                            tmplog.Type = "DML";
                            tmplog.TransactionID = log.Transaction_ID;
                            tmplog.BeginTime = BeginTime;
                            tmplog.EndTime = EndTime;
                            tmplog.ObjectName = $"[{SchemaName}].[{TableName}]";
                            tmplog.Operation = log.Operation;
                            tmplog.RedoSQL = REDOSQL;
                            tmplog.UndoSQL = UNDOSQL;
                            tmplog.Message = stemp;
                            logs.Add(tmplog);
                        }
                    }
                    else
                    {
                        tpageinfo = GetPageInfo(log.Page_ID);
                        stemp = tpageinfo.PageData;

                        if (log.Operation == "LOP_FORMAT_PAGE")
                        {
                            lobpagedata.Remove(log.Page_ID);
                            if (PrevPages == null)
                            {
                                PrevPages = new List<(string, string)>();
                            }
                            else
                            {
                                if (PrevPages.Any(p => p.pageid == log.Page_ID) == true)
                                {
                                    PrevPages.RemoveAll(p => p.pageid == log.Page_ID);
                                }
                            }
                            PrevPages.Add((log.Page_ID, log.Current_LSN));
                        }
                        else
                        {
                            if (log.Operation == "LOP_INSERT_ROWS")
                            {
                                stemp = stemp.Stuff(tpageinfo.SlotBeginIndex[Convert.ToInt32(log.Slot_ID)] * 2 + (log.Offset_in_Row ?? 0),
                                                    log.RowLog_Contents_0.Length * 2,
                                                    log.RowLog_Contents_0.ToText());
                            }

                            if (log.Operation == "LOP_MODIFY_ROW")
                            {
                                if (tpageinfo.SlotBeginIndex.Length - 1 >= log.Slot_ID)
                                {
                                    stemp = stemp.Stuff(tpageinfo.SlotBeginIndex[Convert.ToInt32(log.Slot_ID)] * 2 + (log.Offset_in_Row ?? 0), //Convert.ToInt32((96 + OffsetinRow) * 2),
                                                        log.RowLog_Contents_1.Length * 2, // (ModifySize ?? 0)
                                                        log.RowLog_Contents_0.ToText());
                                }
                            }

                            lobpagedata[log.Page_ID].PageData = stemp;
                        }
                    }

                }
                catch (Exception ex)
                {
#if DEBUG
                    stemp = $"Message:{(ex.Message ?? "")}  StackTrace:{(ex.StackTrace ?? "")} ";
                    throw new Exception(stemp);
#else
                        tmplog = new DatabaseLog();
                        tmplog.LSN = log.Current_LSN;
                        tmplog.Type = "DML";
                        tmplog.TransactionID = log.Transaction_ID;
                        tmplog.BeginTime = BeginTime;
                        tmplog.EndTime = EndTime;
                        tmplog.ObjectName = $"[{SchemaName}].[{TableName}]";
                        tmplog.Operation = log.Operation;
                        tmplog.RedoSQL = "";
                        tmplog.UndoSQL = "";
                        tmplog.Message = "";
                        logs.Add(tmplog);
#endif
                }

            }

            return logs;
        }

        private bool IsLCXTEXT(FLOG log)
        {
            return
              (
               log.Operation == "LOP_FORMAT_PAGE"
               || log.Context == "LCX_TEXT_TREE"
               || log.Context == "LCX_TEXT_MIX"
              );
        }

        private byte[] GetMR1(FLOG pLog, string pPrimaryKeyValue)
        {
            byte[] mr1;
            string fileid_dec, pageid_dec, checkvalue1, checkvalue2;
            bool isfound;
            
            fileid_dec = Convert.ToInt16(pLog.Page_ID.Split(':')[0], 16).ToString();
            pageid_dec = Convert.ToInt32(pLog.Page_ID.Split(':')[1], 16).ToString();
            stsql = $"DBCC PAGE([{DatabaseName}],{fileid_dec},{pageid_dec},3) with tableresults,no_infomsgs; ";
            stsql = "set transaction isolation level read uncommitted; "
                    + $"insert into #temppagedata(ParentObject,Object,Field,Value) exec('{stsql}'); ";
            DB.ExecuteSQL(stsql, false);

            stsql = $"update #temppagedata set LSN=N'{pLog.Current_LSN}' where LSN is null; ";
            DB.ExecuteSQL(stsql, false);

            switch (pLog.Operation)
            {
                case "LOP_MODIFY_ROW":
                    checkvalue1 = pLog.RowLog_Contents_1.ToText();
                    checkvalue2 = pPrimaryKeyValue;
                    break;
                case "LOP_MODIFY_COLUMNS":
                    checkvalue1 = "";
                    checkvalue2 = "";
                    break;
                case "LOP_INSERT_ROWS":
                    checkvalue1 = pLog.RowLog_Contents_0.ToText().Substring(8, 4 * 2);
                    checkvalue2 = "";
                    break;
                default:
                    checkvalue1 = "";
                    checkvalue2 = "";
                    break;
            }
            
            isfound = false;

            stsql = "truncate table #ModifiedRawData; ";
            DB.ExecuteSQL(stsql, false);

            stsql = " insert into #ModifiedRawData([RowLog Contents 0_var]) "
                    + " select [RowLog Contents 0_var]=upper(replace(stuff((select replace(substring(C.[Value],charindex(N':',[Value],1)+1,48),N'†',N'') "
                    + "                                                     from #temppagedata C "
                    + $"                                                    where C.[LSN]=N'{pLog.Current_LSN}' "
                    + $"                                                    and C.[ParentObject] like 'Slot {pLog.Slot_ID.ToString()} Offset%' "
                    + "                                                     and C.[Object] like N'%Memory Dump%' "
                    + "                                                     for xml path('')),1,1,N''),N' ',N'')); ";
            DB.ExecuteSQL(stsql, false);

            if (TableInfos.GetCompressionType(pLog.PartitionId) != CompressionType.NONE)
            {
                isfound = true;
            }
            else
            {
                stsql = "select count(1) from #ModifiedRawData where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%'; ";
                if (Convert.ToInt32(DB.Query11(stsql, false)) > 0)
                {
                    isfound = true;
                }

                if (isfound == false && pLog.Operation == "LOP_MODIFY_ROW")
                {
                    stsql = "truncate table #ModifiedRawData; ";
                    DB.ExecuteSQL(stsql, false);

                    stsql = "with t as("
                            + "select *,SlotID=replace(substring(ParentObject,5,charindex(N'Offset',ParentObject)-5),N' ',N'') "
                            + " from #temppagedata "
                            + " where LSN=N'" + pLog.Current_LSN + "' "
                            + " and Object like N'%Memory Dump%'), "
                            + "u as("
                            + "select [SlotID]=a.SlotID, "
                            + "       [RowLog Contents 0_var]=upper(replace(stuff((select replace(substring(b.Value,charindex(N':',b.Value,1)+1,48),N'†',N'') "
                            + "                                                    from t b "
                            + "                                                    where b.SlotID=a.SlotID "
                            + "                                                    group by b.Value "
                            + "                                                    for xml path('')),1,1,N''),N' ',N'')) "
                            + " from t a "
                            + " group by a.SlotID) "
                            + "insert into #ModifiedRawData([SlotID],[RowLog Contents 0_var]) "
                            + "select [SlotID],[RowLog Contents 0_var] "
                            + " from u "
                            + " where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%' "
                            + " and substring([RowLog Contents 0_var],9,len([RowLog Contents 0_var])-8) like N'%" + (checkvalue2.Length <= 3998 ? checkvalue2 : checkvalue2.Substring(0, 3998)) + "%'; ";
                    DB.ExecuteSQL(stsql, false);

                    stsql = "select count(1) from #ModifiedRawData where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%'; ";
                    if (Convert.ToInt32(DB.Query11(stsql, false)) > 0)
                    {
                        isfound = true;
                    }
                }
            }

            if (isfound == true)
            {
                stsql = @"update #ModifiedRawData set [RowLog Contents 0]=cast('' as xml).value('xs:hexBinary(substring(sql:column(""[RowLog Contents 0_var]""), 0) )', 'varbinary(max)'); ";
                DB.ExecuteSQL(stsql, false);

                stsql = "select top 1 'MR1'=[RowLog Contents 0] from #ModifiedRawData; ";
                mr1 = DB.Query<byte[]>(stsql, false).FirstOrDefault();
            }
            else
            {
                mr1 = null;
            }

            return mr1;
        }

        private FPageInfo GetPageInfo(string pPageID)
        {
            FPageInfo r;
            List<string> ds;
            int i, j, m_slotCnt;
            string tmpstr, slotarray, tsql;
            (string pageid, string lsn) pp;
            List<FLOG> prevtran;

            pp = (PrevPages == null ? (null,null) : PrevPages.FirstOrDefault(p => p.pageid == pPageID));
            if (pp.pageid != null)
            {
                tsql = "set transaction isolation level read uncommitted; " 
                       + "select * "
                       + "  from sys.fn_dblog(null,null) t "
                       + $" where [Current LSN]<'{pp.lsn}' "
                       + $" and [Current LSN]>=(select max([Current LSN]) from sys.fn_dblog(null,null) b where b.[Current LSN]<'{pp.lsn}' and b.[Page ID]=t.[Page ID] and b.Operation='LOP_FORMAT_PAGE') "
                       + $" and [Page ID]='{pp.pageid}' "
                       + "  and Operation in('LOP_FORMAT_PAGE','LOP_INSERT_ROWS','LOP_MODIFY_ROW') "
                       + "  order by [Current LSN] ";
                prevtran = DB.Query<FLOG>(tsql, false);

                r = new FPageInfo();
                tmpstr = new string(' ', 96 * 2);
                foreach (FLOG log in prevtran)
                {
                    switch (log.Operation)
                    {
                        case "LOP_FORMAT_PAGE":
                            r.PageType = log.PageFormat_PageType.ToString();
                            r.SlotBeginIndex = new int[1] { 96 };
                            break;
                        case "LOP_INSERT_ROWS":
                            tmpstr = tmpstr + log.RowLog_Contents_0.ToText();
                            break;
                        case "LOP_MODIFY_ROW":
                            tmpstr = tmpstr.Stuff((Convert.ToInt32(log.Offset_in_Row) + 96) * 2,
                                                  log.RowLog_Contents_0.Length * 2,
                                                  log.RowLog_Contents_1.ToText());
                            break;
                    }
                }
                tmpstr = tmpstr + new string(' ', 42 * 2);
                r.PageData = tmpstr;

                if (lobpagedata.ContainsKey(pPageID) == true)
                {
                    lobpagedata.Remove(pPageID);
                }
                lobpagedata.Add(pPageID, r);
            }
            else
            {
                if (lobpagedata.ContainsKey(pPageID) == true)
                {
                    r = lobpagedata[pPageID];
                }
                else
                {
                    r = new FPageInfo();
                    r.FileNum = Convert.ToInt16(pPageID.Split(':')[0], 16);
                    r.PageNum = Convert.ToInt32(pPageID.Split(':')[1], 16);
                    r.FileNumPageNum_Hex = pPageID;

                    stsql = "truncate table #temppagedatalob; ";
                    DB.ExecuteSQL(stsql, false);

                    stsql = $"DBCC PAGE([{DatabaseName}],{r.FileNum.ToString()},{r.PageNum.ToString()},2) with tableresults,no_infomsgs; ";
                    stsql = "set transaction isolation level read uncommitted; "
                            + $"insert into #temppagedatalob(ParentObject,Object,Field,Value) exec('{stsql}'); ";
                    DB.ExecuteSQL(stsql, false);

                    // pagedata
                    stsql = "select rn=row_number() over(order by Value)-1,Value=replace(upper(substring(Value,21,44)),N' ',N'') from #temppagedatalob where ParentObject=N'DATA:'; ";
                    ds = DB.Query<(int rn, string Value)>(stsql, false).Select(p => p.Value).ToList();
                    r.PageData = string.Join("", ds);
                    if (r.PageData.Length > 1024 * 8 * 2)
                    {
                        r.PageData = r.PageData.Substring(0, 1024 * 8 * 2);
                    }

                    // pagetype
                    stsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_type'; ";
                    r.PageType = DB.Query11(stsql, false);

                    // SlotCnt
                    stsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_slotCnt'; ";
                    m_slotCnt = Convert.ToInt32(DB.Query11(stsql, false));
                    r.SlotCnt = m_slotCnt;

                    // SlotBeginIndex
                    r.SlotBeginIndex = new int[m_slotCnt];
                    slotarray = r.PageData.Replace("†", "").Substring(r.PageData.Replace("†", "").Length - m_slotCnt * 2 * 2,
                                                                      m_slotCnt * 2 * 2);

                    for (i = 0, j = slotarray.Length - 2;
                         i <= m_slotCnt - 1;
                         i = i + 1, j = j - 4)
                    {
                        tmpstr = $"{slotarray.Substring(j, 2)}{slotarray.Substring(j - 2, 2)}";
                        r.SlotBeginIndex[i] = Convert.ToInt32(tmpstr, 16);
                    }

                    lobpagedata.Add(pPageID, r);
                }
            }

            return r;
        }

        public void AnalyzeUpdate(FLOG curlog, byte[] mr1,
                                  ref string ValueList1, ref string ValueList0, 
                                  ref string WhereList1, ref string WhereList0, 
                                  ref byte[] mr0)
        {
            int i;
            string mr0_str;
            TableColumn[] columns0, columns1;
            CompressionType compressiontype;

            columns0 = TableColumns.CopyToNew().Cast<TableColumn>().ToArray();
            columns1 = TableColumns.CopyToNew().Cast<TableColumn>().ToArray();

            compressiontype = TableInfos.GetCompressionType(curlog.PartitionId);
            switch (compressiontype)
            {
                case CompressionType.NONE:
                case CompressionType.COLUMNSTORE:
                    TranslateData(mr1, columns1);
                    break;
                case CompressionType.ROW:
                    TranslateData_CompressionROW(mr1, columns1, curlog);
                    break;
                case CompressionType.PAGE:

                    break;
            }
            
            switch (curlog.Operation)
            {
                case "LOP_MODIFY_ROW":
                    mr0_str = RESTORE_LOP_MODIFY_ROW(curlog, mr1);
                    break;
                case "LOP_MODIFY_COLUMNS":
                    mr0_str = RESTORE_LOP_MODIFY_COLUMNS(curlog, mr1, columns0, columns1);
                    break;
                default:
                    mr0_str = mr1.ToText();
                    break;
            }

            mr0 = mr0_str.ToByteArray();

            switch (compressiontype)
            {
                case CompressionType.NONE:
                case CompressionType.COLUMNSTORE:
                    TranslateData(mr0, columns0);
                    break;
                case CompressionType.ROW:
                    TranslateData_CompressionROW(mr0, columns0, curlog);
                    break;
                case CompressionType.PAGE:

                    break;
            }

            ValueList1 = "";
            ValueList0 = "";
            WhereList1 = "";
            WhereList0 = "";
            for (i = 0; i <= TableColumns.Length - 1; i++)
            {
                if (TableColumns[i].PhysicalStorageType == SqlDbType.Timestamp || TableColumns[i].IsComputed == true) { continue; }

                if ((columns0[i].IsNull == false
                     && columns1[i].IsNull == false
                     && columns0[i].Value != null
                     && columns1[i].Value != null
                     && columns0[i].Value.ToString() != columns1[i].Value.ToString())
                    || (columns0[i].IsNull == true && columns1[i].IsNull == false)
                    || (columns0[i].IsNull == false && columns1[i].IsNull == true))
                {
                    ValueList0 = ValueList0 + (ValueList0.Length > 0 ? "," : "")
                                 + $"[{columns0[i].ColumnName}]="
                                 + ColumnValue2SQLValue(columns0[i]);
                    ValueList1 = ValueList1 + (ValueList1.Length > 0 ? "," : "")
                                 + $"[{columns1[i].ColumnName}]="
                                 + ColumnValue2SQLValue(columns1[i]);
                }

                if (TableInfos.PrimaryKeyColumns.Count == 0
                    || TableInfos.PrimaryKeyColumns.Contains(TableColumns[i].ColumnName))
                {
                    WhereList0 = WhereList0 + (WhereList0.Length > 0 ? " and " : "")
                                  + ColumnName2SQLName(TableColumns[i]) 
                                  + (columns1[i].IsNull ? " is " : "=")
                                  + ColumnValue2SQLValue(columns1[i]);
                    WhereList1 = WhereList1 + (WhereList1.Length > 0 ? " and " : "")
                                  + ColumnName2SQLName(TableColumns[i]) 
                                  + (columns0[i].IsNull ? " is " : "=")
                                  + ColumnValue2SQLValue(columns0[i]);
                }
            }
        }

        private string RESTORE_LOP_MODIFY_ROW(FLOG log, byte[] mr1)
        {
            string mr0_str, bq;
            FPageInfo tpageinfo;
            int slotid;

            try
            {
                if (mr1.Length >= 4)
                {
                    mr0_str = mr1.ToText().Stuff(Convert.ToInt32(log.Offset_in_Row) * 2,
                                                 log.RowLog_Contents_1.ToText().Length,
                                                 log.RowLog_Contents_0.ToText());

                    if (log.RowLog_Contents_0.Length < log.Modify_Size)
                    {
                        tpageinfo = GetPageInfo(log.Page_ID);
                        slotid = (Convert.ToInt32(log.Slot_ID) <= tpageinfo.SlotBeginIndex.Length - 1 ? Convert.ToInt32(log.Slot_ID) : tpageinfo.SlotBeginIndex.Length - 1);
                        bq = tpageinfo.PageData.Substring((tpageinfo.SlotBeginIndex[slotid] + log.RowLog_Contents_0.Length) * 2,
                                                          Convert.ToInt32(log.Modify_Size - log.RowLog_Contents_0.Length) * 2);
                        mr0_str = mr0_str + bq;
                    }
                }
                else
                {
                    mr0_str = mr1.ToText();
                }
            }
            catch(Exception ex)
            {
                mr0_str = mr1.ToText();
            }

            return mr0_str;
        }

        private string RESTORE_LOP_MODIFY_COLUMNS(FLOG log, byte[] mr1, TableColumn[] columns0, TableColumn[] columns1)
        {
            string mr0_str, mr1_str, LogRecord_str, r3_str, rowlogdata, fvalue0, fvalue1, ts;
            int i, j, k, n, m, fstart0, fstart1, flength0, flength0f4, flength1, flength1f4;
            List<string> tls;
            byte[] mr0;
            bool bfinish;
            TableColumn tmpcol;

            mr0_str = null;
            mr1_str = mr1.ToText();
            LogRecord_str = log.Log_Record.ToText();
            r3_str = log.RowLog_Contents_3.ToText();
            rowlogdata = LogRecord_str.Substring(LogRecord_str.IndexOf(r3_str) + r3_str.Length,
                                                 LogRecord_str.Length - LogRecord_str.IndexOf(r3_str) - r3_str.Length);
            if ((LogRecord_str.Length - rowlogdata.Length) % 8 != 0)
            {
                rowlogdata = rowlogdata.Substring((LogRecord_str.Length - rowlogdata.Length) % 8);
            }

            try
            {
                mr0_str = mr1_str;
                for (i = 1, j = 0; i <= (log.RowLog_Contents_0.Length / 4); i++)
                {
                    fstart0 = Convert.ToInt32(log.RowLog_Contents_0[i * 4 - 3].ToString("X2") + log.RowLog_Contents_0[i * 4 - 4].ToString("X2"), 16);
                    fstart1 = Convert.ToInt32(log.RowLog_Contents_0[i * 4 - 1].ToString("X2") + log.RowLog_Contents_0[i * 4 - 2].ToString("X2"), 16);

                    flength0 = Convert.ToInt32(log.RowLog_Contents_1[i * 2 - 1].ToString("X2") + log.RowLog_Contents_1[i * 2 - 2].ToString("X2"), 16);
                    flength0f4 = (flength0 % 4 == 0 ? flength0 : flength0 + (4 - flength0 % 4));

                    fvalue0 = rowlogdata.Substring(j * 2, flength0 * 2);
                    j = j + flength0f4;
                    
                    flength1 = flength0;
                    if (i == (log.RowLog_Contents_0.Length / 4) && (j * 2) < (rowlogdata.Length - 1))
                    {
                        flength1 = rowlogdata.Length / 2 - j;
                    }
                    flength1f4 = (flength1 % 4 == 0 ? flength1 : flength1 + (4 - flength1 % 4));

                    fvalue1 = rowlogdata.Substring(j * 2, flength1 * 2);
                    j = j + flength1f4;

                    mr0_str = mr0_str.Stuff(fstart0 * 2, flength1 * 2, fvalue0);
                }

                mr0 = mr0_str.ToByteArray();
                TranslateData(mr0, columns0);
                bfinish = true;
            }
            catch(Exception ex)
            {
                bfinish = false;
            }

            if (bfinish == false)
            {
                tls = new List<string>();
                for (i = 0; i <= (int)(Math.Pow(2, (log.RowLog_Contents_0.Length / 4)) - 1); i++)
                {
                    ts = Convert.ToString(i, 2).PadLeft(log.RowLog_Contents_0.Length / 4, '0');
                    tls.Add(ts);
                }

                foreach (string cc in tls)
                {
                    try
                    {
                        mr0_str = mr1_str;
                        for (i = 1, j = 0; i <= (log.RowLog_Contents_0.Length / 4); i++)
                        {
                            fstart0 = Convert.ToInt32(log.RowLog_Contents_0[i * 4 - 3].ToString("X2") + log.RowLog_Contents_0[i * 4 - 4].ToString("X2"), 16);
                            fstart1 = Convert.ToInt32(log.RowLog_Contents_0[i * 4 - 1].ToString("X2") + log.RowLog_Contents_0[i * 4 - 2].ToString("X2"), 16);

                            flength0 = Convert.ToInt32(log.RowLog_Contents_1[i * 2 - 1].ToString("X2") + log.RowLog_Contents_1[i * 2 - 2].ToString("X2"), 16);
                            flength0f4 = (flength0 % 4 == 0 ? flength0 : flength0 + (4 - flength0 % 4));

                            fvalue0 = rowlogdata.Substring(j * 2, flength0 * 2);
                            j = j + flength0f4;

                            k = Convert.ToInt32(mr1[3].ToString("X2") + mr1[2].ToString("X2"), 16);
                            if ((fstart1 + 1) >= 5
                                 && (fstart1 + 1) <= k
                                 && (fstart1 + flength0 + 1) >= 5
                                 && (fstart1 + flength0 + 1) <= k)
                            {
                                flength1 = flength0;
                            }
                            else
                            {
                                if (fstart1 == k + 2
                                    && columns1.Any(p => p.IsVarLenDataType == true))
                                {
                                    tmpcol = columns1.Where(p => p.IsVarLenDataType == true).OrderBy(p => p.ColumnID).FirstOrDefault();
                                    m = tmpcol.LogContentsEndIndex - tmpcol.LogContents.Length / 2;
                                }
                                else
                                {
                                    m = 999999999;
                                }

                                if ((j * 2) <= (rowlogdata.Length - 2))
                                {
                                    flength1 = 0;
                                    for (k = j, n = fstart1;
                                         rowlogdata.Substring(k * 2, 2) == mr1_str.Substring(n * 2, 2)
                                         && n <= m - 1;)
                                    {
                                        flength1 = flength1 + 1;
                                        k = k + 1;
                                        n = n + 1;

                                        if ((k * 2) > (rowlogdata.Length - 2) || (n * 2) > (mr1_str.Length - 2))
                                        {
                                            break;
                                        }
                                    }
                                    flength1 = flength1 - (cc.Substring(i - 1, 1) == "1" ? 1 : 0);
                                }
                                else
                                {
                                    flength1 = 0;
                                }
                            }
                            flength1f4 = (flength1 % 4 == 0 ? flength1 : flength1 + (4 - flength1 % 4));

                            fvalue1 = rowlogdata.Substring(j * 2, flength1 * 2);
                            j = j + flength1f4;

                            mr0_str = mr0_str.Stuff(fstart0 * 2, flength1 * 2, fvalue0);
                        }

                        mr0 = mr0_str.ToByteArray();
                        TranslateData(mr0, columns0);
                        bfinish = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }
            }

            if (bfinish == false || string.IsNullOrEmpty(mr0_str) == true)
            {
                mr0_str = mr1_str;
            }

            return mr0_str;
        }

        private void TranslateData(byte[] rowdata, TableColumn[] columns)
        {
            int index, index2, index3,
                BitValueStartIndex,
                tempint;
            string rowdata_text,
                   NullStatus,  // 列null值状态列表
                   tempstr,
                   ValueHex,
                   Value,
                   VariantCollation;
            byte[] m_bBitColumnData;
            short i, j, 
                  BitColumnCount, 
                  UniqueidentifierColumnCount, 
                  BitColumnDataLength, 
                  BitColumnDataIndex,
                  AllColumnCount,              // 字段总数_实际字段总数
                  AllColumnCountLog,           // 字段总数_日志里的字段总数
                  MaxColumnID,                 // 最大ColumnID       
                  NullStatusLength,            // 列null值状态列表存储所需长度(字节)
                  VarColumnCount,              // 变长字段数量
                  VarColumnStartIndex,         // 变长列字段值开始位置
                  VarColumnEndIndex;           // 变长列字段值结束位置
            short? VariantLength, 
                   VariantScale;
            TableColumn[] columns2,  // 补齐ColumnID,并移除所有计算列的字段列表.
                          columns3;  // 实际用于解析的字段列表.
            SqlDbType? VariantBaseType;
            TableColumn TmpTableColumn;
            List<FVarColumnInfo> VarlenColumns;  // 变长字段数据
            FVarColumnInfo tvc;

            if (rowdata == null || rowdata.Length <= 4) { return; }

            index = 4;  // 行数据从第5字节开始
            rowdata_text = rowdata.ToText();
            AllColumnCount = Convert.ToInt16(columns.Length);

            // 预处理Bit字段
            BitColumnCount = Convert.ToInt16(columns.Count(p => p.PhysicalStorageType == SqlDbType.Bit));
            BitColumnDataLength = (short)Math.Ceiling((double)BitColumnCount / (double)8.0); // 根据Bit字段数 计算Bit字段值列表长度(字节数)
            m_bBitColumnData = new byte[BitColumnDataLength];
            BitColumnDataIndex = -1;
            BitValueStartIndex = 0;

            // 预处理Uniqueidentifier字段
            UniqueidentifierColumnCount = Convert.ToInt16(columns.Count(p => p.PhysicalStorageType == SqlDbType.UniqueIdentifier));

            if (UniqueidentifierColumnCount >= 2
                && TableInfos.IsHeapTable == false) // 堆表不适用本规则
            {
                columns2 = new TableColumn[columns.Length];

                j = 0;
                for (i = (short)(columns.Length - 1); i >= 0; i--)
                {
                    if (columns[i].PhysicalStorageType == SqlDbType.UniqueIdentifier)
                    {
                        columns2[j] = columns[i];
                        j++;
                    }
                }

                for (i = 0; i <= columns.Length - 1; i++)
                {
                    if (columns[i].PhysicalStorageType != SqlDbType.UniqueIdentifier)
                    {
                        columns2[j] = columns[i];
                        j++;
                    }
                }

                columns = columns2;
            }

            index2 = Convert.ToInt32(rowdata[3].ToString("X2") + rowdata[2].ToString("X2"), 16);  // 指针暂先跳过所有定长字段的值
            AllColumnCountLog = BitConverter.ToInt16(rowdata, index2);

            index2 = index2 + 2;

            if (AllColumnCount == AllColumnCountLog)
            {
                columns2 = columns;
            }
            else
            {
                // 补齐ColumnID
                MaxColumnID = columns.Select(p => p.ColumnID).Max();
                columns2 = new TableColumn[MaxColumnID];

                for (i = 0; i <= MaxColumnID - 1; i++)
                {
                    TmpTableColumn = columns.Where(p => p.ColumnID == i + 1).FirstOrDefault();
                    if (TmpTableColumn == null)
                    {
                        columns2[i] = new TableColumn(Convert.ToInt16(i + 1), false); // 虚拟字段
                    }
                    else
                    {
                        columns2[i] = TmpTableColumn;
                    }
                }
            }

            // 移除所有计算列
            columns2 = columns2.Where(p => p.IsComputed == false).ToArray();

            // 预处理聚集索引字段
            if (TableInfos.ClusteredIndexColumns.Count > 0)
            {
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string cc in TableInfos.ClusteredIndexColumns)
                {
                    TmpTableColumn = columns2.Where(p => p.ColumnName == cc && p.IsVarLenDataType == false).FirstOrDefault();
                    if (TmpTableColumn != null)
                    {
                        columns3[i] = TmpTableColumn;
                        i++;
                    }
                }

                // 其他字段置后
                foreach (TableColumn oth in columns2)
                {
                    TmpTableColumn = columns3.Where(p => p != null && p.ColumnID == oth.ColumnID).FirstOrDefault();
                    if (TmpTableColumn == null)
                    {
                        columns3[i] = oth;
                        i++;
                    }
                }
            }
            else
            {
                columns3 = columns2;
            }

            columns2 = columns3;

            // 预处理主键字段
            if (TableInfos.ClusteredIndexColumns.Count == 0 && TableInfos.PrimaryKeyColumns.Count > 0)
            {
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string pc in TableInfos.PrimaryKeyColumns)
                {
                    TmpTableColumn = columns2.Where(p => p.ColumnName == pc && p.IsVarLenDataType == false).FirstOrDefault();
                    if (TmpTableColumn != null)
                    {
                        columns3[i] = TmpTableColumn;
                        i++;
                    }
                }

                // 其他字段置后
                foreach (TableColumn oth in columns2)
                {
                    TmpTableColumn = columns3.Where(p => p != null && p.ColumnID == oth.ColumnID).FirstOrDefault();
                    if (TmpTableColumn == null)
                    {
                        columns3[i] = oth;
                        i++;
                    }
                }
            }
            else
            {
                columns3 = columns2;
            }

            // 根据字段总数 计算null值列表长度(字节数)
            NullStatusLength = (short)Math.Ceiling((double)AllColumnCountLog / (double)8.0);
            NullStatus = "";
            for (i = 0; i <= NullStatusLength - 1; i++)
            {
                NullStatus = rowdata[index2].ToBinaryString() + NullStatus;
                index2 = index2 + 1;
            }
            NullStatus = NullStatus.Reverse();  // 字符串反转

            if (TableInfos.IsHeapTable == false && TableInfos.PrimaryKeyColumns.SequenceEqual(TableInfos.ClusteredIndexColumns) == false)
            {
                NullStatus = NullStatus.Substring(1, NullStatus.Length - 1);
            }

            while (NullStatus.Length < columns3.Length)
            {
                NullStatus = NullStatus + "0";
            }

            foreach (TableColumn c in columns3)
            {
                if (c.IsNullable == false)
                {
                    c.IsNull = false;
                }
                else
                {
                    if (c.LeafNullBit - 1 >= 0)
                    {
                        c.IsNull = (NullStatus.Substring(c.LeafNullBit - 1, 1) == "1" ? true : false);
                    }
                    else
                    {
                        c.IsNull = true;
                    }
                }
            }

            // 定长字段
            foreach (TableColumn c in columns3)
            {
                if (c.IsVarLenDataType == true || c.IsExists == false) { continue; }

                index3 = index;
                if (index != c.LeafOffset)
                {
                    index = c.LeafOffset;
                }
                c.LogContentsStartIndex = index;

                if (c.IsNull == true && c.IsNullable == true && c.PhysicalStorageType != System.Data.SqlDbType.Bit)
                {
                    c.Value = "nullvalue";
                    index = index + c.Length;
                }
                else
                {
                    switch (c.PhysicalStorageType)
                    {
                        case System.Data.SqlDbType.Char:
                            c.Value = System.Text.Encoding.Default.GetString(rowdata, index, c.Length).TrimEnd();
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.NChar:
                            c.Value = System.Text.Encoding.Unicode.GetString(rowdata, index, c.Length).TrimEnd();
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.DateTime:
                            c.Value = TranslateData_DateTime(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.DateTime2:
                            c.Value = TranslateData_DateTime2(rowdata, index, c.Length, c.Scale);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.DateTimeOffset:
                            c.Value = TranslateData_DateTimeOffset(rowdata, index, c.Length, c.Scale);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.SmallDateTime:
                            c.Value = TranslateData_SmallDateTime(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Date:
                            c.Value = TranslateData_Date(rowdata, index);
                            index = index + 3;
                            break;
                        case System.Data.SqlDbType.Time:
                            c.Value = TranslateData_Time(rowdata, index, c.Length, c.Scale);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Int:
                            c.Value = BitConverter.ToInt32(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.BigInt:
                            c.Value = BitConverter.ToInt64(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.SmallInt:
                            c.Value = BitConverter.ToInt16(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.TinyInt:
                            c.Value = Convert.ToInt32(rowdata[index]);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Decimal:
                            c.Value = TranslateData_Decimal(rowdata, index, c.Length, c.Scale);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Real:
                            c.Value = TranslateData_Real(rowdata, index, c.Length);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Float:
                            c.Value = TranslateData_Float(rowdata, index, c.Length);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Money:
                            c.Value = TranslateData_Money(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.SmallMoney:
                            c.Value = TranslateData_SmallMoney(rowdata, index);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Bit:
                            int iJumpIndexLength;
                            string bValueBit;

                            BitValueStartIndex = (BitColumnDataIndex == -1 ? index : BitValueStartIndex);
                            iJumpIndexLength = 0;
                            bValueBit = TranslateData_Bit(rowdata, columns, index, c.ColumnName, BitColumnCount, m_bBitColumnData, BitColumnDataIndex, ref iJumpIndexLength, ref m_bBitColumnData, ref BitColumnDataIndex);

                            BitValueStartIndex = (iJumpIndexLength > 0 ? index : BitValueStartIndex);
                            index = index + iJumpIndexLength;

                            c.LogContentsStartIndex = BitValueStartIndex;
                            c.Value = bValueBit;
                            c.LogContentsEndIndex = BitValueStartIndex;
                            break;
                        case System.Data.SqlDbType.Binary:
                            c.Value = TranslateData_Binary(rowdata, index, c.Length);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.Timestamp:
                            c.Value = "null";
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.UniqueIdentifier:
                            c.Value = TranslateData_UniqueIdentifier(rowdata, index, c.Length);
                            index = index + c.Length;
                            break;
                        default:
                            break;
                    }
                }

                c.LogContentsEndIndex = (c.PhysicalStorageType != SqlDbType.Bit ? index - 1 : c.LogContentsEndIndex);
                c.LogContents = rowdata_text.Substring(c.LogContentsStartIndex * 2, (c.LogContentsEndIndex - c.LogContentsStartIndex + 1) * 2);
                index = index3;
            }

            index = index2;

            // 变长字段
            if (index + 1 <= rowdata.Length - 1)
            {
                // 变长字段数量(不一定等于字段类型=变长类型的字段数量)
                tempstr = rowdata_text.Substring((index + 1) * 2, 2) + rowdata_text.Substring(index * 2, 2);
                tempint = Int32.Parse(tempstr, System.Globalization.NumberStyles.HexNumber);
                if (tempint <= 32767 && tempint <= AllColumnCountLog)
                {
                    VarColumnCount = (short)tempint;
                }
                else
                {
                    VarColumnCount = (short)columns3.Count(p => p.IsVarLenDataType == true);
                }
                
                index = index + 2;
                VarlenColumns = new List<FVarColumnInfo>();
                if (index < rowdata.Length - 1)
                {
                    tempstr = rowdata_text.Substring(index * 2, 2 * 2);
                    VarColumnStartIndex = (short)(index + VarColumnCount * 2);
                    VarColumnEndIndex = BitConverter.ToInt16(rowdata, index);
                    
                    for (i = 1, index2 = index; i <= VarColumnCount; i++)
                    {
                        tvc = new FVarColumnInfo();
                        tvc.FIndex = Convert.ToInt16(i * -1);
                        tvc.FEndIndexHex = tempstr;
                        tvc.InRow = tempstr.Substring(2, 2).ToBinaryString().StartsWith("0");

                        tvc.FStartIndex = VarColumnStartIndex;
                        if (tvc.InRow == false)
                        {
                            VarColumnEndIndex = Convert.ToInt16(tempstr.Substring(2, 2).ToBinaryString().Stuff(0, 1, "0") + tempstr.Substring(0, 2).ToBinaryString(), 2);
                        }
                        tvc.FEndIndex = VarColumnEndIndex;

                        tvc.FLogContents = rowdata_text.Substring(VarColumnStartIndex * 2, (VarColumnEndIndex - VarColumnStartIndex) * 2);

                        VarlenColumns.Add(tvc);

                        if (i < VarColumnCount)
                        {
                            index2 = index2 + 2;

                            tempstr = rowdata_text.Substring(index2 * 2, 2 * 2);
                            VarColumnStartIndex = VarColumnEndIndex;
                            VarColumnEndIndex = BitConverter.ToInt16(rowdata, index2);
                        }
                        else
                        {
                            //if (rowdata.Length > VarColumnEndIndex)
                            //{
                            //    throw new Exception();
                            //}
                        }
                    }
                }

                // 循环变长字段列表读取数据
                foreach (TableColumn c in columns3)
                {
                    if (c.IsVarLenDataType == false && c.IsExists == true) { continue; }

                    tvc = VarlenColumns.FirstOrDefault(p => p.FIndex == c.LeafOffset);
                    if (tvc != null)
                    {
                        c.LogContentsStartIndex = tvc.FStartIndex;
                        c.LogContentsEndIndex = tvc.FEndIndex;
                        c.LogContentsEndIndexHex = tvc.FEndIndexHex;
                        c.LogContents = tvc.FLogContents;
                    }

                    if (c.IsNull == true
                        || c.IsExists == false
                        || (tvc == null && c.IsNull == true))
                    {
                        c.IsNull = true;
                        c.Value = "nullvalue";
                        c.ValueHex = "";

                        continue;
                    }

                    if (tvc != null)
                    {
                        switch (c.PhysicalStorageType)
                        {
                            case System.Data.SqlDbType.VarChar:
                                (ValueHex, Value) = TranslateData_VarChar(rowdata, tvc, false);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                break;
                            case System.Data.SqlDbType.NVarChar:
                                (ValueHex, Value) = TranslateData_VarChar(rowdata, tvc, true);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                break;
                            case System.Data.SqlDbType.VarBinary:
                                (ValueHex, Value) = TranslateData_VarBinary(rowdata, tvc);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                break;
                            case System.Data.SqlDbType.Variant:
                                (ValueHex, Value, VariantBaseType, VariantLength, VariantScale, VariantCollation) = TranslateData_Variant(rowdata, tvc);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                c.VariantBaseType = VariantBaseType;
                                c.VariantLength = VariantLength;
                                c.VariantScale = VariantScale;
                                c.VariantCollation = VariantCollation;
                                break;
                            case System.Data.SqlDbType.Xml:
                                (ValueHex, Value) = TranslateData_XML(tvc);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                break;
                            case System.Data.SqlDbType.Text:
                                (ValueHex, Value) = TranslateData_Text(rowdata, tvc, false, TableInfos.TextInRow);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                c.IsNull = (ValueHex == null && Value == "nullvalue");
                                break;
                            case System.Data.SqlDbType.NText:
                                (ValueHex, Value) = TranslateData_Text(rowdata, tvc, true, TableInfos.TextInRow);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                c.IsNull = (ValueHex == null && Value == "nullvalue");
                                break;
                            case System.Data.SqlDbType.Image:
                                (ValueHex, Value) = TranslateData_Image(rowdata, tvc);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                break;
                            default:
                                break;
                        }

                        continue;
                    }
                    else
                    {
                        if (c.IsNull == false
                            && (c.PhysicalStorageType == System.Data.SqlDbType.VarChar || c.PhysicalStorageType == System.Data.SqlDbType.NVarChar))
                        {
                            c.Value = "";
                            c.ValueHex = "";

                            continue;
                        }
                    }
                }
            }
            else
            {
                foreach (TableColumn c in columns)
                {
                    if (c.IsVarLenDataType == true) { c.IsNull = true; }
                }
            }

            // 重新赋值回columns.
            foreach (TableColumn x in columns)
            {
                TmpTableColumn = columns3.Where(p => p.ColumnID == x.ColumnID).FirstOrDefault();

                if (TmpTableColumn != null)
                {
                    x.IsNull = TmpTableColumn.IsNull;
                    x.Value = TmpTableColumn.Value;
                    x.LogContentsStartIndex = TmpTableColumn.LogContentsStartIndex;
                    x.LogContentsEndIndex = TmpTableColumn.LogContentsEndIndex;
                }
                else
                {
                    x.IsNull = true;
                    x.Value = "nullvalue";
                    x.LogContentsStartIndex = -1;
                    x.LogContentsEndIndex = -1;
                }
            }
        }

        private void TranslateData_CompressionROW(byte[] rowdata, TableColumn[] columns, FLOG pLog)
        {
            int i, j, k, offset, length, physicallength;
            string rowdatahex, colconts, colconts2, valuehex, temp;
            List<(int offset, int length, int physicallength)> cols;
            List<int> intail;
            FVarColumnInfo vc;

            rowdatahex = rowdata.ToText();
            cols = new List<(int offset, int length, int physicallength)>();

            //if (bypageinfo == true && 1==2)
            //{
            //    stsql = $"select distinct Object=case when Object like N'Slot%' then Object else substring(Object,charindex(N'Slot',Object),256) end "
            //            + $" from #temppagedata "
            //            + $" where LSN=N'{pLog.Current_LSN}' "
            //            + $" and ParentObject like '%Slot {pLog.Slot_ID.ToString()} Offset%' "
            //            + $" and Object like '%(physical)%'; ";
            //    pl = DB.Query<string>(stsql, false).ToList();

            //    for (i = 1; i <= columns.Length; i = i + 1)
            //    {
            //        plitem = pl.FirstOrDefault(p => p.Contains($"Column {i.ToString()} ") == true);

            //        cols.Add((
            //                  Int32.Parse(plitem.Split(' ')[5].Replace("0x", ""), System.Globalization.NumberStyles.HexNumber), // offset
            //                  Convert.ToInt32(plitem.Split(' ')[7]), // length
            //                  Convert.ToInt32(plitem.Split(' ')[10]) // physicallength
            //                ));
            //    }
            //}
            //else
            //{

            //}
            i = (columns.Length % 2 == 0 ? columns.Length : columns.Length + 1);
            colconts = rowdatahex.Substring(4, i);

            for (j = 1, temp = ""; j <= colconts.Length - 1; j = j + 2)
            {
                temp = temp + colconts.Substring(j - 1, 2).Reverse();
            }
            colconts = temp;

            k = 2 + i / 2;
            intail = new List<int>();
            for (i = 0; i <= columns.Length - 1; i = i + 1)
            {
                temp = colconts.Substring(i, 1);
                switch (temp)
                {
                    case "0": // null
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        break;
                    case "1": // 0
                        length = columns[i].Length;
                        physicallength = 0;
                        offset = k + physicallength;
                        break;
                    case "2":
                    case "3":
                    case "4":
                    case "5":
                    case "6":
                    case "7":
                    case "8":
                    case "9":
                        length = (RowCompressionAffectsStorage.Contains(columns[i].DataType) == false ?
                                    Convert.ToInt32(temp, 16) - 1
                                    :
                                    columns[i].Length);
                        physicallength = Convert.ToInt32(temp, 16) - 1;
                        offset = k;
                        break;
                    case "A":
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        intail.Add(i);
                        break;
                    case "B":
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        break;
                    default:
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        break;
                }

                if (columns[i].DataType == "bit")
                {
                    if (temp == "0")
                    {
                        columns[i].IsNull = true;
                        columns[i].Value = null;
                    }
                    else
                    {
                        columns[i].IsNull = false;
                        columns[i].Value = (temp == "B" ? 1 : 0);
                    }
                }

                cols.Add((offset, length, physicallength));
                k = k + physicallength;
            }

            if (intail.Count > 0)
            {
                colconts2 = rowdatahex.Substring(k * 2, (2 + intail.Count * 2 + 1) * 2);

                offset = k + (2 + intail.Count * 2 + 1);
                for (i = 0, j = 0; i <= intail.Count - 1; i = i + 1)
                {
                    temp = colconts2.Substring((2 + i * 2) * 2, 2 * 2);
                    length = physicallength = Convert.ToInt32(temp, 16) - j;

                    cols[intail[i]] = (offset, length, physicallength);
                    j = j + length;
                    offset = offset + length;
                }
            }

            for (i = 0; i <= columns.Length - 1; i = i + 1)
            {
                if (columns[i].DataType == "bit") { continue; }

                valuehex = rowdatahex.Substring(cols[i].offset * 2, cols[i].physicallength * 2);
                columns[i].ValueHexCompression = valuehex;
                columns[i].IsNull = (cols[i].offset == 0 && cols[i].length == 0 ? true : false);

                if (columns[i].IsNull == false)
                {
                    if (RowCompressionAffectsStorage.Contains(columns[i].DataType) == false)
                    {
                        columns[i].ValueHex = valuehex;
                        switch (columns[i].DataType)
                        {
                            case "tinyint":
                                columns[i].Value = Convert.ToInt32(valuehex.ToByteArray()[0]);
                                break;
                            case "smalldatetime":
                                columns[i].Value = TranslateData_SmallDateTime(valuehex.ToByteArray(), 0);
                                break;
                            case "date":
                                columns[i].Value = TranslateData_Date(valuehex.ToByteArray(), 0);
                                break;
                            case "time":
                                columns[i].Value = TranslateData_Time(valuehex.ToByteArray(), 0, columns[i].Length, columns[i].Scale);
                                break;
                            case "varchar":
                                vc = new FVarColumnInfo() { InRow = true, FLogContents = valuehex };
                                (columns[i].ValueHex, columns[i].Value) = TranslateData_VarChar(valuehex.ToByteArray(), vc, false);
                                break;
                            case "nvarchar":
                                vc = new FVarColumnInfo() { InRow = true, FLogContents = valuehex };
                                (columns[i].ValueHex, columns[i].Value) = TranslateData_VarChar(valuehex.ToByteArray(), vc, true);
                                break;
                            case "xml":
                                vc = new FVarColumnInfo() { FLogContents = valuehex };
                                (_, columns[i].Value) = TranslateData_XML(vc);
                                break;
                        }
                    }
                    else
                    {
                        switch (columns[i].DataType)
                        {
                            case "smallint":
                                columns[i].ValueHex = UnCompression_SMALLINT(valuehex);
                                columns[i].Value = BitConverter.ToInt16(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "int":
                                columns[i].ValueHex = UnCompression_INT(valuehex);
                                columns[i].Value = BitConverter.ToInt32(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "bigint":
                                columns[i].ValueHex = UnCompression_BIGINT(valuehex);
                                columns[i].Value = BitConverter.ToInt64(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "decimal":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = TranslateData_VarDecimal(valuehex);
                                break;
                            case "bit":
                                break;
                            case "smallmoney":
                                columns[i].ValueHex = UnCompression_SMALLMONEY(valuehex);
                                columns[i].Value = TranslateData_SmallMoney(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "money":
                                columns[i].ValueHex = UnCompression_MONEY(valuehex);
                                columns[i].Value = TranslateData_Money(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "float":
                                columns[i].ValueHex = UnCompression_FLOAT(valuehex, columns[i].Length);
                                columns[i].Value = TranslateData_Float(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length);
                                break;
                            case "real":
                                columns[i].ValueHex = UnCompression_REAL(valuehex, columns[i].Length);
                                columns[i].Value = TranslateData_Real(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length);
                                break;
                            case "datetime":
                                columns[i].ValueHex = UnCompression_DATETIME(valuehex);
                                columns[i].Value = TranslateData_DateTime(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "datetime2":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = TranslateData_DateTime2(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length, columns[i].Scale);
                                break;
                            case "datetimeoffset":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = TranslateData_DateTimeOffset(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length, columns[i].Scale);
                                break;
                            case "char":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = System.Text.Encoding.Default.GetString(columns[i].ValueHex.ToByteArray(), 0, valuehex.Length / 2).TrimEnd();
                                break;
                            case "nchar":
                                columns[i].ValueHex = UnCompression_NCHAR(valuehex);
                                columns[i].Value = System.Text.Encoding.Unicode.GetString(columns[i].ValueHex.ToByteArray(), 0, columns[i].ValueHex.Length / 2).TrimEnd();
                                break;
                            case "binary":
                                columns[i].ValueHex = UnCompression_BINARY(valuehex, columns[i].Length);
                                columns[i].Value = TranslateData_Binary(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length);
                                break;
                            case "timestamp":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = "null";
                                break;
                            default:
                                columns[i].ValueHex = valuehex;
                                break;
                        }
                    }
                }
            }

        }

        private string UnCompression_SMALLINT(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(2); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(2 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_INT(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(4); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(4 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_BIGINT(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(8); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(8 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_SMALLMONEY(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(4); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(4 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_MONEY(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(8); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(8 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_FLOAT(string pcvalue, short len)
        {
            string rvalue;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(len); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                rvalue = "00".Replicate(len - pcvalue.Length / 2) + pcvalue;
            }

            return rvalue;
        }

        private string UnCompression_REAL(string pcvalue, short len)
        {
            string rvalue;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(len); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                rvalue = "00".Replicate(len - pcvalue.Length / 2) + pcvalue;
            }

            return rvalue;
        }

        private string UnCompression_DATETIME(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(8); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(8 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_NCHAR(string pcvalue)
        {
            string rvalue, t, n;
            int i;

            if ((pcvalue.Length / 2) % 2 == 0)
            {
                rvalue = pcvalue;
            }
            else
            {
                rvalue = "";
                for (i = 0; i <= pcvalue.Length - 2; i = i + 2)
                {
                    t = pcvalue.Substring(i, 2);
                    n = (pcvalue.Length - 1 > i + 2 ? pcvalue.Substring(i + 2, 2) : "");

                    if (t == "10" && i == pcvalue.Length - 2)
                    {
                        break;
                    }

                    if (t != "00" && n == "00")
                    {
                        rvalue = rvalue + t + n;
                        i = i + 2;
                        continue;
                    }

                    if (t == "0E")
                    {
                        i = i + 2;
                        t = pcvalue.Substring(i, 2);
                        n = pcvalue.Substring(i + 2, 2);

                        rvalue = rvalue + n + t;
                        i = i + 2;

                        continue;
                    }

                    rvalue = rvalue + t + "00";
                }
            }

            return rvalue;
        }

        private string UnCompression_BINARY(string pcvalue, short len)
        {
            string rvalue;

            rvalue = pcvalue + "00".Replicate(len - pcvalue.Length / 2);

            return rvalue;
        }

        private string TranslateData_VarDecimal(string pcvalue)
        {
            string rvalue, pcvalue2, sg, zs, ws;
            int zsv, wsv;
            double bv;

            pcvalue2 = pcvalue.ToBinaryString();
            sg = (pcvalue2.StartsWith("1") ? "" : "-");
            zs = pcvalue2.Substring(1, 7);
            ws = pcvalue2.Substring(8, pcvalue2.Length - 8);

            zsv = Convert.ToInt32(zs, 2) - 64;
            ws = ws + new string('0', 10 * Convert.ToInt32(Math.Ceiling(ws.Length / 10.0)) - ws.Length);
            wsv = Convert.ToInt32(ws, 2);
            bv = Convert.ToDouble(wsv.ToString().Insert(1, ".")) * Math.Pow(10, zsv);
            rvalue = $"{sg}{bv.ToString()}";

            return rvalue;
        }

        private (TableInformation, TableColumn[]) GetTableInfo(string pSchemaName, string pTablename)
        {
            string stemp;
            TableInformation tableinfo;
            TableColumn[] tablecolumns;

            tableinfo = new TableInformation();

            // PrimaryKeyColumns
            stsql = "select primarykeycolumn=c.name "
                     + " from sys.indexes a "
                     + " join sys.index_columns b on a.object_id=b.object_id and a.index_id=b.index_id "
                     + " join sys.columns c on b.object_id=c.object_id and b.column_id=c.column_id "
                     + " join sys.objects d on a.object_id=d.object_id "
                     + " join sys.schemas s on d.schema_id=s.schema_id "
                     + " where a.is_primary_key=1 "
                     + $" and s.name=N'{pSchemaName}' "
                     + "  and d.type='U' "
                     + $" and d.name=N'{pTablename}' "
                     + "  order by b.key_ordinal; ";
            tableinfo.PrimaryKeyColumns = DB.Query<string>(stsql, false).ToList();

            // ClusteredIndexColumns
            stsql = "select clusteredindexcolumn=c.name "
                    + "  from sys.indexes a "
                    + "  join sys.index_columns b on a.object_id=b.object_id and a.index_id=b.index_id "
                    + "  join sys.columns c on b.object_id=c.object_id and b.column_id=c.column_id "
                    + "  join sys.objects d on a.object_id=d.object_id "
                    + "  join sys.schemas s on d.schema_id=s.schema_id "
                    + "  where a.index_id<=1 "
                    + "  and a.type=1 "
                    + $" and s.name=N'{pSchemaName}' "
                    + "  and d.type='U' "
                    + $" and d.name=N'{pTablename}' "
                    + "  order by b.key_ordinal; ";
            tableinfo.ClusteredIndexColumns = DB.Query<string>(stsql, false).ToList();

            // IsHeapTable
            stsql = "select isheaptable=cast(case when exists(select 1 "
                      + "                                     from sys.tables t "
                      + "                                     join sys.schemas s on t.schema_id=s.schema_id "
                      + "                                     join sys.indexes i on t.object_id=i.object_id "
                      + $"                                    where s.name=N'{pSchemaName}' "
                      + $"                                    and t.name=N'{pTablename}' "
                      + "                                     and i.index_id=0) then 1 else 0 end as bit); ";
            tableinfo.IsHeapTable = DB.Query<bool>(stsql, false).FirstOrDefault();

            // AllocUnitName
            stsql = "select allocunitname=isnull(d.name,N'') "
                    + "  from sys.tables a "
                    + "  join sys.schemas s on a.schema_id=s.schema_id "
                    + "  join sys.indexes d on a.object_id=d.object_id "
                    + "  where d.type in(0,1,5) "
                    + $" and s.name=N'{pSchemaName}' "
                    + $" and a.name=N'{pTablename}'; ";
            tableinfo.AllocUnitName = DB.Query<string>(stsql, false).FirstOrDefault();

            // TextInRow
            stsql = "select textinrow=a.text_in_row_limit, "
                    + $"    isnodetable={(DB.Vesion >= 2017 ? "a.is_node" : "0")}, "
                    + $"    isedgetable={(DB.Vesion >= 2017 ? "a.is_edge" : "0")}"
                    + "  from sys.tables a "
                    + "  join sys.schemas s on a.schema_id=s.schema_id "
                    + $" where s.name=N'{pSchemaName}' "
                    + $" and a.name=N'{pTablename}'; ";
            (tableinfo.TextInRow, tableinfo.IsNodeTable, tableinfo.IsEdgeTable) = DB.Query<(int, bool, bool)>(stsql, false).FirstOrDefault();

            // IsColumnStore
            stsql = "select iscolumnstore=cast(case when exists(select 1 "
                      + "                                       from sys.tables t "
                      + "                                       join sys.schemas s on t.schema_id=s.schema_id "
                      + "                                       join sys.indexes i on t.object_id=i.object_id "
                      + $"                                      where s.name=N'{pSchemaName}' "
                      + $"                                      and t.name=N'{pTablename}' "
                      + "                                       and i.index_id=1 "
                      + "                                       and i.type=5) then 1 else 0 end as bit); ";
            tableinfo.IsColumnStore = DB.Query<bool>(stsql, false).FirstOrDefault();

            // DataCompressionType
            stsql = "select PartitionId=p.partition_id, "
                    + "     CompressionType=case p.data_compression when 0 then N'NONE' when 1 then N'ROW' when 2 then N'PAGE' when 3 then N'COLUMNSTORE' when 4 then N'COLUMNSTORE_ARCHIVE' else N'' end "
                    + "  from sys.tables t "
                    + "  join sys.schemas s on t.schema_id=s.schema_id "
                    + "  join sys.partitions p on t.object_id=p.object_id "
                    + $" where s.name=N'{pSchemaName}' "
                    + $" and t.name=N'{pTablename}' "
                    + "  and p.index_id<=1; ";
            tableinfo.DataCompressionType = DB.Query<(long PartitionId, CompressionType CompressionType)>(stsql, false).ToDictionary(p => p.PartitionId, p => p.CompressionType);

            stsql = "select cast(("
                        + "select ColumnID,ColumnName,DataType,PhysicalStorageType,Length,Precision,IsNullable,Scale,IsIdentity,IsComputed,LeafOffset,LeafNullBit,IsHidden,GraphType "
                        + " from (select 'ColumnID'=b.column_id, "
                        + "              'ColumnName'=b.name, "
                        + "              'DataType'=c.name, "
                        + "              'PhysicalStorageType'=c2.name, "
                        + "              'Length'=b.max_length, "
                        + "              'Precision'=b.precision, "
                        + "              'IsNullable'=b.is_nullable, "
                        + "              'Scale'=b.scale, "
                        + "              'IsIdentity'=b.is_identity, "
                        + "              'IsComputed'=b.is_computed, "
                        + "              'LeafOffset'=isnull(d2.leaf_offset,0), "
                        + "              'LeafNullBit'=isnull(d2.leaf_null_bit,0), "
                        + $"             'IsHidden'={(DB.Vesion >= 2017 ? "b.is_hidden" : "0")}, "
                        + $"             'GraphType'=isnull({(DB.Vesion >= 2017 ? "b.graph_type" : "null")},-1) "
                        + "       from sys.tables a "
                        + "       join sys.schemas s on a.schema_id=s.schema_id "
                        + "       join sys.columns b on a.object_id=b.object_id "
                        + "       join sys.systypes c on b.system_type_id=c.xtype and b.user_type_id=c.xusertype "
                        + "       join sys.systypes c2 on c.xtype=c2.xtype and c.xtype=c2.xusertype "
                        + "       outer apply (select d.leaf_offset,d.leaf_null_bit "
                        + "                    from sys.system_internals_partition_columns d "
                        + "                    where d.partition_column_id=b.column_id "
                        + "                    and d.partition_id in (select partitionss.partition_id "
                        + "                                           from sys.allocation_units allocunits "
                        + "                                           join sys.partitions partitionss on (allocunits.type in(1, 3) and allocunits.container_id=partitionss.hobt_id) "
                        + "                                                                              or (allocunits.type=2 and allocunits.container_id=partitionss.partition_id) "
                        + "                                           where partitionss.object_id=a.object_id and partitionss.index_id<=1)) d2 "
                        + $"      where s.name=N'{pSchemaName}' "
                        + $"      and a.name=N'{pTablename}') t "
                        + " order by ColumnID "
                        + " for xml raw('Column'),root('ColumnList') "
                        + ") as nvarchar(max)); ";
            stemp = DB.Query11(stsql, false);
            tablecolumns = AnalyzeTablelayout(stemp);

            return (tableinfo, tablecolumns);
        }

        public TableColumn[] AnalyzeTablelayout(string TableLayout)
        {
            int i;
            XmlDocument xmlDoc;
            XmlNode xmlRootnode;
            XmlNodeList xmlNodelist;
            TableColumn[] TableColumns;
            TableColumn fcol;

            xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(TableLayout);
            xmlRootnode = xmlDoc.SelectSingleNode("ColumnList");
            xmlNodelist = xmlRootnode.ChildNodes;

            TableColumns = new TableColumn[xmlNodelist.Count];
            i = 0;
            foreach (XmlNode xmlNode in xmlNodelist)
            {
                fcol = new TableColumn();
                fcol.ColumnID = Convert.ToInt16(xmlNode.Attributes["ColumnID"].Value.ToString());
                fcol.ColumnName = xmlNode.Attributes["ColumnName"].Value;
                fcol.DataType = xmlNode.Attributes["DataType"].Value;
                switch (xmlNode.Attributes["PhysicalStorageType"].Value)
                {
                    case "bigint": fcol.PhysicalStorageType = System.Data.SqlDbType.BigInt; break;
                    case "binary": fcol.PhysicalStorageType = System.Data.SqlDbType.Binary; break;
                    case "bit": fcol.PhysicalStorageType = System.Data.SqlDbType.Bit; break;
                    case "char": fcol.PhysicalStorageType = System.Data.SqlDbType.Char; break;
                    case "date": fcol.PhysicalStorageType = System.Data.SqlDbType.Date; break;
                    case "datetime": fcol.PhysicalStorageType = System.Data.SqlDbType.DateTime; break;
                    case "datetime2": fcol.PhysicalStorageType = System.Data.SqlDbType.DateTime2; break;
                    case "datetimeoffset": fcol.PhysicalStorageType = System.Data.SqlDbType.DateTimeOffset; break;
                    case "decimal": fcol.PhysicalStorageType = System.Data.SqlDbType.Decimal; break;
                    case "float": fcol.PhysicalStorageType = System.Data.SqlDbType.Float; break;
                    case "geography": fcol.PhysicalStorageType = System.Data.SqlDbType.VarBinary; break;
                    case "geometry": fcol.PhysicalStorageType = System.Data.SqlDbType.VarBinary; break;
                    case "hierarchyid": fcol.PhysicalStorageType = System.Data.SqlDbType.VarBinary; break;
                    case "image": fcol.PhysicalStorageType = System.Data.SqlDbType.Image; break;
                    case "int": fcol.PhysicalStorageType = System.Data.SqlDbType.Int; break;
                    case "money": fcol.PhysicalStorageType = System.Data.SqlDbType.Money; break;
                    case "nchar": fcol.PhysicalStorageType = System.Data.SqlDbType.NChar; break;
                    case "ntext": fcol.PhysicalStorageType = System.Data.SqlDbType.NText; break;
                    case "numeric": fcol.PhysicalStorageType = System.Data.SqlDbType.Decimal; break; // numeric=decimal
                    case "nvarchar": fcol.PhysicalStorageType = System.Data.SqlDbType.NVarChar; break;
                    case "real": fcol.PhysicalStorageType = System.Data.SqlDbType.Real; break;
                    case "smalldatetime": fcol.PhysicalStorageType = System.Data.SqlDbType.SmallDateTime; break;
                    case "smallint": fcol.PhysicalStorageType = System.Data.SqlDbType.SmallInt; break;
                    case "smallmoney": fcol.PhysicalStorageType = System.Data.SqlDbType.SmallMoney; break;
                    case "sql_variant": fcol.PhysicalStorageType = System.Data.SqlDbType.Variant; break;
                    case "sysname": fcol.PhysicalStorageType = System.Data.SqlDbType.NVarChar; break;
                    case "text": fcol.PhysicalStorageType = System.Data.SqlDbType.Text; break;
                    case "time": fcol.PhysicalStorageType = System.Data.SqlDbType.Time; break;
                    case "timestamp": fcol.PhysicalStorageType = System.Data.SqlDbType.Timestamp; break;
                    case "tinyint": fcol.PhysicalStorageType = System.Data.SqlDbType.TinyInt; break;
                    case "uniqueidentifier": fcol.PhysicalStorageType = System.Data.SqlDbType.UniqueIdentifier; break;
                    case "varbinary": fcol.PhysicalStorageType = System.Data.SqlDbType.VarBinary; break;
                    case "varchar": fcol.PhysicalStorageType = System.Data.SqlDbType.VarChar; break;
                    case "xml": fcol.PhysicalStorageType = System.Data.SqlDbType.Xml; break;
                    default: break;
                }
                fcol.Length = Convert.ToInt16(xmlNode.Attributes["Length"].Value);
                fcol.Precision = Convert.ToInt16(xmlNode.Attributes["Precision"].Value);
                fcol.Scale = Convert.ToInt16(xmlNode.Attributes["Scale"].Value);
                fcol.IsIdentity = (xmlNode.Attributes["IsIdentity"].Value.ToString() == "0" ? false : true);
                fcol.IsComputed = (xmlNode.Attributes["IsComputed"].Value.ToString() == "0" ? false : true);
                fcol.LeafOffset = Convert.ToInt16(xmlNode.Attributes["LeafOffset"].Value);
                fcol.LeafNullBit = Convert.ToInt16(xmlNode.Attributes["LeafNullBit"].Value);
                fcol.IsNullable = (Convert.ToInt16(xmlNode.Attributes["IsNullable"].Value) == 1 ? true : false);
                fcol.IsHidden = (xmlNode.Attributes["IsHidden"].Value.ToString() == "0" ? false : true);
                fcol.GraphType = Convert.ToInt16(xmlNode.Attributes["GraphType"].Value);

                TableColumns[i] = fcol;
                i = i + 1;
            }

            return TableColumns;
        }

        private string ColumnValue2SQLValue(TableColumn pcol)
        {
            string sValue;
            bool bNeedSeparatorchar, bIsUnicodeType;
            string[] NoSeparatorchar, UnicodeType;
            SqlDbType? datatype;

            datatype = (pcol.PhysicalStorageType != SqlDbType.Variant ? pcol.PhysicalStorageType : pcol.VariantBaseType);

            if (pcol.IsNull == true || pcol.Value == null || datatype == null)
            {
                sValue = "null";
            }
            else
            {
                NoSeparatorchar = new string[] { "tinyint", "bigint", "smallint", "int", "money", "smallmoney", "bit", "decimal", "numeric", "float", "real", "varbinary", "binary", "image" };
                UnicodeType = new string[] { "nvarchar", "nchar", "ntext", "xml" };

                bNeedSeparatorchar = (NoSeparatorchar.Any(p => p == datatype.ToString().ToLower()) ? false : true);
                bIsUnicodeType = (UnicodeType.Any(p => p == datatype.ToString().ToLower()) ? true : false);
                
                sValue = (bIsUnicodeType ? "N" : "") + (bNeedSeparatorchar ? "'" : "") + pcol.Value.ToString().Replace("'", "''") + (bNeedSeparatorchar ? "'" : "");

                if (pcol.PhysicalStorageType == SqlDbType.Variant)
                {
                    switch (datatype)
                    {
                        case SqlDbType.UniqueIdentifier:
                            sValue = $"cast({sValue} as uniqueIdentifier)";
                            break;
                        case SqlDbType.Date:
                            sValue = $"cast({sValue} as date)";
                            break;
                        case SqlDbType.Time:
                            sValue = $"cast({sValue} as time({pcol.VariantScale.ToString()}))";
                            break;
                        case SqlDbType.DateTime2:
                            sValue = $"cast({sValue} as datetime2({pcol.VariantScale.ToString()}))";
                            break;
                        case SqlDbType.DateTimeOffset:
                            sValue = $"cast({sValue} as datetimeoffset({pcol.VariantScale.ToString()}))";
                            break;
                        case SqlDbType.TinyInt:
                            sValue = $"cast({sValue} as tinyint)";
                            break;
                        case SqlDbType.SmallInt:
                            sValue = $"cast({sValue} as smallint)";
                            break;
                        case SqlDbType.Int:
                            sValue = $"cast({sValue} as int)";
                            break;
                        case SqlDbType.SmallDateTime:
                            sValue = $"cast({sValue} as smalldatetime)";
                            break;
                        case SqlDbType.Real:
                            sValue = $"cast({sValue} as real)";
                            break;
                        case SqlDbType.Money:
                            sValue = $"cast({sValue} as money)";
                            break;
                        case SqlDbType.DateTime:
                            sValue = $"cast({sValue} as datetime)";
                            break;
                        case SqlDbType.Float:
                            sValue = $"cast({sValue} as float({pcol.VariantLength.ToString()}))";
                            break;
                        case SqlDbType.Bit:
                            sValue = $"cast({sValue} as bit)";
                            break;
                        case SqlDbType.Decimal:  // numeric decimal
                            sValue = $"cast({sValue} as numeric({pcol.VariantLength.ToString()},{pcol.VariantScale.ToString()}))";
                            break;
                        case SqlDbType.VarBinary:
                            sValue = $"cast({sValue} as varbinary({pcol.VariantLength.ToString()}))";
                            break;
                        case SqlDbType.Binary:
                            sValue = $"cast({sValue} as binary({pcol.VariantLength.ToString()}))";
                            break;
                        case SqlDbType.Char:
                            sValue = $"cast({sValue} {(string.IsNullOrEmpty(pcol.VariantCollation) == false ? "collate " + pcol.VariantCollation : "")} as char({pcol.VariantLength.ToString()}))";
                            break;
                        case SqlDbType.SmallMoney:
                            sValue = $"cast({sValue} as smallmoney)";
                            break;
                        case SqlDbType.BigInt:
                            sValue = $"cast({sValue} as bigint)";
                            break;
                        case SqlDbType.VarChar:
                            sValue = $"cast({sValue} {(string.IsNullOrEmpty(pcol.VariantCollation) == false ? "collate " + pcol.VariantCollation : "")} as varchar({pcol.VariantLength.ToString()}))";
                            break;
                        case SqlDbType.NVarChar:
                            sValue = $"cast({sValue} {(string.IsNullOrEmpty(pcol.VariantCollation) == false ? "collate " + pcol.VariantCollation : "")} as nvarchar({pcol.VariantLength.ToString()}))";
                            break;
                        case SqlDbType.NChar:
                            sValue = $"cast({sValue} {(string.IsNullOrEmpty(pcol.VariantCollation) == false ? "collate " + pcol.VariantCollation : "")} as nchar({pcol.VariantLength.ToString()}))";
                            break;
                    }
                }
            }

            return sValue;
        }

        private string ColumnName2SQLName(TableColumn pcol)
        {
            string sqlname;

            switch (pcol.PhysicalStorageType)
            {
                case SqlDbType.Text:
                    sqlname = $"cast([{pcol.ColumnName}] as varchar(max))";
                    break;
                case SqlDbType.NText:
                    sqlname = $"cast([{pcol.ColumnName}] as nvarchar(max))";
                    break;
                default:
                    sqlname = $"[{pcol.ColumnName}]";
                    break;
            }

            return sqlname;
        }

        #region 翻译字段值
        private string TranslateData_Bit(byte[] data, TableColumn[] columns, int iCurrentIndex, string sColumnName, short sBitColumnCount, byte[] m_bBitColumnData0, short sBitColumnDataIndex0, ref int iJumpIndexLength, ref byte[] m_bBitColumnData1, ref short sBitColumnDataIndex1)
        {
            string rBit, sBitColumnData2;
            short i, sCurrentColumnIDinBit;  // 当前字段为第几个Bit类型字段

            m_bBitColumnData1 = m_bBitColumnData0;
            sBitColumnDataIndex1 = sBitColumnDataIndex0;
            sCurrentColumnIDinBit = 0;
            for (i = 0; i <= columns.Length - 1; i++)
            {
                if (columns[i].PhysicalStorageType == SqlDbType.Bit)
                {
                    sCurrentColumnIDinBit = (short)(sCurrentColumnIDinBit + 1);
                    if (columns[i].ColumnName == sColumnName) { break; }
                }
            }

            iJumpIndexLength = 0;
            if (sBitColumnDataIndex1 == -1 || (sBitColumnDataIndex1 + 1) * 8 < sCurrentColumnIDinBit)
            {
                sBitColumnDataIndex1 = (short)(sBitColumnDataIndex1 + 1);
                Array.Copy(data, iCurrentIndex, m_bBitColumnData1, sBitColumnDataIndex1, 1);  // 读入1个字节
                iJumpIndexLength = iJumpIndexLength + 1;
            }

            sBitColumnData2 = string.Empty;
            for (i = sBitColumnDataIndex1; i >= 0; i--)
            {
                sBitColumnData2 = sBitColumnData2 + m_bBitColumnData1[i].ToBinaryString();
            }

            sBitColumnData2 = sBitColumnData2.Reverse();   // 字符串反转
            rBit = sBitColumnData2.Substring(sCurrentColumnIDinBit - 1, 1);

            return rBit;
        }

        private string TranslateData_Date(byte[] data, int iCurrentIndex)
        {
            string returnDate, hDate;
            DateTime date1;
            byte[] bDate;
            int days_date;

            date1 = new DateTime(1900, 1, 1, 0, 0, 0);
            bDate = new byte[3];
            Array.Copy(data, iCurrentIndex, bDate, 0, 3);

            hDate = "";
            foreach (byte b in bDate)
            {
                hDate = b.ToString("X2") + hDate;
            }

            days_date = Convert.ToInt32(hDate, 16) - 693595;
            date1 = date1.AddDays(days_date);
            returnDate = date1.ToString("yyyy-MM-dd");

            return returnDate;
        }

        private string TranslateData_DateTime(byte[] data, int iCurrentIndex)
        {
            string sReturnDatetime;
            DateTime date0;
            int second, days;

            date0 = new DateTime(1900, 1, 1, 0, 0, 0);

            // 前四个字节  以1/300秒保存
            second = BitConverter.ToInt32(data, iCurrentIndex);
            date0 = date0.AddMilliseconds(second * 3.3333333333);
            iCurrentIndex = iCurrentIndex + 4;

            // 后四个字节  为1900-1-1后的天数
            days = BitConverter.ToInt32(data, iCurrentIndex);
            date0 = date0.AddDays(days);

            sReturnDatetime = date0.ToString("yyyy-MM-dd HH:mm:ss.fff");

            return sReturnDatetime;
        }

        private string TranslateData_Time(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sTimeHex, sTimeDec, sTimeSeconds, sTimeSeconds2, sReturnTime;
            byte[] bTime;
            System.DateTime date2;

            bTime = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bTime, 0, sLength);
            
            sTimeHex = "";
            foreach (byte b in bTime)
            {
                sTimeHex = b.ToString("X2") + sTimeHex;
            }
             
            sTimeDec = Convert.ToInt64(sTimeHex, 16).ToString();
            if (sTimeDec.Length <= sScale)
            {
                sTimeSeconds = "0";
                sTimeSeconds2 = new string('0', sScale);
                sTimeSeconds2 = sTimeSeconds2 + sTimeDec;      // 秒的小数部分
                sTimeSeconds2 = sTimeSeconds2.Substring(sTimeSeconds2.Length - sScale, sScale);
            }
            else
            {
                sTimeSeconds = sTimeDec.Substring(0, sTimeDec.Length - sScale);
                sTimeSeconds2 = sTimeDec.Substring(sTimeDec.Length - sScale, sScale);    // 秒的小数部分
            }

            date2 = new DateTime(1900, 1, 1, 0, 0, 0);
            date2 = date2.AddSeconds(Convert.ToDouble(sTimeSeconds));
            sReturnTime = date2.ToString("HH:mm:ss") + (sTimeSeconds2.Length > 0 ? "." : "") + sTimeSeconds2;

            return sReturnTime;
        }

        private string TranslateData_DateTime2(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sReturnDatetime2, sDate, sTime;
            byte[] bDatetime2;

            bDatetime2 = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDatetime2, 0, sLength);
            sTime = TranslateData_Time(bDatetime2, 0, (short)(sLength - 3), sScale);
            sDate = TranslateData_Date(bDatetime2, sLength - 3);
            sReturnDatetime2 = $"{sDate} {sTime}";

            return sReturnDatetime2;
        }

        private string TranslateData_DateTimeOffset(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sReturnDateTimeOffset, sDate, sTime, sOffset;
            short sSignOffset, iOffset;
            byte[] bDateTimeOffset;
            DateTime d0;

            bDateTimeOffset = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDateTimeOffset, 0, sLength);

            // offset
            sSignOffset = 1;
            iOffset = Convert.ToInt16(bDateTimeOffset[sLength - 1].ToString("X2").Substring(1, 1) + bDateTimeOffset[sLength - 2].ToString("X2"), 16);
            if (bDateTimeOffset[sLength - 1].ToBinaryString().Substring(0, 1) == "1")
            {
                sSignOffset = -1;
                iOffset = (short)(Convert.ToInt16("FFF", 16) + 1 - iOffset);
            }

            d0 = new DateTime(1900, 1, 1, 0, 0, 0);
            d0 = d0.AddMinutes(iOffset);
            sOffset = (sSignOffset == 1 ? "+" : "-") + d0.ToString("HH:mm");

            // date
            sDate = TranslateData_Date(bDateTimeOffset, sLength - 5);

            // time
            sTime = TranslateData_Time(bDateTimeOffset, 0, (short)(sLength - 5), sScale);

            // 计算offset
            d0 = new DateTime();
            d0 = DateTime.Parse(sDate + " " + sTime);
            d0 = d0.AddMinutes(sSignOffset * iOffset);

            sDate = d0.ToString("yyyy-MM-dd");
            sTime = d0.ToString("HH:mm:ss.fffffff");

            sTime = sTime.Substring(0, sTime.IndexOf(".", 0) + 1)
                    + Convert.ToInt32(sTime.Substring(sTime.IndexOf(".", 0) + 1, sTime.Length - sTime.IndexOf(".", 0) - 1).Reverse()).ToString().Reverse();

            sReturnDateTimeOffset = sDate + " " + sTime + sOffset;

            return sReturnDateTimeOffset;
        }

        private string TranslateData_SmallDateTime(byte[] data, int iCurrentIndex)
        {
            string sReturnSmallDatetime;

            byte[] bSmallDatetime = new byte[4];
            Array.Copy(data, iCurrentIndex, bSmallDatetime, 0, 4);

            System.DateTime date0 = new DateTime(1900, 1, 1, 0, 0, 0);

            // 前2个字节保存分钟数
            int iMinutes = Convert.ToInt32(bSmallDatetime[1].ToString("X2") + bSmallDatetime[0].ToString("X2"), 16);
            date0 = date0.AddMinutes(iMinutes);

            // 后2个字节为1900-1-1后的天数
            int iDays = Convert.ToInt32(bSmallDatetime[3].ToString("X2") + bSmallDatetime[2].ToString("X2"), 16);
            date0 = date0.AddDays(iDays);

            sReturnSmallDatetime = date0.ToString("yyyy-MM-dd HH:mm:ss");

            return sReturnSmallDatetime;
        }

        private string TranslateData_Money(byte[] data, int iCurrentIndex)
        {
            string sReturnMoney, sSign;
            byte[] bMoney;

            bMoney = new byte[8];
            Array.Copy(data, iCurrentIndex, bMoney, 0, 8);

            if (bMoney[7].ToBinaryString().Substring(7, 1) == "0")
            { sSign = ""; }
            else
            { sSign = "-"; }

            string sMoneyHex, sMoney, sTemp;
            short iMoney;

            sMoneyHex = "";
            for (iMoney = 7; iMoney >= 0; iMoney--)
            {
                sMoneyHex = sMoneyHex + bMoney[iMoney].ToString("X2");
            }

            sMoney = BigInteger.Parse(sMoneyHex, System.Globalization.NumberStyles.HexNumber).ToString();

            if (sSign == "")
            { // 正数

            }
            else
            { // 负数
                BigInteger bigintMoney;
                bigintMoney = BigInteger.Parse("FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber) 
                              + 1
                              - BigInteger.Parse(sMoneyHex, System.Globalization.NumberStyles.HexNumber);

                sMoney = bigintMoney.ToString();
            }

            sTemp = new string('0', (sMoney.Length < 5 ? 5 - sMoney.Length : 0));
            sMoney = sTemp + sMoney;
            sMoney = sMoney.Insert(sMoney.Length - 4, ".");

            if (sSign == "-" && sMoney.StartsWith("-"))
            {
                sSign = "";
                sMoney = sMoney.Stuff(0, 1, "");
            }

            sReturnMoney = sSign + sMoney;

            return sReturnMoney;
        }

        private string TranslateData_SmallMoney(byte[] data, int iCurrentIndex)
        {
            string sReturnSmallMoney, sSign, sSmallMoneyHex, sSmallMoney;
            byte[] bSmallMoney;
            short iSmallMoney;
            BigInteger bigintSmallMoney;

            bSmallMoney = new byte[4];
            Array.Copy(data, iCurrentIndex, bSmallMoney, 0, 4);

            sSign = (bSmallMoney[3].ToBinaryString().Substring(7, 1) == "0" ? "" : "-");
            sSmallMoneyHex = "";
            for (iSmallMoney = 3; iSmallMoney >= 0; iSmallMoney--)
            {
                sSmallMoneyHex = sSmallMoneyHex + bSmallMoney[iSmallMoney].ToString("X2");
            }

            sSmallMoney = BigInteger.Parse(sSmallMoneyHex, System.Globalization.NumberStyles.HexNumber).ToString();

            if (sSign != "")
            {   // 负数
                bigintSmallMoney = BigInteger.Parse("FFFFFFFF", System.Globalization.NumberStyles.HexNumber) + 1
                                   - BigInteger.Parse(sSmallMoneyHex, System.Globalization.NumberStyles.HexNumber);
                sSmallMoney = bigintSmallMoney.ToString();
            }

            sSmallMoney = "0".Replicate((sSmallMoney.Length < 5 ? 5 - sSmallMoney.Length : 0)) + sSmallMoney;
            sSmallMoney = sSmallMoney.Insert(sSmallMoney.Length - 4, ".");

            if (sSign == "-" && sSmallMoney.StartsWith("-"))
            {
                sSign = "";
                sSmallMoney = sSmallMoney.Stuff(0, 1, "");
            }
            sReturnSmallMoney = sSign + sSmallMoney;

            return sReturnSmallMoney;
        }

        private string TranslateData_Decimal(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            byte[] bDecimal;
            string sDecimalHex, sDecimal, sTemp;
            short sSignDecimal;
            int iDecimal;

            bDecimal = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDecimal, 0, sLength);

            sSignDecimal = Convert.ToInt16(bDecimal[0].ToString("X2") == "00" ? -1 : 1);

            sDecimalHex = "";
            for (iDecimal = 1; iDecimal <= bDecimal.Length - 1; iDecimal++)
            {
                sDecimalHex = bDecimal[iDecimal].ToString("X2") + sDecimalHex;
            }

            sDecimal = BigInteger.Parse(sDecimalHex, System.Globalization.NumberStyles.HexNumber).ToString();
            sTemp = new string('0', (sDecimal.Length < (sScale + 1) ? sScale + 1 - sDecimal.Length : 0));
            sDecimal = sTemp + sDecimal;
            sDecimal = sDecimal.Insert(sDecimal.Length - sScale, ".");

            sDecimal = (sSignDecimal == 1 ? "" : "-") + sDecimal;
            return sDecimal;
        }

        private string TranslateData_Real(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnReal, sExpReal, sFractionReal;
            byte[] bReal;
            short sSignReal;
            int iExpReal, iReal;
            double dFractionReal;

            bReal = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bReal, 0, sLenth);
            
            sSignReal = Convert.ToInt16(bReal[3].ToBinaryString().Substring(0, 1) == "1" ? -1 : 1);

            // 指数
            sExpReal = bReal[3].ToBinaryString().Substring(1, 7)
                       + bReal[2].ToBinaryString().Substring(0, 1);
            iExpReal = Convert.ToInt32(sExpReal, 2);

            // 尾数
            sFractionReal = bReal[2].ToBinaryString().Substring(1, 7)
                            + bReal[1].ToBinaryString()
                            + bReal[0].ToBinaryString();

            if (iExpReal == 0 && sFractionReal == new string('0', 23))
            {
                sReturnReal = "0";
            }
            else
            {
                dFractionReal = 1;
                for (iReal = 0; iReal <= sFractionReal.Length - 1; iReal++)
                {
                    if (sFractionReal.Substring(iReal, 1) == "1")
                    {
                        dFractionReal = dFractionReal + Math.Pow(2, -1 * (iReal + 1));
                    }
                }

                dFractionReal = sSignReal * dFractionReal * Math.Pow(2, iExpReal - 127);
                sReturnReal = ((float)dFractionReal).ToString();
            }

            return sReturnReal;
        }

        private string TranslateData_Float(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sFloatValue, sExpFloat, sFractionFloat;
            byte[] bFloat;
            short sSignFloat;
            int iExpFloat, iFloat;
            double dFractionFloat;

            bFloat = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bFloat, 0, sLenth);

            sSignFloat = Convert.ToInt16(bFloat[7].ToBinaryString().Substring(0, 1) == "1" ? -1 : 1);

            // 指数
            sExpFloat = bFloat[sLenth - 1].ToBinaryString().Substring(1, 7)
                        + bFloat[sLenth - 2].ToBinaryString().Substring(0, 4);
            iExpFloat = Convert.ToInt32(sExpFloat, 2);

            // 尾数
            sFractionFloat = bFloat[6].ToBinaryString().Substring(4, 4)
                             + bFloat[5].ToBinaryString()
                             + bFloat[4].ToBinaryString()
                             + bFloat[3].ToBinaryString()
                             + bFloat[2].ToBinaryString()
                             + bFloat[1].ToBinaryString()
                             + bFloat[0].ToBinaryString();

            if (iExpFloat == 0 && sFractionFloat == new string('0', 52))
            {
                sFloatValue = "0";
            }
            else
            {
                dFractionFloat = 1;
                for (iFloat = 0; iFloat <= sFractionFloat.Length - 1; iFloat++)
                {
                    if (sFractionFloat.Substring(iFloat, 1) == "1")
                    {
                        dFractionFloat = dFractionFloat + Math.Pow(2, -1 * (iFloat + 1));
                    }
                }

                dFractionFloat = sSignFloat * dFractionFloat * Math.Pow(2, iExpFloat - 1023);
                sFloatValue = dFractionFloat.ToString();
            }

            return sFloatValue;
        }

        private string TranslateData_Binary(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnBinary;
            byte[] bBinary;
            short iBinary;

            sReturnBinary = "0x";
            bBinary = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bBinary, 0, sLenth);

            for (iBinary = 0; iBinary <= sLenth - 1; iBinary++)
            {
                sReturnBinary = sReturnBinary + bBinary[iBinary].ToString("X2");
            }

            return sReturnBinary;
        }

        private (string, string) TranslateData_VarBinary(byte[] data, FVarColumnInfo pvc)
        {
            string fvaluehex, fvalue, pointer, pagedata, tmpstr;
            byte[] bVarBinary;
            short iVarBinary, sActualLenth;
            int iCurrentIndex, i, pageqty, cutlen;
            FPageInfo firstpage, textmixpage;
            List<FPageInfo> tmps;

            if (pvc.InRow == true)
            {
                iCurrentIndex = pvc.FStartIndex;
                sActualLenth = (short)(pvc.FEndIndex - pvc.FStartIndex);
                bVarBinary = new byte[sActualLenth];
                Array.Copy(data, iCurrentIndex, bVarBinary, 0, sActualLenth);

                fvaluehex = "";
                for (iVarBinary = 0; iVarBinary <= sActualLenth - 1; iVarBinary++)
                {
                    fvaluehex = fvaluehex + bVarBinary[iVarBinary].ToString("X2");
                }
            }
            else
            {
                try
                {
                    pointer = pvc.FLogContents;
                    pointer = pointer.Stuff(0, 12 * 2, ""); // 跳过12个字节

                    i = 0;
                    tmpstr = pointer.Substring(i * 2, 12 * 2);
                    firstpage = new FPageInfo(tmpstr);
                    i = i + 12;

                    textmixpage = GetPageInfo(firstpage.FileNumPageNum_Hex);
                    firstpage.PageData = textmixpage.PageData;
                    firstpage.PageType = textmixpage.PageType;

                    tmps = new List<FPageInfo>();

                    if (firstpage.PageType == "4")  // TEXT_TREE_PAGE
                    {
                        pagedata = firstpage.PageData;
                        pagedata = pagedata.Stuff(0, (96 + 16) * 2, "");
                        tmpstr = pagedata.Substring(0, 4 * 2);
                        pageqty = Convert.ToInt32(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);

                        for (i = 0; i <= pageqty - 1; i++)
                        {
                            tmpstr = pagedata.Substring(8 + i * 16 * 2, 16 * 2);
                            textmixpage = new FPageInfo(tmpstr, "TEXT_TREE_PAGE");
                            tmps.Add(textmixpage);
                        }
                    }

                    if (firstpage.PageType == "3")  // TEXT_MIX_PAGE
                    {
                        tmps.Add(firstpage);

                        while (i + 12 <= (pointer.Length / 2))
                        {
                            tmpstr = pointer.Substring(i * 2, 12 * 2);
                            textmixpage = new FPageInfo(tmpstr);
                            tmps.Add(textmixpage);
                            i = i + 12;
                        }
                    }

                    fvaluehex = "";
                    i = 0;
                    foreach (FPageInfo tp in tmps)
                    {
                        cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));
                        textmixpage = GetPageInfo(tp.FileNumPageNum_Hex);

                        pagedata = textmixpage.PageData;
                        pagedata = pagedata.Stuff(0, (96 + 14) * 2, "");
                        pagedata = pagedata.Stuff(pagedata.Length - 42 * 2, 42 * 2, "");
                        pagedata = pagedata.Substring(0, cutlen * 2);

                        fvaluehex = fvaluehex + pagedata;
                        i = i + 1;
                    }
                }
                catch (Exception ex)
                {
                    fvaluehex = "";
                }
            }

            fvalue = "0x" + fvaluehex;

            return (fvaluehex, fvalue);
        }

        private string TranslateData_UniqueIdentifier(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnUniqueIdentifier;
            byte[] bUniqueIdentifier;
            short iUniqueIdentifier;

            sReturnUniqueIdentifier = "";
            bUniqueIdentifier = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bUniqueIdentifier, 0, sLenth);

            // 前4个字节反转
            for (iUniqueIdentifier = 0; iUniqueIdentifier <= 3; iUniqueIdentifier++)
            {
                sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[3 - iUniqueIdentifier].ToString("X2");
            }
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + "-";

            // 反转2个字节
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[5].ToString("X2") + bUniqueIdentifier[4].ToString("X2") + "-";

            // 反转2个字节
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[7].ToString("X2") + bUniqueIdentifier[6].ToString("X2") + "-";

            // 顺序读2个字节
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[8].ToString("X2") + bUniqueIdentifier[9].ToString("X2") + "-";

            // 顺序读6个字节
            for (iUniqueIdentifier = 10; iUniqueIdentifier <= sLenth - 1; iUniqueIdentifier++)
            {
                sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[iUniqueIdentifier].ToString("X2");
            }

            return sReturnUniqueIdentifier;
        }

        private (string, string) TranslateData_Text(byte[] data, FVarColumnInfo pv, bool isNText, int textinrow)
        {
            string fvaluehex, fvalue;

            if (pv.InRow == false)
            {
                if (textinrow == 0)
                {
                    fvaluehex = GetLOBDataHEX(pv.FLogContents);
                }
                else
                {
                    fvaluehex = GetLOBDataHEX_ForTextInRow(pv.FLogContents);
                }
            }
            else
            {
                fvaluehex = pv.FLogContents;
            }
            
            if (fvaluehex != null)
            {
                if (isNText == false)
                {
                    fvalue = System.Text.Encoding.Default.GetString(fvaluehex.ToByteArray());
                }
                else
                {
                    fvalue = System.Text.Encoding.Unicode.GetString(fvaluehex.ToByteArray());
                }
            }
            else
            {
                fvalue = "nullvalue";
            }

            return (fvaluehex, fvalue);
        }

        private (string, string) TranslateData_VarChar(byte[] data, FVarColumnInfo pvc, bool isunicode)
        {
            string fvaluehex, fvalue, pointer, pagedata, tmpstr;
            int i, cutlen;
            FPageInfo firstpage, temppage;
            List<FPageInfo> pagelist;

            if (pvc.InRow == true)
            {
                fvaluehex = pvc.FLogContents;
            }
            else
            {
                try
                {
                    pointer = pvc.FLogContents;
                    pointer = pointer.Stuff(0, 12 * 2, ""); // 跳过12个字节

                    i = 0;
                    tmpstr = pointer.Substring(i * 2, 12 * 2);
                    firstpage = new FPageInfo(tmpstr);
                    i = i + 12;

                    temppage = GetPageInfo(firstpage.FileNumPageNum_Hex);
                    firstpage.PageData = temppage.PageData;
                    firstpage.PageType = temppage.PageType;

                    pagelist = new List<FPageInfo>();

                    if (firstpage.PageType == "4")  // TEXT_TREE_PAGE
                    {
                        pagelist = GetTEXTTREEPAGESubPages(firstpage);
                    }

                    if (firstpage.PageType == "3")  // TEXT_MIX_PAGE
                    {
                        pagelist.Add(firstpage);

                        while (i + 12 <= (pointer.Length / 2))
                        {
                            tmpstr = pointer.Substring(i * 2, 12 * 2);
                            temppage = new FPageInfo(tmpstr);
                            pagelist.Add(temppage);
                            i = i + 12;
                        }
                    }

                    fvaluehex = "";
                    i = 0;
                    foreach (FPageInfo tp in pagelist)
                    {
                        cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : pagelist[i - 1].Offset));
                        temppage = GetPageInfo(tp.FileNumPageNum_Hex);

                        pagedata = temppage.PageData;
                        pagedata = pagedata.Stuff(0, (96 + 14) * 2, "");
                        pagedata = pagedata.Stuff(pagedata.Length - 42 * 2, 42 * 2, "");
                        if (pagedata.Length / 2 >= cutlen)
                        {
                            pagedata = (cutlen > 0 ? pagedata.Substring(0, cutlen * 2) : pagedata);
                        }
                        else
                        {
                            pagedata = pagedata + new StringBuilder((cutlen - pagedata.Length / 2) * 2).Insert(0, "78", (cutlen - pagedata.Length / 2)).ToString();
                        }

                        fvaluehex = fvaluehex + pagedata;
                        i = i + 1;
                    }
                }
                catch (Exception ex)
                {
                    fvaluehex = "";
                }
            }

            if (isunicode)
            {
                fvalue = System.Text.Encoding.Unicode.GetString(fvaluehex.ToByteArray()).TrimEnd();
            }
            else
            {
                fvalue = System.Text.Encoding.Default.GetString(fvaluehex.ToByteArray()).TrimEnd();
            }

            return (fvaluehex, fvalue);
        }

        private List<FPageInfo> GetTEXTTREEPAGESubPages(FPageInfo fpage)
        {
            string pagedata, tmpstr, tmpstr2;
            int i, pageqty;
            FPageInfo textmixpage, temppage;
            List<FPageInfo> pagelist;

            pagelist = new List<FPageInfo>();
            pagedata = fpage.PageData;
            pagedata = pagedata.Stuff(0, (96 + 16) * 2, "");
            tmpstr = pagedata.Substring(0, 4 * 2);
            pageqty = Convert.ToInt32(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);

            for (i = 0; i <= pageqty - 1; i++)
            {
                tmpstr = pagedata.Substring(8 + i * 16 * 2, 16 * 2);
                temppage = new FPageInfo(tmpstr, "TEXT_TREE_PAGE");
                tmpstr2 = temppage.FileNumPageNum_Hex;
                temppage = GetPageInfo(tmpstr2);

                switch (temppage.PageType)
                {
                    case "3": // TEXT_MIX_PAGE
                        textmixpage = new FPageInfo(tmpstr, "TEXT_TREE_PAGE");
                        pagelist.Add(textmixpage);
                        break;
                    case "4": // TEXT_TREE_PAGE
                        pagelist.AddRange(GetTEXTTREEPAGESubPages(temppage));
                        break;
                    default:
                        break;
                }
            }

            return pagelist;
        }

        private (string, string) TranslateData_Image(byte[] data, FVarColumnInfo pvc)
        {
            string fvaluehex, fvalue;

            if (pvc.InRow == true)
            {
                fvaluehex = pvc.FLogContents;
            }
            else
            {
                fvaluehex = GetLOBDataHEX(pvc.FLogContents);
            }

            fvalue = "0x" + fvaluehex;

            return (fvaluehex, fvalue);
        }

        private (string, string) TranslateData_XML(FVarColumnInfo pvc)
        {
            int i, length;
            string fvaluehex, fvalue, logcont, nlen1, nlen2, ncont, nvalue, f0type, lastnode;
            List<string> stacks;

            fvaluehex = pvc.FLogContents;
            fvalue = "";

            try
            {
                logcont = pvc.FLogContents;
                logcont = logcont.Stuff(0, 10, "");
                stacks = new List<string>();
                f0type = "";
                lastnode = "";

                for (i = 0; i <= logcont.Length - 1;)
                {
                    switch (logcont.Substring(i, 2)) // node type
                    {
                        case "F0":
                            i = i + 2;
                            nlen1 = logcont.Substring(i, 2);
                            if (Convert.ToInt32(nlen1, 16) < 128)
                            {
                                length = Convert.ToInt32(nlen1, 16);
                            }
                            else
                            {
                                i = i + 2;
                                nlen2 = logcont.Substring(i, 2);
                                length = (Convert.ToInt32(nlen2, 16) * 128) + (Convert.ToInt32(nlen1, 16) - 128);
                            }
                            i = i + 2;
                            ncont = logcont.Substring(i, length * 4);
                            nvalue = System.Text.Encoding.Unicode.GetString(ncont.ToByteArray());
                            i = i + length * 4;
                            i = i + 12;
                            f0type = logcont.Substring(i - 4, 2);
                            if (f0type == "F8")
                            {
                                fvalue = fvalue + $"<{nvalue}>";
                                lastnode = nvalue;
                                stacks.Add(nvalue);
                            }
                            if (f0type == "F6")
                            {
                                if (fvalue.EndsWith(">"))
                                {
                                    fvalue = fvalue.Substring(0, fvalue.Length - 1);
                                }
                                fvalue = fvalue + $" {nvalue}=";
                            }
                            break;
                        case "11":
                            i = i + 2;
                            nlen1 = logcont.Substring(i, 2);
                            if (Convert.ToInt32(nlen1, 16) < 128)
                            {
                                length = Convert.ToInt32(nlen1, 16);
                            }
                            else
                            {
                                i = i + 2;
                                nlen2 = logcont.Substring(i, 2);
                                length = (Convert.ToInt32(nlen2, 16) * 128) + (Convert.ToInt32(nlen1, 16) - 128);
                            }
                            i = i + 2;
                            ncont = logcont.Substring(i, length * 4);
                            nvalue = System.Text.Encoding.Unicode.GetString(ncont.ToByteArray());
                            if (f0type == "F8")
                            {
                                fvalue = fvalue + $"{nvalue}";
                            }
                            if (f0type == "F6")
                            {
                                fvalue = fvalue + $"\"{nvalue}\"";
                            }
                            i = i + length * 4;
                            break;
                        case "F7":
                            nvalue = stacks.Last();
                            fvalue = fvalue + $"</{nvalue}>";
                            i = i + 2;
                            stacks.RemoveAt(stacks.Count - 1);
                            break;
                        case "F5":
                            fvalue = fvalue + ">";
                            f0type = "F8";
                            i = i + 2;
                            break;
                        case "F8":
                            fvalue = fvalue + $"<{lastnode}>";
                            stacks.Add(lastnode);
                            i = i + 4;
                            break;
                        default:
                            i = i + 2;
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                fvalue = "";
            }

            return (fvaluehex, fvalue);
        }

        private (string, string, SqlDbType?, short?, short?, string) TranslateData_Variant(byte[] data, FVarColumnInfo pvc)
        {
            string fvaluehex, fvalue, logcont, tmp, VariantCollation;
            short length;
            short? VariantLength, VariantScale;
            SqlDbType? VariantBaseType;

            fvaluehex = "";
            fvalue = "";
            VariantBaseType = null;
            VariantLength = null;
            VariantScale = null;
            VariantCollation = null;

            logcont = pvc.FLogContents;

            switch (logcont.Substring(0, 4))
            {
                case "2401": // uniqueidentifier
                    VariantBaseType = SqlDbType.UniqueIdentifier;
                    fvaluehex = logcont.Stuff(0, "2401".Length, "");
                    fvalue = TranslateData_UniqueIdentifier(data, pvc.FStartIndex + 2, 16);
                    break;
                case "2801": // date
                    VariantBaseType = SqlDbType.Date;
                    fvaluehex = logcont.Stuff(0, "2801".Length, "");
                    fvalue = TranslateData_Date(data, pvc.FStartIndex + 2);
                    break;
                case "2901": // time
                    VariantBaseType = SqlDbType.Time;
                    fvaluehex = logcont.Stuff(0, "2901".Length + 2, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 3);
                    VariantScale = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_Time(data, pvc.FStartIndex + 3, length, Convert.ToInt16(VariantScale));
                    break;
                case "2A01": // datetime2
                    VariantBaseType = SqlDbType.DateTime2;
                    fvaluehex = logcont.Stuff(0, "2A01".Length + 2, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 3);
                    VariantScale = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_DateTime2(data, pvc.FStartIndex + 3, length, Convert.ToInt16(VariantScale));
                    break;
                case "2B01": // datetimeoffset
                    VariantBaseType = SqlDbType.DateTimeOffset;
                    fvaluehex = logcont.Stuff(0, "2B01".Length + 2, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 3);
                    VariantScale = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_DateTimeOffset(data, pvc.FStartIndex + 3, length, Convert.ToInt16(VariantScale));
                    break;
                case "3001": // tinyint
                    VariantBaseType = SqlDbType.TinyInt;
                    fvaluehex = logcont.Stuff(0, "3001".Length, "");
                    fvalue = Convert.ToInt32(fvaluehex, 16).ToString();
                    break;
                case "3401": // smallint
                    VariantBaseType = SqlDbType.SmallInt;
                    fvaluehex = logcont.Stuff(0, "3401".Length, "");
                    tmp = fvaluehex.Substring(2, 2) + fvaluehex.Substring(0, 2);
                    fvalue = Convert.ToInt16(tmp, 16).ToString();
                    break;
                case "3801": // int
                    VariantBaseType = SqlDbType.Int;
                    fvaluehex = logcont.Stuff(0, "3801".Length, "");
                    tmp = fvaluehex.Substring(6, 2) + fvaluehex.Substring(4, 2) + fvaluehex.Substring(2, 2) + fvaluehex.Substring(0, 2);
                    fvalue = Convert.ToInt32(tmp, 16).ToString();
                    break;
                case "3A01": // smalldatetime
                    VariantBaseType = SqlDbType.SmallDateTime;
                    fvaluehex = logcont.Stuff(0, "3A01".Length, "");
                    fvalue = TranslateData_SmallDateTime(data, pvc.FStartIndex + 2);
                    break;
                case "3B01": // real
                    VariantBaseType = SqlDbType.Real;
                    fvaluehex = logcont.Stuff(0, "3B01".Length, "");
                    VariantLength = Convert.ToInt16(fvaluehex.Length / 2);
                    fvalue = TranslateData_Real(data, pvc.FStartIndex + 2, Convert.ToInt16(VariantLength));
                    break;
                case "3C01": // money
                    VariantBaseType = SqlDbType.Money;
                    fvaluehex = logcont.Stuff(0, "3C01".Length, "");
                    fvalue = TranslateData_Money(data, pvc.FStartIndex + 2);
                    break;
                case "3D01": // datetime
                    VariantBaseType = SqlDbType.DateTime;
                    fvaluehex = logcont.Stuff(0, "3D01".Length, "");
                    fvalue = TranslateData_DateTime(data, pvc.FStartIndex + 2);
                    break;
                case "3E01": // float
                    VariantBaseType = SqlDbType.Float;
                    fvaluehex = logcont.Stuff(0, "3E01".Length, "");
                    length = Convert.ToInt16(fvaluehex.Length / 2);
                    VariantLength = Convert.ToInt16(length == 8 ? 53 : 24);
                    fvalue = TranslateData_Float(data, pvc.FStartIndex + 2, length);
                    break;
                case "6801": // bit
                    VariantBaseType = SqlDbType.Bit;
                    fvaluehex = logcont.Stuff(0, "6801".Length, "");
                    fvalue = (fvaluehex == "01" ? "1" : "0");
                    break;
                case "6C01": // numeric decimal
                    VariantBaseType = SqlDbType.Decimal;
                    fvaluehex = logcont.Stuff(0, "6C01".Length + 4, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 4);
                    VariantLength = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    VariantScale = Int16.Parse(logcont.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_Decimal(data, pvc.FStartIndex + 4, length, Convert.ToInt16(VariantScale));
                    break;
                case "A501": // varbinary
                    VariantBaseType = SqlDbType.VarBinary;
                    fvaluehex = logcont.Stuff(0, "A501".Length + 4, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    (_, fvalue) = TranslateData_VarBinary(data, new FVarColumnInfo() { InRow = true, FStartIndex = pvc.FStartIndex + 4, FEndIndex = pvc.FEndIndex });
                    break;
                case "AD01": // binary
                    VariantBaseType = SqlDbType.Binary;
                    fvaluehex = logcont.Stuff(0, "AD01".Length + 4, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_Binary(data, pvc.FStartIndex + 4, Convert.ToInt16(VariantLength));
                    break;
                case "AF01": // char
                    VariantBaseType = SqlDbType.Char;
                    fvaluehex = logcont.Stuff(0, "AF01".Length + 12, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Default.GetString(data, pvc.FStartIndex + 8, Convert.ToInt16(VariantLength)).TrimEnd();
                    break;
                case "7A01": // smallmoney
                    VariantBaseType = SqlDbType.SmallMoney;
                    fvaluehex = logcont.Stuff(0, "7A01".Length, "");
                    fvalue = TranslateData_SmallMoney(data, pvc.FStartIndex + 2);
                    break;
                case "7F01": // bigint
                    VariantBaseType = SqlDbType.BigInt;
                    fvaluehex = logcont.Stuff(0, "7F01".Length, "");
                    fvalue = BitConverter.ToInt64(data, pvc.FStartIndex + 2).ToString();
                    break;
                case "A701": // varchar
                    VariantBaseType = SqlDbType.VarChar;
                    fvaluehex = logcont.Stuff(0, "A701".Length + 12, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Default.GetString(fvaluehex.ToByteArray());
                    break;
                case "E701": // nvarchar
                    VariantBaseType = SqlDbType.NVarChar;
                    fvaluehex = logcont.Stuff(0, "E701".Length + 12, "");
                    VariantLength = Convert.ToInt16(Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / Convert.ToInt16(2));
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Unicode.GetString(fvaluehex.ToByteArray());
                    break;
                case "EF01": // nchar
                    VariantBaseType = SqlDbType.NChar;
                    fvaluehex = logcont.Stuff(0, "EF01".Length + 12, "");
                    VariantLength = Convert.ToInt16(Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 2);
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Unicode.GetString(data, pvc.FStartIndex + 8, Convert.ToInt16(VariantLength * 2)).TrimEnd();
                    break;
                default:
                    break;
            }

            return (fvaluehex, fvalue, VariantBaseType, VariantLength, VariantScale, VariantCollation);
        }

        private string GetLOBDataHEX(string lobpointer)
        {
            int cutlen, pageqty, pageqty2, i;
            string fvaluehex, tmpstr, tmpstr2, storagetype, pagedata, subpage, fid, pagenum, filenum, slotnum;
            FPageInfo firstpage, tmppage;
            List<FPageInfo> tmps;

            try
            {
                fid = lobpointer.Substring(0, 8 * 2);
                pagenum = lobpointer.Substring(8 * 2, 4 * 2);
                filenum = lobpointer.Substring((8 + 4) * 2, 2 * 2);
                slotnum = lobpointer.Substring((8 + 4 + 2) * 2, 2 * 2);

                tmpstr = $"{new string('0', 4 * 2)}{pagenum}{filenum}{slotnum}";
                firstpage = new FPageInfo(tmpstr);
                tmppage = GetPageInfo(firstpage.FileNumPageNum_Hex);

                tmpstr = tmppage.PageData;
                tmpstr = tmpstr.Stuff(0, tmppage.SlotBeginIndex[firstpage.SlotNum] * 2, "");
                tmpstr = tmpstr.Stuff(0, tmpstr.IndexOf(fid) + fid.Length, "");
                storagetype = tmpstr.Substring(0, 4);

                switch (storagetype)
                {
                    case "0000":
                        tmpstr = tmpstr.Stuff(0, 2 * 2, "");
                        cutlen = Convert.ToInt16(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);
                        tmpstr = tmpstr.Stuff(0, 6 * 2, "");
                        fvaluehex = tmpstr.Substring(0, cutlen * 2);
                        break;
                    case "0500":
                        tmpstr = tmpstr.Stuff(0, 4 * 2, "");
                        pageqty = Convert.ToInt32(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);
                        tmpstr = tmpstr.Stuff(0, 8 * 2, "");
                        tmps = new List<FPageInfo>();
                        for (i = 0; i <= pageqty - 1; i++)
                        {
                            subpage = tmpstr.Substring(i * 12 * 2, 12 * 2);
                            tmppage = new FPageInfo(subpage);
                            firstpage = GetPageInfo(tmppage.FileNumPageNum_Hex);

                            if (firstpage.PageType == "3")  // TEXT_MIX_PAGE
                            {
                                tmps.Add(tmppage);
                                continue;
                            }

                            if (firstpage.PageType == "4")  // TEXT_TREE_PAGE
                            {
                                pagedata = firstpage.PageData;
                                pagedata = pagedata.Stuff(0, (96 + 16) * 2, "");
                                tmpstr2 = pagedata.Substring(0, 4 * 2);
                                pageqty2 = Convert.ToInt32(tmpstr2.Substring(2, 2) + tmpstr2.Substring(0, 2), 16);

                                for (i = 0; i <= pageqty2 - 1; i++)
                                {
                                    tmpstr2 = pagedata.Substring(8 + i * 16 * 2, 16 * 2);
                                    tmppage = new FPageInfo(tmpstr2, "TEXT_TREE_PAGE");
                                    tmps.Add(tmppage);
                                }
                                continue;
                            }
                        }
                        fvaluehex = "";
                        i = 0;
                        foreach (FPageInfo tp in tmps)
                        {
                            cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));
                            tmppage = GetPageInfo(tp.FileNumPageNum_Hex);

                            subpage = tmppage.PageData;
                            subpage = subpage.Stuff(0, tmppage.SlotBeginIndex[tp.SlotNum] * 2, "");
                            subpage = subpage.Stuff(0, subpage.IndexOf(fid) + fid.Length, "");
                            subpage = subpage.Stuff(0, 2 * 2, "");
                            subpage = subpage.Substring(0, cutlen * 2);

                            fvaluehex = fvaluehex + subpage;
                            i = i + 1;
                        }
                        break;
                    case "0800":
                        fvaluehex = null;
                        break;
                    default:
                        fvaluehex = "";
                        break;
                }
            }
            catch (Exception ex)
            {
                fvaluehex = "";
            }

            return fvaluehex;
        }

        private string GetLOBDataHEX_ForTextInRow(string logcontents)
        {
            int i, pageqty, cutlen;
            string tmpstr, subpage, fvaluehex;
            List<FPageInfo> tmps;
            FPageInfo tmppage;

            try
            {
                tmpstr = logcontents;
                tmpstr = tmpstr.Stuff(0, 12 * 2, "");
                pageqty = tmpstr.Length / 2 / 12;
                tmps = new List<FPageInfo>();
                for (i = 0; i <= pageqty - 1; i++)
                {
                    subpage = tmpstr.Substring(i * 12 * 2, 12 * 2);
                    tmppage = new FPageInfo(subpage);
                    tmps.Add(tmppage);
                }

                fvaluehex = "";
                i = 0;
                foreach (FPageInfo tp in tmps)
                {
                    cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));
                    tmppage = GetPageInfo(tp.FileNumPageNum_Hex);

                    subpage = tmppage.PageData;
                    subpage = subpage.Stuff(0, tmppage.SlotBeginIndex[tp.SlotNum] * 2, "");
                    subpage = subpage.Stuff(0, 14 * 2, "");
                    subpage = subpage.Substring(0, cutlen * 2);

                    fvaluehex = fvaluehex + subpage;
                    i = i + 1;
                }
            }
            catch(Exception ex)
            {
                fvaluehex = "";
            }

            return fvaluehex;
        }

        #endregion

    }

    public class FVarColumnInfo
    {
        public short FIndex { get; set; }
        public string FLogContents { get; set; }
        public int FStartIndex { get; set; }
        public int FEndIndex { get; set; }
        public string FEndIndexHex { get; set; }
        public bool InRow { get; set; }
    }

    public class FPageInfo
    {
        private string offset_str, pagenum_str, filenum_str, slotnum_str;

        public FPageInfo()
        {

        }

        public FPageInfo(string ppageinfo, string pfrom = "INROW")
        {
            if (pfrom == "INROW")
            {
                offset_str = ppageinfo.Substring(0, 4 * 2);
                pagenum_str = ppageinfo.Substring(4 * 2, 4 * 2);
                filenum_str = ppageinfo.Substring(8 * 2, 2 * 2);
                slotnum_str = ppageinfo.Substring(10 * 2, 2 * 2);
            }
            else
            {   // TEXT_TREE_PAGE
                offset_str = ppageinfo.Substring(0, 4 * 2);
                pagenum_str = ppageinfo.Substring(8 * 2, 4 * 2);
                filenum_str = ppageinfo.Substring(12 * 2, 2 * 2);
                slotnum_str = ppageinfo.Substring(14 * 2, 2 * 2);
            }

            Offset = Convert.ToInt64(offset_str.Substring(6, 2) + offset_str.Substring(4, 2) + offset_str.Substring(2, 2) + offset_str.Substring(0, 2), 16);
            PageNum = Convert.ToInt64(pagenum_str.Substring(6, 2) + pagenum_str.Substring(4, 2) + pagenum_str.Substring(2, 2) + pagenum_str.Substring(0, 2), 16);
            FileNum = Convert.ToInt32(filenum_str.Substring(2, 2) + filenum_str.Substring(0, 2), 16);
            SlotNum = Convert.ToInt32(slotnum_str.Substring(2, 2) + slotnum_str.Substring(0, 2), 16);
            FileNumPageNum_Hex = $"{filenum_str.Substring(2, 2)}{filenum_str.Substring(0, 2)}:{pagenum_str.Substring(6, 2)}{pagenum_str.Substring(4, 2)}{pagenum_str.Substring(2, 2)}{pagenum_str.Substring(0, 2)}";
        }

        public long Offset { get; set; }
        public long PageNum { get; set; }
        public int FileNum { get; set; }
        public int SlotNum { get; set; }
        public string FileNumPageNum_Hex { get; set; }
        public string PageData { get; set; }
        public string PageType { get; set; }
        
        public int SlotCnt { get; set; }
        public int[] SlotBeginIndex { get; set; }
    }

    public class SQLGraphNode
    {
        public string type { get; set; }
        public string schema { get; set; }
        public string table { get; set; }
        public int id { get; set; }
    }


}
