using DBLOG.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DBLOG
{
    // log analyzer for DML & DDL
    public partial class DBLOG_DML_DDL
    {
        public static List<FLOG> DDLLogs, DDLLogs_Tran;
        private static List<TransactionInfo> DDLLogs_FTranID;
        private static string DatabaseName, tsql, LogFile;
        private static List<string> RowCompressionAffectsStorage;
        private static List<(string pageid, string lsn)> PrevPages; // fileid+pageid 
        private static DatabaseOperation DB;

        private string TableName,
                       SchemaName,
                       AllocUnitType;
        private TableInfo FTableInfo;
        private Dictionary<string, FPageInfo> lobpagedata; // key:fileid+pageid value:FPageInfo
        public List<FLOG> DTLogs;     // original logs
        private List<DatabaseLog> wslogs;

        public DBLOG_DML_DDL(string PSchemaName, string PTableName)
        {
            TableName = PTableName;
            SchemaName = PSchemaName;
            AllocUnitType = "NORMAL";

            FTableInfo = GetTableInfo(SchemaName, TableName);
        }

        public DBLOG_DML_DDL(long PAllocUnitId, string PMaxLsn)
        {
            string wstranid;
            DatabaseLog tmplog;
            TransactionInfo traninfo;

            AllocUnitType = "UNKNOWN";
            traninfo = DDLLogs_FTranID.FirstOrDefault(t => string.Compare(t.LSNList.Min(), PMaxLsn) == 1
                                                           && t.TransactionName == "DROPOBJ" 
                                                           && t.AllocUnitId.Contains(PAllocUnitId.ToString()) == true
                                                     );
            if (traninfo != null)
            {
                TableName = traninfo.AllocUnitName.Split('.')[1].Replace("[", "").Replace("]", "");
                SchemaName = traninfo.AllocUnitName.Split('.')[0].Replace("[", "").Replace("]", "");
                FTableInfo = UserTables[$"{SchemaName}.{TableName}"];
            }
            else
            {
                wstranid = DDLLogs.Where(p => string.Compare(p.Current_LSN, PMaxLsn) == 1
                                              && p.AllocUnitId == PAllocUnitId
                                              && DDLLogs.Any(e => e.Transaction_ID == p.Transaction_ID && e.Transaction_Name == "DROPOBJ") == true
                                              && DDLLogs_FTranID.Any(t => t.TransactionID == p.Transaction_ID) == false)
                                  .Select(p => p.Transaction_ID)
                                  .FirstOrDefault();

                if (string.IsNullOrEmpty(wstranid) == false)
                {
                    wslogs = new List<DatabaseLog>();

                    tmplog = AnalyzeDDLTran(wstranid);
                    wslogs.Add(tmplog);

                    TableName = tmplog.ObjectName.Split('.')[1].Replace("[", "").Replace("]", "");
                    SchemaName = tmplog.ObjectName.Split('.')[0].Replace("[", "").Replace("]", "");
                    FTableInfo = GetTableInfo(SchemaName, TableName);
                }
                else
                {
                    TableName = "";
                    SchemaName = "";
                    FTableInfo = null;
                }
            }

        }

        public DBLOG_DML_DDL()
        {
            TableName = "";
            SchemaName = "";
            AllocUnitType = "NORMAL";

        }

        public static void Init(string PDatabaseName, DatabaseOperation PDB, string PLogFile)
        {
            DatabaseName = PDatabaseName;
            DB = PDB;
            LogFile = PLogFile;

            #region RowCompressionAffectsStorage
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
            #endregion RowCompressionAffectsStorage

            SystemTables = new Dictionary<string, TableInfo>();
            UserTables = new Dictionary<string, TableInfo>();
            DDLLogs_FTranID = new List<TransactionInfo>();

            tsql = "select schema_id,name from sys.schemas; ";
            Schemas = DB.Query<(int schema_id, string name)>(tsql, false).ToDictionary(p => p.schema_id, p => p.name);

            tsql = "select typeid=rtrim(xtype)+'_'+rtrim(xusertype),name from sys.systypes; ";
            Systypes = DB.Query<(string typeid, string name)>(tsql, false).ToDictionary(p => p.typeid, p => p.name);

        }

        public List<DatabaseLog> AnalyzeLog()
        {
            List<DatabaseLog> logs;
            DatabaseLog tmplog;
            int j, minlen;
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
            CompressionType compressiontype;
            List<FLOG> wslog, vlog;
            FLOG llog, tlog;
            List<string> ddltranids;
            TableInfo FTableInfo0;

            logs = new List<DatabaseLog>();
            if (DTLogs != null && FTableInfo != null)
            {
                ColumnList = string.Join(",", FTableInfo.Columns
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

                tsql = @"if object_id('tempdb..#temppagedata') is not null drop table #temppagedata; 
                        create table #temppagedata(LSN nvarchar(1000),ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
                DB.ExecuteSQL(tsql, false);

                tsql = "create index ix_#temppagedata on #temppagedata(LSN); ";
                DB.ExecuteSQL(tsql, false);

                tsql = @"if object_id('tempdb..#temppagedatalob') is not null drop table #temppagedatalob; 
                        create table #temppagedatalob(ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
                DB.ExecuteSQL(tsql, false);

                tsql = @"if object_id('tempdb..#ModifiedRawData') is not null drop table #ModifiedRawData; 
                        create table #ModifiedRawData([SlotID] int,[RowLog Contents 0_var] nvarchar(max),[RowLog Contents 0] varbinary(max)); ";
                DB.ExecuteSQL(tsql, false);

                lobpagedata = new Dictionary<string, FPageInfo>();

                stemp = "";
                if (AllocUnitType == "NORMAL")
                {
                    stemp = $"{SchemaName}.{TableName}{(FTableInfo.AllocUnitName.Length == 0 ? "" : "." + FTableInfo.AllocUnitName)}";
                }
                if (AllocUnitType == "UNKNOWN")
                {
                    stemp = $"Unknown Alloc Unit";
                }

                vlog = new List<FLOG>();
                foreach (string tranid in DTLogs.Select(p => p.Transaction_ID).Distinct())
                {
                    wslog = DTLogs.Where(p => p.Transaction_ID == tranid).ToList();
                    if (wslog.Any(p => p.Context == "LCX_CLUSTERED" || p.Context == "LCX_HEAP" || p.Context == "LCX_MARK_AS_GHOST") == false)
                    {
                        tlog = wslog.OrderBy(p => p.Current_LSN).FirstOrDefault();
                        tsql = $"with b as "
                                + $"(select top 1 b1.* "
                                + $" from sys.fn_dblog(null,null) b1 "
                                + $" where b1.[Current LSN]<N'{tlog.Current_LSN}' "
                                + $" and b1.[Page ID]='{tlog.Page_ID}' "
                                + $" and b1.[Slot ID]={tlog.Slot_ID} "
                                + $" and exists(select 1 from sys.fn_dblog(null,null) b2 where b2.[Transaction ID]=b1.[Transaction ID] and b2.Context in(N'LCX_CLUSTERED',N'LCX_HEAP',N'LCX_MARK_AS_GHOST')) "
                                + $" order by b1.[Current LSN] desc)"
                                + $"select top 1 t.* "
                                + $"from sys.fn_dblog(null,null) t "
                                + $"join b on t.[Transaction ID]=b.[Transaction ID] and t.[Current LSN]>b.[Current LSN] "
                                + $"where t.Context in(N'LCX_CLUSTERED',N'LCX_HEAP',N'LCX_MARK_AS_GHOST') "
                                + $"order by t.[Current LSN] ";
                        tlog = DB.Query<FLOG>(tsql, false).FirstOrDefault();

                        if (tlog != null)
                        {
                            llog = new FLOG();
                            llog.Current_LSN = wslog.OrderByDescending(p => p.Current_LSN).First().Current_LSN + "V";
                            llog.Operation = "LOP_MODIFY_ROW";
                            llog.Context = (FTableInfo.IsHeapTable ? "LCX_HEAP" : "LCX_CLUSTERED");
                            llog.Transaction_ID = tranid;
                            llog.IsVirtual = true;
                            llog.AllocUnitName = stemp;
                            llog.Page_ID = tlog.Page_ID;
                            llog.Slot_ID = tlog.Slot_ID;

                            vlog.Add(llog);
                        }
                    }
                }
                DTLogs.AddRange(vlog);

                foreach (FLOG log in DTLogs.Where(p => (
                                                        (FTableInfo.IsColumnStore == false && p.AllocUnitName == stemp)
                                                        ||
                                                        (FTableInfo.IsColumnStore == true && p.AllocUnitName.StartsWith(stemp) == true)
                                                       )
                                                       &&
                                                       IsLCXTEXT(p) == false)
                                           .OrderByDescending(p => p.Transaction_ID + p.Current_LSN)
                        )
                {
                    try
                    {
                        FTableInfo0 = FTableInfo;
                        ddltranids = DDLLogs.Where(p => string.Compare(p.Transaction_ID, log.Transaction_ID) == 1
                                                        && DDLLogs_FTranID.Any(t => t.TransactionID == p.Transaction_ID) == false)
                                            .Select(p => p.Transaction_ID)
                                            .Distinct()
                                            .ToList();
                        foreach (string tranid in ddltranids.OrderByDescending(p => p))
                        {
                            tmplog = AnalyzeDDLTran(tranid);
                            logs.Add(tmplog);
                        }
                        FTableInfo = FTableInfo0;

                        if (log.Operation == "LOP_MODIFY_ROW" || log.Operation == "LOP_MODIFY_COLUMNS")
                        {
                            llog = DTLogs
                                   .Where(p => p.Transaction_ID == log.Transaction_ID
                                               && string.Compare(p.Current_LSN, log.Current_LSN) == -1
                                               && IsLCXTEXT(p) == false)
                                   .OrderByDescending(p => p.Current_LSN)
                                   .FirstOrDefault();
                            stemp = (llog != null ? llog.Current_LSN : "");
                            wslog = DTLogs
                                    .Where(p => p.Transaction_ID == log.Transaction_ID
                                                && IsLCXTEXT(p) == true
                                                && string.Compare(p.Current_LSN, log.Current_LSN) == -1
                                                && string.Compare(p.Current_LSN, stemp) == 1)
                                    .ToList();
                        }
                        else
                        {
                            wslog = DTLogs
                                    .Where(p => p.Transaction_ID == log.Transaction_ID
                                                && IsLCXTEXT(p) == true)
                                    .ToList();
                        }

#if DEBUG
                        FCommon.WriteTextFile(LogFile, $"TRANID={log.Transaction_ID} LSN={log.Current_LSN},LSN2={string.Join(",", wslog.Select(x => x.Current_LSN))},Operation={log.Operation} ");
#endif

                        tsql = $"select top 1 BeginTime=substring(BeginTime,1,19),EndTime=substring(EndTime,1,19) from #TransactionList where TransactionID='{log.Transaction_ID}'; ";
                        (BeginTime, EndTime) = DB.Query<(string BeginTime, string EndTime)>(tsql, false).FirstOrDefault();

                        compressiontype = FTableInfo.GetCompressionType(log.PartitionId);

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

                        if (log.Operation == "LOP_DELETE_ROWS"
                            && log.Current_LSN == DTLogs
                                                  .Where(p => p.Transaction_ID == log.Transaction_ID && IsLCXTEXT(p) == false)
                                                  .OrderByDescending(p => p.Current_LSN)
                                                  .FirstOrDefault()
                                                  .Current_LSN
                           )
                        {
                            FRestoreLCXTEXT(log, wslog);
                        }

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
                                        minlen = 2 + FTableInfo.Columns.Where(p => p.IsVarLenDataType == false).Sum(p => p.Length) + 2;

                                        if (log.RowLog_Contents_0.Length >= minlen)
                                        {
                                            try
                                            {
                                                TranslateData(log.RowLog_Contents_0, FTableInfo.Columns);
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

                                                if (MR0.Length < minlen) { continue; }
                                                TranslateData(MR0, FTableInfo.Columns);
                                            }
                                        }
                                        else
                                        {
                                            if (AllocUnitType == "NORMAL")
                                            {
                                                MR0 = GetMR1(log, "");
                                                if (MR0.Length < minlen) { continue; }
                                                TranslateData(MR0, FTableInfo.Columns);
                                            }
                                            
                                        }
                                        break;
                                    case CompressionType.ROW:
                                    case CompressionType.PAGE:
                                        MR0 = log.RowLog_Contents_0;
                                        TranslateData_Compression(MR0, FTableInfo.Columns);
                                        break;
                                }

                                for (j = 0; j <= FTableInfo.Columns.Length - 1; j++)
                                {
                                    if (FTableInfo.Columns[j].PhysicalStorageType == SqlDbType.Timestamp
                                        || FTableInfo.Columns[j].IsComputed == true
                                        || FTableInfo.Columns[j].IsHidden == true)
                                    {
                                        continue;
                                    }

                                    if ((FTableInfo.IsNodeTable == true && j < 2)
                                        || (FTableInfo.IsEdgeTable == true && j < 8))

                                    {
                                        tj = new SQLGraphNode { };

                                        if (FTableInfo.IsNodeTable == true)
                                        {   // NodeTable
                                            tj.type = "node";
                                            tj.schema = SchemaName;
                                            tj.table = TableName;
                                            tj.id = Convert.ToInt32(FTableInfo.Columns.FirstOrDefault(p => p.ColumnName.StartsWith("graph_id") == true).Value);
                                        }
                                        else
                                        {   // EdgeTable
                                            if (FTableInfo.Columns[j].ColumnName.StartsWith("$edge_id") == true)
                                            {
                                                tj.type = "edge";
                                                tj.schema = SchemaName;
                                                tj.table = TableName;
                                                tj.id = Convert.ToInt32(FTableInfo.Columns.FirstOrDefault(p => p.ColumnName.StartsWith("graph_id") == true).Value);
                                            }
                                            if (FTableInfo.Columns[j].ColumnName.StartsWith("$from_id") == true)
                                            {
                                                tj.type = "node";
                                                tsql = "select schemaname=s.name,tablename=a.name "
                                                        + " from sys.tables a "
                                                        + " join sys.schemas s on a.schema_id=s.schema_id "
                                                        + $" where a.object_id={FTableInfo.Columns.FirstOrDefault(p => p.ColumnName.StartsWith("from_obj_id") == true).Value}; ";
                                                (tj.schema, tj.table) = DB.Query<(string, string)>(tsql, false).FirstOrDefault();
                                                tj.id = Convert.ToInt32(FTableInfo.Columns.FirstOrDefault(p => p.ColumnName.StartsWith("from_id") == true).Value);
                                            }
                                            if (FTableInfo.Columns[j].ColumnName.StartsWith("$to_id") == true)
                                            {
                                                tj.type = "node";
                                                tsql = "select schemaname=s.name,tablename=a.name "
                                                        + " from sys.tables a "
                                                        + " join sys.schemas s on a.schema_id=s.schema_id "
                                                        + $" where a.object_id={FTableInfo.Columns.FirstOrDefault(p => p.ColumnName.StartsWith("to_obj_id") == true).Value}; ";
                                                (tj.schema, tj.table) = DB.Query<(string, string)>(tsql, false).FirstOrDefault();
                                                tj.id = Convert.ToInt32(FTableInfo.Columns.FirstOrDefault(p => p.ColumnName.StartsWith("to_id") == true).Value);
                                            }
                                        }

                                        FTableInfo.Columns[j].IsNull = false;
                                        FTableInfo.Columns[j].Value = JsonConvert.SerializeObject(tj);
                                    }

                                    Value = ColumnValue2SQLValue(FTableInfo.Columns[j]);
                                    ValueList1 = ValueList1 + (ValueList1.Length > 0 ? "," : "") + Value;

                                    if (FTableInfo.PrimaryKeyColumns.Count == 0
                                        || FTableInfo.PrimaryKeyColumns.Contains(FTableInfo.Columns[j].ColumnName))
                                    {
                                        WhereList0 = WhereList0
                                                     + (WhereList0.Length > 0 ? " and " : "")
                                                     + ColumnName2SQLName(FTableInfo.Columns[j])
                                                     + (FTableInfo.Columns[j].IsNull ? " is " : "=")
                                                     + Value;
                                    }
                                }

                                // 产生redo sql和undo sql -- Insert
                                if (log.Operation == "LOP_INSERT_ROWS")
                                {
                                    REDOSQL = $"insert into [{SchemaName}].[{TableName}]({ColumnList}) values({ValueList1}); ";
                                    UNDOSQL = $"delete top(1) from [{SchemaName}].[{TableName}] where {WhereList0}; ";

                                    if (FTableInfo.Columns.Any(p => p.IsIdentity) == true)
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

                                    if (FTableInfo.Columns.Any(p => p.IsIdentity) == true)
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
                                    AnalyzeUpdate(log, MR1, wslog, ref ValueList1, ref ValueList0, ref WhereList1, ref WhereList0, ref MR0);
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
                        FCommon.WriteTextFile(LogFile, $"LSN={log.Current_LSN},Operation={log.Operation},\r\nREDOSQL={REDOSQL},\r\nUNDOSQL={UNDOSQL} ");
#endif

                        if (string.IsNullOrEmpty(BeginTime) == false)
                        {
                            tmplog = new DatabaseLog();
                            tmplog.LSN = log.Current_LSN;
                            tmplog.Type = "DML";
                            tmplog.TransactionID = log.Transaction_ID;
                            tmplog.BeginTime = Convert.ToDateTime(BeginTime); // DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                            tmplog.EndTime = Convert.ToDateTime(EndTime); // DateTime.ParseExact(EndTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                            tmplog.ObjectName = $"[{SchemaName}].[{TableName}]";
                            tmplog.Operation = log.Operation;
                            tmplog.RedoSQL = REDOSQL;
                            tmplog.UndoSQL = UNDOSQL;
                            tmplog.Message = stemp;
                            logs.Add(tmplog);
                        }

                        if (log.Operation == "LOP_INSERT_ROWS"
                            && log.Current_LSN == DTLogs
                                                  .Where(p => p.Transaction_ID == log.Transaction_ID && IsLCXTEXT(p) == false)
                                                  .OrderBy(p => p.Current_LSN)
                                                  .FirstOrDefault()
                                                  .Current_LSN
                           )
                        {
                            FRestoreLCXTEXT(log, wslog);
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
                        tmplog.BeginTime = Convert.ToDateTime(BeginTime);
                        tmplog.EndTime = Convert.ToDateTime(EndTime);
                        tmplog.ObjectName = $"[{SchemaName}].[{TableName}]";
                        tmplog.Operation = log.Operation;
                        tmplog.RedoSQL = "";
                        tmplog.UndoSQL = "";
                        tmplog.Message = "";
                        logs.Add(tmplog);
#endif
                    }
                }
            }

            if (wslogs != null && wslogs.Count > 0)
            {
                logs.AddRange(wslogs);
            }

            return logs;
        }

        private void FRestoreLCXTEXT(FLOG clog, List<FLOG> wslog)
        {
            FPageInfo tpage;
            string stemp, pagetail;
            int slotid, slotbegin, modilen;

            List<(string pageid, string lsn)> pps;

            tsql = $"select [Page ID],lsn=N'{clog.Current_LSN}' " // min([Current LSN])
                   + $"from sys.fn_dblog(null,null) t "
                   + $"where t.[Current LSN]>N'{clog.Current_LSN}' "
                   //+ $"and t.[Transaction ID]=N'{clog.Transaction_ID}' " // 不可限制于本事務内
                   + $"and t.Operation=N'LOP_FORMAT_PAGE' "
                   + $"and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.Operation=N'LOP_COMMIT_XACT') "
                   + $"group by t.[Page ID] ";
            pps = DB.Query<(string, string)>(tsql, false);
            foreach ((string pageid, string lsn) in pps)
            {
                SetPrevPages(pageid, lsn);
            }

            foreach (FLOG log in wslog
                                 .OrderBy(p => p.Page_ID)
                                 .ThenBy(p => p.Slot_ID ?? 0)
                                 .ThenByDescending(p => p.Current_LSN)
                    )
            {
                if (log.Operation != "LOP_FORMAT_PAGE" 
                    && wslog.Any(p => p.Page_ID == log.Page_ID
                                      && string.Compare(p.Current_LSN, log.Current_LSN) == -1
                                      && p.Operation == "LOP_FORMAT_PAGE"
                                ) == true)
                {
                    continue;
                }
                
                if (log.Operation == "LOP_FORMAT_PAGE")
                {
                    lobpagedata.Remove(log.Page_ID);
                    SetPrevPages(log.Page_ID, log.Current_LSN);
                }
                else
                {
                    tpage = GetPageInfo(log.Page_ID);
                    stemp = tpage.PageData;

                    if (log.Operation == "LOP_INSERT_ROWS")
                    {
                        if (log.Slot_ID <= tpage.SlotBeginIndex.Count - 1)
                        {
                            slotid = Convert.ToInt32(log.Slot_ID);
                            stemp = stemp.Stuff((tpage.SlotBeginIndex[slotid] + (log.Offset_in_Row ?? 0)) * 2,
                                                log.RowLog_Contents_0.Length * 2,
                                                log.RowLog_Contents_0.ToText());
                        }
                    }

                    if (log.Operation == "LOP_MODIFY_ROW")
                    {
                        if (tpage.SlotBeginIndex.Count - 1 >= log.Slot_ID)
                        {
                            slotid = Convert.ToInt32(log.Slot_ID);
                            modilen = ((log.Modify_Size ?? 0) != 0 ? (log.Modify_Size ?? 0) : log.RowLog_Contents_1.Length) * 2;

                            if (tpage.SlotData == null) { tpage.SlotData = new Dictionary<int, string>(); }
                            if (tpage.SlotData.ContainsKey(slotid) == false) { tpage.SlotData.Add(slotid, ""); }
                            if (modilen <= 8000)
                            {
                                tpage.SlotData[slotid] = tpage.SlotData[slotid]
                                                              .Stuff((log.Offset_in_Row ?? 0) * 2,
                                                                     modilen,
                                                                     (log.RowLog_Contents_0.Length > 0 ? log.RowLog_Contents_0.ToText() : new string('0', modilen))
                                                                    );
                            }
                            else
                            {
                                tpage.SlotData[slotid] = log.RowLog_Contents_0.ToText();
                            }

                            stemp = stemp.Substring(0, 96 * 2);
                            pagetail = stemp.Substring(stemp.Length - tpage.SlotData.Count * 2 * 2, tpage.SlotData.Count * 2 * 2);
                            for (slotid = 0; slotid <= tpage.SlotData.Count - 1; slotid = slotid + 1)
                            {
                                stemp = stemp + tpage.SlotData[slotid];
                            }
                            stemp = stemp + pagetail;
                        }
                    }

                    if (log.Operation == "LOP_DELETE_ROWS")
                    {
                        slotid = Convert.ToInt32(log.Slot_ID);
                        if (tpage.SlotBeginIndex.Count < slotid + 1) { tpage.SlotBeginIndex.Add(0); }
                        slotbegin = (slotid == 0 ?
                                       96 // 96-byte header that is used to store system information about the page
                                       :
                                       tpage.SlotBeginIndex[slotid - 1] + log.RowLog_Contents_0.Length);
                        tpage.SlotBeginIndex[slotid] = slotbegin;
                        stemp = stemp.Stuff((slotbegin + (log.Offset_in_Row ?? 0)) * 2,
                                            log.RowLog_Contents_0.Length * 2,
                                            log.RowLog_Contents_0.ToText());
                        if (tpage.SlotData.ContainsKey(slotid) == false) { tpage.SlotData.Add(slotid, ""); }
                        tpage.SlotData[slotid] = log.RowLog_Contents_0.ToText();
                    }

                    lobpagedata[log.Page_ID].PageData = stemp;
                }
            }

        }

        private void SetPrevPages(string Page_ID, string Current_LSN)
        {
            if (PrevPages == null)
            {
                PrevPages = new List<(string, string)>();
            }
            else
            {
                if (PrevPages.Any(p => p.pageid == Page_ID) == true)
                {
                    PrevPages.RemoveAll(p => p.pageid == Page_ID);
                }
            }
            PrevPages.Add((Page_ID, Current_LSN));
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
            tsql = $"DBCC PAGE([{DatabaseName}],{fileid_dec},{pageid_dec},3) with tableresults,no_infomsgs; ";
            tsql = "set transaction isolation level read uncommitted; "
                    + $"insert into #temppagedata(ParentObject,Object,Field,Value) exec('{tsql}'); ";
            DB.ExecuteSQL(tsql, false);

            tsql = $"update #temppagedata set LSN=N'{pLog.Current_LSN}' where LSN is null; ";
            DB.ExecuteSQL(tsql, false);

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

            tsql = "truncate table #ModifiedRawData; ";
            DB.ExecuteSQL(tsql, false);

            tsql = " insert into #ModifiedRawData([RowLog Contents 0_var]) "
                    + " select [RowLog Contents 0_var]=upper(replace(stuff((select replace(substring(C.[Value],charindex(N':',[Value],1)+1,48),N'†',N'') "
                    + "                                                     from #temppagedata C "
                    + $"                                                    where C.[LSN]=N'{pLog.Current_LSN}' "
                    + $"                                                    and C.[ParentObject] like N'Slot {pLog.Slot_ID.ToString()} Offset%' "
                    + "                                                     and C.[Object] like N'%Memory Dump%' "
                    + "                                                     order by C.[Value] "
                    + "                                                     for xml path('')),1,1,N''),N' ',N'')); ";
            DB.ExecuteSQL(tsql, false);

            if (FTableInfo.GetCompressionType(pLog.PartitionId) != CompressionType.NONE)
            {
                isfound = true;
            }
            else
            {
                tsql = "select count(1) from #ModifiedRawData where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%'; ";
                if (Convert.ToInt32(DB.Query11(tsql, false)) > 0)
                {
                    isfound = true;
                }

                if (isfound == false && pLog.Operation == "LOP_MODIFY_ROW")
                {
                    tsql = "truncate table #ModifiedRawData; ";
                    DB.ExecuteSQL(tsql, false);

                    tsql = "with t as("
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
                    DB.ExecuteSQL(tsql, false);

                    tsql = "select count(1) from #ModifiedRawData where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%'; ";
                    if (Convert.ToInt32(DB.Query11(tsql, false)) > 0)
                    {
                        isfound = true;
                    }
                }
            }

            if (isfound == true)
            {
                tsql = @"update #ModifiedRawData set [RowLog Contents 0]=cast('' as xml).value('xs:hexBinary(substring(sql:column(""[RowLog Contents 0_var]""), 0) )', 'varbinary(max)'); ";
                DB.ExecuteSQL(tsql, false);

                tsql = "select top 1 'MR1'=[RowLog Contents 0] from #ModifiedRawData; ";
                mr1 = DB.Query<byte[]>(tsql, false).FirstOrDefault();
            }
            else
            {
                mr1 = null;
            }

            return mr1;
        }

        private FPageInfo GetPageInfo(string pageid)
        {
            FPageInfo r;
            List<string> ds;
            int i, j, m_slotCnt;
            string tmpstr, slotarray;
            (string pageid, string lsn) pp;
            List<FLOG> prevlogs;

            pageid = pageid.ToLower();
            pp = (PrevPages == null ? (null,null) : PrevPages.FirstOrDefault(p => p.pageid == pageid));
            if (pp.pageid != null)
            {
                tsql = "set transaction isolation level read uncommitted; " 
                       + "select * "
                       + "  from sys.fn_dblog(null,null) t "
                       + $" where [Current LSN]<N'{pp.lsn}' "
                       + $" and [Current LSN]>=(select max([Current LSN]) from sys.fn_dblog(null,null) b where b.[Current LSN]<N'{pp.lsn}' and b.[Page ID]=t.[Page ID] and b.Operation=N'LOP_FORMAT_PAGE') "
                       + $" and [Page ID]=N'{pp.pageid}' "
                       + "  and Operation in(N'LOP_FORMAT_PAGE',N'LOP_INSERT_ROWS',N'LOP_MODIFY_ROW') "
                       + "  order by [Current LSN] ";
                prevlogs = DB.Query<FLOG>(tsql, false);

                r = new FPageInfo();
                r.SlotCnt = (prevlogs.Count > 0 ? prevlogs.Where(p => p.Slot_ID != -1).Max(p => p.Slot_ID ?? 0) + 1 : 0);
                r.SlotBeginIndex = new List<int>();
                r.SlotData = new Dictionary<int, string>();
                for (i = 0; i <= r.SlotCnt - 1; i = i + 1) { r.SlotBeginIndex.Add(0); r.SlotData.Add(i, ""); }
                foreach (FLOG log in prevlogs
                                     .OrderBy(p => (p.Slot_ID ?? 0).ToString().PadLeft(5, '0') + p.Current_LSN)
                        )
                {
                    i = (log.Slot_ID ?? 0);

                    switch (log.Operation)
                    {
                        case "LOP_FORMAT_PAGE":
                            r.PageType = log.PageFormat_PageType.ToString();
                            break;
                        case "LOP_INSERT_ROWS":
                            r.SlotData[i] = log.RowLog_Contents_0.ToText();
                            break;
                        case "LOP_MODIFY_ROW":
                            if (prevlogs.Any(p => string.Compare(p.Current_LSN, log.Current_LSN) == -1
                                                  && p.Operation == "LOP_INSERT_ROWS" 
                                                  && p.Page_ID == log.Page_ID 
                                                  && p.Slot_ID == log.Slot_ID) == true)
                            {
                                r.SlotData[i] = r.SlotData[i].Stuff(Convert.ToInt32(log.Offset_in_Row) * 2,
                                                                    log.RowLog_Contents_0.Length * 2,
                                                                    log.RowLog_Contents_1.ToText());
                            }
                            
                            break;
                    }

                    if (i >= 0)
                    {
                        r.SlotBeginIndex[i] = 96 + r.SlotData.Where(p => p.Key < i).Sum(p => p.Value.Length / 2);
                    }
                }

                tmpstr = new string(' ', 96 * 2);
                for (i = 0; i <= r.SlotCnt - 1; i = i + 1) 
                { 
                    tmpstr = tmpstr + r.SlotData[i];
                }
                tmpstr = tmpstr 
                         + "78".Replicate(1024 * 8 - 96 - 42 - r.SlotData.Sum(p => p.Value.Length / 2))
                         + new string(' ', 42 * 2);
                r.PageData = tmpstr;

                if (lobpagedata.ContainsKey(pageid) == true)
                {
                    lobpagedata.Remove(pageid);
                }
                lobpagedata.Add(pageid, r);

                PrevPages.RemoveAll(p => p.pageid == pageid);
            }
            else
            {
                if (lobpagedata.ContainsKey(pageid) == true)
                {
                    r = lobpagedata[pageid];
                }
                else
                {
                    r = new FPageInfo();
                    r.FileNum = Convert.ToInt16(pageid.Split(':')[0], 16);
                    r.PageNum = Convert.ToInt32(pageid.Split(':')[1], 16);
                    r.FileNumPageNum_Hex = pageid;

                    tsql = "truncate table #temppagedatalob; ";
                    DB.ExecuteSQL(tsql, false);

                    tsql = $"DBCC PAGE([{DatabaseName}],{r.FileNum.ToString()},{r.PageNum.ToString()},2) with tableresults,no_infomsgs; ";
                    tsql = "set transaction isolation level read uncommitted; "
                            + $"insert into #temppagedatalob(ParentObject,Object,Field,Value) exec('{tsql}'); ";
                    DB.ExecuteSQL(tsql, false);

                    // pagedata
                    tsql = $"select rn=row_number() over(order by Value)-1,Value=replace(upper(substring(Value,21,44)),N' ',N'') "
                         + $"from #temppagedatalob "
                         + $"where ParentObject=N'DATA:' "
                         + $"and Object like N'Memory Dump%'; ";
                    ds = DB.Query<(int rn, string Value)>(tsql, false).Select(p => p.Value).ToList();
                    r.PageData = string.Join("", ds);
                    if (r.PageData.Length > 1024 * 8 * 2)
                    {
                        r.PageData = r.PageData.Substring(0, 1024 * 8 * 2);
                    }

                    // pagetype
                    tsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_type'; ";
                    r.PageType = DB.Query11(tsql, false);

                    // SlotCnt
                    tsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_slotCnt'; ";
                    m_slotCnt = Convert.ToInt32(DB.Query11(tsql, false));
                    r.SlotCnt = m_slotCnt;

                    // SlotBeginIndex
                    r.SlotBeginIndex = new List<int>();
                    slotarray = r
                                .PageData
                                .Replace("†", "")
                                .Substring(r.PageData.Replace("†", "").Length - m_slotCnt * 2 * 2, m_slotCnt * 2 * 2);
                    for (i = 0, j = slotarray.Length - 2;
                         i <= m_slotCnt - 1;
                         i = i + 1, j = j - 4)
                    {
                        tmpstr = $"{slotarray.Substring(j, 2)}{slotarray.Substring(j - 2, 2)}";
                        r.SlotBeginIndex.Add(Convert.ToInt32(tmpstr, 16));
                    }

                    // SlotData
                    r.SlotData = new Dictionary<int, string>();
                    for (i = 0; i <= m_slotCnt - 1; i = i + 1)
                    {
                        if (r.SlotBeginIndex[i] >= 96)
                        {
                            j = (i < m_slotCnt - 1 ? 
                                    (r.SlotBeginIndex[i + 1] - r.SlotBeginIndex[i]) * 2 
                                    : (r.PageData.Length / 2 - m_slotCnt * 2 - r.SlotBeginIndex[i]) * 2
                                );
                            tmpstr = r.PageData.Substring(r.SlotBeginIndex[i] * 2, j);
                        }
                        else
                        {
                            tmpstr = "";
                        }
                        r.SlotData.Add(i, tmpstr);
                    }

                    lobpagedata.Add(pageid, r);
                }
            }

            return r;
        }

        public void AnalyzeUpdate(FLOG curlog, byte[] mr1, List<FLOG> wslog,
                                  ref string ValueList1, ref string ValueList0, 
                                  ref string WhereList1, ref string WhereList0, 
                                  ref byte[] mr0)
        {
            int i;
            string mr0_str;
            TableColumn[] columns0, columns1;
            CompressionType compressiontype;

            columns0 = FTableInfo.Columns.CopyToNew().Cast<TableColumn>().ToArray();
            columns1 = FTableInfo.Columns.CopyToNew().Cast<TableColumn>().ToArray();

            compressiontype = FTableInfo.GetCompressionType(curlog.PartitionId);
            switch (compressiontype)
            {
                case CompressionType.NONE:
                case CompressionType.COLUMNSTORE:
                    TranslateData(mr1, columns1);
                    break;
                case CompressionType.ROW:
                case CompressionType.PAGE:
                    TranslateData_Compression(mr1, columns1);
                    break;
            }
            
            switch (curlog.Operation)
            {
                case "LOP_MODIFY_ROW":
                    mr0_str = RESTORE_LOP_MODIFY_ROW(curlog, mr1);
                    break;
                case "LOP_MODIFY_COLUMNS":
                    mr0_str = RESTORE_LOP_MODIFY_COLUMNS(curlog, mr1, columns0, columns1, compressiontype);
                    break;
                default:
                    mr0_str = mr1.ToText();
                    break;
            }

            FRestoreLCXTEXT(curlog, wslog);

            mr0 = mr0_str.ToByteArray();

            switch (compressiontype)
            {
                case CompressionType.NONE:
                case CompressionType.COLUMNSTORE:
                    TranslateData(mr0, columns0);
                    break;
                case CompressionType.ROW:
                case CompressionType.PAGE:
                    TranslateData_Compression(mr0, columns0);
                    break;
            }

            ValueList1 = "";
            ValueList0 = "";
            WhereList1 = "";
            WhereList0 = "";
            for (i = 0; i <= FTableInfo.Columns.Length - 1; i++)
            {
                if (FTableInfo.Columns[i].PhysicalStorageType == SqlDbType.Timestamp || FTableInfo.Columns[i].IsComputed == true) { continue; }

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

                if (FTableInfo.PrimaryKeyColumns.Count == 0
                    || FTableInfo.PrimaryKeyColumns.Contains(FTableInfo.Columns[i].ColumnName))
                {
                    WhereList0 = WhereList0 + (WhereList0.Length > 0 ? " and " : "")
                                  + ColumnName2SQLName(FTableInfo.Columns[i]) 
                                  + (columns1[i].IsNull ? " is " : "=")
                                  + ColumnValue2SQLValue(columns1[i]);
                    WhereList1 = WhereList1 + (WhereList1.Length > 0 ? " and " : "")
                                  + ColumnName2SQLName(FTableInfo.Columns[i]) 
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
                if (mr1.Length >= 4 && log.IsVirtual == false)
                {
                    mr0_str = mr1.ToText().Stuff(Convert.ToInt32(log.Offset_in_Row) * 2,
                                                 log.RowLog_Contents_1.ToText().Length,
                                                 log.RowLog_Contents_0.ToText());

                    if (log.RowLog_Contents_0.Length < log.Modify_Size)
                    {
                        tpageinfo = GetPageInfo(log.Page_ID);
                        slotid = (Convert.ToInt32(log.Slot_ID) <= tpageinfo.SlotBeginIndex.Count - 1 ? Convert.ToInt32(log.Slot_ID) : tpageinfo.SlotBeginIndex.Count - 1);
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

        private string RESTORE_LOP_MODIFY_COLUMNS(FLOG log, byte[] mr1, TableColumn[] columns0, TableColumn[] columns1, CompressionType compressiontype)
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
                switch (compressiontype)
                {
                    case CompressionType.NONE:
                    case CompressionType.COLUMNSTORE:
                    case CompressionType.PAGE:
                    case CompressionType.ROW:
                        mr0_str = mr1_str;
                        for (i = 1, j = 0; i <= (log.RowLog_Contents_0.Length / 4); i++)
                        {
                            fstart0 = Convert.ToInt32(log.RowLog_Contents_0[i * 4 - 3].ToString("X2") + log.RowLog_Contents_0[i * 4 - 4].ToString("X2"), 16);
                            fstart1 = Convert.ToInt32(log.RowLog_Contents_0[i * 4 - 1].ToString("X2") + log.RowLog_Contents_0[i * 4 - 2].ToString("X2"), 16);

                            flength0 = Convert.ToInt32(log.RowLog_Contents_1[i * 2 - 1].ToString("X2") + log.RowLog_Contents_1[i * 2 - 2].ToString("X2"), 16);
                            flength0f4 = (flength0 % 4 == 0 ? flength0 : flength0 + (4 - flength0 % 4));

                            fvalue0 = rowlogdata.Substring(j * 2, flength0 * 2);
                            j = j + flength0f4;

                            //flength1 = flength0;
                            //if (
                            //    i == (log.RowLog_Contents_0.Length / 4)
                            //    && (j * 2) < (rowlogdata.Length - 1)
                            //   )
                            //{
                            //    flength1 = rowlogdata.Length / 2 - j;
                            //}
                            if (i < (log.RowLog_Contents_0.Length / 4))
                            {
                                // 對於 1 到 N-1 個元素：利用下一個元素的偏移量變化(Shift)來精確計算當前元素的新長度
                                int next_fstart0 = Convert.ToInt32(log.RowLog_Contents_0[(i + 1) * 4 - 3].ToString("X2") + log.RowLog_Contents_0[(i + 1) * 4 - 4].ToString("X2"), 16);
                                int next_fstart1 = Convert.ToInt32(log.RowLog_Contents_0[(i + 1) * 4 - 1].ToString("X2") + log.RowLog_Contents_0[(i + 1) * 4 - 2].ToString("X2"), 16);

                                int currentShift = fstart1 - fstart0;
                                int nextShift = next_fstart1 - next_fstart0;

                                flength1 = flength0 + (nextShift - currentShift);
                            }
                            else
                            {
                                // 對於最後一個元素：因為沒有下一個偏移量可供參考，我們根據 rowlogdata 剩餘長度，與 mr1_str 進行精確比對以排除 Padding 補零位
                                int max_flen1 = rowlogdata.Length / 2 - j;
                                flength1 = 0;

                                int max_check = Math.Min(max_flen1, mr1_str.Length / 2 - fstart1);
                                for (k = 0; k < max_check; k++)
                                {
                                    // 逐 Byte 比對 Payload 與實際的 After Image，精確算出有效長度
                                    if (rowlogdata.Substring((j + k) * 2, 2) == mr1_str.Substring((fstart1 + k) * 2, 2))
                                    {
                                        flength1++;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            flength1f4 = (flength1 % 4 == 0 ? flength1 : flength1 + (4 - flength1 % 4));

                            fvalue1 = rowlogdata.Substring(j * 2, flength1 * 2);
                            j = j + flength1f4;

                            mr0_str = mr0_str.Stuff(fstart0 * 2, flength1 * 2, fvalue0);
                        }

                        mr0 = mr0_str.ToByteArray();
                        if (compressiontype == CompressionType.NONE || compressiontype == CompressionType.COLUMNSTORE)
                        {
                            TranslateData(mr0, columns0);
                        }
                        if (compressiontype == CompressionType.PAGE || compressiontype == CompressionType.ROW)
                        {
                            TranslateData_Compression(mr0, columns0);
                        }
                        bfinish = true;
                        break;
                    default:
                        mr0_str = mr1_str;
                        bfinish = true;
                        break;
                }
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
                        if (compressiontype == CompressionType.NONE || compressiontype == CompressionType.COLUMNSTORE)
                        {
                            TranslateData(mr0, columns0);
                        }
                        if (compressiontype == CompressionType.PAGE || compressiontype == CompressionType.ROW)
                        {
                            TranslateData_Compression(mr0, columns0);
                        }
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
                && FTableInfo.IsHeapTable == false) // 堆表不适用本规则
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
            if (index2 > rowdata.Length - 2) { return; }

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
            if (FTableInfo.ClusteredIndexColumns.Count > 0)
            {
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string cc in FTableInfo.ClusteredIndexColumns)
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
            if (FTableInfo.ClusteredIndexColumns.Count == 0 && FTableInfo.PrimaryKeyColumns.Count > 0)
            {
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string pc in FTableInfo.PrimaryKeyColumns)
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

            if (FTableInfo.IsHeapTable == false && FTableInfo.PrimaryKeyColumns.SequenceEqual(FTableInfo.ClusteredIndexColumns) == false)
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
                                (ValueHex, Value) = TranslateData_Text(rowdata, tvc, false, FTableInfo.TextInRow);
                                c.ValueHex = ValueHex;
                                c.Value = Value;
                                c.IsNull = (ValueHex == null && Value == "nullvalue");
                                break;
                            case System.Data.SqlDbType.NText:
                                (ValueHex, Value) = TranslateData_Text(rowdata, tvc, true, FTableInfo.TextInRow);
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
        
        private TableInfo GetTableInfo(string PSchemaName, string PTablename, bool save = true)
        {
            string stemp;
            TableInfo tableinfo;

            if (UserTables.ContainsKey($"{PSchemaName}.{PTablename}") == true)
            {
                tableinfo = UserTables[$"{PSchemaName}.{PTablename}"];
            }
            else 
            {
                tableinfo = new TableInfo();

                // PrimaryKeyColumns
                tsql = "select primarykeycolumn=c.name "
                         + " from sys.indexes a "
                         + " join sys.index_columns b on a.object_id=b.object_id and a.index_id=b.index_id "
                         + " join sys.columns c on b.object_id=c.object_id and b.column_id=c.column_id "
                         + " join sys.objects d on a.object_id=d.object_id "
                         + " join sys.schemas s on d.schema_id=s.schema_id "
                         + " where a.is_primary_key=1 "
                         + $" and s.name=N'{PSchemaName}' "
                         + "  and d.type='U' "
                         + $" and d.name=N'{PTablename}' "
                         + "  order by b.key_ordinal; ";
                tableinfo.PrimaryKeyColumns = DB.Query<string>(tsql, false).ToList();

                // ClusteredIndexColumns
                tsql = "select clusteredindexcolumn=c.name "
                        + "  from sys.indexes a "
                        + "  join sys.index_columns b on a.object_id=b.object_id and a.index_id=b.index_id "
                        + "  join sys.columns c on b.object_id=c.object_id and b.column_id=c.column_id "
                        + "  join sys.objects d on a.object_id=d.object_id "
                        + "  join sys.schemas s on d.schema_id=s.schema_id "
                        + "  where a.index_id<=1 "
                        + "  and a.type=1 "
                        + $" and s.name=N'{PSchemaName}' "
                        + "  and d.type='U' "
                        + $" and d.name=N'{PTablename}' "
                        + "  order by b.key_ordinal; ";
                tableinfo.ClusteredIndexColumns = DB.Query<string>(tsql, false).ToList();

                // IsHeapTable
                tsql = "select isheaptable=cast(case when exists(select 1 "
                          + "                                     from sys.tables t "
                          + "                                     join sys.schemas s on t.schema_id=s.schema_id "
                          + "                                     join sys.indexes i on t.object_id=i.object_id "
                          + $"                                    where s.name=N'{PSchemaName}' "
                          + $"                                    and t.name=N'{PTablename}' "
                          + "                                     and i.index_id=0) then 1 else 0 end as bit); ";
                tableinfo.IsHeapTable = DB.Query<bool>(tsql, false).FirstOrDefault();

                // AllocUnitName
                tsql = "select allocunitname=isnull(d.name,N'') "
                        + "  from sys.tables a "
                        + "  join sys.schemas s on a.schema_id=s.schema_id "
                        + "  join sys.indexes d on a.object_id=d.object_id "
                        + "  where d.type in(0,1,5) "
                        + $" and s.name=N'{PSchemaName}' "
                        + $" and a.name=N'{PTablename}'; ";
                tableinfo.AllocUnitName = DB.Query<string>(tsql, false).FirstOrDefault();

                // TextInRow
                tsql = "select textinrow=a.text_in_row_limit, "
                        + $"    isnodetable={(DB.Vesion >= 2017 ? "a.is_node" : "0")}, "
                        + $"    isedgetable={(DB.Vesion >= 2017 ? "a.is_edge" : "0")}"
                        + "  from sys.tables a "
                        + "  join sys.schemas s on a.schema_id=s.schema_id "
                        + $" where s.name=N'{PSchemaName}' "
                        + $" and a.name=N'{PTablename}'; ";
                (tableinfo.TextInRow, tableinfo.IsNodeTable, tableinfo.IsEdgeTable) = DB.Query<(int, bool, bool)>(tsql, false).FirstOrDefault();

                // IsColumnStore
                tsql = "select iscolumnstore=cast(case when exists(select 1 "
                          + "                                       from sys.tables t "
                          + "                                       join sys.schemas s on t.schema_id=s.schema_id "
                          + "                                       join sys.indexes i on t.object_id=i.object_id "
                          + $"                                      where s.name=N'{PSchemaName}' "
                          + $"                                      and t.name=N'{PTablename}' "
                          + "                                       and i.index_id=1 "
                          + "                                       and i.type=5) then 1 else 0 end as bit); ";
                tableinfo.IsColumnStore = DB.Query<bool>(tsql, false).FirstOrDefault();

                // DataCompressionType
                tsql = "select PartitionId=p.partition_id, "
                        + "    CompressionType=case p.data_compression when 0 then N'NONE' when 1 then N'ROW' when 2 then N'PAGE' when 3 then N'COLUMNSTORE' when 4 then N'COLUMNSTORE_ARCHIVE' else N'' end "
                        + "  from sys.tables t "
                        + "  join sys.schemas s on t.schema_id=s.schema_id "
                        + "  join sys.partitions p on t.object_id=p.object_id "
                        + $" where s.name=N'{PSchemaName}' "
                        + $" and t.name=N'{PTablename}' "
                        + "  and p.index_id<=1; ";
                tableinfo.DataCompressionType = DB.Query<(long PartitionId, CompressionType CompressionType)>(tsql, false).ToDictionary(p => p.PartitionId, p => p.CompressionType);

                tsql = "select cast(("
                            + "select ColumnID,ColumnName,DataType,PhysicalStorageType,Length,Precision,IsNullable,Scale,IsIdentity,IsComputed,LeafOffset,LeafNullBit,IsHidden,GraphType "
                            + " from (select 'ColumnID'=b.column_id, "
                            + "              'ColumnName'=b.name, "
                            + "              'DataType'=c.name, "
                            + "              'PhysicalStorageType'=case when c.name not in(N'geography',N'geometry',N'hierarchyid') then c2.name else N'varbinary' end, "
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
                            + "       from sys.objects a " // sys.tables
                            + "       join sys.schemas s on a.schema_id=s.schema_id "
                            + "       join sys.columns b on a.object_id=b.object_id "
                            + "       join sys.systypes c on b.system_type_id=c.xtype and b.user_type_id=c.xusertype "
                            + "       left join sys.systypes c2 on c.xtype=c2.xtype and c.xtype=c2.xusertype "
                            + "       outer apply (select d.leaf_offset,d.leaf_null_bit "
                            + "                    from sys.system_internals_partition_columns d "
                            + "                    where d.partition_column_id=b.column_id "
                            + "                    and d.partition_id in (select partitionss.partition_id "
                            + "                                           from sys.allocation_units allocunits "
                            + "                                           join sys.partitions partitionss on (allocunits.type in(1, 3) and allocunits.container_id=partitionss.hobt_id) "
                            + "                                                                              or (allocunits.type=2 and allocunits.container_id=partitionss.partition_id) "
                            + "                                           where partitionss.object_id=a.object_id and partitionss.index_id<=1)) d2 "
                            + $"      where s.name=N'{PSchemaName}' "
                            + $"      and a.name=N'{PTablename}') t "
                            + " order by ColumnID "
                            + " for xml raw('Column'),root('ColumnList') "
                            + ") as nvarchar(max)); ";
                stemp = DB.Query11(tsql, false);
                tableinfo.Columns = AnalyzeTablelayout(stemp);
                tableinfo.Version = "";

                if (save == true)
                {
                    if (UserTables.ContainsKey($"{PSchemaName}.{PTablename}") == true)
                    {
                        UserTables.Remove($"{PSchemaName}.{PTablename}");
                    }
                    UserTables.Add($"{PSchemaName}.{PTablename}", tableinfo);
                }
            }

            return tableinfo;
        }

        private TableColumn[] AnalyzeTablelayout(string TableLayout)
        {
            int i;
            XmlDocument xmlDoc;
            XmlNode xmlRootnode;
            XmlNodeList xmlNodelist;
            TableColumn[] Columns;
            TableColumn fcol;

            xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(TableLayout);
            xmlRootnode = xmlDoc.SelectSingleNode("ColumnList");
            xmlNodelist = xmlRootnode.ChildNodes;

            Columns = new TableColumn[xmlNodelist.Count];
            i = 0;
            foreach (XmlNode xmlNode in xmlNodelist)
            {
                fcol = new TableColumn();
                fcol.ColumnID = Convert.ToInt16(xmlNode.Attributes["ColumnID"].Value.ToString());
                fcol.ColumnName = xmlNode.Attributes["ColumnName"].Value;
                fcol.DataType = xmlNode.Attributes["DataType"].Value;
                fcol.PhysicalStorageType = GetPhysicalStorageType(xmlNode.Attributes["PhysicalStorageType"].Value);
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

                Columns[i] = fcol;
                i = i + 1;
            }

            return Columns;
        }

        private System.Data.SqlDbType GetPhysicalStorageType(string ftype)
        {
            System.Data.SqlDbType r;
            
            switch (ftype)
            {
                case "bigint": r = System.Data.SqlDbType.BigInt; break;
                case "binary": r = System.Data.SqlDbType.Binary; break;
                case "bit": r = System.Data.SqlDbType.Bit; break;
                case "char": r = System.Data.SqlDbType.Char; break;
                case "date": r = System.Data.SqlDbType.Date; break;
                case "datetime": r = System.Data.SqlDbType.DateTime; break;
                case "datetime2": r = System.Data.SqlDbType.DateTime2; break;
                case "datetimeoffset": r = System.Data.SqlDbType.DateTimeOffset; break;
                case "decimal": r = System.Data.SqlDbType.Decimal; break;
                case "float": r = System.Data.SqlDbType.Float; break;
                case "geography": r = System.Data.SqlDbType.VarBinary; break;
                case "geometry": r = System.Data.SqlDbType.VarBinary; break;
                case "hierarchyid": r = System.Data.SqlDbType.VarBinary; break;
                case "image": r = System.Data.SqlDbType.Image; break;
                case "int": r = System.Data.SqlDbType.Int; break;
                case "money": r = System.Data.SqlDbType.Money; break;
                case "nchar": r = System.Data.SqlDbType.NChar; break;
                case "ntext": r = System.Data.SqlDbType.NText; break;
                case "numeric": r = System.Data.SqlDbType.Decimal; break; // numeric=decimal
                case "nvarchar": r = System.Data.SqlDbType.NVarChar; break;
                case "real": r = System.Data.SqlDbType.Real; break;
                case "smalldatetime": r = System.Data.SqlDbType.SmallDateTime; break;
                case "smallint": r = System.Data.SqlDbType.SmallInt; break;
                case "smallmoney": r = System.Data.SqlDbType.SmallMoney; break;
                case "sql_variant": r = System.Data.SqlDbType.Variant; break;
                case "sysname": r = System.Data.SqlDbType.NVarChar; break;
                case "text": r = System.Data.SqlDbType.Text; break;
                case "time": r = System.Data.SqlDbType.Time; break;
                case "timestamp": r = System.Data.SqlDbType.Timestamp; break;
                case "tinyint": r = System.Data.SqlDbType.TinyInt; break;
                case "uniqueidentifier": r = System.Data.SqlDbType.UniqueIdentifier; break;
                case "varbinary": r = System.Data.SqlDbType.VarBinary; break;
                case "varchar": r = System.Data.SqlDbType.VarChar; break;
                case "xml": r = System.Data.SqlDbType.Xml; break;
                default: r = System.Data.SqlDbType.Variant; break;
            }

            return r;
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

    }

}
