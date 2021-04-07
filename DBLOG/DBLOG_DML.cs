using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DBLOG
{
    // DML log Analyzer for DML
    public partial class DBLOG_DML
    {
        private DatabaseOperation oDB;  // 数据库操作
        private string sTsql;      // 动态SQL

        public string sDatabasename;  // 数据库名
        public string sTableName;     // 表名
        public string sSchemaName;    // 架构名

        public TableColumn[] TableColumns;  // 表结构定义
        public TableInformation TabInfos;   // 表信息

        public int iColumncount;
        public string sColumnlist;
        public string CurrentLSN;    // 当前LSN
        public string TransactionID; // 事务ID

        public DataRow[] dtLogs;     // 原始日志信息
        private DataRow[] drTemp;
        private DataTable dtMRlist;   // 行数据前版本 
        private Dictionary<string, string> lsns; // key:lsn value:pageid
        private Dictionary<string, FPageInfo> lobpagedata; // key:fileid+pageid value:FPageInfo
        //private List<FSlotColumnData> slotcolumndata;

        public DBLOG_DML(string pDatabasename, string pSchemaName, string pTableName, DatabaseOperation poDB)
        {
            oDB = poDB;
            sDatabasename = pDatabasename;
            sTableName = pTableName;
            sSchemaName = pSchemaName;
            
            dtMRlist = new DataTable();
            dtMRlist.Columns.Add("PAGEID", typeof(string));
            dtMRlist.Columns.Add("SlotID", typeof(string));
            dtMRlist.Columns.Add("AllocUnitId", typeof(string));
            dtMRlist.Columns.Add("MR1", typeof(byte[]));
            dtMRlist.Columns.Add("MR1TEXT", typeof(string));
        }

        // 解析日志
        public List<DatabaseLog> AnalyzeLog(string pStartLSN, string pEndLSN)
        {
            List<DatabaseLog> logs;
            DatabaseLog tmplog;
            int i, j, iR0Minimumlength;
            short? OffsetinRow,      // Offset in Row
                   ModifySize;       // Modify Size
            string Operation,        // 操作类型
                   Context,          // Context
                   PageID,           // PageID
                   SlotID,           // SlotID
                   AllocUnitId,      // AllocUnitId
                   AllocUnitName,    // AllocUnitName
                   BeginTime = string.Empty, // 事务开始时间
                   EndTime = string.Empty,   // 事务结束时间
                   REDOSQL = string.Empty,   // redo sql
                   UNDOSQL = string.Empty,   // undo sql
                   stemp, sValueList1, sValueList0, sValue, sWhereList;
            byte[] R0,               // 日志数据 RowLog Contents 0
                   R1,               // 日志数据 RowLog Contents 1
                   R2,               // 日志数据 RowLog Contents 2
                   R3,               // 日志数据 RowLog Contents 3
                   R4,               // 日志数据 RowLog Contents 4
                   LogRecord,        // 日志数据 Log Record
                   MR0 = null,
                   MR1 = null;
            DataRow Mrtemp;
            DataTable dtTemp;
            bool isfound;

            logs = new List<DatabaseLog>();

            try
            {
                var TableInfo = GetTableInfo(sSchemaName, sTableName);
                TabInfos = TableInfo.Item1;
                TableColumns = TableInfo.Item2;
                iColumncount = TableColumns.Length;
                sColumnlist = string.Join(",", TableColumns.Where(p => p.DataType != SqlDbType.Timestamp && p.isComputed == false).Select(p => $"[{p.ColumnName}]"));

                sTsql = @"if object_id('tempdb..#temppagedata') is not null 
                             drop table #temppagedata; 
                          create table #temppagedata(LSN nvarchar(1000),ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
                oDB.ExecuteSQL(sTsql, false);

                sTsql = "create index ix_#temppagedata on #temppagedata(LSN); ";
                oDB.ExecuteSQL(sTsql, false);

                sTsql = @"if object_id('tempdb..#temppagedatalob') is not null 
                             drop table #temppagedatalob; 
                          create table #temppagedatalob(ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
                oDB.ExecuteSQL(sTsql, false);

                //sTsql = @"if object_id('tempdb..#slotcolumndata') is not null 
                //             drop table #slotcolumndata; 
                //          create table #slotcolumndata(tablename nvarchar(255),columnid int,columnname nvarchar(255),offset nvarchar(255),length nvarchar(255),value nvarchar(max)); ";
                //oDB.ExecuteSQL(sTsql, false);

                sTsql = @"if object_id('tempdb..#ModifiedRawData') is not null 
                             drop table #ModifiedRawData; 
                          create table #ModifiedRawData([SlotID] int,[RowLog Contents 0_var] nvarchar(max),[RowLog Contents 0] varbinary(max)); ";
                oDB.ExecuteSQL(sTsql, false);
                
                lsns = new Dictionary<string, string>();
                lobpagedata = new Dictionary<string, FPageInfo>();

                for (i = dtLogs.Length - 1; i >= 0; i--)  // 从后往前解析
                {
                    Operation = dtLogs[i]["Operation"].ToString();
                    TransactionID = dtLogs[i]["Transaction ID"].ToString();
                    Context = dtLogs[i]["Context"].ToString();
                    R0 = (byte[])dtLogs[i]["RowLog Contents 0"];
                    R1 = (byte[])dtLogs[i]["RowLog Contents 1"];
                    R2 = (byte[])dtLogs[i]["RowLog Contents 2"];
                    R3 = (byte[])dtLogs[i]["RowLog Contents 3"];
                    R4 = (byte[])dtLogs[i]["RowLog Contents 4"];
                    LogRecord = (byte[])dtLogs[i]["Log Record"];
                    PageID = dtLogs[i]["PAGE ID"].ToString();
                    SlotID = dtLogs[i]["Slot ID"].ToString();
                    AllocUnitId = dtLogs[i]["AllocUnitId"].ToString();
                    AllocUnitName = dtLogs[i]["AllocUnitName"].ToString();
                    CurrentLSN = dtLogs[i]["Current LSN"].ToString();
                    OffsetinRow = null; if (dtLogs[i]["Offset in Row"] != null) { OffsetinRow = Convert.ToInt16(dtLogs[i]["Offset in Row"]); }
                    ModifySize = null; if (dtLogs[i]["Modify Size"] != null) { ModifySize = Convert.ToInt16(dtLogs[i]["Modify Size"]); }

                    if (AllocUnitName != $"{sSchemaName}.{sTableName}" + (TabInfos.FAllocUnitName.Length == 0 ? "" : "." + TabInfos.FAllocUnitName)
                        || Context == "LCX_TEXT_TREE" 
                        || Context == "LCX_TEXT_MIX")
                    {
                        continue;
                    }

                    lsns.Add(CurrentLSN, PageID);

                    sTsql = "select top 1 BeginTime=substring(BeginTime,1,19),EndTime=substring(EndTime,1,19) from #TransactionList where TransactionID='" + TransactionID + "'; ";
                    dtTemp = oDB.Query(sTsql, false);
                    if (dtTemp.Rows.Count > 0)
                    {
                        BeginTime = dtTemp.Rows[0]["BeginTime"].ToString();
                        EndTime = dtTemp.Rows[0]["EndTime"].ToString();
                    }
                    else
                    {
                        BeginTime = "";
                        EndTime = "";
                    }

#if DEBUG
                    sTsql = "insert into dbo.LogExplorer_AnalysisLog(ADate,TableName,Logdescr,Operation,LSN) "
                            + " select getdate(),'" + $"[{sSchemaName}].[{sTableName}]" + "', N'RunAnalysisLog...', '" + Operation + "','" + CurrentLSN + "' ";
                    oDB.ExecuteSQL(sTsql, false);
#endif

                    if (Operation == "LOP_MODIFY_ROW" || Operation == "LOP_MODIFY_COLUMNS")
                    {
                        isfound = false;

                        drTemp = dtMRlist.Select("PAGEID='" + PageID + "' and SlotID='" + SlotID + "' and AllocUnitId='" + AllocUnitId + "' ");
                        if (drTemp.Length > 0
                            && (
                                (Operation == "LOP_MODIFY_COLUMNS")
                                || 
                                (Operation == "LOP_MODIFY_ROW" && drTemp[0]["MR1TEXT"].ToString().Contains(R1.ToText()))
                               )
                           )
                        {
                            isfound = true;
                        }

                        if (isfound == false && Operation == "LOP_MODIFY_ROW")
                        {
                            drTemp = dtMRlist.Select("PAGEID='" + PageID + "' and MR1TEXT like '%" + R1.ToText() + "%' ");
                            if (drTemp.Length > 0)
                            {
                                isfound = true;
                            }
                        }

                        if (isfound == false)
                        {
                            MR1 = GetMR1(Operation, PageID, AllocUnitId, CurrentLSN, pStartLSN, pEndLSN, R1.ToText());

                            if (MR1 != null)
                            {
                                if (drTemp.Length > 0)
                                {
                                    dtMRlist.Rows.Remove(drTemp[0]);
                                }

                                Mrtemp = dtMRlist.NewRow();
                                Mrtemp["PAGEID"] = PageID;
                                Mrtemp["SlotID"] = SlotID;
                                Mrtemp["AllocUnitId"] = AllocUnitId;
                                Mrtemp["MR1"] = MR1;
                                Mrtemp["MR1TEXT"] = MR1.ToText();

                                dtMRlist.Rows.Add(Mrtemp);
                            }
                        }
                        else
                        {
                            MR1 = (byte[])drTemp[0]["MR1"];
                        }

                        //sTsql = $"select tablename,columnid,columnname,offset,length,value from #slotcolumndata where tablename=N'{sSchemaName}.{sTableName}'; ";
                        //dtTemp = oDB.Query(sTsql, false);
                        //slotcolumndata = new List<FSlotColumnData>();
                        //foreach(DataRow dr in dtTemp.Rows)
                        //{
                        //    slotcolumndata.Add(
                        //        new FSlotColumnData() 
                        //        { 
                        //          tablename = dr["tablename"].ToString(),
                        //          columnid = Convert.ToInt32(dr["columnid"]),
                        //          columnname = dr["columnname"].ToString(),
                        //          offset = Convert.ToInt32(dr["offset"].ToString(), 16),
                        //          length = Convert.ToInt32(dr["length"].ToString()),
                        //          value = dr["value"].ToString()
                        //        });
                        //}
                    }

                    stemp = string.Empty;
                    REDOSQL = string.Empty;
                    UNDOSQL = string.Empty;
                    sValueList1 = string.Empty;
                    sValueList0 = string.Empty;
                    sWhereList = string.Empty;
                    MR0 = new byte[1];

                    #region Insert / Delete
                    if (Operation == "LOP_INSERT_ROWS" || Operation == "LOP_DELETE_ROWS")
                    {
                        iR0Minimumlength = 2;
                        iR0Minimumlength = iR0Minimumlength + TableColumns.Where(p => p.isVarLenDataType == false).Sum(p => p.Length);
                        iR0Minimumlength = iR0Minimumlength + 2;

                        if (R0.Length >= iR0Minimumlength)
                        {
                            TranslateData(R0, TableColumns, TabInfos.PrimarykeyColumnList, TabInfos.ClusteredindexColumnList);

                            MR0 = new byte[R0.Length];
                            MR0 = R0;

                            for (j = 0; j <= iColumncount - 1; j++)
                            {
                                if (TableColumns[j].DataType == SqlDbType.Timestamp || TableColumns[j].isComputed == true) { continue; }

                                sValue = ColumnValue2SQLValue(TableColumns[j].DataType, TableColumns[j].Value, TableColumns[j].isNull);
                                sValueList1 = sValueList1 + (sValueList1.Length > 0 ? ", " : "") + sValue;

                                if (TableColumns[j].isNull == false)
                                {
                                    // 无主键时用全部字段过滤
                                    if (TabInfos.PrimarykeyColumnList.Length == 0)
                                    {
                                        sWhereList = sWhereList + (sWhereList.Length > 0 ? " and " : "") + "[" + TableColumns[j].ColumnName + "]=" + sValue;
                                    }
                                    else
                                    {
                                        if (TabInfos.PrimarykeyColumnList.IndexOf("," + TableColumns[j].ColumnName + ",", 0) > -1)
                                        {
                                            sWhereList = sWhereList + (sWhereList.Length > 0 ? " and " : "") + "[" + TableColumns[j].ColumnName + "]=" + sValue;
                                        }
                                    }
                                }
                            }

                            // 产生redo sql和undo sql -- Insert
                            if (Operation == "LOP_INSERT_ROWS")
                            {
                                REDOSQL = "insert into " + $"[{sSchemaName}].[{sTableName}]" + "(" + sColumnlist + ") values(" + sValueList1 + "); ";
                                UNDOSQL = "delete from " + $"[{sSchemaName}].[{sTableName}]" + " where " + sWhereList + "; ";

                                if (TabInfos.IdentityColumn.Length > 0)
                                {
                                    REDOSQL = "set identity_insert " + $"[{sSchemaName}].[{sTableName}]" + " on; " + "\r\n"
                                            + REDOSQL + "\r\n"
                                            + "set identity_insert " + $"[{sSchemaName}].[{sTableName}]" + " off; " + "\r\n";
                                }
                            }

                            // 产生redo sql和undo sql -- Delete
                            if (Operation == "LOP_DELETE_ROWS")
                            {
                                REDOSQL = "delete from " + $"[{sSchemaName}].[{sTableName}]" + " where " + sWhereList + "; ";
                                UNDOSQL = "insert into " + $"[{sSchemaName}].[{sTableName}]" + "(" + sColumnlist + ") values(" + sValueList1 + "); ";

                                if (TabInfos.IdentityColumn.Length > 0)
                                {
                                    UNDOSQL = "set identity_insert " + $"[{sSchemaName}].[{sTableName}]" + " on; " + "\r\n"
                                            + UNDOSQL + "\r\n"
                                            + "set identity_insert " + $"[{sSchemaName}].[{sTableName}]" + " off; " + "\r\n";
                                }
                            }
                        }
                        else
                        {
                            REDOSQL = string.Empty;
                            UNDOSQL = string.Empty;
                            stemp = $"R0.Length[{R0.Length.ToString()}] < iR0Minimumlength[{iR0Minimumlength.ToString()}]";
                        }
                    }
                    #endregion

                    #region Update
                    if (Operation == "LOP_MODIFY_COLUMNS" || Operation == "LOP_MODIFY_ROW")
                    {
                        if (MR1 != null)
                        {
                            AnalyzeUpdate(MR1, R0, R1, R3, R4, LogRecord, TableColumns, iColumncount, TabInfos.PrimarykeyColumnList, Operation, CurrentLSN, OffsetinRow, ModifySize, ref sValueList1, ref sValueList0, ref sWhereList, ref MR0);

                            if (sValueList1.Length > 0)
                            {
                                REDOSQL = $"update [{sSchemaName}].[{sTableName}] set {sValueList1} where {sWhereList}; ";
                                UNDOSQL = $"update [{sSchemaName}].[{sTableName}] set {sValueList0} where {sWhereList}; ";
                                stemp = "S: "
                                        + " MR1=" + MR1.ToText() + ", "
                                        + " MR0=" + MR0.ToText() + ", "
                                        + " R1=" + R1.ToText() + ", "
                                        + " R0=" + R0.ToText() + ". ";
                            }
                            else
                            {
                                REDOSQL = string.Empty;
                                UNDOSQL = string.Empty;
                                stemp = "sValueList1.Length=0, "
                                        + " MR1=" + MR1.ToText() + ", "
                                        + " MR0=" + MR0.ToText() + ", "
                                        + " R1=" + R1.ToText() + ", "
                                        + " R0=" + R0.ToText() + ". ";
                            }
                        }
                        else
                        {
                            REDOSQL = string.Empty;
                            UNDOSQL = string.Empty;
                            stemp = "MR1=null";
                        }
                    }
                    #endregion

                    if (Operation == "LOP_MODIFY_ROW" || Operation == "LOP_MODIFY_COLUMNS" || Operation == "LOP_DELETE_ROWS")
                    {
                        drTemp = dtMRlist.Select("PAGEID='" + PageID + "' and SlotID='" + SlotID + "' and AllocUnitId='" + AllocUnitId + "' ");
                        if (drTemp.Length > 0) { dtMRlist.Rows.Remove(drTemp[0]); }

                        Mrtemp = dtMRlist.NewRow();
                        Mrtemp["PAGEID"] = PageID;
                        Mrtemp["SlotID"] = SlotID;
                        Mrtemp["AllocUnitId"] = AllocUnitId;
                        Mrtemp["MR1"] = MR0;
                        Mrtemp["MR1TEXT"] = MR0.ToText();

                        dtMRlist.Rows.Add(Mrtemp);
                    }

#if DEBUG
                    sTsql = "insert into dbo.LogExplorer_AnalysisLog(ADate,TableName,Logdescr,Operation,LSN) "
                            + $" select ADate=getdate(),TableName=N'[{sSchemaName}].[{sTableName}]',Logdescr=N'{REDOSQL.Replace("'", "''")}',Operation='{Operation}',LSN='{CurrentLSN}'; ";
                    oDB.ExecuteSQL(sTsql, false);
#endif

                    if (BeginTime.Length > 0)
                    {
                        tmplog = new DatabaseLog();
                        tmplog.LSN = CurrentLSN;
                        tmplog.Type = "DML";
                        tmplog.TransactionID = TransactionID;
                        tmplog.BeginTime = BeginTime;
                        tmplog.EndTime = EndTime;
                        tmplog.ObjectName = $"[{sSchemaName}].[{sTableName}]";
                        tmplog.Operation = Operation;
                        tmplog.RedoSQL = REDOSQL;
                        tmplog.UndoSQL = UNDOSQL;

                        tmplog.RedoSQLFile = REDOSQL.ToFileByteArray();
                        tmplog.UndoSQLFile = UNDOSQL.ToFileByteArray();
#if DEBUG
                        tmplog.Message = stemp;
#else
                        tmplog.Message = "";
#endif

                        logs.Add(tmplog);
                    }
                }

                return logs;
            }
            catch (Exception ex)
            {
#if DEBUG
                stemp = $"Message:{(ex.Message ?? "")}  StackTrace:{(ex.StackTrace ?? "")} ";
                throw new Exception(stemp);
#else
                return logs;
#endif
            }
        }

        private byte[] GetMR1(string pOperation, string pPageID, string pAllocUnitId, string pCurrentLSN, string pStartLSN, string pEndLSN, string pR1)
        {
            byte[] mr1;
            string fileid, pageid_dec, checkvalue;
            DataTable dtTemp;
            List<string> lsns2;
            bool isfound;

            fileid = pPageID.Substring(0, pPageID.IndexOf(":", 0));
            pageid_dec = Convert.ToInt64(pPageID.Substring(pPageID.IndexOf(":", 0) + 1, pPageID.Length - pPageID.IndexOf(":", 0) - 1), 16).ToString();

            // #temppagedata
            sTsql = "DBCC PAGE(''" + sDatabasename + "''," + fileid + "," + pageid_dec + ",3) with tableresults,no_infomsgs; ";
            sTsql = "insert into #temppagedata(ParentObject,Object,Field,Value) exec('" + sTsql + "'); ";
            oDB.ExecuteSQL(sTsql, false);

            sTsql = "update #temppagedata set LSN=N'" + pCurrentLSN + "' where LSN is null; ";
            oDB.ExecuteSQL(sTsql, false);

            // #slotcolumndata
            //sTsql = "with t as("
            //      + "select columnid=cast(replace(substring(b.[Object],charindex(N'Column',b.[Object])+6,charindex(N'Offset',b.[Object])-charindex(N'Column',b.[Object])-6),N' ','') as int), "
            //      + "       columnname=b.Field, "
            //      + "       offset=replace(substring(b.[Object],charindex(N'Offset',b.[Object])+6,charindex(N'Length',b.[Object])-charindex(N'Offset',b.[Object])-6),N' ',''), "
            //      + "       length=substring(b.[Object],charindex(N'Length (physical)',b.[Object])+17,1000000000), "
            //      + "       value=b.[Value] "
            //      + " from #LogList a "
            //      + " inner join #temppagedata b on b.[ParentObject] like N'Slot '+ltrim(rtrim(A.[Slot ID]))+' Offset%' "
            //      + " where a.[Current LSN]='" + pCurrentLSN + "' "
            //      + " and b.[Object] not like '%Memory Dump%') "
            //      + "insert into #slotcolumndata(tablename,columnid,columnname,offset,length,value) "
            //      + $" select tablename=N'{sSchemaName}.{sTableName}',columnid,columnname,offset,length,value "
            //      + " from t "
            //      + $" where not exists(select 1 from #slotcolumndata where tablename=N'{sSchemaName}.{sTableName}') "
            //      + " order by columnid; ";
            //oDB.ExecuteSQL(sTsql, false);

            lsns2 = new List<string>();
            lsns2.Add(pCurrentLSN);
            //lsns2.Add(lsns
            //          .Where(p => p.Value == pPageID && p.Key.CompareTo(pCurrentLSN) > 0)
            //          .OrderByDescending(p => p.Key)
            //          .FirstOrDefault()
            //          .Key);

            mr1 = null;
            checkvalue = (pOperation == "LOP_MODIFY_ROW" ? pR1 : "");
            isfound = false;

            foreach (string tl in lsns2)
            {
                sTsql = "truncate table #ModifiedRawData; ";
                oDB.ExecuteSQL(sTsql, false);

                sTsql = " insert into #ModifiedRawData([RowLog Contents 0_var]) "
                        + " select [RowLog Contents 0_var]=replace(stuff((select replace(substring(C.[Value],charindex(N':',[Value],1)+1,48),N'†',N'') "
                        + "                                               from #temppagedata C "
                        + "                                               where C.[LSN]=N'" + tl + "' "
                        + "                                               and C.[ParentObject] like 'Slot '+ltrim(rtrim(A.[Slot ID]))+' Offset%' "
                        + "                                               and C.[Object] like N'%Memory Dump%' "
                        + "                                               group by C.[Value] "
                        + "                                               for xml path('')),1,1,N''),N' ',N'') "
                        + " from #LogList A "
                        + " where A.[Current LSN]='" + pCurrentLSN + "'; ";
                oDB.ExecuteSQL(sTsql, false);
                
                sTsql = "select count(1) from #ModifiedRawData where substring([RowLog Contents 0_var],9,len([RowLog Contents 0_var])-8) like N'%" + checkvalue + "%'; ";
                if (Convert.ToInt32(oDB.Query11(sTsql, false)) > 0)
                {
                    isfound = true;
                }

                if (isfound == false && pOperation == "LOP_MODIFY_ROW")
                {
                    sTsql = "truncate table #ModifiedRawData; ";
                    oDB.ExecuteSQL(sTsql, false);

                    sTsql = "with t as("
                          + "select *,SlotID=replace(substring(ParentObject,5,charindex(N'Offset',ParentObject)-5),N' ',N'') "
                          + " from #temppagedata "
                          + " where LSN=N'" + tl + "' "
                          + " and Object like N'%Memory Dump%'), "
                          + "u as("
                          + "select [SlotID]=a.SlotID, "
                          + "       [RowLog Contents 0_var]=replace(stuff((select replace(substring(b.Value,charindex(N':',b.Value,1)+1,48),N'†',N'') "
                          + "                                              from t b "
                          + "                                              where b.SlotID=a.SlotID "
                          + "                                              group by b.Value "
                          + "                                              for xml path('')),1,1,N''),N' ',N'') "
                          + " from t a "
                          + " group by a.SlotID) "
                          + "insert into #ModifiedRawData([SlotID],[RowLog Contents 0_var]) "
                          + "select [SlotID],[RowLog Contents 0_var] "
                          + " from u "
                          + " where substring([RowLog Contents 0_var],9,len([RowLog Contents 0_var])-8) like N'%" + checkvalue + "%'; ";
                    oDB.ExecuteSQL(sTsql, false);

                    sTsql = "select count(1) from #ModifiedRawData where substring([RowLog Contents 0_var],9,len([RowLog Contents 0_var])-8) like N'%" + checkvalue + "%'; ";
                    if (Convert.ToInt32(oDB.Query11(sTsql, false)) > 0)
                    {
                        isfound = true;
                    }
                }

                if (isfound == true)
                {
                    sTsql = @"update #ModifiedRawData set [RowLog Contents 0]=cast('' as xml).value('xs:hexBinary(substring(sql:column(""[RowLog Contents 0_var]""), 0) )', 'varbinary(max)'); ";
                    oDB.ExecuteSQL(sTsql, false);

                    sTsql = "select top 1 'MR1'=[RowLog Contents 0] from #ModifiedRawData; ";
                    dtTemp = oDB.Query(sTsql, false);

                    mr1 = (byte[])dtTemp.Rows[0]["MR1"];
                    break;
                }
            }

            return mr1;
        }

        private FPageInfo GetPageInfo(string pPageID)
        {
            FPageInfo r;
            List<DBCCPAGE_DATA> ds;

            r = new FPageInfo();
            r.FileNum = Convert.ToInt32(pPageID.Split(':')[0], 16).ToString();
            r.PageNum = Convert.ToInt64(pPageID.Split(':')[1], 16).ToString();
            r.FileNumPageNum_Hex = pPageID;

            sTsql = "truncate table #temppagedatalob; ";
            oDB.ExecuteSQL(sTsql, false);

            sTsql = "DBCC PAGE(''" + sDatabasename + "''," + r.FileNum + "," + r.PageNum + ",2) with tableresults,no_infomsgs; ";
            sTsql = "insert into #temppagedatalob(ParentObject,Object,Field,Value) exec('" + sTsql + "'); ";
            oDB.ExecuteSQL(sTsql, false);

            // pagedata
            sTsql = "select rn=row_number() over(order by Value)-1,Value=replace(upper(substring(Value,21,44)),N' ',N'') from #temppagedatalob where ParentObject=N'DATA:'; ";
            ds = oDB.Query<DBCCPAGE_DATA>(sTsql, false);
            r.PageData = string.Join("", ds.Select(p => p.Value));

            // pagetype
            sTsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_type'; ";
            r.PageType = oDB.Query11(sTsql, false);

            return r;
        }

        public void AnalyzeUpdate(byte[] mr1, byte[] r0, byte[] r1, byte[] r3, byte[] r4, byte[] bLogRecord, TableColumn[] columns, int iColumncount, string sPrimarykeyColumnList, string sOperation, string pCurrentLSN, short? pOffsetinRow, short? pModifySize,
                                  ref string sValueList1, ref string sValueList0, ref string sWhereList, ref byte[] mr0)
        {
            int i;
            string mr0_str, mr1_str, r0_str, r1_str, r3_str, r4_str, sLogRecord;
            TableColumn[] columns0, columns1;
            List<TableColumn> lUnChangedColumns;

            mr1_str = mr1.ToText();
            r0_str = r0.ToText();  // .RowLog Contents 0
            r1_str = r1.ToText();  // .RowLog Contents 1
            r3_str = r3.ToText();  // .RowLog Contents 3
            r4_str = r4.ToText();  // .RowLog Contents 4
            sLogRecord = bLogRecord.ToText();  // .Log Record

            columns0 = new TableColumn[iColumncount];
            columns1 = new TableColumn[iColumncount];
            i = 0;
            foreach (TableColumn c in columns)
            {
                columns0[i] = new TableColumn(c.ColumnID, c.ColumnName, c.DataType, c.Length, c.Precision, c.Scale, c.LeafOffset, c.LeafNullBit, c.isNullable, c.isComputed);
                columns1[i] = new TableColumn(c.ColumnID, c.ColumnName, c.DataType, c.Length, c.Precision, c.Scale, c.LeafOffset, c.LeafNullBit, c.isNullable, c.isComputed);
                i = i + 1;
            }

            TranslateData(mr1, columns1, TabInfos.PrimarykeyColumnList, TabInfos.ClusteredindexColumnList);

            // 由 mr1_str 构造 mr0_str
            switch (sOperation)
            {
                case "LOP_MODIFY_ROW":
                    mr0_str = RESTORE_LOP_MODIFY_ROW(mr1_str, r1_str, r0_str, pOffsetinRow, pModifySize);
                    break;
                case "LOP_MODIFY_COLUMNS":
                    mr0_str = RESTORE_LOP_MODIFY_COLUMNS(sLogRecord, r3_str, r0, r1, mr1, mr1_str, columns0);
                    break;
                default:
                    mr0_str = null;
                    break;
            }

            RestoreLobPage();

            mr0 = mr0_str.ToByteArray();
            TranslateData(mr0, columns0, TabInfos.PrimarykeyColumnList, TabInfos.ClusteredindexColumnList);

            // 新/旧值列表
            lUnChangedColumns = new List<TableColumn>();
            sValueList0 = "";
            sValueList1 = "";

            for (i = 0; i <= iColumncount - 1; i++)
            {
                if (columns0[i].isNull == false
                    && columns1[i].isNull == false
                    && columns0[i].Value != null
                    && columns1[i].Value != null)
                {
                    if (columns0[i].Value.ToString() != columns1[i].Value.ToString())
                    {
                        sValueList0 = sValueList0 + (sValueList0.Length > 0 ? ", " : "")
                                      + "[" + columns0[i].ColumnName + "]="
                                      + ColumnValue2SQLValue(columns0[i].DataType, columns0[i].Value, columns0[i].isNull);

                        sValueList1 = sValueList1 + (sValueList1.Length > 0 ? ", " : "")
                                      + "[" + columns1[i].ColumnName + "]="
                                      + ColumnValue2SQLValue(columns1[i].DataType, columns1[i].Value, columns1[i].isNull);
                    }
                    else
                    {
                        lUnChangedColumns.Add(columns0[i]);
                    }
                }

                if ((columns0[i].isNull == true && columns1[i].isNull == false)
                    || (columns0[i].isNull == false && columns1[i].isNull == true))
                {
                    sValueList0 = sValueList0 + (sValueList0.Length > 0 ? ", " : "")
                                  + "[" + columns0[i].ColumnName + "]="
                                  + ColumnValue2SQLValue(columns0[i].DataType, columns0[i].Value, columns0[i].isNull);

                    sValueList1 = sValueList1 + (sValueList1.Length > 0 ? ", " : "")
                                  + "[" + columns1[i].ColumnName + "]="
                                  + ColumnValue2SQLValue(columns1[i].DataType, columns1[i].Value, columns1[i].isNull);
                }
            }

            // where clause
            sWhereList = "";
            for (i = 0; i <= lUnChangedColumns.Count - 1; i++)
            {
                if (lUnChangedColumns[i].DataType == SqlDbType.Timestamp) { continue; }

                if (sPrimarykeyColumnList.Length == 0)   // 无主键时,用所有未变更字段.
                {
                    sWhereList = sWhereList + (sWhereList.Length > 0 ? " and " : "")
                                 + "[" + lUnChangedColumns[i].ColumnName + "]="
                                 + ColumnValue2SQLValue(lUnChangedColumns[i].DataType, lUnChangedColumns[i].Value, lUnChangedColumns[i].isNull);

                }
                else
                {
                    if (sPrimarykeyColumnList.IndexOf("," + lUnChangedColumns[i].ColumnName + ",", 0) > -1)
                    {
                        sWhereList = sWhereList + (sWhereList.Length > 0 ? " and " : "")
                                     + "[" + lUnChangedColumns[i].ColumnName + "]="
                                     + ColumnValue2SQLValue(lUnChangedColumns[i].DataType, lUnChangedColumns[i].Value, lUnChangedColumns[i].isNull);
                    }
                }
            }

        }

        private void RestoreLobPage()
        {
            int i;
            string PageID, Operation, stemp;
            DataRow[] lobpagelogs;
            FPageInfo tpageinfo;
            byte[] R0, R1;
            short? OffsetinRow, ModifySize;

            lobpagelogs = dtLogs.Where(p => p["Transaction ID"].ToString() == TransactionID
                                            && (p["Context"].ToString() == "LCX_TEXT_TREE" || p["Context"].ToString() == "LCX_TEXT_MIX")
                                      ).ToArray();
            for (i = lobpagelogs.Length - 1; i >= 0; i--)  // 从后往前解析
            {
                PageID = lobpagelogs[i]["PAGE ID"].ToString().ToUpper();
                Operation = lobpagelogs[i]["Operation"].ToString();
                R0 = (byte[])lobpagelogs[i]["RowLog Contents 0"];
                R1 = (byte[])lobpagelogs[i]["RowLog Contents 1"];
                OffsetinRow = null; if (lobpagelogs[i]["Offset in Row"] != null) { OffsetinRow = Convert.ToInt16(lobpagelogs[i]["Offset in Row"]); }
                ModifySize = null; if (lobpagelogs[i]["Modify Size"] != null) { ModifySize = Convert.ToInt16(lobpagelogs[i]["Modify Size"]); }
                CurrentLSN = lobpagelogs[i]["Current LSN"].ToString();

                if (lobpagedata.ContainsKey(PageID) == false)
                {
                    tpageinfo = GetPageInfo(PageID);
                    lobpagedata.Add(PageID, tpageinfo);
                }
                else
                {
                    tpageinfo = lobpagedata[PageID];
                }

                stemp = tpageinfo.PageData;

                if (Operation == "LOP_INSERT_ROWS")
                {
                    stemp = stemp.Stuff(96 * 2, 
                                        R0.Length * 2, 
                                        R0.ToText());
                }

                if (Operation == "LOP_MODIFY_ROW")
                {
                    stemp = stemp.Stuff(Convert.ToInt32((96 + OffsetinRow) * 2), 
                                        R1.Length * 2,  // (ModifySize ?? 0)
                                        R0.ToText()); 
                }

                lobpagedata[PageID].PageData = stemp;
            }
        }

        private string RESTORE_LOP_MODIFY_ROW(string mr1_str, string r1_str, string r0_str, short? pOffsetinRow, short? pModifySize)
        {
            string mr0_str, stemp;
            
            try
            {
                //mr0_str = mr1_str;
                //mr0_str = mr0_str.Replace(r1_str, r0_str);

                //mr0_str = mr1_str.Substring(0, 8);
                //mr0_str = mr0_str + mr1_str.Substring(8, mr1_str.IndexOf(r1_str, 8) - 8);
                //mr0_str = mr0_str + r0_str;
                //mr0_str = mr0_str + mr1_str.Substring(mr1_str.IndexOf(r1_str, 8) + r1_str.Length);

                if (mr1_str.Length >= 8)
                {
                    mr0_str = mr1_str.Stuff(Convert.ToInt32(pOffsetinRow) * 2,
                                            r1_str.Length,
                                            r0_str);
                }
                else
                {
                    mr0_str = mr1_str;
                }
            }
            catch(Exception ex)
            {
                mr0_str = mr1_str;
#if DEBUG
                stemp = $"Message:{(ex.Message ?? "")} \r\nStackTrace:{(ex.StackTrace ?? "")} \r\nmr1_str={(mr1_str ?? "")}  r1_str={(r1_str ?? "")}  r0_str={(r0_str ?? "")}";
                throw new Exception(stemp);           
#endif
            }

            return mr0_str;
        }

        private string RESTORE_LOP_MODIFY_COLUMNS(string sLogRecord, string r3_str, byte[] r0, byte[] r1, byte[] mr1, string mr1_str, TableColumn[] columns0)
        {
            string mr0_str, rowlogdata, fvalue0, fvalue1, ts;
            int i, j, k, n, fstart0, fstart1, flength0, flength0f4, flength1, flength1f4;
            List<string> tls;
            byte[] mr0;

            mr0_str = null;
            rowlogdata = sLogRecord.Substring(sLogRecord.IndexOf(r3_str) + r3_str.Length,
                                              sLogRecord.Length - sLogRecord.IndexOf(r3_str) - r3_str.Length);
            if ((sLogRecord.Length - rowlogdata.Length) % 8 != 0)
            {
                rowlogdata = rowlogdata.Substring((sLogRecord.Length - rowlogdata.Length) % 8);
            }

            tls = new List<string>();
            for (i = 0; i <= (int)(Math.Pow(2, (r0.Length / 4)) - 1); i++)
            {
                ts = Convert.ToString(i, 2).PadLeft(r0.Length / 4, '0');
                tls.Add(ts);
            }

            foreach (string cc in tls)
            {
                try
                {
                    mr0_str = mr1_str;
                    for (i = 1, j = 0; i <= (r0.Length / 4); i++)
                    {
                        fstart0 = Convert.ToInt32(r0[i * 4 - 3].ToString("X2") + r0[i * 4 - 4].ToString("X2"), 16);
                        fstart1 = Convert.ToInt32(r0[i * 4 - 1].ToString("X2") + r0[i * 4 - 2].ToString("X2"), 16);

                        flength0 = Convert.ToInt32(r1[i * 2 - 1].ToString("X2") + r1[i * 2 - 2].ToString("X2"), 16);
                        flength0f4 = (flength0 % 4 == 0 ? flength0 : flength0 + (4 - flength0 % 4));

                        fvalue0 = rowlogdata.Substring(j * 2, flength0 * 2);
                        j = j + flength0f4;

                        if ((fstart1 + 1) >= 5
                            && (fstart1 + 1) <= Convert.ToInt32(mr1[3].ToString("X2") + mr1[2].ToString("X2"), 16)
                            && (fstart1 + flength0 + 1) >= 5
                            && (fstart1 + flength0 + 1) <= Convert.ToInt32(mr1[3].ToString("X2") + mr1[2].ToString("X2"), 16))
                        {
                            flength1 = flength0;
                        }
                        else
                        {
                            if ((j * 2) <= (rowlogdata.Length - 2))
                            {
                                for (flength1 = 0, k = j, n = fstart1;
                                     rowlogdata.Substring(k * 2, 2) == mr1_str.Substring(n * 2, 2);)
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

                    //ts = Convert.ToString(mr0.Length, 16).ToUpper().PadLeft(4, '0');
                    //if (sLogRecord.IndexOf(ts) == -1 
                    //    && sLogRecord.IndexOf(ts.Substring(2, 2) + ts.Substring(0, 2)) == -1)
                    //{
                    //    continue;
                    //}

                    TranslateData(mr0, columns0, TabInfos.PrimarykeyColumnList, TabInfos.ClusteredindexColumnList);

                    break;
                }
                catch (Exception ex)
                {
                    continue;
                }
            }

            if (string.IsNullOrEmpty(mr0_str) == true)
            {
                mr0_str = mr1_str;
            }

            return mr0_str;
        }

        private void TranslateData(byte[] data, TableColumn[] columns, string sPrimarykeyColumns, string sClusteredindexColumns)
        {
            if (data == null || data.Length <= 4) { return; }

            // 行数据从第5字节开始
            int index, index2, index3;  // 指针
            string sData;

            index = 4;
            sData = data.ToText();

            // 预处理Bit数据
            byte[] m_bBitColumnData;
            short i, j, sBitColumnCount, sBitColumnDataLength, sBitColumnDataIndex;
            int sBitValueStartIndex;

            sBitColumnCount = 0;
            for (i = 0; i <= columns.Length - 1; i++)
            {
                if (columns[i].DataType == SqlDbType.Bit) { sBitColumnCount = (short)(sBitColumnCount + 1); }
            }

            // 根据Bit字段总数 计算Bit值列表长度(字节数)
            sBitColumnDataLength = (short)Math.Ceiling((double)sBitColumnCount / (double)8.0);
            m_bBitColumnData = new byte[sBitColumnDataLength];
            sBitColumnDataIndex = -1;
            sBitValueStartIndex = 0;

            // 预处理Uniqueidentifier数据
            short iUniqueidentifierColumnCount;

            iUniqueidentifierColumnCount = 0;
            for (i = 0; i <= columns.Length - 1; i++)
            {
                if (columns[i].DataType == SqlDbType.UniqueIdentifier) { iUniqueidentifierColumnCount = (short)(iUniqueidentifierColumnCount + 1); }
            }

            if (iUniqueidentifierColumnCount >= 2
                && TabInfos.IsHeapTable == false) // 堆表不适用本规则
            {
                TableColumn[] columns_temp = new TableColumn[columns.Length];

                j = 0;
                for (i = (short)(columns.Length - 1); i >= 0; i--)
                {
                    if (columns[i].DataType == SqlDbType.UniqueIdentifier)
                    {
                        columns_temp[j] = columns[i];
                        j++;
                    }
                }

                for (i = 0; i <= columns.Length - 1; i++)
                {
                    if (columns[i].DataType != SqlDbType.UniqueIdentifier)
                    {
                        columns_temp[j] = columns[i];
                        j++;
                    }
                }

                columns = columns_temp;
            }

            index2 = Convert.ToInt32(data[3].ToString("X2") + data[2].ToString("X2"), 16);  // 指针暂先跳过所有定长字段的值

            short sAllColumnCount,              // 列总数_实际列总数
                  sAllColumnCountLog,           // 列总数_日志里的列总数
                  sMaxColumnID,                 // 最大ColumnID       
                  sNullStatusLength,            // 列null值状态列表存储所需长度(字节)
                  sVarColumnCount = 0,          // 变长字段数量
                  sVarColumnStartIndex = 0,     // 变长列字段值开始位置
                  sVarColumnEndIndex = 0;       // 变长列字段值结束位置

            string sNullStatus,  // 列null值状态列表
                   sTemp,
                   lobpointer;

            bool isExceed,       // 指针是否已越界
                 hasJumpRowID;   // 是否已跳过RowID,用于无PrimaryKey的表.

            TableColumn[] columns2,  // 补齐ColumnID, 并移除所有计算列的字段列表
                          columns3;  // 实际用于解析的字段列表

            TableColumn tmpTableColumn;

            List<FVarColumnData> vcs = new List<FVarColumnData>(); // 变长字段数据
            FVarColumnData tvc;

            // 取字段总数
            sAllColumnCount = Convert.ToInt16(columns.Length);
            sAllColumnCountLog = BitConverter.ToInt16(data, index2);

            if (sPrimarykeyColumns.Length > 0) { sPrimarykeyColumns = sPrimarykeyColumns.Substring(1, sPrimarykeyColumns.Length - 2); }
            if (sClusteredindexColumns.Length > 0) { sClusteredindexColumns = sClusteredindexColumns.Substring(1, sClusteredindexColumns.Length - 2); }

            if (TabInfos.IsHeapTable == true)
            {
                hasJumpRowID = true; // true false  某些堆表没RowID
            }
            else
            {
                hasJumpRowID = (sPrimarykeyColumns == sClusteredindexColumns ? true : false);
            }

            index2 = index2 + 2;

            if (sAllColumnCount == sAllColumnCountLog)
            {
                columns2 = columns;
            }
            else
            {
                // 补齐ColumnID
                sMaxColumnID = columns.Select(p => p.ColumnID).Max();
                columns2 = new TableColumn[sMaxColumnID];

                for (i = 0; i <= sMaxColumnID - 1; i++)
                {
                    tmpTableColumn = columns.Where(p => p.ColumnID == i + 1).FirstOrDefault();

                    if (tmpTableColumn == null)
                    {
                        columns2[i] = new TableColumn(Convert.ToInt16(i + 1), string.Empty, SqlDbType.Int, 4, 0, 0, 0, 0, true, false);  // 虚拟字段 isExists = false
                    }
                    else
                    {
                        columns2[i] = tmpTableColumn;
                    }
                }
            }

            // 移除所有计算列
            columns2 = columns2.Where(p => p.isComputed == false).ToArray();

            // 预处理聚集索引字段
            if (sClusteredindexColumns.Length > 0)
            {
                List<string> sClusteredindexColumnList;

                sClusteredindexColumnList = sClusteredindexColumns.Split(',').ToList();
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string sColumnname in sClusteredindexColumnList)
                {
                    tmpTableColumn = columns2.Where(p => p.ColumnName == sColumnname && p.isVarLenDataType == false).FirstOrDefault();

                    if (tmpTableColumn != null)
                    {
                        columns3[i] = tmpTableColumn;
                        i++;
                    }
                }

                // 其他字段置后
                foreach (TableColumn oth in columns2)
                {
                    tmpTableColumn = columns3.Where(p => p != null && p.ColumnID == oth.ColumnID).FirstOrDefault();

                    if (tmpTableColumn == null)
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
            if (sClusteredindexColumns.Length == 0 && sPrimarykeyColumns.Length > 0)
            {
                List<string> sPrimarykeyColumnList;

                sPrimarykeyColumnList = sPrimarykeyColumns.Split(',').ToList();
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string sColumnname in sPrimarykeyColumnList)
                {
                    tmpTableColumn = columns2.Where(p => p.ColumnName == sColumnname && p.isVarLenDataType == false).FirstOrDefault();

                    if (tmpTableColumn != null)
                    {
                        columns3[i] = tmpTableColumn;
                        i++;
                    }
                }

                // 其他字段置后
                foreach (TableColumn oth in columns2)
                {
                    tmpTableColumn = columns3.Where(p => p != null && p.ColumnID == oth.ColumnID).FirstOrDefault();

                    if (tmpTableColumn == null)
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
            sNullStatusLength = (short)Math.Ceiling((double)sAllColumnCountLog / (double)8.0);

            sNullStatus = "";
            for (i = 0; i <= sNullStatusLength - 1; i++)
            {
                sTemp = Byte2String(data[index2]);
                sNullStatus = sTemp + sNullStatus;
                index2 = index2 + 1;
            }

            sNullStatus = sNullStatus.Reverse();  // 字符串反转

            if (TabInfos.IsHeapTable == false && sPrimarykeyColumns != sClusteredindexColumns)
            {
                sNullStatus = sNullStatus.Substring(1, sNullStatus.Length - 1);
            }

            while (sNullStatus.Length < columns3.Length)
            {
                sNullStatus = sNullStatus + "0";
            }

            foreach (TableColumn c in columns3)
            {
                if (c.isNullable == false)
                {
                    c.isNull = false;
                }
                else
                {
                    if (c.LeafNullBit - 1 >= 0)
                    {
                        c.isNull = (sNullStatus.Substring(c.LeafNullBit - 1, 1) == "1" ? true : false);
                    }
                    else
                    {
                        c.isNull = true;
                    }
                }
            }

            //取定长字段
            foreach (TableColumn c in columns3)
            {
                if (c.isVarLenDataType == true || c.isExists == false) { continue; }

                index3 = index;
                if (index != c.LeafOffset) // slotcolumndata.FirstOrDefault(p => p.columnid == c.ColumnID).offset
                {
                    index = c.LeafOffset; // slotcolumndata.FirstOrDefault(p => p.columnid == c.ColumnID).offset;
                }
                c.ValueStartIndex = index;

                if (c.isNull == true && c.isNullable == true && c.DataType != System.Data.SqlDbType.Bit)
                {
                    c.Value = "nullvalue";
                    index = index + c.Length;
                }
                else
                {
                    switch (c.DataType)
                    {
                        case System.Data.SqlDbType.Char:
                            c.Value = System.Text.Encoding.Default.GetString(data, index, c.Length).TrimEnd();
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.NChar:
                            c.Value = System.Text.Encoding.Unicode.GetString(data, index, c.Length).TrimEnd();
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.DateTime:
                            c.Value = TranslateData_DateTime(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.DateTime2:
                            c.Value = TranslateData_DateTime2(data, index, c.Length, c.Precision, c.Scale);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.DateTimeOffset:
                            c.Value = TranslateData_DateTimeOffset(data, index, c.Length, c.Precision, c.Scale);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.SmallDateTime:
                            c.Value = TranslateData_SmallDateTime(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Date:
                            c.Value = TranslateData_Date(data, index);
                            index = index + 3;
                            break;

                        case System.Data.SqlDbType.Time:
                            c.Value = TranslateData_Time(data, index, c.Length, c.Precision, c.Scale);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Int:
                            c.Value = BitConverter.ToInt32(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.BigInt:
                            c.Value = BitConverter.ToInt64(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.SmallInt:
                            c.Value = BitConverter.ToInt16(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.TinyInt:
                            c.Value = Convert.ToInt32(data[index]);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Decimal:
                            c.Value = TranslateData_Decimal(data, index, c.Length, c.Scale);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Real:
                            c.Value = TranslateData_Real(data, index, c.Length);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Float:
                            c.Value = TranslateData_Float(data, index, c.Length);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Money:
                            c.Value = TranslateData_Money(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.SmallMoney:
                            c.Value = TranslateData_SmallMoney(data, index);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Bit:
                            int iJumpIndexLength;
                            string bValueBit;

                            sBitValueStartIndex = (sBitColumnDataIndex == -1 ? index : sBitValueStartIndex);
                            iJumpIndexLength = 0;
                            bValueBit = TranslateData_Bit(data, columns, index, c.ColumnName, sBitColumnCount, m_bBitColumnData, sBitColumnDataIndex, ref iJumpIndexLength, ref m_bBitColumnData, ref sBitColumnDataIndex);

                            sBitValueStartIndex = (iJumpIndexLength > 0 ? index : sBitValueStartIndex);
                            index = index + iJumpIndexLength;

                            c.ValueStartIndex = sBitValueStartIndex;
                            c.Value = bValueBit;
                            c.ValueEndIndex = sBitValueStartIndex;

                            break;

                        case System.Data.SqlDbType.Binary:
                            c.Value = TranslateData_Binary(data, index, c.Length);
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.Timestamp:
                            c.Value = "null";
                            index = index + c.Length;
                            break;

                        case System.Data.SqlDbType.UniqueIdentifier:
                            c.Value = TranslateData_UniqueIdentifier(data, index, c.Length);
                            index = index + c.Length;
                            break;

                        default:
                            break;
                    }
                }

                c.ValueEndIndex = (c.DataType != SqlDbType.Bit ? index - 1 : c.ValueEndIndex);
                c.ValueHex = sData.Substring(c.ValueStartIndex * 2, (c.ValueEndIndex - c.ValueStartIndex + 1) * 2);
                index = index3;
            }

            index = index2;

            // 取变长字段数量
            if (index + 2 < data.Length - 1)
            {
                isExceed = false;
                sVarColumnCount = BitConverter.ToInt16(data, index); // 变长字段数量(不一定等于字段类型=变长类型的字段数量)
                index = index + 2;

                if (index < data.Length - 1)
                {
                    // 接下来每2个字节保存一个变长字段的结束位置,第一个变长字段的开始和结束位置可以算出来.
                    sTemp = sData.Substring(index * 2, 2 * 2);
                    sVarColumnStartIndex = (short)(index + sVarColumnCount * 2);
                    sVarColumnEndIndex = BitConverter.ToInt16(data, index);

                    vcs = new List<FVarColumnData>();
                    for (i = 1, index2 = index; i <= sVarColumnCount; i++)
                    {
                        tvc = new FVarColumnData();
                        tvc.FIndex = Convert.ToInt16(i * -1);
                        tvc.FEnd = sTemp;
                        tvc.InRow = sTemp.Substring(2, 2).ToBinaryString().StartsWith("0");

                        tvc.FValueStart = sVarColumnStartIndex;
                        if (tvc.InRow == false)
                        {
                            sVarColumnEndIndex = Convert.ToInt16(sTemp.Substring(2, 2).ToBinaryString().Stuff(0, 1, "0") + sTemp.Substring(0, 2).ToBinaryString(), 2);
                        }
                        tvc.FValueEnd = sVarColumnEndIndex;

                        if (tvc.InRow == true
                            && sVarColumnStartIndex >= 0
                            && sVarColumnEndIndex - sVarColumnStartIndex >= 0)
                        {
                            tvc.IsNullOrEmpty = false;
                            tvc.FValueHex = sData.Substring(sVarColumnStartIndex * 2,
                                                            (sVarColumnEndIndex - sVarColumnStartIndex) * 2);
                        }
                        else
                        {
                            if (tvc.InRow == true)
                            {
                                tvc.IsNullOrEmpty = true;
                                tvc.FValueHex = "";
                            }
                            else
                            {
                                tvc.IsNullOrEmpty = false;
                                lobpointer = sData.Substring(sVarColumnStartIndex * 2,
                                                             (sVarColumnEndIndex - sVarColumnStartIndex) * 2);
                                tvc.FValueHex = GetLobValue(lobpointer);
                            }
                        }
                        vcs.Add(tvc);

                        index2 = index2 + 2;
                        sTemp = sData.Substring(index2 * 2, 2 * 2);
                        sVarColumnStartIndex = sVarColumnEndIndex;
                        sVarColumnEndIndex = BitConverter.ToInt16(data, index2);
                    }
                }
            }
            else
            {
                isExceed = true;
            }

            if (isExceed == true)
            {
                foreach (TableColumn c in columns)
                {
                    if (c.isVarLenDataType == true) { c.isNull = true; }
                }
            }
            else
            {
                // 跳过1个变长字段(可能为表的RowID).
                if (hasJumpRowID == false)
                {
                    hasJumpRowID = true;

                    if (isExceed == false)
                    {
                        sVarColumnStartIndex = sVarColumnEndIndex;
                        index = index + 2;
                        if (index + 2 >= data.Length - 1) { return; }
                        sVarColumnEndIndex = BitConverter.ToInt16(data, index);
                    }
                }

                // 循环变长字段列表读取数据
                foreach (TableColumn c in columns3)
                {
                    if (c.isVarLenDataType == false && c.isExists == true) { continue; }

                    tvc = vcs.FirstOrDefault(p => p.FIndex == c.LeafOffset);

                    if (c.isNull == true
                        || c.isExists == false
                        || (tvc == null && c.isNull == true)
                        || (tvc != null && tvc.IsNullOrEmpty == true))
                    {
                        c.isNull = true;
                        c.Value = "nullvalue";
                        c.ValueHex = "";

                        continue;
                    }

                    if (tvc == null
                        && c.isNull == false
                        && (c.DataType == System.Data.SqlDbType.VarChar || c.DataType == System.Data.SqlDbType.NVarChar))
                    {
                        c.Value = "";
                        c.ValueHex = "";

                        continue;
                    }

                    if (tvc != null
                        && tvc.IsNullOrEmpty == false)
                    {
                        c.ValueHex = tvc.FValueHex;
                        c.ValueStartIndex = tvc.FValueStart;
                        c.ValueEndIndex = tvc.FValueEnd;
                        c.EndIndex = tvc.FEnd;

                        switch (c.DataType)
                        {
                            case System.Data.SqlDbType.VarChar:
                                c.Value = System.Text.Encoding.Default.GetString(tvc.FValueHex.ToByteArray()).TrimEnd();

                                break;

                            case System.Data.SqlDbType.NVarChar:
                                c.Value = System.Text.Encoding.Unicode.GetString(tvc.FValueHex.ToByteArray()).TrimEnd();

                                break;

                            case System.Data.SqlDbType.VarBinary:
                                c.Value = (tvc.InRow == true ?
                                            TranslateData_VarBinary(data, c.ValueStartIndex, (short)(c.ValueEndIndex - c.ValueStartIndex)) :
                                            "0x" + tvc.FValueHex
                                          );
                                break;

                            case System.Data.SqlDbType.Variant:  // 通用型
                                string sVariantValue;
                                sVariantValue = string.Empty;
                                c.Value = sVariantValue;

                                break;

                            case System.Data.SqlDbType.Xml:
                                c.Value = string.Empty; // TODO
                                break;

                            case System.Data.SqlDbType.Text:
                                c.Value = string.Empty; // TODO
                                break;

                            case System.Data.SqlDbType.NText:
                                c.Value = string.Empty; // TODO
                                break;

                            case System.Data.SqlDbType.Image:
                                c.Value = (tvc.InRow == true ? "0x" + tvc.FValueHex : string.Empty);
                                break;

                            default:
                                break;
                        }

                        continue;
                    }
                }
            }

            // 重新赋值回columns后返回.
            foreach (TableColumn x in columns)
            {
                tmpTableColumn = columns3.Where(p => p.ColumnID == x.ColumnID).FirstOrDefault();

                if (tmpTableColumn != null)
                {
                    x.isNull = tmpTableColumn.isNull;
                    x.Value = tmpTableColumn.Value;
                    x.ValueStartIndex = tmpTableColumn.ValueStartIndex;
                    x.ValueEndIndex = tmpTableColumn.ValueEndIndex;
                }
                else
                {
                    x.isNull = true;
                    x.Value = "nullvalue";
                    x.ValueStartIndex = -1;
                    x.ValueEndIndex = -1;
                }
            }
        }

        private string GetLobValue(string pointer)
        {
            string lobvalue, pagedata, tmpstr;
            FPageInfo firstpage, textmixpage;
            List<FPageInfo> tmps;
            int i, pageqty, cutlen;

            try
            {
                pointer = pointer.Stuff(0, 12 * 2, ""); // 跳过12个字节
                i = 0;
                firstpage = new FPageInfo(pointer.Substring(i * 2, 12 * 2));
                i = i + 12;

                if (lobpagedata.ContainsKey(firstpage.FileNumPageNum_Hex) == false)
                {
                    textmixpage = GetPageInfo(firstpage.FileNumPageNum_Hex);
                    lobpagedata.Add(firstpage.FileNumPageNum_Hex, textmixpage);
                }
                else
                {
                    textmixpage = lobpagedata[firstpage.FileNumPageNum_Hex];
                }
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

                lobvalue = "";
                i = 0;
                foreach (FPageInfo tp in tmps)
                {
                    cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));

                    if (lobpagedata.ContainsKey(tp.FileNumPageNum_Hex) == false)
                    {
                        textmixpage = GetPageInfo(tp.FileNumPageNum_Hex);
                        lobpagedata.Add(tp.FileNumPageNum_Hex, textmixpage);
                    }
                    else
                    {
                        textmixpage = lobpagedata[tp.FileNumPageNum_Hex];
                    }

                    pagedata = textmixpage.PageData;
                    pagedata = pagedata.Stuff(0, (96 + 14) * 2, "");
                    pagedata = pagedata.Stuff(pagedata.Length - 42 * 2, 42 * 2, "");
                    pagedata = pagedata.Substring(0, cutlen * 2);

                    lobvalue = lobvalue + pagedata;
                    i = i + 1;
                }

            }
            catch (Exception ex)
            {
                lobvalue = "";
            }

            return lobvalue;
        }

        private ValueTuple<TableInformation, TableColumn[]> GetTableInfo(string pSchemaName, string pTablename)
        {
            string stsql, stemp;
            TableInformation tableinfo;
            TableColumn[] tablecolumns;
            ValueTuple<TableInformation, TableColumn[]> r;

            stsql = "declare @primarykeyColumnList nvarchar(1000),@ClusteredindexColumnList nvarchar(1000), @identityColumn nvarchar(100), @IsHeapTable bit, @FAllocUnitName nvarchar(1000) "
                      + " select @primarykeyColumnList=isnull(@primarykeyColumnList+N',',N'')+c.name "
                      + "    from sys.indexes a "
                      + "    inner join sys.index_columns b on a.object_id=b.object_id and a.index_id=b.index_id "
                      + "    inner join sys.columns c on b.object_id=c.object_id and b.column_id=c.column_id "
                      + "    inner join sys.objects d on a.object_id=d.object_id "
                      + "    inner join sys.schemas s on d.schema_id=s.schema_id "
                      + "    where a.is_primary_key=1 "
                      + $"   and s.name=N'{pSchemaName}' "
                      + "    and d.type='U' "
                      + $"   and d.name=N'{pTablename}' "
                      + "    order by b.key_ordinal; "

                      + " select @ClusteredindexColumnList=isnull(@ClusteredindexColumnList+N',',N'')+c.name "
                      + "    from sys.indexes a "
                      + "    inner join sys.index_columns b on a.object_id=b.object_id and a.index_id=b.index_id "
                      + "    inner join sys.columns c on b.object_id=c.object_id and b.column_id=c.column_id "
                      + "    inner join sys.objects d on a.object_id=d.object_id "
                      + "    inner join sys.schemas s on d.schema_id=s.schema_id "
                      + "    where a.index_id<=1 "
                      + "    and a.type=1 "
                      + $"   and s.name=N'{pSchemaName}' "
                      + "    and d.type='U' "
                      + $"   and d.name=N'{pTablename}' "
                      + "    order by b.key_ordinal; "

                      + " select @identityColumn=a.name "
                      + "    from sys.columns a "
                      + "    inner join sys.objects b on a.object_id=b.object_id "
                      + "    inner join sys.schemas s on b.schema_id=s.schema_id "
                      + "    where a.is_identity=1 "
                      + $"   and s.name=N'{pSchemaName}' "
                      + "    and b.type='U' "
                      + $"   and b.name=N'{pTablename}'; "

                      + " select @IsHeapTable=case when exists(select 1 "
                      + "                                       from sys.tables t "
                      + "                                       inner join sys.schemas s on t.schema_id=s.schema_id "
                      + "                                       inner join sys.indexes i on t.object_id=i.object_id "
                      + $"                                      where s.name=N'{pSchemaName}' and t.name=N'{pTablename}' "
                      + "                                       and i.index_id=0) then 1 else 0 end; "

                      + " select @FAllocUnitName=isnull(d.name,N'') "
                      + "  from sys.tables a "
                      + "  inner join sys.schemas s on a.schema_id=s.schema_id "
                      + "  inner join sys.indexes d on a.object_id=d.object_id "
                      + "  where d.type in(0,1) "
                      + $" and s.name=N'{pSchemaName}' "
                      + $" and a.name=N'{pTablename}'; "

                      + " select ItemName,ItemValue "
                      + "    from (select ItemName='PrimarykeyColumnList', "
                      + "                 ItemValue=isnull(','+@primarykeyColumnList+',','') "
                      + "          union all "
                      + "          select ItemName='ClusteredindexColumnList', "
                      + "                 ItemValue=isnull(','+@ClusteredindexColumnList+',','') "
                      + "          union all "
                      + "          select ItemName='IdentityColumn', "
                      + "                 ItemValue=isnull(@identityColumn,'') "
                      + "          union all "
                      + "          select ItemName='IsHeapTable', "
                      + "                 ItemValue=rtrim(isnull(@IsHeapTable,0)) "
                      + "          union all "
                      + "          select ItemName='FAllocUnitName', "
                      + "                 ItemValue=rtrim(isnull(@FAllocUnitName,N'')) "
                      + "        ) t for xml raw('Item'),root('TableInfomation'); ";
            stemp = oDB.Query11(stsql, false);
            tableinfo = AnalyzeTableInformation(stemp);

            stsql = "select cast(("
                        + "select ColumnID,ColumnName,DataType,Length,Precision,Nullable,Scale,IsComputed,LeafOffset,LeafNullBit "
                        + " from (select 'ColumnID'=b.column_id, "
                        + "              'ColumnName'=b.name, "
                        + "              'DataType'=c.name, "
                        + "              'Length'=b.max_length, "
                        + "              'Precision'=b.precision, "
                        + "              'Nullable'=b.is_nullable, "
                        + "              'Scale'=b.scale, "
                        + "              'IsComputed'=b.is_computed, "
                        + "              'LeafOffset'=isnull(d2.leaf_offset,0), "
                        + "              'LeafNullBit'=isnull(d2.leaf_null_bit,0) "
                        + "       from sys.tables a "
                        + "       inner join sys.schemas s on a.schema_id=s.schema_id "
                        + "       inner join sys.columns b on a.object_id=b.object_id "
                        + "       inner join sys.systypes c on b.system_type_id=c.xtype and b.user_type_id=c.xusertype "
                        + "       outer apply (select d.leaf_offset,d.leaf_null_bit "
                        + "                     from sys.system_internals_partition_columns d "
                        + "                     where d.partition_column_id=b.column_id "
                        + "                     and d.partition_id in (select partitionss.partition_id "
                        + "                                             from sys.allocation_units allocunits "
                        + "                                             inner join sys.partitions partitionss on (allocunits.type in(1, 3) and allocunits.container_id=partitionss.hobt_id) "
                        + "                                                                                       or (allocunits.type=2 and allocunits.container_id=partitionss.partition_id) "
                        + "                                             where partitionss.object_id=a.object_id and partitionss.index_id<=1)) d2 "
                        + $"      where s.name=N'{pSchemaName}' and a.name=N'{pTablename}') t "
                        + " order by ColumnID "
                        + " for xml raw('Column'),root('ColumnList') "
                        + ") as nvarchar(max)); ";
            stemp = oDB.Query11(stsql, false);
            tablecolumns = AnalyzeTablelayout(stemp);

            r = new ValueTuple<TableInformation, TableColumn[]>(tableinfo, tablecolumns);

            return r;
        }

        // 解析表信息.
        private static TableInformation AnalyzeTableInformation(string sTableInformation)
        {
            TableInformation TabInfo;
            XmlDocument xmlDoc;
            XmlNode xmlRootnode;
            XmlNodeList xmlNodelist;
            string sItemvalue;

            TabInfo = new TableInformation();

            xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(sTableInformation);
            xmlRootnode = xmlDoc.SelectSingleNode("TableInfomation");
            xmlNodelist = xmlRootnode.ChildNodes;
            sItemvalue = "";

            foreach (XmlNode xmlNode in xmlNodelist)
            {
                sItemvalue = xmlNode.Attributes["ItemValue"].Value;

                switch (xmlNode.Attributes["ItemName"].Value)
                {
                    case "PrimarykeyColumnList": TabInfo.PrimarykeyColumnList = sItemvalue; break;
                    case "ClusteredindexColumnList": TabInfo.ClusteredindexColumnList = sItemvalue; break;
                    case "IdentityColumn": TabInfo.IdentityColumn = sItemvalue; break;
                    case "IsHeapTable": TabInfo.IsHeapTable = (sItemvalue == "1" ? true : false); break;
                    case "FAllocUnitName": TabInfo.FAllocUnitName = sItemvalue; break;
                    default: break;
                }
            }

            return TabInfo;
        }

        // 解析表结构定义(XML).
        public static TableColumn[] AnalyzeTablelayout(string sTableLayout)
        {
            short iColumnID;
            string sColumnName;
            SqlDbType sDataType;
            short sLength, sPrecision, sScale, sLeafOffset, sLeafNullBit;
            int i, iColumncount;
            bool isNullable, isComputed;
            XmlDocument xmlDoc;
            XmlNode xmlRootnode;
            XmlNodeList xmlNodelist;
            TableColumn[] TableColumns;

            xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(sTableLayout);
            xmlRootnode = xmlDoc.SelectSingleNode("ColumnList");
            xmlNodelist = xmlRootnode.ChildNodes;
            iColumncount = xmlNodelist.Count;

            TableColumns = new TableColumn[iColumncount];
            i = 0;
            sDataType = SqlDbType.Int;

            foreach (XmlNode xmlNode in xmlNodelist)
            {
                // ColumnID
                iColumnID = Convert.ToInt16(xmlNode.Attributes["ColumnID"].Value.ToString());

                // ColumnName
                sColumnName = xmlNode.Attributes["ColumnName"].Value;

                // DataType
                switch (xmlNode.Attributes["DataType"].Value)
                {
                    case "bigint": sDataType = System.Data.SqlDbType.BigInt; break;
                    case "binary": sDataType = System.Data.SqlDbType.Binary; break;
                    case "bit": sDataType = System.Data.SqlDbType.Bit; break;
                    case "char": sDataType = System.Data.SqlDbType.Char; break;
                    case "date": sDataType = System.Data.SqlDbType.Date; break;
                    case "datetime": sDataType = System.Data.SqlDbType.DateTime; break;
                    case "datetime2": sDataType = System.Data.SqlDbType.DateTime2; break;
                    case "datetimeoffset": sDataType = System.Data.SqlDbType.DateTimeOffset; break;
                    case "decimal": sDataType = System.Data.SqlDbType.Decimal; break;
                    case "float": sDataType = System.Data.SqlDbType.Float; break;
                    case "geography": sDataType = System.Data.SqlDbType.VarBinary; break;
                    case "geometry": sDataType = System.Data.SqlDbType.VarBinary; break;
                    case "hierarchyid": sDataType = System.Data.SqlDbType.VarBinary; break;
                    case "image": sDataType = System.Data.SqlDbType.Image; break;
                    case "int": sDataType = System.Data.SqlDbType.Int; break;
                    case "money": sDataType = System.Data.SqlDbType.Money; break;
                    case "nchar": sDataType = System.Data.SqlDbType.NChar; break;
                    case "ntext": sDataType = System.Data.SqlDbType.NText; break;
                    case "numeric": sDataType = System.Data.SqlDbType.Decimal; break;    // numeric=decimal
                    case "nvarchar": sDataType = System.Data.SqlDbType.NVarChar; break;
                    case "real": sDataType = System.Data.SqlDbType.Real; break;
                    case "smalldatetime": sDataType = System.Data.SqlDbType.SmallDateTime; break;
                    case "smallint": sDataType = System.Data.SqlDbType.SmallInt; break;
                    case "smallmoney": sDataType = System.Data.SqlDbType.SmallMoney; break;
                    case "sql_variant": sDataType = System.Data.SqlDbType.Variant; break;
                    case "sysname": sDataType = System.Data.SqlDbType.NVarChar; break;
                    case "text": sDataType = System.Data.SqlDbType.Text; break;
                    case "time": sDataType = System.Data.SqlDbType.Time; break;
                    case "timestamp": sDataType = System.Data.SqlDbType.Timestamp; break;
                    case "tinyint": sDataType = System.Data.SqlDbType.TinyInt; break;
                    case "uniqueidentifier": sDataType = System.Data.SqlDbType.UniqueIdentifier; break;
                    case "varbinary": sDataType = System.Data.SqlDbType.VarBinary; break;
                    case "varchar": sDataType = System.Data.SqlDbType.VarChar; break;
                    case "xml": sDataType = System.Data.SqlDbType.Xml; break;
                    default: break;
                }

                // Length
                sLength = Convert.ToInt16(xmlNode.Attributes["Length"].Value);
                // Precision
                sPrecision = Convert.ToInt16(xmlNode.Attributes["Precision"].Value);
                // Scale
                sScale = Convert.ToInt16(xmlNode.Attributes["Scale"].Value);
                // IsComputed
                isComputed = (xmlNode.Attributes["IsComputed"].Value.ToString() == "0" ? false : true);
                // LeafOffset
                sLeafOffset = Convert.ToInt16(xmlNode.Attributes["LeafOffset"].Value);
                // LeafNullBit
                sLeafNullBit = Convert.ToInt16(xmlNode.Attributes["LeafNullBit"].Value);
                // Nullable
                isNullable = (Convert.ToInt16(xmlNode.Attributes["Nullable"].Value) == 1 ? true : false);

                TableColumns[i] = new TableColumn(iColumnID, sColumnName, sDataType, sLength, sPrecision, sScale, sLeafOffset, sLeafNullBit, isNullable, isComputed);

                i = i + 1;
            }

            return TableColumns;
        }

        // 获取字段数据值的SQL形式
        private static string ColumnValue2SQLValue(System.Data.SqlDbType datatype, object oValue, bool isNull)
        {
            string sValue;
            bool bNeedSeparatorchar, bIsUnicodeType;
            string[] NoSeparatorchar;

            if (isNull == true || oValue == null)
            {
                sValue = "null";
            }
            else
            {
                NoSeparatorchar = new string[] { "tinyint", "bigint", "smallint", "int", "money", "smallmoney", "bit", "decimal", "numeric", "float", "real", "varbinary" };
                bNeedSeparatorchar = (NoSeparatorchar.Any(p => p == datatype.ToString().ToLower()) ? false : true);
                bIsUnicodeType = (datatype == SqlDbType.NVarChar || datatype == SqlDbType.NChar ? true : false);
                sValue = (bIsUnicodeType ? "N" : "") + (bNeedSeparatorchar ? "'" : "") + oValue.ToString().Replace("'", "''") + (bNeedSeparatorchar ? "'" : "");
            }

            return sValue;
        }

        // 字节转二进制数格式(8位)
        private static string Byte2String(byte pByte)
        {
            string r;

            r = (Convert.ToString(pByte, 2).Length <= 4 ? "00000000" : "0000") + Convert.ToString(pByte, 2);
            r = r.Substring(r.Length - 8, 8);

            return r;
        }

        #region 翻译字段值
        // 翻译Bit类型
        private static string TranslateData_Bit(byte[] data,
                                                TableColumn[] columns,
                                                int iCurrentIndex,
                                                string sColumnName,
                                                short sBitColumnCount,
                                                byte[] m_bBitColumnData0,
                                                short sBitColumnDataIndex0,
                                                ref int iJumpIndexLength,
                                                ref byte[] m_bBitColumnData1,
                                                ref short sBitColumnDataIndex1)
        {
            string rBit;
            m_bBitColumnData1 = m_bBitColumnData0;
            sBitColumnDataIndex1 = sBitColumnDataIndex0;

            short i;
            short sCurrentColumnIDinBit;  // 当前字段为第几个Bit类型字段

            sCurrentColumnIDinBit = 0;
            for (i = 0; i <= columns.Length - 1; i++)
            {
                if (columns[i].DataType == SqlDbType.Bit)
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

            string sBitColumnData2;
            sBitColumnData2 = string.Empty;
            for (i = sBitColumnDataIndex1; i >= 0; i--)
            {
                sBitColumnData2 = sBitColumnData2 + Byte2String(m_bBitColumnData1[i]);
            }

            sBitColumnData2 = sBitColumnData2.Reverse();   // 字符串反转

            rBit = sBitColumnData2.Substring(sCurrentColumnIDinBit - 1, 1);
            return rBit;
        }

        // 翻译Date类型
        private static string TranslateData_Date(byte[] data, int iCurrentIndex)
        {
            string returnDate;

            System.DateTime date1 = new DateTime(1900, 1, 1, 0, 0, 0);

            byte[] bDate = new byte[3];
            Array.Copy(data, iCurrentIndex, bDate, 0, 3);

            string hDate;
            hDate = "";
            foreach (byte b in bDate)
            {
                hDate = b.ToString("X2") + hDate;
            }

            int days_date = Convert.ToInt32(hDate, 16);
            days_date = days_date - 693595;

            date1 = date1.AddDays(days_date);
            returnDate = date1.ToString("yyyy-MM-dd");

            return returnDate;
        }

        // 翻译DateTime类型
        private static string TranslateData_DateTime(byte[] data, int iCurrentIndex)
        {
            string sReturnDatetime;
            System.DateTime date0 = new DateTime(1900, 1, 1, 0, 0, 0);

            // 前四个字节  以1/300秒保存
            int second = BitConverter.ToInt32(data, iCurrentIndex);
            date0 = date0.AddMilliseconds(second * 3.3333333333);
            iCurrentIndex = iCurrentIndex + 4;

            // 后四个字节  为1900-1-1后的天数
            int days = BitConverter.ToInt32(data, iCurrentIndex);
            date0 = date0.AddDays(days);

            sReturnDatetime = date0.ToString("yyyy-MM-dd HH:mm:ss.fff");

            return sReturnDatetime;
        }

        // 翻译Time类型
        private static string TranslateData_Time(byte[] data, int iCurrentIndex, short sLength, short sPrecision, short sScale)
        {
            byte[] bTime = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bTime, 0, sLength);

            string sTimeHex;
            sTimeHex = "";
            foreach (byte b in bTime)
            {
                sTimeHex = b.ToString("X2") + sTimeHex;
            }

            string sTimeDec, sTimeSeconds, sTimeSeconds2, sReturnTime;
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

            System.DateTime date2 = new DateTime(1900, 1, 1, 0, 0, 0);
            date2 = date2.AddSeconds(Convert.ToDouble(sTimeSeconds));
            sReturnTime = date2.ToString("HH:mm:ss") + (sTimeSeconds2.Length > 0 ? "." : "") + sTimeSeconds2;

            return sReturnTime;
        }

        // 翻译DateTime2类型
        private static string TranslateData_DateTime2(byte[] data, int iCurrentIndex, short sLength, short sPrecision, short sScale)
        {
            string sReturnDatetime2, sDate, sTime;

            byte[] bDatetime2 = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDatetime2, 0, sLength);

            sTime = TranslateData_Time(bDatetime2, 0, (short)(sLength - 3), sPrecision, sScale);
            sDate = TranslateData_Date(bDatetime2, sLength - 3);
            sReturnDatetime2 = sDate + " " + sTime;

            return sReturnDatetime2;
        }

        // 翻译DateTimeOffset类型
        private static string TranslateData_DateTimeOffset(byte[] data, int iCurrentIndex, short sLength, short sPrecision, short sScale)
        {
            string sReturnDateTimeOffset, sDate, sTime, sOffset;
            short sSignOffset, iOffset;

            byte[] bDateTimeOffset = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDateTimeOffset, 0, sLength);

            // offset
            sSignOffset = 1;
            iOffset = Convert.ToInt16(bDateTimeOffset[sLength - 1].ToString("X2").Substring(1, 1) + bDateTimeOffset[sLength - 2].ToString("X2"), 16);
            if (Byte2String(bDateTimeOffset[sLength - 1]).Substring(0, 1) == "1")
            {
                sSignOffset = -1;
                iOffset = (short)(Convert.ToInt16("FFF", 16) + 1 - iOffset);
            }

            DateTime d0 = new DateTime(1900, 1, 1, 0, 0, 0);
            d0 = d0.AddMinutes(iOffset);

            sOffset = (sSignOffset == 1 ? "+" : "-") + d0.ToString("HH:mm");

            // date
            sDate = TranslateData_Date(bDateTimeOffset, sLength - 5);

            // time
            sTime = TranslateData_Time(bDateTimeOffset, 0, (short)(sLength - 5), sPrecision, sScale);

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

        // 翻译SmallDateTime类型
        private static string TranslateData_SmallDateTime(byte[] data, int iCurrentIndex)
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

        // 翻译Money类型
        private static string TranslateData_Money(byte[] data, int iCurrentIndex)
        {
            string sReturnMoney, sSign;
            byte[] bMoney;

            bMoney = new byte[8];
            Array.Copy(data, iCurrentIndex, bMoney, 0, 8);

            if (Byte2String(bMoney[7]).Substring(7, 1) == "0")
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
                bigintMoney = BigInteger.Parse("FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber) + 1
                           - BigInteger.Parse(sMoneyHex, System.Globalization.NumberStyles.HexNumber);

                sMoney = bigintMoney.ToString();
            }

            sTemp = new string('0', (sMoney.Length < 5 ? 5 - sMoney.Length : 0));
            sMoney = sTemp + sMoney;
            sMoney = sMoney.Insert(sMoney.Length - 4, ".");

            sReturnMoney = sSign + sMoney;

            return sReturnMoney;
        }

        // 翻译SmallMoney类型
        private static string TranslateData_SmallMoney(byte[] data, int iCurrentIndex)
        {
            string sReturnSmallMoney;

            byte[] bSmallMoney = new byte[4];
            Array.Copy(data, iCurrentIndex, bSmallMoney, 0, 4);

            string sSign;
            if (Byte2String(bSmallMoney[3]).Substring(7, 1) == "0")
            { sSign = ""; }
            else
            { sSign = "-"; }

            string sSmallMoneyHex, sSmallMoney, sTemp;
            short iSmallMoney;

            sSmallMoneyHex = "";
            for (iSmallMoney = 3; iSmallMoney >= 0; iSmallMoney--)
            {
                sSmallMoneyHex = sSmallMoneyHex + bSmallMoney[iSmallMoney].ToString("X2");
            }

            sSmallMoney = BigInteger.Parse(sSmallMoneyHex, System.Globalization.NumberStyles.HexNumber).ToString();

            if (sSign == "")
            { // 正数

            }
            else
            { // 负数
                BigInteger bigintSmallMoney;
                bigintSmallMoney = BigInteger.Parse("FFFFFFFF", System.Globalization.NumberStyles.HexNumber) + 1
                                 - BigInteger.Parse(sSmallMoneyHex, System.Globalization.NumberStyles.HexNumber);

                sSmallMoney = bigintSmallMoney.ToString();
            }

            sTemp = new string('0', (sSmallMoney.Length < 5 ? 5 - sSmallMoney.Length : 0));
            sSmallMoney = sTemp + sSmallMoney;
            sSmallMoney = sSmallMoney.Insert(sSmallMoney.Length - 4, ".");

            sReturnSmallMoney = sSign + sSmallMoney;

            return sReturnSmallMoney;
        }

        // 翻译Decimal类型
        private static string TranslateData_Decimal(byte[] data, int iCurrentIndex, short sLenth, short sScale)
        {
            byte[] bDecimal;
            string sDecimalHex, sDecimal, sTemp;
            short sSignDecimal;
            int iDecimal;

            bDecimal = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bDecimal, 0, sLenth);

            sSignDecimal = 1;
            if (bDecimal[0].ToString("X2") == "00")
            {
                sSignDecimal = -1;
            }

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

        // 翻译Real类型
        private static string TranslateData_Real(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnReal;

            byte[] bReal = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bReal, 0, sLenth);

            short sSignReal;
            sSignReal = 1;
            if (Byte2String(bReal[3]).Substring(0, 1) == "1")
            {
                sSignReal = -1;
            }

            // 指数
            string sExpReal;
            int iExpReal;
            sExpReal = Byte2String(bReal[3]).Substring(1, 7)
                     + Byte2String(bReal[2]).Substring(0, 1);
            iExpReal = Convert.ToInt32(sExpReal, 2);

            // 尾数
            string sFractionReal;
            int iReal;
            double dFractionReal;
            sFractionReal = Byte2String(bReal[2]).Substring(1, 7)
                          + Byte2String(bReal[1])
                          + Byte2String(bReal[0]);

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

        // 翻译Float类型
        private static string TranslateData_Float(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sFloatValue, sExpFloat, sFractionFloat;
            byte[] bFloat;
            short sSignFloat;
            int iExpFloat, iFloat;
            double dFractionFloat;

            bFloat = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bFloat, 0, sLenth);

            sSignFloat = 1;
            if (Byte2String(bFloat[7]).Substring(0, 1) == "1")
            {
                sSignFloat = -1;
            }

            // 指数
            sExpFloat = Byte2String(bFloat[sLenth - 1]).Substring(1, 7)
                        + Byte2String(bFloat[sLenth - 2]).Substring(0, 4);

            iExpFloat = Convert.ToInt32(sExpFloat, 2);

            // 尾数
            sFractionFloat = Byte2String(bFloat[6]).Substring(4, 4)
                           + Byte2String(bFloat[5])
                           + Byte2String(bFloat[4])
                           + Byte2String(bFloat[3])
                           + Byte2String(bFloat[2])
                           + Byte2String(bFloat[1])
                           + Byte2String(bFloat[0]);

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

        // 翻译Binary类型
        private static string TranslateData_Binary(byte[] data, int iCurrentIndex, short sLenth)
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

        // 翻译VarBinary类型
        private static string TranslateData_VarBinary(byte[] data, int iCurrentIndex, short sActualLenth)
        {
            string sReturnVarBinary;
            byte[] bVarBinary;
            short iVarBinary;

            sReturnVarBinary = "0x";
            bVarBinary = new byte[sActualLenth];
            Array.Copy(data, iCurrentIndex, bVarBinary, 0, sActualLenth);

            for (iVarBinary = 0; iVarBinary <= sActualLenth - 1; iVarBinary++)
            {
                sReturnVarBinary = sReturnVarBinary + bVarBinary[iVarBinary].ToString("X2");
            }

            return sReturnVarBinary;
        }

        // 翻译UniqueIdentifier类型
        private static string TranslateData_UniqueIdentifier(byte[] data, int iCurrentIndex, short sLenth)
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
        #endregion

    }

    //public class FSlotColumnData
    //{
    //    public string tablename { get; set; }
    //    public int columnid { get; set; }
    //    public string columnname { get; set; }
    //    public int offset { get; set; }
    //    public int length { get; set; }
    //    public string value { get; set; }
    //}

    public class FVarColumnData
    {
        public short FIndex { get; set; }
        public string FEnd { get; set; }
        public int FValueStart { get; set; }
        public int FValueEnd { get; set; }
        public string FValueHex { get; set; }
        public bool InRow { get; set; }
        public bool IsNullOrEmpty { get; set; }
    }

    public class DBCCPAGE_DATA
    {
        public long rn { get; set; }
        public string Value { get; set; }
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
            PageNum = Convert.ToInt64(pagenum_str.Substring(6, 2) + pagenum_str.Substring(4, 2) + pagenum_str.Substring(2, 2) + pagenum_str.Substring(0, 2), 16).ToString();
            FileNum = Convert.ToInt32(filenum_str.Substring(2, 2) + filenum_str.Substring(0, 2), 16).ToString();
            SlotNum = Convert.ToInt32(slotnum_str.Substring(2, 2) + slotnum_str.Substring(0, 2), 16).ToString();
            FileNumPageNum_Hex = $"{filenum_str.Substring(2, 2)}{filenum_str.Substring(0, 2)}:{pagenum_str.Substring(6, 2)}{pagenum_str.Substring(4, 2)}{pagenum_str.Substring(2, 2)}{pagenum_str.Substring(0, 2)}";
        }

        public long Offset { get; set; }
        public string PageNum { get; set; }
        public string FileNum { get; set; }
        public string SlotNum { get; set; }
        public string FileNumPageNum_Hex { get; set; }
        public string PageData { get; set; }
        public string PageType { get; set; }
    }

}
