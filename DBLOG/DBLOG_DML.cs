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
        private DatabaseOperation DB; // 数据库操作
        private string sTsql,          // 动态SQL
                       sDatabaseName,  // 数据库名
                       sTableName,     // 表名
                       sSchemaName;    // 架构名
        private TableColumn[] TableColumns;  // 表结构定义
        private TableInformation TableInfos;   // 表信息
        private Dictionary<string, FPageInfo> lobpagedata; // key:fileid+pageid value:FPageInfo
        public List<FLOG> dtLogs;     // 原始日志信息

        public DBLOG_DML(string pDatabasename, string pSchemaName, string pTableName, DatabaseOperation poDB)
        {
            DB = poDB;
            sDatabaseName = pDatabasename;
            sTableName = pTableName;
            sSchemaName = pSchemaName;

            (TableInfos, TableColumns) = GetTableInfo(sSchemaName, sTableName);
        }

        // 解析日志
        public List<DatabaseLog> AnalyzeLog(string pStartLSN, string pEndLSN)
        {
            List<DatabaseLog> logs;
            DatabaseLog tmplog;
            int j, iMinimumlength;
            string BeginTime = string.Empty, // 事务开始时间
                   EndTime = string.Empty,   // 事务结束时间
                   REDOSQL = string.Empty,   // redo sql
                   UNDOSQL = string.Empty,   // undo sql
                   stemp, sColumnlist, sValueList1, sValueList0, sValue, sWhereList1, sWhereList0, sPrimaryKeyValue;
            byte[] MR0 = null,
                   MR1 = null;
            DataRow Mrtemp;
            DataTable dtMRlist;  // 行数据前版本
            bool isfound;
            DataRow[] drTemp;

            logs = new List<DatabaseLog>();
            sColumnlist = string.Join(",", TableColumns.Where(p => p.DataType != SqlDbType.Timestamp && p.isComputed == false).Select(p => $"[{p.ColumnName}]"));

            dtMRlist = new DataTable();
            dtMRlist.Columns.Add("PAGEID", typeof(string));
            dtMRlist.Columns.Add("SlotID", typeof(string));
            dtMRlist.Columns.Add("AllocUnitId", typeof(string));
            dtMRlist.Columns.Add("MR1", typeof(byte[]));
            dtMRlist.Columns.Add("MR1TEXT", typeof(string));

            sTsql = @"if object_id('tempdb..#temppagedata') is not null drop table #temppagedata; 
                        create table #temppagedata(LSN nvarchar(1000),ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
            DB.ExecuteSQL(sTsql, false);

            sTsql = "create index ix_#temppagedata on #temppagedata(LSN); ";
            DB.ExecuteSQL(sTsql, false);

            sTsql = @"if object_id('tempdb..#temppagedatalob') is not null drop table #temppagedatalob; 
                        create table #temppagedatalob(ParentObject sysname,Object sysname,Field sysname,Value nvarchar(max)); ";
            DB.ExecuteSQL(sTsql, false);

            sTsql = @"if object_id('tempdb..#ModifiedRawData') is not null drop table #ModifiedRawData; 
                        create table #ModifiedRawData([SlotID] int,[RowLog Contents 0_var] nvarchar(max),[RowLog Contents 0] varbinary(max)); ";
            DB.ExecuteSQL(sTsql, false);

            lobpagedata = new Dictionary<string, FPageInfo>();

            foreach (FLOG log in dtLogs.Where(p => p.AllocUnitName == $"{sSchemaName}.{sTableName}" + (TableInfos.AllocUnitName.Length == 0 ? "" : "." + TableInfos.AllocUnitName)
                                                   && (p.Context != "LCX_TEXT_TREE" && p.Context != "LCX_TEXT_MIX"))
                                       .OrderByDescending(p => p.Current_LSN))  // 从后往前解析
            {
                try
                {
                    sTsql = $"select top 1 BeginTime=substring(BeginTime,1,19),EndTime=substring(EndTime,1,19) from #TransactionList where TransactionID='{log.Transaction_ID}'; ";
                    (BeginTime, EndTime) = DB.Query<(string BeginTime, string EndTime)>(sTsql, false).FirstOrDefault();

#if DEBUG
                    sTsql = "insert into dbo.LogExplorer_AnalysisLog(ADate,TableName,Logdescr,Operation,LSN) "
                            + " select getdate(),'" + $"[{sSchemaName}].[{sTableName}]" + "', N'RunAnalysisLog...', '" + log.Operation + "','" + log.Current_LSN + "' ";
                    DB.ExecuteSQL(sTsql, false);
#endif

                    if (log.Operation == "LOP_MODIFY_ROW" || log.Operation == "LOP_MODIFY_COLUMNS")
                    {
                        isfound = false;
                        sPrimaryKeyValue = "";

                        drTemp = dtMRlist.Select("PAGEID='" + log.Page_ID + "' and SlotID='" + log.Slot_ID + "' and AllocUnitId='" + log.AllocUnitId + "' ");
                        if (drTemp.Length > 0
                            && (
                                (log.Operation == "LOP_MODIFY_COLUMNS")
                                ||
                                (log.Operation == "LOP_MODIFY_ROW" && drTemp[0]["MR1TEXT"].ToString().Contains(log.RowLog_Contents_1.ToText()))
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
                                        sPrimaryKeyValue = stemp.Substring(2, stemp.Length - 4 * 2);
                                        break;
                                    case "36":
                                        sPrimaryKeyValue = stemp.Substring(16);
                                        break;
                                    default:
                                        sPrimaryKeyValue = "";
                                        break;
                                }
                            }
                            else
                            {
                                sPrimaryKeyValue = "";
                            }

                            drTemp = dtMRlist.Select("PAGEID='" + log.Page_ID + "' and MR1TEXT like '%" + log.RowLog_Contents_1.ToText() + "%' and MR1TEXT like '%" + sPrimaryKeyValue + "%' ");
                            if (drTemp.Length > 0)
                            {
                                isfound = true;
                            }
                        }

                        if (isfound == false)
                        {
                            MR1 = GetMR1(log.Operation, log.Page_ID, log.AllocUnitId.ToString(), log.Current_LSN, pStartLSN, pEndLSN, log.RowLog_Contents_0.ToText(), log.RowLog_Contents_1.ToText(), sPrimaryKeyValue);

                            if (MR1 != null)
                            {
                                if (drTemp.Length > 0)
                                {
                                    dtMRlist.Rows.Remove(drTemp[0]);
                                }

                                Mrtemp = dtMRlist.NewRow();
                                Mrtemp["PAGEID"] = log.Page_ID;
                                Mrtemp["SlotID"] = log.Slot_ID;
                                Mrtemp["AllocUnitId"] = log.AllocUnitId;
                                Mrtemp["MR1"] = MR1;
                                Mrtemp["MR1TEXT"] = MR1.ToText();

                                dtMRlist.Rows.Add(Mrtemp);
                            }
                        }
                        else
                        {
                            MR1 = (byte[])drTemp[0]["MR1"];
                        }
                    }

                    stemp = string.Empty;
                    REDOSQL = string.Empty;
                    UNDOSQL = string.Empty;
                    sValueList1 = string.Empty;
                    sValueList0 = string.Empty;
                    sWhereList1 = string.Empty;
                    sWhereList0 = string.Empty;
                    MR0 = new byte[1];

                    switch (log.Operation)
                    {
                        // Insert / Delete
                        case "LOP_INSERT_ROWS":
                        case "LOP_DELETE_ROWS":
                            iMinimumlength = 2 + TableColumns.Where(p => p.isVarLenDataType == false).Sum(p => p.Length) + 2;
                            if (log.RowLog_Contents_0.Length >= iMinimumlength)
                            {
                                TranslateData(log.RowLog_Contents_0, TableColumns);
                                MR0 = new byte[log.RowLog_Contents_0.Length];
                                MR0 = log.RowLog_Contents_0;
                            }
                            else
                            {
                                MR0 = GetMR1(log.Operation, log.Page_ID, log.AllocUnitId.ToString(), log.Current_LSN, pStartLSN, pEndLSN, log.RowLog_Contents_0.ToText(), log.RowLog_Contents_1.ToText(), "");
                                if (MR0.Length < iMinimumlength) { continue; }
                                TranslateData(MR0, TableColumns);
                            }
                            for (j = 0; j <= TableColumns.Length - 1; j++)
                            {
                                if (TableColumns[j].DataType == SqlDbType.Timestamp || TableColumns[j].isComputed == true) { continue; }

                                sValue = ColumnValue2SQLValue(TableColumns[j]);
                                sValueList1 = sValueList1 + (sValueList1.Length > 0 ? "," : "") + sValue;

                                if (TableInfos.PrimaryKeyColumns.Count == 0
                                    || TableInfos.PrimaryKeyColumns.Contains(TableColumns[j].ColumnName))
                                {
                                    sWhereList0 = sWhereList0
                                                  + (sWhereList0.Length > 0 ? " and " : "")
                                                  + ColumnName2SQLName(TableColumns[j])
                                                  + (TableColumns[j].isNull ? " is " : "=")
                                                  + sValue;
                                }
                            }
                            // 产生redo sql和undo sql -- Insert
                            if (log.Operation == "LOP_INSERT_ROWS")
                            {
                                REDOSQL = $"insert into [{sSchemaName}].[{sTableName}]({sColumnlist}) values({sValueList1}); ";
                                UNDOSQL = $"delete top(1) from [{sSchemaName}].[{sTableName}] where {sWhereList0}; ";

                                if (TableInfos.IdentityColumn.Length > 0)
                                {
                                    REDOSQL = $"set identity_insert [{sSchemaName}].[{sTableName}] on; " + "\r\n"
                                              + REDOSQL + "\r\n"
                                              + $"set identity_insert [{sSchemaName}].[{sTableName}] off; " + "\r\n";
                                }
                            }
                            // 产生redo sql和undo sql -- Delete
                            if (log.Operation == "LOP_DELETE_ROWS")
                            {
                                REDOSQL = $"delete top(1) from [{sSchemaName}].[{sTableName}] where {sWhereList0}; ";
                                UNDOSQL = $"insert into [{sSchemaName}].[{sTableName}]({sColumnlist}) values({sValueList1}); ";

                                if (TableInfos.IdentityColumn.Length > 0)
                                {
                                    UNDOSQL = $"set identity_insert [{sSchemaName}].[{sTableName}] on; " + "\r\n"
                                              + UNDOSQL + "\r\n"
                                              + $"set identity_insert [{sSchemaName}].[{sTableName}] off; " + "\r\n";
                                }
                            }
                            break;
                        // Update
                        case "LOP_MODIFY_ROW":
                        case "LOP_MODIFY_COLUMNS":
                            if (MR1 != null)
                            {
                                AnalyzeUpdate(log.Transaction_ID, MR1, log.RowLog_Contents_0, log.RowLog_Contents_1, log.RowLog_Contents_3, log.RowLog_Contents_4, log.Log_Record, TableColumns, log.Operation, log.Current_LSN, log.Offset_in_Row, log.Modify_Size, ref sValueList1, ref sValueList0, ref sWhereList1, ref sWhereList0, ref MR0);
                                if (sValueList1.Length > 0)
                                {
                                    REDOSQL = $"update top(1) [{sSchemaName}].[{sTableName}] set {sValueList1} where {sWhereList1}; ";
                                    UNDOSQL = $"update top(1) [{sSchemaName}].[{sTableName}] set {sValueList0} where {sWhereList0}; ";
                                }
                                stemp = "debug info: "
                                            + " sValueList1=" + sValueList1
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
                        drTemp = dtMRlist.Select("PAGEID='" + log.Page_ID + "' and SlotID='" + log.Slot_ID + "' and AllocUnitId='" + log.AllocUnitId + "' ");
                        if (drTemp.Length > 0) { dtMRlist.Rows.Remove(drTemp[0]); }

                        Mrtemp = dtMRlist.NewRow();
                        Mrtemp["PAGEID"] = log.Page_ID;
                        Mrtemp["SlotID"] = log.Slot_ID;
                        Mrtemp["AllocUnitId"] = log.AllocUnitId;
                        Mrtemp["MR1"] = MR0;
                        Mrtemp["MR1TEXT"] = MR0.ToText();

                        dtMRlist.Rows.Add(Mrtemp);
                    }

#if DEBUG
                    sTsql = "insert into dbo.LogExplorer_AnalysisLog(ADate,TableName,Logdescr,Operation,LSN) "
                            + $" select ADate=getdate(),TableName=N'[{sSchemaName}].[{sTableName}]',Logdescr=N'{REDOSQL.Replace("'", "''")}',Operation='{log.Operation}',LSN='{log.Current_LSN}'; ";
                    DB.ExecuteSQL(sTsql, false);
#endif

                    if (string.IsNullOrEmpty(BeginTime) == false)
                    {
                        tmplog = new DatabaseLog();
                        tmplog.LSN = log.Current_LSN;
                        tmplog.Type = "DML";
                        tmplog.TransactionID = log.Transaction_ID;
                        tmplog.BeginTime = BeginTime;
                        tmplog.EndTime = EndTime;
                        tmplog.ObjectName = $"[{sSchemaName}].[{sTableName}]";
                        tmplog.Operation = log.Operation;
                        tmplog.RedoSQL = REDOSQL;
                        tmplog.RedoSQLFile = REDOSQL.ToFileByteArray();
                        tmplog.UndoSQL = UNDOSQL;
                        tmplog.UndoSQLFile = UNDOSQL.ToFileByteArray();
                        tmplog.Message = stemp;
                        logs.Add(tmplog);
                    }
                }
                catch(Exception ex)
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
                    tmplog.ObjectName = $"[{sSchemaName}].[{sTableName}]";
                    tmplog.Operation = log.Operation;
                    tmplog.RedoSQL = "";
                    tmplog.UndoSQL = "";
                    tmplog.RedoSQLFile = "".ToFileByteArray();
                    tmplog.UndoSQLFile = "".ToFileByteArray();
                    tmplog.Message = "";
                    logs.Add(tmplog);
#endif
                }
            }

            return logs;
        }

        private byte[] GetMR1(string pOperation, string pPageID, string pAllocUnitId, string pCurrentLSN, string pStartLSN, string pEndLSN, string pR0, string pR1, string pPrimaryKeyValue)
        {
            byte[] mr1;
            string fileid_dec, pageid_dec, checkvalue1, checkvalue2;
            DataTable dtTemp;
            bool isfound;

            fileid_dec = Convert.ToInt16(pPageID.Split(':')[0], 16).ToString();
            pageid_dec = Convert.ToInt32(pPageID.Split(':')[1], 16).ToString();

            // #temppagedata
            sTsql = $"DBCC PAGE(''{sDatabaseName}'',{fileid_dec},{pageid_dec},3) with tableresults,no_infomsgs; ";
            sTsql = "set transaction isolation level read uncommitted; "
                    + $"insert into #temppagedata(ParentObject,Object,Field,Value) exec('{sTsql}'); ";
            DB.ExecuteSQL(sTsql, false);

            sTsql = $"update #temppagedata set LSN=N'{pCurrentLSN}' where LSN is null; ";
            DB.ExecuteSQL(sTsql, false);

            mr1 = null;
            switch (pOperation)
            {
                case "LOP_MODIFY_ROW":
                    checkvalue1 = pR1;
                    checkvalue2 = pPrimaryKeyValue;
                    break;
                case "LOP_MODIFY_COLUMNS":
                    checkvalue1 = "";
                    checkvalue2 = "";
                    break;
                case "LOP_INSERT_ROWS":
                    checkvalue1 = pR0.Substring(8, 4 * 2);
                    checkvalue2 = "";
                    break;
                default:
                    checkvalue1 = "";
                    checkvalue2 = "";
                    break;
            }
            
            isfound = false;

            sTsql = "truncate table #ModifiedRawData; ";
            DB.ExecuteSQL(sTsql, false);

            sTsql = " insert into #ModifiedRawData([RowLog Contents 0_var]) "
                    + " select [RowLog Contents 0_var]=upper(replace(stuff((select replace(substring(C.[Value],charindex(N':',[Value],1)+1,48),N'†',N'') "
                    + "                                                     from #temppagedata C "
                    + "                                                     where C.[LSN]=N'" + pCurrentLSN + "' "
                    + "                                                     and C.[ParentObject] like 'Slot '+ltrim(rtrim(A.[Slot ID]))+' Offset%' "
                    + "                                                     and C.[Object] like N'%Memory Dump%' "
                    + "                                                     group by C.[Value] "
                    + "                                                     for xml path('')),1,1,N''),N' ',N'')) "
                    + " from #LogList A "
                    + " where A.[Current LSN]='" + pCurrentLSN + "'; ";
            DB.ExecuteSQL(sTsql, false);

            sTsql = "select count(1) from #ModifiedRawData where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%'; ";
            if (Convert.ToInt32(DB.Query11(sTsql, false)) > 0)
            {
                isfound = true;
            }

            if (isfound == false && pOperation == "LOP_MODIFY_ROW")
            {
                sTsql = "truncate table #ModifiedRawData; ";
                DB.ExecuteSQL(sTsql, false);

                sTsql = "with t as("
                      + "select *,SlotID=replace(substring(ParentObject,5,charindex(N'Offset',ParentObject)-5),N' ',N'') "
                      + " from #temppagedata "
                      + " where LSN=N'" + pCurrentLSN + "' "
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
                DB.ExecuteSQL(sTsql, false);

                sTsql = "select count(1) from #ModifiedRawData where [RowLog Contents 0_var] like N'%" + (checkvalue1.Length <= 3998 ? checkvalue1 : checkvalue1.Substring(0, 3998)) + "%'; ";
                if (Convert.ToInt32(DB.Query11(sTsql, false)) > 0)
                {
                    isfound = true;
                }
            }

            if (isfound == true)
            {
                sTsql = @"update #ModifiedRawData set [RowLog Contents 0]=cast('' as xml).value('xs:hexBinary(substring(sql:column(""[RowLog Contents 0_var]""), 0) )', 'varbinary(max)'); ";
                DB.ExecuteSQL(sTsql, false);

                sTsql = "select top 1 'MR1'=[RowLog Contents 0] from #ModifiedRawData; ";
                dtTemp = DB.Query(sTsql, false);

                mr1 = (byte[])dtTemp.Rows[0]["MR1"];
            }

            return mr1;
        }

        private FPageInfo GetPageInfo(string pPageID)
        {
            FPageInfo r;
            List<string> ds;
            int i, j, m_slotCnt;
            string tmpstr, slotarray;

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

                sTsql = "truncate table #temppagedatalob; ";
                DB.ExecuteSQL(sTsql, false);

                sTsql = $"DBCC PAGE(''{sDatabaseName}'',{r.FileNum.ToString()},{r.PageNum.ToString()},2) with tableresults,no_infomsgs; ";
                sTsql = "set transaction isolation level read uncommitted; "
                        + $"insert into #temppagedatalob(ParentObject,Object,Field,Value) exec('{sTsql}'); ";
                DB.ExecuteSQL(sTsql, false);

                // pagedata
                sTsql = "select rn=row_number() over(order by Value)-1,Value=replace(upper(substring(Value,21,44)),N' ',N'') from #temppagedatalob where ParentObject=N'DATA:'; ";
                ds = DB.Query<(int rn, string Value)>(sTsql, false).Select(p => p.Value).ToList();
                r.PageData = string.Join("", ds);

                // pagetype
                sTsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_type'; ";
                r.PageType = DB.Query11(sTsql, false);

                // SlotCnt
                sTsql = "select Value from #temppagedatalob where ParentObject=N'PAGE HEADER:' and Field=N'm_slotCnt'; ";
                m_slotCnt = Convert.ToInt32(DB.Query11(sTsql, false));
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

            return r;
        }

        public void AnalyzeUpdate(string pTransactionID, byte[] mr1, byte[] r0, byte[] r1, byte[] r3, byte[] r4, byte[] bLogRecord, TableColumn[] columns, string sOperation, string pCurrentLSN, short? pOffsetinRow, short? pModifySize,
                                  ref string sValueList1, ref string sValueList0, ref string sWhereList1, ref string sWhereList0, ref byte[] mr0)
        {
            int i;
            string mr0_str, mr1_str, r0_str, r1_str, r3_str, sLogRecord;
            TableColumn[] columns0, columns1;

            mr1_str = mr1.ToText();
            r0_str = r0.ToText();  // .RowLog Contents 0
            r1_str = r1.ToText();  // .RowLog Contents 1
            r3_str = r3.ToText();  // .RowLog Contents 3
            sLogRecord = bLogRecord.ToText();  // .Log Record

            columns0 = new TableColumn[columns.Length];
            columns1 = new TableColumn[columns.Length];
            i = 0;
            foreach (TableColumn c in columns)
            {
                columns0[i] = new TableColumn(c.ColumnID, c.ColumnName, c.DataType, c.Length, c.Precision, c.Scale, c.LeafOffset, c.LeafNullBit, c.isNullable, c.isComputed);
                columns1[i] = new TableColumn(c.ColumnID, c.ColumnName, c.DataType, c.Length, c.Precision, c.Scale, c.LeafOffset, c.LeafNullBit, c.isNullable, c.isComputed);
                i = i + 1;
            }

            TranslateData(mr1, columns1);
            RestoreLobPage(pTransactionID);

            // 由 mr1_str 构造 mr0_str
            switch (sOperation)
            {
                case "LOP_MODIFY_ROW":
                    mr0_str = RESTORE_LOP_MODIFY_ROW(mr1_str, r1_str, r0_str, pOffsetinRow, pModifySize);
                    break;
                case "LOP_MODIFY_COLUMNS":
                    mr0_str = RESTORE_LOP_MODIFY_COLUMNS(sLogRecord, r3_str, r0, r1, mr1, mr1_str, columns0, columns1);
                    break;
                default:
                    mr0_str = mr1_str;
                    break;
            }

            mr0 = mr0_str.ToByteArray();
            TranslateData(mr0, columns0);

            sValueList1 = "";
            sValueList0 = "";
            sWhereList1 = "";
            sWhereList0 = "";
            for (i = 0; i <= columns.Length - 1; i++)
            {
                if (columns[i].DataType == SqlDbType.Timestamp || columns[i].isComputed == true) { continue; }

                if ((columns0[i].isNull == false
                     && columns1[i].isNull == false
                     && columns0[i].Value != null
                     && columns1[i].Value != null
                     && columns0[i].Value.ToString() != columns1[i].Value.ToString())
                    || (columns0[i].isNull == true && columns1[i].isNull == false)
                    || (columns0[i].isNull == false && columns1[i].isNull == true))
                {
                    sValueList0 = sValueList0 + (sValueList0.Length > 0 ? "," : "")
                                  + $"[{columns0[i].ColumnName}]="
                                  + ColumnValue2SQLValue(columns0[i]);
                    sValueList1 = sValueList1 + (sValueList1.Length > 0 ? "," : "")
                                  + $"[{columns1[i].ColumnName}]="
                                  + ColumnValue2SQLValue(columns1[i]);
                }

                if (TableInfos.PrimaryKeyColumns.Count == 0
                    || TableInfos.PrimaryKeyColumns.Contains(columns[i].ColumnName))
                {
                    sWhereList0 = sWhereList0 + (sWhereList0.Length > 0 ? " and " : "")
                                  + ColumnName2SQLName(columns[i]) 
                                  + (columns1[i].isNull ? " is " : "=")
                                  + ColumnValue2SQLValue(columns1[i]);
                    sWhereList1 = sWhereList1 + (sWhereList1.Length > 0 ? " and " : "")
                                  + ColumnName2SQLName(columns[i]) 
                                  + (columns0[i].isNull ? " is " : "=")
                                  + ColumnValue2SQLValue(columns0[i]);
                }
            }
        }

        private void RestoreLobPage(string pTransactionID)
        {
            string stemp;
            FPageInfo tpageinfo;

            foreach(FLOG log in dtLogs.Where(p => p.Transaction_ID == pTransactionID
                                                  && (p.Context == "LCX_TEXT_TREE" || p.Context == "LCX_TEXT_MIX"))
                                      .OrderByDescending(p => p.Current_LSN))   // 从后往前解析
            {
                tpageinfo = GetPageInfo(log.Page_ID);
                stemp = tpageinfo.PageData;

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

        private string RESTORE_LOP_MODIFY_ROW(string mr1_str, string r1_str, string r0_str, short? pOffsetinRow, short? pModifySize)
        {
            string mr0_str;

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

            return mr0_str;
        }

        private string RESTORE_LOP_MODIFY_COLUMNS(string sLogRecord, string r3_str, byte[] r0, byte[] r1, byte[] mr1, string mr1_str, TableColumn[] columns0, TableColumn[] columns1)
        {
            string mr0_str, rowlogdata, fvalue0, fvalue1, ts;
            int i, j, k, n, m, fstart0, fstart1, flength0, flength0f4, flength1, flength1f4;
            List<string> tls;
            byte[] mr0;
            bool bfinish;
            TableColumn tmpcol;

            mr0_str = null;
            rowlogdata = sLogRecord.Substring(sLogRecord.IndexOf(r3_str) + r3_str.Length,
                                              sLogRecord.Length - sLogRecord.IndexOf(r3_str) - r3_str.Length);
            if ((sLogRecord.Length - rowlogdata.Length) % 8 != 0)
            {
                rowlogdata = rowlogdata.Substring((sLogRecord.Length - rowlogdata.Length) % 8);
            }

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
                    
                    flength1 = flength0;
                    flength1f4 = (flength1 % 4 == 0 ? flength1 : flength1 + (4 - flength1 % 4));

                    fvalue1 = rowlogdata.Substring(j * 2, flength1 * 2);
                    j = j + flength1f4;

                    if (i == (r0.Length / 4) && (j * 2) < (rowlogdata.Length - 1))
                    {
                        flength1 = rowlogdata.Length / 2 - j;
                        flength1f4 = (flength1 % 4 == 0 ? flength1 : flength1 + (4 - flength1 % 4));

                        fvalue1 = rowlogdata.Substring(j * 2, flength1 * 2);
                        j = j + flength1f4;
                    }

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
                                    && columns1.Any(p => p.isVarLenDataType == true))
                                {
                                    tmpcol = columns1.Where(p => p.isVarLenDataType == true).OrderBy(p => p.ColumnID).FirstOrDefault();
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

        private void TranslateData(byte[] data, TableColumn[] columns)
        {
            int index, index2, index3,
                iBitValueStartIndex,
                iVarColumnCount;
            string sData,
                   sNullStatus,  // 列null值状态列表
                   sTemp,
                   sValueHex,
                   sValue,
                   VariantCollation;
            byte[] m_bBitColumnData;
            short i, j, 
                  sBitColumnCount, 
                  iUniqueidentifierColumnCount, 
                  sBitColumnDataLength, 
                  sBitColumnDataIndex,
                  sAllColumnCount,              // 字段总数_实际字段总数
                  sAllColumnCountLog,           // 字段总数_日志里的字段总数
                  sMaxColumnID,                 // 最大ColumnID       
                  sNullStatusLength,            // 列null值状态列表存储所需长度(字节)
                  sVarColumnCount,              // 变长字段数量
                  sVarColumnStartIndex,         // 变长列字段值开始位置
                  sVarColumnEndIndex;           // 变长列字段值结束位置
            short? VariantLength, 
                   VariantScale;
            bool hasJumpRowID;       // 是否已跳过RowID,用于无PrimaryKey的表.
            TableColumn[] columns2,  // 补齐ColumnID,并移除所有计算列的字段列表.
                          columns3;  // 实际用于解析的字段列表.
            SqlDbType? VariantBaseType;
            TableColumn tmpTableColumn;
            List<FVarColumnInfo> varlencolumns;  // 变长字段数据
            FVarColumnInfo tvc;

            if (data == null || data.Length <= 4) { return; }

            index = 4;  // 行数据从第5字节开始
            sData = data.ToText();
            sAllColumnCount = Convert.ToInt16(columns.Length);

            // 预处理Bit字段
            sBitColumnCount = Convert.ToInt16(columns.Count(p => p.DataType == SqlDbType.Bit));
            sBitColumnDataLength = (short)Math.Ceiling((double)sBitColumnCount / (double)8.0); // 根据Bit字段数 计算Bit字段值列表长度(字节数)
            m_bBitColumnData = new byte[sBitColumnDataLength];
            sBitColumnDataIndex = -1;
            iBitValueStartIndex = 0;

            // 预处理Uniqueidentifier字段
            iUniqueidentifierColumnCount = Convert.ToInt16(columns.Count(p => p.DataType == SqlDbType.UniqueIdentifier));

            if (iUniqueidentifierColumnCount >= 2
                && TableInfos.IsHeapTable == false) // 堆表不适用本规则
            {
                columns2 = new TableColumn[columns.Length];

                j = 0;
                for (i = (short)(columns.Length - 1); i >= 0; i--)
                {
                    if (columns[i].DataType == SqlDbType.UniqueIdentifier)
                    {
                        columns2[j] = columns[i];
                        j++;
                    }
                }

                for (i = 0; i <= columns.Length - 1; i++)
                {
                    if (columns[i].DataType != SqlDbType.UniqueIdentifier)
                    {
                        columns2[j] = columns[i];
                        j++;
                    }
                }

                columns = columns2;
            }

            index2 = Convert.ToInt32(data[3].ToString("X2") + data[2].ToString("X2"), 16);  // 指针暂先跳过所有定长字段的值
            sAllColumnCountLog = BitConverter.ToInt16(data, index2);

            if (TableInfos.IsHeapTable == true)
            {
                hasJumpRowID = true; // true false  某些堆表没RowID?
            }
            else
            {
                hasJumpRowID = (TableInfos.PrimaryKeyColumns.SequenceEqual(TableInfos.ClusteredIndexColumns) ? true : false);
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
            if (TableInfos.ClusteredIndexColumns.Count > 0)
            {
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string cc in TableInfos.ClusteredIndexColumns)
                {
                    tmpTableColumn = columns2.Where(p => p.ColumnName == cc && p.isVarLenDataType == false).FirstOrDefault();
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
            if (TableInfos.ClusteredIndexColumns.Count == 0 && TableInfos.PrimaryKeyColumns.Count > 0)
            {
                i = 0;
                columns3 = new TableColumn[columns2.Length];

                // 主键字段置前
                foreach (string pc in TableInfos.PrimaryKeyColumns)
                {
                    tmpTableColumn = columns2.Where(p => p.ColumnName == pc && p.isVarLenDataType == false).FirstOrDefault();
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
                sNullStatus = data[index2].ToBinaryString() + sNullStatus;
                index2 = index2 + 1;
            }
            sNullStatus = sNullStatus.Reverse();  // 字符串反转

            if (TableInfos.IsHeapTable == false && TableInfos.PrimaryKeyColumns.SequenceEqual(TableInfos.ClusteredIndexColumns) == false)
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

            // 定长字段
            foreach (TableColumn c in columns3)
            {
                if (c.isVarLenDataType == true || c.isExists == false) { continue; }

                index3 = index;
                if (index != c.LeafOffset)
                {
                    index = c.LeafOffset;
                }
                c.LogContentsStartIndex = index;

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
                            c.Value = TranslateData_DateTime2(data, index, c.Length, c.Scale);
                            index = index + c.Length;
                            break;
                        case System.Data.SqlDbType.DateTimeOffset:
                            c.Value = TranslateData_DateTimeOffset(data, index, c.Length, c.Scale);
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
                            c.Value = TranslateData_Time(data, index, c.Length, c.Scale);
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

                            iBitValueStartIndex = (sBitColumnDataIndex == -1 ? index : iBitValueStartIndex);
                            iJumpIndexLength = 0;
                            bValueBit = TranslateData_Bit(data, columns, index, c.ColumnName, sBitColumnCount, m_bBitColumnData, sBitColumnDataIndex, ref iJumpIndexLength, ref m_bBitColumnData, ref sBitColumnDataIndex);

                            iBitValueStartIndex = (iJumpIndexLength > 0 ? index : iBitValueStartIndex);
                            index = index + iJumpIndexLength;

                            c.LogContentsStartIndex = iBitValueStartIndex;
                            c.Value = bValueBit;
                            c.LogContentsEndIndex = iBitValueStartIndex;
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

                c.LogContentsEndIndex = (c.DataType != SqlDbType.Bit ? index - 1 : c.LogContentsEndIndex);
                c.LogContents = sData.Substring(c.LogContentsStartIndex * 2, (c.LogContentsEndIndex - c.LogContentsStartIndex + 1) * 2);
                index = index3;
            }

            index = index2;

            // 变长字段
            if (index + 1 <= data.Length - 1)
            {
                // 变长字段数量(不一定等于字段类型=变长类型的字段数量)
                sTemp = sData.Substring((index + 1) * 2, 2) + sData.Substring(index * 2, 2);
                iVarColumnCount = Int32.Parse(sTemp, System.Globalization.NumberStyles.HexNumber);
                if (iVarColumnCount <= 32767 && iVarColumnCount <= sAllColumnCountLog)
                {
                    sVarColumnCount = (short)iVarColumnCount;
                }
                else
                {
                    sVarColumnCount = (short)columns3.Count(p => p.isVarLenDataType == true);
                }
                
                index = index + 2;
                varlencolumns = new List<FVarColumnInfo>();
                if (index < data.Length - 1)
                {
                    // 接下来每2个字节保存一个变长字段的结束位置,第一个变长字段的开始和结束位置可以算出来.
                    sTemp = sData.Substring(index * 2, 2 * 2);
                    sVarColumnStartIndex = (short)(index + sVarColumnCount * 2);
                    sVarColumnEndIndex = BitConverter.ToInt16(data, index);
                    
                    for (i = 1, index2 = index; i <= sVarColumnCount; i++)
                    {
                        tvc = new FVarColumnInfo();
                        tvc.FIndex = Convert.ToInt16(i * -1);
                        tvc.FEndIndexHex = sTemp;
                        tvc.InRow = sTemp.Substring(2, 2).ToBinaryString().StartsWith("0");

                        tvc.FStartIndex = sVarColumnStartIndex;
                        if (tvc.InRow == false)
                        {
                            sVarColumnEndIndex = Convert.ToInt16(sTemp.Substring(2, 2).ToBinaryString().Stuff(0, 1, "0") + sTemp.Substring(0, 2).ToBinaryString(), 2);
                        }
                        tvc.FEndIndex = sVarColumnEndIndex;

                        tvc.FLogContents = sData.Substring(sVarColumnStartIndex * 2,
                                                           (sVarColumnEndIndex - sVarColumnStartIndex) * 2);

                        varlencolumns.Add(tvc);

                        if (i < sVarColumnCount)
                        {
                            index2 = index2 + 2;

                            sTemp = sData.Substring(index2 * 2, 2 * 2);
                            sVarColumnStartIndex = sVarColumnEndIndex;
                            sVarColumnEndIndex = BitConverter.ToInt16(data, index2);
                        }
                    }
                }

                // 跳过1个变长字段(可能为表的RowID).
                //if (hasJumpRowID == false)
                //{
                //    hasJumpRowID = true;

                //    sVarColumnStartIndex = sVarColumnEndIndex;
                //    index = index + 2;
                //    if (index + 2 >= data.Length - 1) { return; }
                //    sVarColumnEndIndex = BitConverter.ToInt16(data, index);
                //}

                // 循环变长字段列表读取数据
                foreach (TableColumn c in columns3)
                {
                    if (c.isVarLenDataType == false && c.isExists == true) { continue; }

                    tvc = varlencolumns.FirstOrDefault(p => p.FIndex == c.LeafOffset);
                    if (tvc != null)
                    {
                        c.LogContentsStartIndex = tvc.FStartIndex;
                        c.LogContentsEndIndex = tvc.FEndIndex;
                        c.LogContentsEndIndexHex = tvc.FEndIndexHex;
                        c.LogContents = tvc.FLogContents;
                    }

                    if (c.isNull == true
                        || c.isExists == false
                        || (tvc == null && c.isNull == true))
                    {
                        c.isNull = true;
                        c.Value = "nullvalue";
                        c.ValueHex = "";

                        continue;
                    }

                    if (tvc != null)
                    {
                        switch (c.DataType)
                        {
                            case System.Data.SqlDbType.VarChar:
                                c.ValueHex = tvc.FLogContents;
                                c.Value = System.Text.Encoding.Default.GetString(tvc.FLogContents.ToByteArray()).TrimEnd();
                                break;
                            case System.Data.SqlDbType.NVarChar:
                                c.ValueHex = tvc.FLogContents;
                                c.Value = System.Text.Encoding.Unicode.GetString(tvc.FLogContents.ToByteArray()).TrimEnd();
                                break;
                            case System.Data.SqlDbType.VarBinary:
                                (sValueHex, sValue) = TranslateData_VarBinary(data, tvc);
                                c.ValueHex = sValueHex;
                                c.Value = sValue;
                                break;
                            case System.Data.SqlDbType.Variant:
                                (sValueHex, sValue, VariantBaseType, VariantLength, VariantScale, VariantCollation) = TranslateData_Variant(data, tvc);
                                c.ValueHex = sValueHex;
                                c.Value = sValue;
                                c.VariantBaseType = VariantBaseType;
                                c.VariantLength = VariantLength;
                                c.VariantScale = VariantScale;
                                c.VariantCollation = VariantCollation;
                                break;
                            case System.Data.SqlDbType.Xml:
                                (sValueHex, sValue) = TranslateData_XML(data, tvc);
                                c.ValueHex = sValueHex;
                                c.Value = sValue;
                                break;
                            case System.Data.SqlDbType.Text:
                                (sValueHex, sValue) = TranslateData_Text(data, tvc, false, TableInfos.TextInRow);
                                c.ValueHex = sValueHex;
                                c.Value = sValue;
                                c.isNull = (sValueHex == null && sValue == "nullvalue");
                                break;
                            case System.Data.SqlDbType.NText:
                                (sValueHex, sValue) = TranslateData_Text(data, tvc, true, TableInfos.TextInRow);
                                c.ValueHex = sValueHex;
                                c.Value = sValue;
                                c.isNull = (sValueHex == null && sValue == "nullvalue");
                                break;
                            case System.Data.SqlDbType.Image:
                                (sValueHex, sValue) = TranslateData_Image(data, tvc);
                                c.ValueHex = sValueHex;
                                c.Value = sValue;
                                break;
                            default:
                                break;
                        }

                        continue;
                    }
                    else
                    {
                        if (c.isNull == false
                            && (c.DataType == System.Data.SqlDbType.VarChar || c.DataType == System.Data.SqlDbType.NVarChar))
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
                    if (c.isVarLenDataType == true) { c.isNull = true; }
                }
            }

            // 重新赋值回columns.
            foreach (TableColumn x in columns)
            {
                tmpTableColumn = columns3.Where(p => p.ColumnID == x.ColumnID).FirstOrDefault();

                if (tmpTableColumn != null)
                {
                    x.isNull = tmpTableColumn.isNull;
                    x.Value = tmpTableColumn.Value;
                    x.LogContentsStartIndex = tmpTableColumn.LogContentsStartIndex;
                    x.LogContentsEndIndex = tmpTableColumn.LogContentsEndIndex;
                }
                else
                {
                    x.isNull = true;
                    x.Value = "nullvalue";
                    x.LogContentsStartIndex = -1;
                    x.LogContentsEndIndex = -1;
                }
            }
        }

        private (TableInformation, TableColumn[]) GetTableInfo(string pSchemaName, string pTablename)
        {
            string stemp;
            TableInformation tableinfo;
            TableColumn[] tablecolumns;

            tableinfo = new TableInformation();

            // PrimaryKeyColumns
            sTsql = "select primarykeycolumn=c.name "
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
            tableinfo.PrimaryKeyColumns = DB.Query<string>(sTsql, false).ToList();

            // ClusteredIndexColumns
            sTsql = "select clusteredindexcolumn=c.name "
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
            tableinfo.ClusteredIndexColumns = DB.Query<string>(sTsql, false).ToList();

            // IdentityColumn
            sTsql = "select identitycolumn=a.name "
                    + "  from sys.columns a "
                    + "  join sys.objects b on a.object_id=b.object_id "
                    + "  join sys.schemas s on b.schema_id=s.schema_id "
                    + "  where a.is_identity=1 "
                    + $" and s.name=N'{pSchemaName}' "
                    + "  and b.type='U' "
                    + $" and b.name=N'{pTablename}'; ";
            tableinfo.IdentityColumn = DB.Query<string>(sTsql, false).FirstOrDefault();

            // IsHeapTable
            sTsql = "select isheaptable=cast(case when exists(select 1 "
                      + "                                     from sys.tables t "
                      + "                                     join sys.schemas s on t.schema_id=s.schema_id "
                      + "                                     join sys.indexes i on t.object_id=i.object_id "
                      + $"                                    where s.name=N'{pSchemaName}' "
                      + $"                                    and t.name=N'{pTablename}' "
                      + "                                     and i.index_id=0) then 1 else 0 end as bit); ";
            tableinfo.IsHeapTable = DB.Query<bool>(sTsql, false).FirstOrDefault();

            // AllocUnitName
            sTsql = "select allocunitname=isnull(d.name,N'') "
                    + "  from sys.tables a "
                    + "  join sys.schemas s on a.schema_id=s.schema_id "
                    + "  join sys.indexes d on a.object_id=d.object_id "
                    + "  where d.type in(0,1) "
                    + $" and s.name=N'{pSchemaName}' "
                    + $" and a.name=N'{pTablename}'; ";
            tableinfo.AllocUnitName = DB.Query<string>(sTsql, false).FirstOrDefault();

            // TextInRow
            sTsql = "select textinrow=a.text_in_row_limit "
                    + "  from sys.tables a "
                    + "  join sys.schemas s on a.schema_id=s.schema_id "
                    + $" where s.name=N'{pSchemaName}' "
                    + $" and a.name=N'{pTablename}'; ";
            tableinfo.TextInRow = DB.Query<int>(sTsql, false).FirstOrDefault();

            sTsql = "select cast(("
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
                        + "       join sys.schemas s on a.schema_id=s.schema_id "
                        + "       join sys.columns b on a.object_id=b.object_id "
                        + "       join sys.systypes c on b.system_type_id=c.xtype and b.user_type_id=c.xusertype "
                        + "       outer apply (select d.leaf_offset,d.leaf_null_bit "
                        + "                    from sys.system_internals_partition_columns d "
                        + "                    where d.partition_column_id=b.column_id "
                        + "                    and d.partition_id in (select partitionss.partition_id "
                        + "                                           from sys.allocation_units allocunits "
                        + "                                           join sys.partitions partitionss on (allocunits.type in(1, 3) and allocunits.container_id=partitionss.hobt_id) "
                        + "                                                                              or (allocunits.type=2 and allocunits.container_id=partitionss.partition_id) "
                        + "                                           where partitionss.object_id=a.object_id and partitionss.index_id<=1)) d2 "
                        + $"      where s.name=N'{pSchemaName}' and a.name=N'{pTablename}') t "
                        + " order by ColumnID "
                        + " for xml raw('Column'),root('ColumnList') "
                        + ") as nvarchar(max)); ";
            stemp = DB.Query11(sTsql, false);
            tablecolumns = AnalyzeTablelayout(stemp);

            return (tableinfo, tablecolumns);
        }

        // 解析表结构定义(XML).
        public TableColumn[] AnalyzeTablelayout(string sTableLayout)
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

        private string ColumnValue2SQLValue(TableColumn pcol)
        {
            string sValue;
            bool bNeedSeparatorchar, bIsUnicodeType;
            string[] NoSeparatorchar, UnicodeType;
            SqlDbType? datatype;

            datatype = (pcol.DataType != SqlDbType.Variant ? pcol.DataType : pcol.VariantBaseType);

            if (pcol.isNull == true || pcol.Value == null || datatype == null)
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

                if (pcol.DataType == SqlDbType.Variant)
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

            switch (pcol.DataType)
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
                sBitColumnData2 = sBitColumnData2 + m_bBitColumnData1[i].ToBinaryString();
            }

            sBitColumnData2 = sBitColumnData2.Reverse();   // 字符串反转

            rBit = sBitColumnData2.Substring(sCurrentColumnIDinBit - 1, 1);
            return rBit;
        }

        private string TranslateData_Date(byte[] data, int iCurrentIndex)
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

        private string TranslateData_DateTime(byte[] data, int iCurrentIndex)
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
            sReturnDatetime2 = sDate + " " + sTime;

            return sReturnDatetime2;
        }

        private string TranslateData_DateTimeOffset(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sReturnDateTimeOffset, sDate, sTime, sOffset;
            short sSignOffset, iOffset;

            byte[] bDateTimeOffset = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDateTimeOffset, 0, sLength);

            // offset
            sSignOffset = 1;
            iOffset = Convert.ToInt16(bDateTimeOffset[sLength - 1].ToString("X2").Substring(1, 1) + bDateTimeOffset[sLength - 2].ToString("X2"), 16);
            if (bDateTimeOffset[sLength - 1].ToBinaryString().Substring(0, 1) == "1")
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
            string sReturnSmallMoney;

            byte[] bSmallMoney = new byte[4];
            Array.Copy(data, iCurrentIndex, bSmallMoney, 0, 4);

            string sSign;
            if (bSmallMoney[3].ToBinaryString().Substring(7, 1) == "0")
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

        private string TranslateData_Real(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnReal;

            byte[] bReal = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bReal, 0, sLenth);

            short sSignReal;
            sSignReal = 1;
            if (bReal[3].ToBinaryString().Substring(0, 1) == "1")
            {
                sSignReal = -1;
            }

            // 指数
            string sExpReal;
            int iExpReal;
            sExpReal = bReal[3].ToBinaryString().Substring(1, 7)
                       + bReal[2].ToBinaryString().Substring(0, 1);
            iExpReal = Convert.ToInt32(sExpReal, 2);

            // 尾数
            string sFractionReal;
            int iReal;
            double dFractionReal;
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

            sSignFloat = 1;
            if (bFloat[7].ToBinaryString().Substring(0, 1) == "1")
            {
                sSignFloat = -1;
            }

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

        private (string, string) TranslateData_XML(byte[] data, FVarColumnInfo pvc)
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

}
