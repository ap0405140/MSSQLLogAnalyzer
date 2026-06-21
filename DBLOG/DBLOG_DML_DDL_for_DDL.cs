using DBLOG.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DBLOG
{
    public partial class DBLOG_DML_DDL
    {
        public static Dictionary<string, TableInfo> SystemTables;
        public static Dictionary<string, List<TableInfo>> UserTables;
        public static Dictionary<int, string> Schemas;
        public static Dictionary<string, string> Systypes;
        private static List<Dictionary<string, string>> fsysschobjs, fsysiscols, fsyscolpars, fsysidxstats, fsysobjvalues, fsysrscols, fsysclsobjs, fsysallocunits, fsysrowsets, fsysscalartypes,
                                                        fsysschobjs0, fsysiscols0, fsyscolpars0, fsysidxstats0, fsysobjvalues0, fsysrscols0, fsysclsobjs0, fsysallocunits0, fsysrowsets0, fsysscalartypes0,
                                                        lsysschobjs, lsysiscols, lsyscolpars, lsysidxstats, lsysobjvalues, lsysrscols, lsysclsobjs, lsysallocunits, lsysrowsets, lsysscalartypes;
        private static Dictionary<string, string> dsysschobjs, dsysiscols, dsyscolpars, dsysidxstats, dsysobjvalues, dsysrscols, dsysclsobjs, dsysscalartypes;
        private static Dictionary<string, List<FLOG>> SELECTINTO_RS; // key:[SELECT INTO_CREATE] tranid  value:[SELECT INTO_INSERT] tran list

        public List<DatabaseLog> AnalyzeDDLLog()
        {
            List<DatabaseLog> r, dr;

            r = new List<DatabaseLog>();
            foreach (string TransactionID in DDLLogs.Where(p => DDLLogs_FTranID.Any(t => t.TransactionID == p.Transaction_ID) == false)
                                                    .Select(p => p.Transaction_ID)
                                                    .Distinct()
                                                    .OrderByDescending(p => p))
            {
                dr = AnalyzeDDLTran(TransactionID);
                r.AddRange(dr);
            }

            return r;
        }

        private List<DatabaseLog> AnalyzeDDLTran(string TransactionID)
        {
            List<DatabaseLog> ddllog, sublog;
            DatabaseLog dr;
            TransactionInfo traninfo, rtran;
            string objectname, redosql, undosql, TransactionName, BeginTime, EndTime, rtranid;
            List<string> AllocUnitIds, PartitionIds;
            FLOG tlog;
            bool ignoretran;

            DDLLogs_Tran = DDLLogs.Where(p => p.Transaction_ID == TransactionID).OrderBy(p => p.Current_LSN).ToList();
            
            TransactionName = DDLLogs_Tran.First().Transaction_Name;
            if (TransactionName == "SELECT INTO")
            {
                if (DDLLogs_Tran.Any(p => string.IsNullOrEmpty(p.AllocUnitName) == false && p.AllocUnitName.StartsWith("sys.") == false) == true)
                {
                    TransactionName = "SELECT INTO_INSERT";
                }
                else
                {
                    TransactionName = "SELECT INTO_CREATE";
                }
            }

            BeginTime = DDLLogs_Tran.First().Begin_Time;
            EndTime = DDLLogs_Tran.Last().End_Time;

            ddllog = new List<DatabaseLog>();
            ignoretran = false;

            traninfo = new TransactionInfo();
            traninfo.TransactionID = TransactionID;
            traninfo.TransactionType = "DDL";
            traninfo.TransactionName = TransactionName.ToUpper();
            traninfo.FTime = DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
            traninfo.AllocUnitId = DDLLogs_Tran.Where(p => p.AllocUnitId != null)
                                               .Select(p => p.AllocUnitId.ToString())
                                               .Distinct()
                                               .ToList();
            traninfo.LSNList = DDLLogs_Tran.Select(p => p.Current_LSN).ToList();

            if (TransactionName != "SELECT INTO_INSERT")
            {
#if DEBUG
                NLogger.Info($"DDL TransactionID={TransactionID} TransactionName={TransactionName}");
#endif

                dr = new DatabaseLog();
                dr.LSN = DDLLogs_Tran.First().Current_LSN;
                dr.Type = "DDL";
                dr.TransactionID = TransactionID;
                dr.BeginTime = DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                dr.EndTime = DateTime.ParseExact(EndTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                dr.Operation = TransactionName.ToUpper();
                dr.Message = "";

                objectname = "";
                redosql = "";
                undosql = "";
                AllocUnitIds = new List<string>();
                PartitionIds = new List<string>();
                switch (TransactionName)
                {
                    case "create-schema":
                        TCreateSchema(1, out objectname, out redosql, out undosql);
                        break;
                    case "DROP SCHEMA":
                        TCreateSchema(-1, out objectname, out redosql, out undosql);
                        break;
                    case "CREATE TABLE":
                    case "SELECT INTO_CREATE":
                        TCreateTable(1, out objectname, out redosql, out undosql, out AllocUnitIds, out PartitionIds);
                        break;
                    case "DROPOBJ":
                        TDropObj(-1, out objectname, out redosql, out undosql, out AllocUnitIds, out PartitionIds);
                        break;
                    case "user_transaction":
                        TUserTransaction(1, out objectname, out redosql, out undosql);
                        break;
                    case "ALTER TABLE":
                        TAlterTable(out objectname, out redosql, out undosql, out AllocUnitIds, out PartitionIds, out ignoretran);

                        if (ignoretran == false)
                        {
                            rtran = DDLLogs_FTranID.Where(p => string.Compare(p.TransactionID, TransactionID) > 0
                                                               && p.TransactionName == "DROPOBJ")
                                                   .OrderBy(p => p.TransactionID)
                                                   .FirstOrDefault();
                            if (redosql.Contains(" rebuild with(data_compression=") == true
                                && AllocUnitIds.Count > 0
                                && rtran != null)
                            {
                                rtran.PartitionID = rtran.PartitionID.Union(PartitionIds).ToList();
                                rtran.AllocUnitId = rtran.AllocUnitId.Union(AllocUnitIds).ToList();
                            }
                        }

                        break;
                    case "TRUNCATE TABLE":
                        TTruncateTable(out objectname, out redosql, out undosql);
                        break;
                    case "CREATE INDEX":
                        TCreateIndex(out objectname, out redosql, out undosql);
                        break;
                    case "DROP INDEX":
                        TDropIndex(out objectname, out redosql, out undosql);
                        break;
                    case "CREATE/ALTER VIEW":
                        TCreateAlterSQLModule("V", out objectname, out redosql, out undosql);
                        break;
                    case "CREATE/ALTER PROCEDURE":
                        TCreateAlterSQLModule("P", out objectname, out redosql, out undosql);
                        break;
                    case "CREATE/ALTER FUNCTION":
                        TCreateAlterSQLModule("FUN", out objectname, out redosql, out undosql); // FUN = IF / FN / TF
                        break;
                    case "CREATE/ALTER TRIGGER":
                        TCreateAlterSQLModule("TR", out objectname, out redosql, out undosql);
                        break;
                    case "CREATE SYNONYM":
                        TCreateSynonym(out objectname, out redosql, out undosql);
                        break;
                    case "CREATE TYPE":
                        TCreateType(out objectname, out redosql, out undosql);
                        break;
                    case "DROP TYPE":
                        TDropType(out objectname, out redosql, out undosql);
                        break;
                }
                dr.ObjectName = objectname;
                dr.RedoSQL = redosql;
                dr.UndoSQL = undosql;

#if DEBUG
                NLogger.Info($"RedoSql:{redosql}\r\nUndoSql:{undosql}");
#endif

                if (ignoretran == false)
                {
                    ddllog.Add(dr);

                    if (TransactionName == "SELECT INTO_CREATE"
                        && SELECTINTO_RS.ContainsKey(TransactionID) == true)
                    {
                        sublog = T_SELECT_INTO_INSERT(TransactionID, objectname);
                        ddllog.AddRange(sublog);

#if DEBUG
                        if (sublog.Count > 0)
                        {
                            redosql = sublog.First().TransactionID + " => \r\n" 
                                      + string.Join("\r\n", sublog.Select(s => s.RedoSQL));
                            NLogger.Info(redosql);
                        }
#endif
                    }
                }
                
                traninfo.PartitionID = PartitionIds;
                traninfo.AllocUnitId.AddRange(AllocUnitIds);
                traninfo.AllocUnitName = objectname;

            }
            else
            {
                // SELECT INTO_INSERT
                tlog = DDLLogs_Tran.First(p => p.AllocUnitName == "sys.sysallocunits.clust");
                rtranid = DDLLogs.Where(p => string.Compare(p.Transaction_ID, TransactionID) < 0
                                             && DDLLogs.Any(x => x.Transaction_ID == p.Transaction_ID && x.Transaction_Name == "SELECT INTO") == true
                                             && p.AllocUnitName == tlog.AllocUnitName 
                                             && p.Page_ID == tlog.Page_ID 
                                             && p.Slot_ID == tlog.Slot_ID)
                                 .OrderByDescending(p => p.Current_LSN)
                                 .First()
                                 .Transaction_ID;
                SELECTINTO_RS.Add(rtranid, DDLLogs_Tran);
            }

            DDLLogs_FTranID.Add(traninfo);

            return ddllog;
        }

        private void TranslateSystemTables(int d)
        {
            List<string> systabs, transystabs;

            #region init
            fsysschobjs = new List<Dictionary<string, string>>();
            fsysiscols = new List<Dictionary<string, string>>();
            fsyscolpars = new List<Dictionary<string, string>>();
            fsysidxstats = new List<Dictionary<string, string>>();
            fsysobjvalues = new List<Dictionary<string, string>>();
            fsysrscols = new List<Dictionary<string, string>>();
            fsysclsobjs = new List<Dictionary<string, string>>();
            fsysallocunits = new List<Dictionary<string, string>>();
            fsysrowsets = new List<Dictionary<string, string>>();

            fsysschobjs0 = new List<Dictionary<string, string>>();
            fsysiscols0 = new List<Dictionary<string, string>>();
            fsyscolpars0 = new List<Dictionary<string, string>>();
            fsysidxstats0 = new List<Dictionary<string, string>>();
            fsysobjvalues0 = new List<Dictionary<string, string>>();
            fsysrscols0 = new List<Dictionary<string, string>>();
            fsysclsobjs0 = new List<Dictionary<string, string>>();
            fsysallocunits0 = new List<Dictionary<string, string>>();
            fsysrowsets0 = new List<Dictionary<string, string>>();

            lsysschobjs = new List<Dictionary<string, string>>();
            lsysiscols = new List<Dictionary<string, string>>();
            lsyscolpars = new List<Dictionary<string, string>>();
            lsysidxstats = new List<Dictionary<string, string>>();
            lsysobjvalues = new List<Dictionary<string, string>>();
            lsysrscols = new List<Dictionary<string, string>>();
            lsysclsobjs = new List<Dictionary<string, string>>();
            lsysallocunits = new List<Dictionary<string, string>>();
            lsysrowsets = new List<Dictionary<string, string>>();
            #endregion init

            systabs = new List<string>() {
                        "sys.sysclsobjs.clst",
                        "sys.sysschobjs.clst",
                        "sys.sysiscols.clst",
                        "sys.sysidxstats.clst",
                        "sys.sysobjvalues.clst",
                        "sys.sysrscols.clst",
                        "sys.syscolpars.clst",
                        "sys.sysallocunits.clust",
                        "sys.sysrowsets.clust",
                        "sys.sysscalartypes.clst" };

            transystabs = DDLLogs_Tran.Where(p => string.IsNullOrEmpty(p.AllocUnitName) == false)
                                      .Select(p => p.AllocUnitName)
                                      .Distinct()
                                      .ToList();
            foreach (string tabname in transystabs)
            {
                if (systabs.Contains(tabname) == false) { continue; }
                
                switch(tabname)
                {
                    case "sys.sysclsobjs.clst":
                        TranslateSystemTable(tabname, out fsysclsobjs0, out fsysclsobjs);
                        break;
                    case "sys.sysschobjs.clst":
                        TranslateSystemTable(tabname, out fsysschobjs0, out fsysschobjs);
                        break;
                    case "sys.sysiscols.clst":
                        TranslateSystemTable(tabname, out fsysiscols0, out fsysiscols);
                        break;
                    case "sys.sysidxstats.clst":
                        TranslateSystemTable(tabname, out fsysidxstats0, out fsysidxstats);
                        break;
                    case "sys.sysobjvalues.clst":
                        TranslateSystemTable(tabname, out fsysobjvalues0, out fsysobjvalues);
                        break;
                    case "sys.sysrscols.clst":
                        TranslateSystemTable(tabname, out fsysrscols0, out fsysrscols);
                        break;
                    case "sys.syscolpars.clst":
                        TranslateSystemTable(tabname, out fsyscolpars0, out fsyscolpars);
                        break;
                    case "sys.sysallocunits.clust":
                        TranslateSystemTable(tabname, out fsysallocunits0, out fsysallocunits);
                        break;
                    case "sys.sysrowsets.clust":
                        TranslateSystemTable(tabname, out fsysrowsets0, out fsysrowsets);
                        break;
                    case "sys.sysscalartypes.clst":
                        TranslateSystemTable(tabname, out fsysscalartypes0, out fsysscalartypes);
                        break;
                }
            }

            SET_LSystemTables(d);

        }

        private void SET_LSystemTables(int d)
        {
            if (d == 1)
            {
                lsysschobjs = fsysschobjs;
                lsysiscols = fsysiscols;
                lsyscolpars = fsyscolpars;
                lsysidxstats = fsysidxstats;
                lsysobjvalues = fsysobjvalues;
                lsysrscols = fsysrscols;
                lsysclsobjs = fsysclsobjs;
                lsysallocunits = fsysallocunits;
                lsysrowsets = fsysrowsets;
                lsysscalartypes = fsysscalartypes;
            }
            else
            {
                lsysschobjs = fsysschobjs0;
                lsysiscols = fsysiscols0;
                lsyscolpars = fsyscolpars0;
                lsysidxstats = fsysidxstats0;
                lsysobjvalues = fsysobjvalues0;
                lsysrscols = fsysrscols0;
                lsysclsobjs = fsysclsobjs0;
                lsysallocunits = fsysallocunits0;
                lsysrowsets = fsysrowsets0;
                lsysscalartypes = fsysscalartypes0;
            }

        }

        private void TCreateSchema(int d, out string objectname, out string redosql, out string undosql)
        {
            int schemaid;

            TranslateSystemTables(d);
            
            dsysclsobjs = lsysclsobjs.First();
            objectname = dsysclsobjs["name"];
            schemaid = Convert.ToInt32(dsysclsobjs["id"]);

            if (d == 1)
            {
                redosql = $"create schema [{objectname}];";
                undosql = $"drop schema [{objectname}];";
            }
            else
            {
                redosql = $"drop schema [{objectname}];";
                undosql = $"create schema [{objectname}];";

                if (Schemas.ContainsKey(schemaid)) { Schemas.Remove(schemaid); }
                Schemas.Add(schemaid, objectname);
            }

            objectname = $"[{dsysclsobjs["name"]}]";
        }

        private void TCreateTable(int d, out string objectname, out string redosql, out string undosql, out List<string> AllocUnitIds, out List<string> PartitionIds)
        {
            string schemaname, columndefinition, constraint, others, stk;
            bool isnode, isedge;
            List<string> lstemp1, lstemp2, lstemp3;
            string stemp1, stemp2, stemp3, stemp4, stemp5;
            TableInfo ftabinfo;
            TableColumn fcol;
            int i;
            long partitionid;

            if (d == 1)
            {
                TranslateSystemTables(d);
            }

            dsysschobjs = lsysschobjs.First(p => p["type"] == "U");
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];
            
            ftabinfo = new TableInfo();
            ftabinfo.SchemaName = schemaname;
            ftabinfo.TableName = objectname;
            ftabinfo.ObjectID = dsysschobjs["id"];

            isnode = ((Convert.ToInt32(dsysschobjs["status2"]) & 0x00000100) != 0 ? true : false);
            ftabinfo.IsNodeTable = isnode;

            isedge = ((Convert.ToInt32(dsysschobjs["status2"]) & 0x00000200) != 0 ? true : false);
            ftabinfo.IsEdgeTable = isedge;

            AllocUnitIds = new List<string>();
            foreach (Dictionary<string, string> dr in lsysallocunits)
            {
                AllocUnitIds.Add(dr["auid"]);
            }

            PartitionIds = lsysrowsets.Where(p => Convert.ToInt32(p["idminor"]) <= 1)  // index_id<=1 [sys.partitions]
                                      .Select(p => p["rowsetid"])
                                      .Distinct()
                                      .ToList();

            lstemp1 = new List<string>();
            ftabinfo.Columns = new TableColumn[lsyscolpars.Count];
            i = 0;
            foreach (Dictionary<string, string> col in lsyscolpars.OrderBy(p => Convert.ToInt32(p["colid"])))
            {
                (stemp1, fcol) = Tgetcolumn(col, ftabinfo, d);
                if (string.IsNullOrEmpty(stemp1) == false) { lstemp1.Add(stemp1); }
                ftabinfo.Columns[i] = fcol;

                i = i + 1;
            }

            columndefinition = string.Join(",\r\n ", lstemp1) + ",";

            constraint = "";
            //  primary key, unique
            if (lsysschobjs.Any(p => p["type"] == "PK" || p["type"] == "UQ") == true)
            {
                dsysschobjs = lsysschobjs.First(p => p["type"] == "PK" || p["type"] == "UQ");
                stemp4 = dsysschobjs["name"]; // constraint name
                stemp5 = (dsysschobjs["type"] == "PK" ? "primary key" : "unique"); // constraint type

                (lstemp1, lstemp2, lstemp3) = Tcreateindex_0(stemp4, "CREATE TABLE", ftabinfo);
                stemp1 = string.Join(" ", lstemp1); // index type   (clustered/nonclustered)
                stemp2 = string.Join(",", lstemp2); // index columns
                stemp3 = string.Join(",", lstemp3); // include columns

                constraint = $" constraint [{stemp4}] "
                           + $"{stemp5} {stemp1} "
                           + $"({stemp2}) "
                           + $"{(lstemp3.Count > 0 ? $"include({stemp3})" : "")}"
                           + $"\r\n";

                if (stemp5 == "primary key")
                {
                    ftabinfo.PrimaryKeyColumns = stemp2.Split(',').Select(p => p.Split(' ')[0].Replace("[", "").Replace("]", "")).ToList();
                }

                if (stemp1 == "clustered")
                {
                    ftabinfo.ClusteredIndexColumns = stemp2.Split(',').Select(p => p.Split(' ')[0].Replace("[", "").Replace("]", "")).ToList();
                }

            }

            // clustered columnstore
            if (lsysidxstats.Any(p => p["type"] == "5") == true) // CLUSTERED COLUMNSTORE
            {
                dsysidxstats = lsysidxstats.First(p => p["type"] == "5");
                constraint = $" index [{dsysidxstats["name"]}] clustered columnstore\r\n";
                ftabinfo.IsColumnStore = true;
            }
            else
            {
                ftabinfo.IsColumnStore = false;
            }

            foreach (Dictionary<string, string> dsysrowsets in lsysrowsets.Where(p => p["ownertype"] == "1"))
            {
                partitionid = Convert.ToInt64(dsysrowsets["rowsetid"]);
                Enum.TryParse<CompressionType>(dsysrowsets["cmprlevel"], out CompressionType enumval);
                ftabinfo.DataCompressionType.Add(partitionid, enumval);
            }

            ftabinfo.IsHeapTable = lsysidxstats.Any(p => p["indid"] == "0");
            ftabinfo.TextInRow = Convert.ToInt32(lsysidxstats.First(p => Convert.ToInt32(p["indid"]) <= 1)["intprop"]);
            ftabinfo.Version = DDLLogs_Tran.Max(p => p.Current_LSN);

            objectname = $"[{schemaname}].[{objectname}]";
            others = "";
            if (isnode == true)
            {
                others = others + " as node";
            }
            if (isedge == true)
            {
                others = others + " as edge";
            }

            if (d == 1)
            {
                redosql = $"create table {objectname}\r\n({columndefinition}\r\n{constraint}){others}; ";
                undosql = $"drop table {objectname}; ";
            }
            else
            {
                redosql = $"drop table {objectname}; ";
                undosql = $"create table {objectname}\r\n({columndefinition}\r\n{constraint}){others}; ";

                stk = objectname.Replace("[", "").Replace("]", "");
                UserTablesAdd(stk, ftabinfo);
            }

        }

        private void TDropObj(int d, out string objectname, out string redosql, out string undosql, out List<string> AllocUnitIds, out List<string> PartitionIds)
        {
            string schemaname, codes;
            List<string> objtypes;
            List<Dictionary<string, string>> dls;

            TranslateSystemTables(d);

            objectname = "";
            redosql = "";
            undosql = "";
            AllocUnitIds = new List<string>();
            PartitionIds = new List<string>();

            dsysschobjs = null;
            objtypes = new List<string>() { "U", "V", "P", "IF", "FN", "TF", "TR", "SN" };
            dls = lsysschobjs.Where(p => objtypes.Contains(p["type"]) == true).ToList();
            if (dls.Count == 1)
            {
                dsysschobjs = dls[0];
            }
            else
            {
                if (dls.Any(p => p["type"] == "U") == true
                    && fsysschobjs.Any(p => p["type"] == "U") == true)
                {
                    dls.RemoveAll(p => p["type"] == "U");
                    dsysschobjs = dls[0];
                }
            }

            if (dsysschobjs != null)
            {
                schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
                objectname = dsysschobjs["name"];

                switch (dsysschobjs["type"])
                {
                    case "U":
                        TCreateTable(-1, out objectname, out redosql, out undosql, out AllocUnitIds, out PartitionIds);
                        break;
                    case "V": // view
                        codes = DecodeSQLCode("V", schemaname, objectname, lsysobjvalues[0]);
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop view {objectname}; ";
                        undosql = codes;
                        break;
                    case "P": // stored procedure
                        codes = DecodeSQLCode("P", schemaname, objectname, lsysobjvalues[0]);
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop proc {objectname}; ";
                        undosql = codes;
                        break;
                    case "IF": // SQL_INLINE_TABLE_VALUED_FUNCTION
                        codes = DecodeSQLCode("IF", schemaname, objectname, lsysobjvalues[0]);
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop function {objectname}; ";
                        undosql = codes;
                        break;
                    case "FN": // SQL_SCALAR_FUNCTION
                        codes = DecodeSQLCode("FN", schemaname, objectname, lsysobjvalues[0]);
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop function {objectname}; ";
                        undosql = codes;
                        break;
                    case "TF": // SQL_TABLE_VALUED_FUNCTION
                        codes = DecodeSQLCode("TF", schemaname, objectname, lsysobjvalues[0]);
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop function {objectname}; ";
                        undosql = codes;
                        break;
                    case "TR": // trigger
                        codes = DecodeSQLCode("TR", schemaname, objectname, lsysobjvalues.FirstOrDefault(p => p["imageval"] != "nullvalue"));
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop trigger {objectname}; ";
                        undosql = codes;
                        break;
                    case "SN": // synonym
                        objectname = $"[{schemaname}].[{objectname}]";
                        redosql = $"drop synonym {objectname}; ";
                        undosql = $"create synonym {objectname} for {lsysobjvalues[0]["value"]}; ";
                        break;
                }
            }

        }

        private void UserTablesAdd(string tablename, TableInfo tableinfo)
        {
            if (UserTables.ContainsKey(tablename) == false)
            { 
                UserTables.Add(tablename, new List<TableInfo>() { tableinfo });
            }
            else
            {
                UserTables[tablename].Add(tableinfo);
            }
        }

        private void TUserTransaction(int d, out string objectname, out string redosql, out string undosql)
        {
            string optionname, val0, val1, schemaname, curlsn;
            Dictionary<string, string> sysidxstats0, sysidxstats1, syscolpars0, syscolpars1;
            List<DiffColumn> diff;
            TableInfo tableinfo, tableinfo0;
            TableColumn tabcol0;

            TranslateSystemTables(d);

            objectname = "";
            redosql = "";
            undosql = "";
            curlsn = DDLLogs_Tran.Max(p => p.Current_LSN);

            // set table option [text in row]
            if (fsysidxstats.Count(p => Convert.ToInt32(p["indid"]) <= 1) > 0
                && fsysidxstats0.Count(p => Convert.ToInt32(p["indid"]) <= 1) > 0)
            {
                sysidxstats1 = fsysidxstats.First(p => Convert.ToInt32(p["indid"]) <= 1);
                sysidxstats0 = fsysidxstats0.First(p => Convert.ToInt32(p["indid"]) <= 1);

                diff = DDL_Compare(sysidxstats0, sysidxstats1);
                if (diff.Any(p => p.ColumnName == "intprop") == true)
                {
                    dsysschobjs = fsysschobjs.First(p => p["id"] == sysidxstats1["id"]);
                    schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
                    objectname = dsysschobjs["name"];
                    tableinfo = GetTableInfo(schemaname, objectname, curlsn, true);

                    optionname = "text in row";
                    val0 = diff.First(p => p.ColumnName == "intprop").OldValue;
                    val1 = diff.First(p => p.ColumnName == "intprop").NewValue;
                    
                    redosql = $"exec sp_tableoption N'{schemaname}.{objectname}','{optionname}','{val1}'; ";
                    undosql = $"exec sp_tableoption N'{schemaname}.{objectname}','{optionname}','{val0}'; ";

                    tableinfo0 = tableinfo.FCopy();
                    tableinfo0.TextInRow = Convert.ToInt32(val0);
                    tableinfo0.Version = curlsn;
                    UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                    objectname = $"[{schemaname}].[{objectname}]";
                }
            }

            // rename column
            if (fsyscolpars.Count == 1 
                && fsyscolpars0.Count == 1)
            {
                syscolpars0 = fsyscolpars0.First();
                syscolpars1 = fsyscolpars.First();

                diff = DDL_Compare(syscolpars0, syscolpars1);
                if (diff.Any(p => p.ColumnName == "name") == true)
                {
                    dsysschobjs = fsysschobjs.First(); // p => p["id"] == syscolpars1["id"]
                    schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
                    objectname = dsysschobjs["name"];
                    tableinfo = GetTableInfo(schemaname, objectname, curlsn, true);

                    val0 = diff.First(p => p.ColumnName == "name").OldValue;
                    val1 = diff.First(p => p.ColumnName == "name").NewValue;
                    
                    redosql = $"exec sp_rename N'{schemaname}.{objectname}.{val0}',N'{val1}','COLUMN'; ";
                    undosql = $"exec sp_rename N'{schemaname}.{objectname}.{val1}',N'{val0}','COLUMN'; ";

                    tableinfo0 = tableinfo.FCopy();
                    tabcol0 = tableinfo0.Columns.First(p => p.ColumnName == val1);
                    tabcol0.ColumnName = val0;
                    tableinfo0.Version = curlsn;
                    UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                    objectname = $"[{schemaname}].[{objectname}]";
                }
            }

        }

        private void TAlterTable(out string objectname, out string redosql, out string undosql, out List<string> AllocUnitIds, out List<string> PartitionIds, out bool ignoretran)
        {
            string coldef, coldef0, schemaname, curlsn, val0, val1, constraintname, indextype, indexcolumns, includecolumns, objectid;
            List<DiffColumn> diff;
            TableInfo tableinfo, tableinfo0;
            TableColumn tablecol, tablecol0;
            int i;
            List<TableColumn> dtcols, cols0;
            Dictionary<string, string> sysrowsets0, sysrowsets1;
            List<string> allocunitids, findextype, findexcolumns, fincludecolumns;
            long partitionid;
            DiffColumn fd;

            TranslateSystemTables(1);

            AllocUnitIds = new List<string>();
            PartitionIds = new List<string>();
            dtcols = new List<TableColumn>();
            curlsn = DDLLogs_Tran.Max(p => p.Current_LSN);
            
            schemaname = "";
            objectname = "";
            tableinfo = new TableInfo();
            dsysschobjs = fsysschobjs.First(p => p["type"] == "U" || p["type"] == "PK");
            if (dsysschobjs["type"] == "U")
            {
                schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
                objectname = $"{dsysschobjs["name"]}";
                tableinfo = GetTableInfo(schemaname, objectname, curlsn, true);
            }
            if (dsysschobjs["type"] == "PK")
            {
                objectid = dsysschobjs["pid"];
                tableinfo = GetTableInfoByObjectID(objectid, curlsn);
                schemaname = tableinfo.SchemaName;
                objectname = tableinfo.TableName;
            }

            redosql = $"alter table [{schemaname}].[{objectname}] ";
            undosql = $"alter table [{schemaname}].[{objectname}] ";

            ignoretran = false;
            if (fsysschobjs.Any(p => p["type"] == "U") == true && fsysschobjs0.Any(p => p["type"] == "U") == true)
            {
                diff = DDL_Compare(fsysschobjs0.First(p => p["type"] == "U"), fsysschobjs.First(p => p["type"] == "U"));
                if (diff.Count == 1 
                    && diff.Any(p => p.ColumnName == "modified")
                    && DDLLogs_Tran.Any(p => p.AllocUnitName != null && (p.AllocUnitName ?? "") != "sys.sysschobjs.clst") == false)
                {
                    ignoretran = true;
                    redosql = "";
                    undosql = "";
                }
            }

            if (ignoretran == false)
            {
                // add column
                if (fsyscolpars.Any() == true && fsyscolpars0.Any() == false)
                {
                    redosql = redosql + "add ";
                    undosql = undosql + "drop column ";

                    for (i = 0; i <= fsyscolpars.Count - 1; i = i + 1)
                    {
                        (coldef, tablecol) = Tgetcolumn(fsyscolpars[i], tableinfo, 1);
                        redosql = redosql + coldef + (i < fsyscolpars.Count - 1 ? ", " : "; ");
                        undosql = undosql + $"[{tablecol.ColumnName}]" + (i < fsyscolpars.Count - 1 ? ", " : "; ");
                        dtcols.Add(tablecol);
                    }

                    tableinfo0 = tableinfo.FCopy();
                    cols0 = new List<TableColumn>();
                    i = 1;
                    foreach (TableColumn col in tableinfo.Columns.OrderBy(p => p.ColumnID))
                    {
                        if (dtcols.Any(dc => dc.ColumnName == col.ColumnName) == false)
                        {
                            col.ColumnID = Convert.ToInt16(i);
                            cols0.Add(col);
                            i = i + 1;
                        }
                    }
                    tableinfo0.Columns = cols0.ToArray();
                    tableinfo0.Version = curlsn;
                    UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                }

                // drop column
                if (fsyscolpars.Any() == false && fsyscolpars0.Any() == true)
                {
                    redosql = redosql + "drop column ";
                    undosql = undosql + "add ";

                    for (i = 0; i <= fsyscolpars0.Count - 1; i = i + 1)
                    {
                        (coldef, tablecol) = Tgetcolumn(fsyscolpars0[i], tableinfo, -1);
                        redosql = redosql + $"[{tablecol.ColumnName}]" + (i < fsyscolpars0.Count - 1 ? ", " : "; ");
                        undosql = undosql + coldef + (i < fsyscolpars0.Count - 1 ? ", " : "; ");
                        dtcols.Add(tablecol);
                    }

                    tableinfo0 = tableinfo.FCopy();
                    cols0 = tableinfo0.Columns.ToList();
                    foreach (TableColumn col in dtcols)
                    {
                        cols0.Add(col);
                    }
                    tableinfo0.Columns = cols0.OrderBy(p => p.ColumnID).ToArray();
                    tableinfo0.Version = curlsn;
                    UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                }

                // alter column
                if (fsyscolpars.Any() == true && fsyscolpars0.Any() == true)
                {
                    for (i = 0; i <= fsyscolpars.Count - 1; i = i + 1)
                    {
                        (coldef, tablecol) = Tgetcolumn(fsyscolpars[i], tableinfo, 1);
                        (coldef0, tablecol0) = Tgetcolumn(fsyscolpars0[i], tableinfo, -1);

                        if (tablecol.HasDefaultValue != tablecol0.HasDefaultValue)
                        {
                            if (tablecol.HasDefaultValue == true)
                            {
                                // add default constraint
                                redosql = redosql
                                          + (i == 0 ? "add " : ", ")
                                          + $"constraint [{tablecol.DefaultConstraintName}] default {tablecol.DefaultValue} for [{tablecol.ColumnName}]"
                                          + (i == fsyscolpars.Count - 1 ? "; " : "");
                                undosql = undosql
                                          + (i == 0 ? "drop " : ", ")
                                          + $"constraint [{tablecol.DefaultConstraintName}]"
                                          + (i == fsyscolpars.Count - 1 ? "; " : "");
                            }
                            else
                            {
                                // drop default constraint
                                redosql = redosql
                                          + (i == 0 ? "drop " : ", ")
                                          + $"constraint [{tablecol0.DefaultConstraintName}]"
                                          + (i == fsyscolpars.Count - 1 ? "; " : "");
                                undosql = undosql
                                          + (i == 0 ? "add " : ", ")
                                          + $"constraint [{tablecol0.DefaultConstraintName}] default {tablecol0.DefaultValue} for [{tablecol0.ColumnName}]"
                                          + (i == fsyscolpars.Count - 1 ? "; " : "");
                            }
                        }
                        else
                        {
                            redosql = redosql + "alter column " + coldef + "; ";
                            undosql = undosql + "alter column " + coldef0 + "; ";
                        }

                        dtcols.Add(tablecol0);
                    }

                    tableinfo0 = tableinfo.FCopy();
                    cols0 = tableinfo0.Columns.ToList();
                    cols0.RemoveAll(p => dtcols.Select(d => d.ColumnID).Contains(p.ColumnID) == true);
                    foreach (TableColumn col in dtcols)
                    {
                        cols0.Add(col);
                    }
                    tableinfo0.Columns = cols0.OrderBy(p => p.ColumnID).ToArray();
                    tableinfo0.Version = curlsn;
                    UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                }

                // rebuild with(data_compression=xxx)
                if (fsysrowsets.Any() == true && fsysrowsets0.Any() == true)
                {
                    sysrowsets0 = fsysrowsets0.First();
                    sysrowsets1 = fsysrowsets.First();
                    diff = DDL_Compare(sysrowsets0, sysrowsets1);
                    if (diff.Any(p => p.ColumnName == "cmprlevel") == true)
                    {
                        val0 = Enum.GetName(typeof(CompressionType), Convert.ToInt32(diff.First(p => p.ColumnName == "cmprlevel").OldValue));
                        val1 = Enum.GetName(typeof(CompressionType), Convert.ToInt32(diff.First(p => p.ColumnName == "cmprlevel").NewValue));

                        redosql = redosql + $"rebuild with(data_compression={val1}); ";
                        undosql = undosql + $"rebuild with(data_compression={val0}); ";

                        allocunitids = DDLLogs_Tran.Where(p => p.AllocUnitId != null).Select(p => p.AllocUnitId.ToString()).Distinct().ToList();
                        AllocUnitIds.AddRange(allocunitids);

                        PartitionIds = fsysrowsets0.Where(p => Convert.ToInt32(p["idminor"]) <= 1).Select(p => p["rowsetid"]).Distinct().ToList(); // index_id<=1 [sys.partitions]

                        tableinfo0 = tableinfo.FCopy();

                        tableinfo0.DataCompressionType = new Dictionary<long, CompressionType>();
                        foreach (Dictionary<string, string> dsysrowsets in fsysrowsets0.Where(p => p["ownertype"] == "1"))
                        {
                            partitionid = Convert.ToInt64(dsysrowsets["rowsetid"]);
                            Enum.TryParse<CompressionType>(dsysrowsets["cmprlevel"], out CompressionType enumval);
                            tableinfo0.DataCompressionType.Add(partitionid, enumval);
                        }

                        cols0 = tableinfo0.Columns.ToList();
                        foreach (TableColumn col in cols0)
                        {
                            col.LeafOffset = (short)(Convert.ToInt32(fsysrscols0.First(p => p["rscolid"] == col.ColumnID.ToString())["offset"]) & 0xFFFF); // convert(smallint, convert(binary(2), c.offset & 0xffff))    [sys.system_internals_partition_columns]
                            col.LeafNullBit = (short)(Convert.ToInt32(fsysrscols0.First(p => p["rscolid"] == col.ColumnID.ToString())["nullbit"]) & 0xFFFF); // convert(smallint, convert(binary(2), c.nullbit & 0xffff))  [sys.system_internals_partition_columns]
                        }

                        tableinfo0.Columns = cols0.OrderBy(p => p.ColumnID).ToArray();
                        tableinfo0.Version = curlsn;
                        UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);
                    }

                }

                // add/drop constraint
                if (fsysidxstats.Any() == true || fsysidxstats0.Any() == true)
                {
                    diff = DDL_Compare((fsysidxstats0.Any() == true ? fsysidxstats0[0] : null),
                                       (fsysidxstats.Any() == true ? fsysidxstats[0] : null)
                                      );
                    fd = diff.FirstOrDefault(p => p.ColumnName == "indid");

                    // add constraint
                    if (fd != null
                        && (fd.OldValue == "0" || fd.OldValue == "") // sys.indexes.index_id =0(heap)
                        && fd.NewValue != "0") // sys.indexes.index_id =1(clustered index) >1(nonclustered index)
                    {
                        dsysidxstats = fsysidxstats.First();
                        constraintname = dsysidxstats["name"];

                        (findextype, findexcolumns, fincludecolumns) = Tcreateindex_0(constraintname, "ALTER TABLE", tableinfo);
                        if (fsysschobjs.Any(p => p["type"] == "PK" && p["name"] == constraintname) == true 
                            && fsysschobjs0.Any(p => p["type"] == "PK" && p["name"] == constraintname) == false)
                        {
                            findextype.Insert(0, "primary key");
                            if (findextype.Contains("unique") == true) { findextype.Remove("unique"); }
                        }

                        indextype = string.Join(" ", findextype);
                        indexcolumns = string.Join(",", findexcolumns);
                        includecolumns = string.Join(",", fincludecolumns);

                        redosql = redosql + $"add constraint [{constraintname}] {indextype} ({indexcolumns}); ";
                        undosql = undosql + $"drop constraint [{constraintname}]; ";

                        tableinfo0 = tableinfo.FCopy();
                        tableinfo0.IsHeapTable = true;
                        tableinfo0.PrimaryKeyColumns = findextype.Contains("primary key") == true ? new List<string>() : tableinfo.PrimaryKeyColumns;
                        tableinfo0.ClusteredIndexColumns = findextype.Contains("clustered") == true ? new List<string>() : tableinfo.ClusteredIndexColumns;
                        tableinfo0.Version = curlsn;
                        UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                    }

                    // drop constraint
                    if (fd != null
                        && fd.OldValue != "0" // sys.indexes.index_id =0(heap)
                        && (fd.NewValue == "0" || fd.NewValue == "")) // sys.indexes.index_id =1(clustered index) >1(nonclustered index)
                    {
                        dsysidxstats = fsysidxstats0.First();
                        constraintname = dsysidxstats["name"];

                        SET_LSystemTables(-1);
                        (findextype, findexcolumns, fincludecolumns) = Tcreateindex_0(constraintname, "ALTER TABLE", tableinfo);
                        if (fsysschobjs0.Any(p => p["type"] == "PK" && p["name"] == constraintname) == true
                            && fsysschobjs.Any(p => p["type"] == "PK" && p["name"] == constraintname) == false)
                        {
                            findextype.Insert(0, "primary key");
                            if (findextype.Contains("unique") == true) { findextype.Remove("unique"); }
                        }

                        indextype = string.Join(" ", findextype);
                        indexcolumns = string.Join(",", findexcolumns);
                        includecolumns = string.Join(",", fincludecolumns);

                        redosql = redosql + $"drop constraint [{constraintname}]; ";
                        undosql = undosql + $"add constraint [{constraintname}] {indextype} ({indexcolumns}); ";

                        tableinfo0 = tableinfo.FCopy();
                        tableinfo0.IsHeapTable = false;
                        tableinfo0.PrimaryKeyColumns = findextype.Contains("primary key") == true ? 
                                                         findexcolumns.Select(p => Regex.Match(p, @"^\[([^\]]+)\]\s+(?:asc|desc)$", RegexOptions.IgnoreCase).Groups[1].Value).ToList()
                                                         : new List<string>();
                        tableinfo0.ClusteredIndexColumns = findextype.Contains("clustered") == true ? 
                                                             findexcolumns.Select(p => Regex.Match(p, @"^\[([^\]]+)\]\s+(?:asc|desc)$", RegexOptions.IgnoreCase).Groups[1].Value).ToList()
                                                             : new List<string>();
                        tableinfo0.Version = curlsn;
                        UserTablesAdd($"{schemaname}.{objectname}", tableinfo0);

                    }

                }

            }

            objectname = $"[{schemaname}].[{objectname}]";
        }

        private TableInfo GetTableInfoByObjectID(string objectid, string curlsn)
        {
            TableInfo tableinfo;
            string schemaname, tablename;

            tableinfo = UserTables.SelectMany(p => p.Value)
                                  .Where(t => t.ObjectID == objectid
                                              && string.Compare(t.Version, curlsn) > 0)
                                  .OrderBy(t => t.Version)
                                  .FirstOrDefault();
            if (tableinfo == null 
                && string.IsNullOrEmpty(objectid) == false)
            {
                tsql = $"select SchemaName=s.name,TableName=o.name "
                       + $"from sys.objects o "
                       + $"join sys.schemas s on o.schema_id=s.schema_id "
                       + $"where o.type='U' "
                       + $"and o.object_id={objectid}; ";
                (schemaname, tablename) = DB.Query<(string, string)>(tsql, false).FirstOrDefault();
                if (string.IsNullOrEmpty(schemaname) == false && string.IsNullOrEmpty(tablename) == false)
                {
                    tableinfo = GetTableInfo(schemaname, tablename);
                }
            }

            return tableinfo;
        }

        private void TTruncateTable(out string objectname, out string redosql, out string undosql)
        {
            string allocunitname, schemaname, tabname, objectid, lockinfo;
            KeyValuePair<string, List<TableInfo>> tab;
            TransactionInfo rtran;
            List<string> allocunitids;

            TranslateSystemTables(1);

            schemaname = "";
            objectname = "";
            redosql = "";
            undosql = "";

            allocunitname = DDLLogs_Tran.Select(p => p.AllocUnitName)
                                        .Where(p => string.IsNullOrEmpty(p) == false && p != "Unknown Alloc Unit" && p.StartsWith("sys.") == false)
                                        .Distinct()
                                        .FirstOrDefault();
            if (string.IsNullOrEmpty(allocunitname) == false)
            {
                schemaname = allocunitname.Split('.')[0];
                objectname = allocunitname.Split('.')[1];
            }

            if (string.IsNullOrEmpty(schemaname) == true && string.IsNullOrEmpty(objectname) == true)
            {
                if (fsyscolpars.Any() == true)
                {
                    objectid = fsyscolpars.First()["id"];
                    tab = UserTables.FirstOrDefault(p => p.Value.Any(x => x.ObjectID == objectid) == true);
                    if (tab.Key != null)
                    {
                        tabname = tab.Key;
                        schemaname = tabname.Split('.')[0];
                        objectname = tabname.Split('.')[1];
                    }
                }
            }

            if (string.IsNullOrEmpty(schemaname) == true && string.IsNullOrEmpty(objectname) == true)
            {
                allocunitids = DDLLogs_Tran.Where(p => p.AllocUnitName == "Unknown Alloc Unit" && p.AllocUnitId != null)
                                           .Select(p =>(p.AllocUnitId ?? 0).ToString())
                                           .ToList();
                rtran = DDLLogs_FTranID.Where(p => string.Compare(p.TransactionID, DDLLogs_Tran.First().Transaction_ID) > 0
                                                   && p.TransactionName == "DROPOBJ"
                                                   && p.AllocUnitId.Intersect(allocunitids).Any() == true)
                                       .OrderBy(p => p.TransactionID)
                                       .FirstOrDefault();
                if (rtran != null)
                {
                    tabname = rtran.AllocUnitName;
                    schemaname = tabname.Split('.')[0].Replace("[", "").Replace("]", "");
                    objectname = tabname.Split('.')[1].Replace("[", "").Replace("]", "");
                }
            }

            if (string.IsNullOrEmpty(schemaname) == true && string.IsNullOrEmpty(objectname) == true)
            {
                lockinfo = DDLLogs_Tran.FirstOrDefault(p => string.IsNullOrEmpty(p.Lock_Information) == false)?.Lock_Information;
                if (string.IsNullOrEmpty(lockinfo) == false) // demo value: HoBt 0:ACQUIRE_LOCK_SCH_M OBJECT: 5:1077578877:0 
                {
                    objectid = lockinfo.Split(new string[] { "OBJECT: " }, StringSplitOptions.None)[1].Split(':')[1];
                    tab = UserTables.FirstOrDefault(p => p.Value.Any(x => x.ObjectID == objectid) == true);
                    if (tab.Key != null)
                    {
                        tabname = tab.Key;
                        schemaname = tabname.Split('.')[0];
                        objectname = tabname.Split('.')[1];
                    }
                }
            }

            if (string.IsNullOrEmpty(schemaname) == false && string.IsNullOrEmpty(objectname) == false)
            {
                redosql = $"truncate table [{schemaname}].[{objectname}]; ";
                undosql = $"";
                objectname = $"[{schemaname}].[{objectname}]";
            }

        }

        private void TCreateIndex(out string objectname, out string redosql, out string undosql)
        {
            string schemaname, indexname, indextype, indexcolumns, includecolumns, curlsn;
            List<string> findextype, findexcolumns, fincludecolumns;

            TranslateSystemTables(1);

            curlsn = DDLLogs_Tran.Max(p => p.Current_LSN);
            dsysschobjs = lsysschobjs.First(p => p["type"] == "U");
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];
            FTableInfo = GetTableInfo(schemaname, objectname, curlsn);

            dsysidxstats = lsysidxstats.First();
            indexname = dsysidxstats["name"];

            (findextype, findexcolumns, fincludecolumns) = Tcreateindex_0(indexname, "CREATE INDEX", FTableInfo);
            indextype = string.Join(" ", findextype);
            indexcolumns = string.Join(",", findexcolumns);
            includecolumns = string.Join(",", fincludecolumns);
            
            redosql = $"create {indextype} index [{indexname}] on [{schemaname}].[{objectname}]({indexcolumns}){(fincludecolumns.Count > 0 ? $" include({includecolumns})" : "")}; ";
            undosql = $"drop index [{indexname}] on [{schemaname}].[{objectname}]; ";
            objectname = $"[{schemaname}].[{objectname}].[{indexname}]";

        }

        private void TDropIndex(out string objectname, out string redosql, out string undosql)
        {
            string schemaname, indexname, indextype, indexcolumns, includecolumns, curlsn;
            List<string> findextype, findexcolumns, fincludecolumns;

            TranslateSystemTables(-1);

            curlsn = DDLLogs_Tran.Max(p => p.Current_LSN);
            dsysschobjs = lsysschobjs.First(p => p["type"] == "U");
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];
            FTableInfo = GetTableInfo(schemaname, objectname, curlsn);

            dsysidxstats = lsysidxstats.First();
            indexname = dsysidxstats["name"];

            (findextype, findexcolumns, fincludecolumns) = Tcreateindex_0(indexname, "DROP INDEX", FTableInfo);
            indextype = string.Join(" ", findextype);
            indexcolumns = string.Join(",", findexcolumns);
            includecolumns = string.Join(",", fincludecolumns);

            redosql = $"drop index [{indexname}] on [{schemaname}].[{objectname}]; ";
            undosql = $"create {indextype} index [{indexname}] on [{schemaname}].[{objectname}]({indexcolumns}){(fincludecolumns.Count > 0 ? $" include({includecolumns})" : "")}; ";
            objectname = $"[{schemaname}].[{objectname}].[{indexname}]";

        }

        private void TCreateAlterSQLModule(string objecttype, out string objectname, out string redosql, out string undosql)
        {
            string schemaname, typename, codes0, codes1;
            List<string> typelist;

            TranslateSystemTables(1);
            
            if (objecttype == "FUN")
            {
                typelist = new List<string>() 
                {
                    "IF",  // SQL_INLINE_TABLE_VALUED_FUNCTION
                    "FN",  // SQL_SCALAR_FUNCTION
                    "TF"   // SQL_TABLE_VALUED_FUNCTION
                };
            }
            else
            {
                typelist = new List<string>() { objecttype };
            }

            dsysschobjs = lsysschobjs.First(p => typelist.Contains(p["type"]) == true);
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];

            switch (objecttype)
            {
                case "V":
                    typename = "view";
                    break;
                case "P":
                    typename = "proc";
                    break;
                case "FUN":
                    typename = "function";
                    break;
                case "TR":
                    typename = "trigger";
                    break;
                default:
                    typename = "";
                    break;
            }

            codes1 = DecodeSQLCode(dsysschobjs["type"], schemaname, objectname, lsysobjvalues[0]);
            redosql = codes1;

            undosql = $"drop {typename} [{schemaname}].[{objectname}]; ";
            if (fsysschobjs.Any(p => p["type"] != "U") == true 
                && fsysschobjs0.Any(p => p["type"] != "U") == true)
            {
                redosql = $"{undosql}\r\ngo" 
                          + "\r\n" 
                          + redosql;

                codes0 = DecodeSQLCode(dsysschobjs["type"], schemaname, objectname, fsysobjvalues0[0]);
                undosql = $"{undosql}\r\ngo"
                          + "\r\n"  
                          + codes0;
            }

            objectname = $"[{schemaname}].[{objectname}]";
        }

        private void TCreateSynonym(out string objectname, out string redosql, out string undosql)
        {
            string schemaname;

            TranslateSystemTables(1);

            dsysschobjs = lsysschobjs.First();
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];
            
            redosql = $"create synonym [{schemaname}].[{objectname}] for {lsysobjvalues[0]["value"]}; ";
            undosql = $"drop synonym [{schemaname}].[{objectname}]; ";
            objectname = $"[{schemaname}].[{objectname}]";
        }

        private void TCreateType(out string objectname, out string redosql, out string undosql)
        {
            string schemaname, defins;

            TranslateSystemTables(1);

            dsysscalartypes = lsysscalartypes.First();
            schemaname = Schemas[Convert.ToInt32(dsysscalartypes["schid"])];
            objectname = dsysscalartypes["name"];

            defins = GetTypeDefinition(dsysscalartypes);

            redosql = $"create type [{schemaname}].[{objectname}] from {defins}; ";
            undosql = $"drop type [{schemaname}].[{objectname}]; ";
            objectname = $"[{schemaname}].[{objectname}]";
        }

        private void TDropType(out string objectname, out string redosql, out string undosql)
        {
            string schemaname, basetype, defins;

            TranslateSystemTables(-1);

            dsysscalartypes = lsysscalartypes.First();
            schemaname = Schemas[Convert.ToInt32(dsysscalartypes["schid"])];
            objectname = dsysscalartypes["name"];

            defins = GetTypeDefinition(dsysscalartypes);

            redosql = $"drop type [{schemaname}].[{objectname}]; ";
            undosql = $"create type [{schemaname}].[{objectname}] from {defins}; ";
            objectname = $"[{schemaname}].[{objectname}]";
        }

        private string GetTypeDefinition(Dictionary<string, string> psysscalartypes)
        {
            string basetype, defins, typelen, nullable;

            tsql = $"select top 1 name from sys.systypes where xusertype={dsysscalartypes["xtype"]} ";
            basetype = DB.Query11(tsql, false);

            typelen = dsysscalartypes["length"];
            if (dsysscalartypes["prec"] != "0")
            {
                typelen = $"{dsysscalartypes["prec"]},{dsysscalartypes["scale"]}";
            }

            nullable = (dsysscalartypes["status"] == "1" ? "not null" : "null");

            defins = $"[{basetype}]({typelen}) {nullable}";

            return defins;
        }

        private string DecodeSQLCode
            (
              string objtype,
              string schemaname,
              string objectname,    
              Dictionary<string, string> psysobjvalue // sys.sysobjvalues
            ) 
        {
            string r, imageval, fake;
            byte[] imagevalBA, imagevalBAF, fakeba, decryptedba, objectidba, dbguidba, subobjba, rc4hashba, rc4key;
            bool isexists;
            int i, objid;
            short subobjval;

            r = string.Empty;
            imageval = psysobjvalue["imageval"];
            if (string.IsNullOrEmpty(imageval) == false)
            {
                imagevalBA = imageval.Stuff(0, 2, "").ToByteArray();

                if (psysobjvalue["value"] == "2") // without encryption
                {
                    r = Encoding.UTF8.GetString(imagevalBA);
                }

                if (psysobjvalue["value"] == "0") // with encryption
                {
                    //tsql = $"select isexists=cast(case when object_id(N'[{schemaname}].[{objectname}]',N'{objtype}')={psysobjvalue["objid"]} then 1 else 0 end as bit) ";
                    //isexists = DB.Query<bool>(tsql, false).FirstOrDefault();
                    isexists = false; // only use RC4 for decode

                    if (isexists == true) 
                    {
                        fake = "";
                        switch (objtype)
                        {
                            case "V":
                                fake = $"alter view [{schemaname}].[{objectname}] with encryption as select X='{new string('-', 40003)}'; ";
                                break;
                            case "P":
                                fake = $"alter proc [{schemaname}].[{objectname}] with encryption as select X='{new string('-', 40003)}'; ";
                                break;
                        }

                        if (string.IsNullOrEmpty(fake) == false)
                        {
                            DB_DAC.ExecuteSQL("begin tran;", false);
                            DB_DAC.ExecuteSQL(fake, false);
                            tsql = $"select top 1 imageval from sys.sysobjvalues with(nolock) where objid=object_id(N'[{schemaname}].[{objectname}]',N'{objtype}') and valclass=1 ";
                            imagevalBAF = DB_DAC.Query<byte[]>(tsql, false).FirstOrDefault();
                            DB_DAC.ExecuteSQL("rollback tran;", true);
                            
                            fake = fake.Stuff(0, 6, "create ");
                            fakeba = Encoding.Unicode.GetBytes(fake);

                            decryptedba = new byte[imagevalBA.Length];
                            for (i = 0; i <= imagevalBA.Length - 1; i = i + 1)
                            {
                                decryptedba[i] = (byte)(imagevalBA[i] ^ (fakeba[i] ^ imagevalBAF[i]));
                            }
                            r = Encoding.Unicode.GetString(decryptedba);
                        }
                    }
                    else
                    {
                        // Reference: https://www.sql.kiwi/2016/05/the-internals-of-with-encryption/
                        dbguidba = Guid.Parse(DB.FamilyGuid).ToByteArray();

                        objid = Convert.ToInt32(psysobjvalue["objid"]);
                        objectidba = BitConverter.GetBytes(objid);

                        subobjval = Convert.ToInt16(objtype == "P" ? 1 : 0);
                        subobjba = BitConverter.GetBytes(subobjval);

                        rc4hashba = new byte[dbguidba.Length + objectidba.Length + subobjba.Length];
                        Buffer.BlockCopy(dbguidba, 0, rc4hashba, 0, dbguidba.Length);
                        Buffer.BlockCopy(objectidba, 0, rc4hashba, dbguidba.Length, objectidba.Length);
                        Buffer.BlockCopy(subobjba, 0, rc4hashba, dbguidba.Length + objectidba.Length, subobjba.Length);
                        
                        using (var sha1 = System.Security.Cryptography.SHA1.Create())
                        {
                            rc4key = sha1.ComputeHash(rc4hashba);
                        }

                        decryptedba = FCommon.DecryptRC4(rc4key, imagevalBA);
                        r = Encoding.Unicode.GetString(decryptedba);
                    }

                    while (r.StartsWith("-*"))
                    {
                        r = r.Stuff(0, 2, "");
                    }
                }

                while (r.StartsWith("\r\n"))
                {
                    r = r.Substring(2);
                }
            }

            return r;
        }

        private List<DatabaseLog> T_SELECT_INTO_INSERT(string TransactionID, string objectname)
        {
            List<DatabaseLog> r;
            DatabaseLog dr;
            string tranid, schemaname, tablename, BeginTime, EndTime, REDOSQL, UNDOSQL, ColumnList, ValueList1, WhereList0, Value;
            FPageInfo fpage;
            int i, j;
            byte[] rc0;

            tranid = SELECTINTO_RS[TransactionID].First().Transaction_ID;
            BeginTime = SELECTINTO_RS[TransactionID].First().Begin_Time;
            EndTime = SELECTINTO_RS[TransactionID].Last().End_Time;

            schemaname = objectname.Split('.')[0].Replace("[", "").Replace("]", "");
            tablename = objectname.Split('.')[1].Replace("[", "").Replace("]", "");
            FTableInfo = GetTableInfo(schemaname, tablename, DDLLogs_Tran.Max(p => p.Current_LSN), false);
            ColumnList = string.Join(",", FTableInfo.Columns
                                          .Where(p => p.PhysicalStorageType != SqlDbType.Timestamp
                                                      && p.IsComputed == false
                                                      && p.IsHidden == false)
                                          .Select(p => $"[{p.ColumnName}]"));

            r = new List<DatabaseLog>();
            foreach (FLOG fplog in SELECTINTO_RS[TransactionID].Where(p => p.Operation == "LOP_FORMAT_PAGE").OrderBy(p => p.Current_LSN))
            {
                GetPrevPages(fplog.Current_LSN);
                if (PrevPages.Any(p => p.pageid == fplog.Page_ID) == false)
                {
                    fpage = GetPageInfo(fplog.Page_ID);
                    for (i = 0; i <= fpage.SlotCnt - 1; i = i + 1)
                    {
                        rc0 = fpage.SlotData[i].ToByteArray();
                        TranslateData(rc0, FTableInfo.Columns);
                        ValueList1 = "";
                        WhereList0 = "";
                        for (j = 0; j <= FTableInfo.Columns.Length - 1; j++)
                        {
                            if (FTableInfo.Columns[j].PhysicalStorageType == SqlDbType.Timestamp
                                || FTableInfo.Columns[j].IsComputed == true
                                || FTableInfo.Columns[j].IsHidden == true)
                            {
                                continue;
                            }

                            Value = ColumnValue2SQLValue(FTableInfo.Columns[j]);
                            ValueList1 = ValueList1 + (ValueList1.Length > 0 ? "," : "") + Value;
                            WhereList0 = WhereList0
                                         + (WhereList0.Length > 0 ? " and " : "")
                                         + ColumnName2SQLName(FTableInfo.Columns[j])
                                         + (FTableInfo.Columns[j].IsNull ? " is " : "=")
                                         + Value;
                        }
                        REDOSQL = $"insert into {objectname}({ColumnList}) values({ValueList1}); ";
                        UNDOSQL = $"delete top(1) from {objectname} where {WhereList0}; ";

                        dr = new DatabaseLog();
                        dr.LSN = DDLLogs_Tran.First().Current_LSN;
                        dr.Type = "DML";
                        dr.TransactionID = tranid;
                        dr.BeginTime = DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                        dr.EndTime = DateTime.ParseExact(EndTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                        dr.Operation = "SELECT INTO_INSERT";
                        dr.Message = "";
                        dr.ObjectName = objectname;
                        dr.RedoSQL = REDOSQL;
                        dr.UndoSQL = UNDOSQL;

                        r.Add(dr);
                    }
                }
                else
                {
                    // NO ROW DATA in SELECT INTO Transaction
                    dr = new DatabaseLog();
                    dr.LSN = DDLLogs_Tran.First().Current_LSN;
                    dr.Type = "DML";
                    dr.TransactionID = tranid;
                    dr.BeginTime = DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                    dr.EndTime = DateTime.ParseExact(EndTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
                    dr.Operation = "SELECT INTO_INSERT";
                    dr.Message = "";
                    dr.ObjectName = objectname;
                    dr.RedoSQL = "";
                    dr.UndoSQL = "";

                    r.Add(dr);
                }

            }

            return r;
        }

        private List<DiffColumn> DDL_Compare(Dictionary<string, string> dr0, Dictionary<string, string> dr1)
        {
            List<DiffColumn> r;
            DiffColumn f;

            r = new List<DiffColumn>();
            if (dr0 != null && dr0.Count > 0)
            {
                foreach (string key in dr0.Keys)
                {
                    if (dr1 != null && dr1.ContainsKey(key) == true)
                    {
                        if (dr0[key] != dr1[key])
                        {
                            f = new DiffColumn() { ColumnName = key, OldValue = dr0[key], NewValue = dr1[key] };
                            r.Add(f);
                        }
                    }
                    else
                    {
                        f = new DiffColumn() { ColumnName = key, OldValue = dr0[key], NewValue = "" };
                        r.Add(f);
                    }
                }
            }
            else
            {
                if (dr1 != null && dr1.Count > 0)
                {
                    foreach (string key in dr1.Keys)
                    {
                        f = new DiffColumn() { ColumnName = key, OldValue = "", NewValue = dr1[key] };
                        r.Add(f);
                    }
                }
            }

            return r;
        }

        private (string columndefin, TableColumn tabcol) Tgetcolumn
            (
                Dictionary<string, string> dsyscolpars, 
                TableInfo ftabinfo,
                int d
            )
        {
            string columndefin, columnname, datatype, graphtype, collationname, constraintname, defaultvalue, computedcolumndefin, temp, partitionid;
            short maxlength, colid;
            long seed, increment;
            bool nullable, isidentity, iscomputed, ishidden;
            TableColumn fcol;
            Dictionary<string, string> dsysrscols, dsysrowsets;

            SET_LSystemTables(d);

            colid = Convert.ToInt16(dsyscolpars["colid"]);
            columnname = dsyscolpars["name"];
            datatype = "";
            nullable = true;
            isidentity = false;
            maxlength = 0;

            ishidden = ((Convert.ToInt32(dsyscolpars["status"]) & 0x2000) != 0 ? true : false);

            // sys.syscolumns.iscomputed
            dsysobjvalues = lsysobjvalues.FirstOrDefault(p => p["objid"] == dsyscolpars["id"]
                                                              && p["subobjid"] == dsyscolpars["colid"]
                                                              && p["valclass"] == "128" // SVC_GRAPHDB_COLUMN_TYPE
                                                              && p["valnum"] == "0");
            iscomputed = (dsysobjvalues == null
                          && 
                          ((Convert.ToInt32(dsyscolpars["status"]) & 0x16) / 16) == 1 ? true : false);

            // sys.syscolumns.graph_type
            graphtype = (dsysobjvalues != null ? dsysobjvalues["value"] : "");

            fcol = new TableColumn();
            fcol.ColumnID = Convert.ToInt16(dsyscolpars["colid"]);
            fcol.ColumnName = columnname;
            fcol.HasDefaultValue = false;
            fcol.DefaultConstraintName = "";
            fcol.DefaultValue = null;

            if (iscomputed == false)
            {
                datatype = Systypes[dsyscolpars["xtype"] + "_" + dsyscolpars["utype"]];
                maxlength = Convert.ToInt16(dsyscolpars["length"]);
                nullable = (1 - (Convert.ToInt32(dsyscolpars["status"]) & 0x1) == 0 ? false : true);

                columndefin = $"[{columnname}] {datatype}";
                switch (datatype)
                {
                    case "char":
                    case "varchar":
                    case "nchar":
                    case "nvarchar":
                        collationname = CollationHelper.GetCollationNameByID(Convert.ToInt32(dsyscolpars["collationid"]));
                        columndefin = columndefin
                                      + $"({(maxlength == -1 ? "max" : (datatype.StartsWith("n") ? maxlength / 2 : maxlength).ToString())})"
                                      + $" collate {collationname}";
                        break;
                    case "binary":
                    case "varbinary":
                        columndefin = columndefin
                                      + $"({(maxlength == -1 ? "max" : maxlength.ToString())})";
                        break;
                    case "time":
                    case "datetime2":
                    case "datetimeoffset":
                        columndefin = columndefin
                                      + $"({dsyscolpars["scale"]})";
                        break;
                    case "int":
                    case "tinyint":
                    case "smallint":
                    case "bigint":
                        isidentity = ((Convert.ToInt32(dsyscolpars["status"]) & 0x4) == 0 ? false : true);
                        if (isidentity == true)
                        {
                            seed = BitConverter.ToInt32(dsyscolpars["idtval"].Replace("0x", "").ToByteArray(),
                                                        Convert.ToInt32(dsyscolpars["length"]) * 2);
                            increment = BitConverter.ToInt32(dsyscolpars["idtval"].Replace("0x", "").ToByteArray(),
                                                             Convert.ToInt32(dsyscolpars["length"]));
                            columndefin = columndefin
                                          + $" identity({seed.ToString()},{increment.ToString()})";
                        }
                        break;
                    case "decimal":
                    case "numeric":
                        columndefin = columndefin
                                      + $"({dsyscolpars["prec"]},{dsyscolpars["scale"]})";
                        break;
                }
                columndefin = columndefin + $"{(nullable ? " null" : " not null")}";

                if (dsyscolpars["dflt"] != "0")
                {
                    constraintname = lsysschobjs.First(p => p["type"] == "D" && p["id"] == dsyscolpars["dflt"])["name"]; // constraint name
                    temp = lsysobjvalues.First(p => p["objid"] == dsyscolpars["dflt"])["imageval"];
                    defaultvalue = System.Text.Encoding.Default.GetString(temp.Substring(2).ToByteArray()); // default value
                    columndefin = columndefin + $" constraint [{constraintname}] default {defaultvalue}";

                    fcol.HasDefaultValue = true;
                    fcol.DefaultConstraintName = constraintname;
                    fcol.DefaultValue = defaultvalue;
                }
            }
            else
            {
                // computed column
                temp = lsysobjvalues.First(p => p["objid"] == dsyscolpars["id"] && p["subobjid"] == dsyscolpars["colid"] && p["valclass"] == "2")["imageval"];
                computedcolumndefin = System.Text.Encoding.Default.GetString(temp.Substring(2).ToByteArray()); // computed column definition
                columndefin = $"[{columnname}] as {computedcolumndefin}";
            }

            if (ishidden == true 
                || graphtype != ""
                || (d == -1 && ((ftabinfo.IsEdgeTable == true && colid <= 8) || (ftabinfo.IsNodeTable == true && colid <= 2)))
               )
            {
                columndefin = "";
            }
            
            fcol.DataType = datatype;
            fcol.PhysicalStorageType = GetPhysicalStorageType(datatype);
            fcol.GraphType = (string.IsNullOrEmpty(graphtype) ? -1 : Convert.ToInt32(graphtype));
            fcol.Length = maxlength;
            fcol.Precision = Convert.ToInt16(dsyscolpars["prec"]);
            fcol.Scale = Convert.ToInt16(dsyscolpars["scale"]);
            fcol.IsNullable = nullable;
            fcol.IsIdentity = isidentity;
            fcol.IsComputed = iscomputed;
            fcol.IsHidden = ishidden;

            if (fsysrowsets.Count + fsysrowsets0.Count == 0 
                && fsysrscols.Count + fsysrscols0.Count == 0)
            {
                fcol.LeafOffset = ftabinfo.Columns.First(p => p.ColumnID == fcol.ColumnID).LeafOffset;
                fcol.LeafNullBit = ftabinfo.Columns.First(p => p.ColumnID == fcol.ColumnID).LeafNullBit;
            }
            else
            {
                dsysrowsets = lsysrowsets.FirstOrDefault(p => Convert.ToInt32(p["idminor"]) <= 1); // index_id<=1 [sys.partitions.index_id]
                if (dsysrowsets != null)
                {
                    partitionid = dsysrowsets["rowsetid"];
                }
                else
                {
                    partitionid = "";
                }
                dsysrscols = lsysrscols.FirstOrDefault(p => p["rscolid"] == dsyscolpars["colid"] && (p["rsid"] == partitionid || dsysrowsets == null)); // [sys.system_internals_partition_columns]
                if (dsysrscols != null)
                {
                    fcol.LeafOffset = (short)(Convert.ToInt32(dsysrscols["offset"]) & 0xFFFF); // convert(smallint, convert(binary(2), c.offset & 0xffff))    [sys.system_internals_partition_columns]
                    fcol.LeafNullBit = (short)(Convert.ToInt32(dsysrscols["nullbit"]) & 0xFFFF); // convert(smallint, convert(binary(2), c.nullbit & 0xffff))  [sys.system_internals_partition_columns]
                }
                else
                {
                    fcol.LeafOffset = 0;
                    fcol.LeafNullBit = 0;
                }
            }

            return (columndefin, fcol);
        }

        private (List<string> indextype, List<string> indexcolumns, List<string> includecolumns) Tcreateindex_0
           (
            string pindexname, 
            string ptrantype, 
            TableInfo tableinfo
           )
        {
            List<string> indextype, indexcolumns, includecolumns;
            string columnname, sorttype;

            // type of index
            indextype = new List<string>();
            dsysidxstats = lsysidxstats.First(p => p["name"] == pindexname);

            if ((ptrantype == "CREATE INDEX" || ptrantype == "DROP INDEX" || ptrantype == "ALTER TABLE")
                && (Convert.ToInt32(dsysidxstats["status"]) & 0x8) != 0) // sys.indexes.is_unique
            {
                indextype.Add("unique");
            }

            switch (dsysidxstats["type"]) // sys.indexes.type
            {
                case "1":
                    indextype.Add("clustered");
                    break;
                case "2":
                    indextype.Add("nonclustered");
                    break;
                // TODO: other index types such as XML, spatial, columnstore, etc.
                default:
                    break;
            }

            // index columns & include columns
            indexcolumns = new List<string>();
            includecolumns = new List<string>();
            foreach (Dictionary<string, string> c in lsysiscols.Where(p => p["idminor"] == dsysidxstats["indid"])
                                                               .OrderBy(p => Convert.ToInt32(p["subid"])))
            {
                columnname = "";
                if (ptrantype == "CREATE TABLE")
                {
                    columnname = lsyscolpars.First(p => p["colid"] == c["intprop"])["name"];
                }

                if (ptrantype == "CREATE INDEX" || ptrantype == "DROP INDEX" || ptrantype == "ALTER TABLE")
                {
                    columnname = tableinfo.Columns.First(p => p.ColumnID == Convert.ToInt16(c["intprop"])).ColumnName;
                }
                
                sorttype = ((Convert.ToInt32(c["status"]) & 0x4) != 0 ? "desc" : "asc");  // sys.index_columns.is_descending_key

                if ((Convert.ToInt32(c["status"]) & 0x10) != 0)  // sys.index_columns.is_included_column
                {
                    includecolumns.Add($"[{columnname}]");
                }
                else
                {
                    indexcolumns.Add($"[{columnname}] {sorttype}");
                }
            }

            return (indextype, indexcolumns, includecolumns);
        }

        private void TranslateSystemTable
            (
              string PAllocUnitName,
              out List<Dictionary<string, string>> DT0,
              out List<Dictionary<string, string>> DT1
            )
        {
            List<(TableColumn[], byte[], FLOG)> dt;
            (TableColumn[], byte[], FLOG) o;
            FLOG[] plogs;
            byte[] mr, slotdata;
            TableColumn[] tca;
            string schemaname, tablename;
            TableInfo tableinfo;
            Dictionary<string, string> dr0;

            schemaname = PAllocUnitName.Split('.')[0];
            tablename = PAllocUnitName.Split('.')[1];

            if (SystemTables.ContainsKey($"{schemaname}.{tablename}") == false)
            {
                tableinfo = GetTableInfo(schemaname, tablename, "", false);
                SystemTables.Add($"{schemaname}.{tablename}", tableinfo);
            }

            dt = new List<(TableColumn[], byte[], FLOG)>();
            DT0 = new List<Dictionary<string, string>>();

            plogs = DDLLogs_Tran.Where(p => p.AllocUnitName == PAllocUnitName
                                            && (p.Context == "LCX_CLUSTERED" || p.Context == "LCX_MARK_AS_GHOST"))
                                .OrderBy(p => p.Current_LSN)
                                .ToArray();
            foreach (FLOG ls in plogs)
            {
                switch (ls.Operation)
                {
                    case "LOP_INSERT_ROWS":
                    case "LOP_DELETE_ROWS":
                        tca = DDL_GetColumnValue(schemaname, tablename, ls.RowLog_Contents_0);

                        if (ls.Operation == "LOP_INSERT_ROWS")
                        {
                            dt.Add((tca, ls.RowLog_Contents_0, ls));
                        }

                        if (ls.Operation == "LOP_DELETE_ROWS")
                        {
                            dr0 = tca.ToDict();
                            DT0.Add(dr0);
                        }
                        break;
                    case "LOP_MODIFY_ROW":
                        o = dt.FirstOrDefault(p => p.Item3.Page_ID == ls.Page_ID && p.Item3.Slot_ID == ls.Slot_ID);

                        if (o.Item3 == null 
                            && DDLLogs_Tran.First().Transaction_Name == "CREATE TABLE" 
                            && PAllocUnitName == "sys.syscolpars.clst")
                        {
                            o = dt.FirstOrDefault(p => p.Item3.RowLog_Contents_0.ToText().Contains(ls.RowLog_Contents_0.ToText()) == true);
                        }

                        if (o.Item3 == null)
                        {
                            slotdata = DDL_GetPrevSlotData(ls);
                            if (plogs.Any(p => p.Operation == "LOP_DELETE_ROWS" 
                                               && p.Page_ID == ls.Page_ID 
                                               && p.Slot_ID == ls.Slot_ID
                                               && string.Compare(p.Current_LSN, ls.Current_LSN) > 0
                                         ) == false)
                            {
                                dr0 = DDL_GetColumnValue(schemaname, tablename, slotdata).ToDict();
                                DT0.Add(dr0);
                            }
                        }
                        else
                        {
                            slotdata = o.Item2;
                        }   

                        mr = REDO_LOP_MODIFY_ROW(ls, slotdata).ToByteArray();
                        tca = DDL_GetColumnValue(schemaname, tablename, mr);
                        dt.Remove(o);
                        if (plogs.Any(p => p.Operation == "LOP_DELETE_ROWS" 
                                           && p.Page_ID == ls.Page_ID 
                                           && p.Slot_ID == ls.Slot_ID
                                           && string.Compare(p.Current_LSN, ls.Current_LSN) > 0
                                     ) == false)
                        {
                            dt.Add((tca, mr, ls));
                        }
                        break;
                }
            }
            DT1 = dt.Select(p => p.Item1.ToDict()).ToList();

        }

        private string REDO_LOP_MODIFY_ROW(FLOG log, byte[] mr1)
        {
            string mr0_str;

            if (mr1.Length >= 4)
            {
                mr0_str = mr1.ToText().Stuff(Convert.ToInt32(log.Offset_in_Row) * 2,
                                             log.RowLog_Contents_0.ToText().Length,
                                             log.RowLog_Contents_1.ToText());
            }
            else
            {
                mr0_str = mr1.ToText();
            }

            return mr0_str;
        }

        private TableColumn[] DDL_GetColumnValue(string schemaname, string tablename, byte[] PRowLogContents0)
        {
            TableColumn[] tablecolumns2;
            TableInfo tableinfo;
            
            tableinfo = SystemTables[$"{schemaname}.{tablename}"];
            tablecolumns2 = tableinfo.Columns.CopyToNew().Cast<TableColumn>().ToArray();
            FTableInfo = tableinfo;
            TranslateData(PRowLogContents0, tablecolumns2);

            return tablecolumns2;
        }

        private byte[] DDL_GetPrevSlotData(FLOG ls)
        {
            byte[] r;
            Dictionary<string, string> dr0;
            FPageInfo fpage;
            int i;
            bool found;
            string slotid, tmpstr;
            FLOG dlog;
            List<FLOG> prevlogs;
            List<string> rechecktrantypes;

            rechecktrantypes = new List<string>() { "ALTER TABLE", "CREATE INDEX" };

            slotid = ls.Slot_ID.ToString();
            tsql = "set transaction isolation level read uncommitted; "
                 + "select * "
                 + "  from sys.fn_dblog(null,null) t "
                 + $" where [Current LSN]<N'{ls.Current_LSN}' "
                 + $" and [Current LSN]>=(select max([Current LSN]) from sys.fn_dblog(null,null) b where b.[Current LSN]<N'{ls.Current_LSN}' and b.[Page ID]=t.[Page ID] and b.[Slot ID]=t.[Slot ID] and b.Operation=N'LOP_INSERT_ROWS') "
                 + $" and [Page ID]=N'{ls.Page_ID}' "
                 + $" and [Slot ID]={slotid} "
                 + "  and [Operation] in(N'LOP_INSERT_ROWS',N'LOP_MODIFY_ROW') " // ,N'LOP_DELETE_ROWS'
                 + "  and exists(select 1 from fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.[Operation]=N'LOP_COMMIT_XACT') "
                 + "  order by [Current LSN] ";
            prevlogs = DB.Query<FLOG>(tsql, false);

            tmpstr = "";
            foreach (FLOG log in prevlogs.OrderBy(p => p.Current_LSN))
            {
                switch (log.Operation)
                {
                    case "LOP_INSERT_ROWS":
                        tmpstr = log.RowLog_Contents_0.ToText();
                        break;
                    case "LOP_MODIFY_ROW":
                        if (prevlogs.Any(p => string.Compare(p.Current_LSN, log.Current_LSN) < 0
                                              && p.Operation == "LOP_INSERT_ROWS") == true)
                        {
                            tmpstr = tmpstr.Stuff(Convert.ToInt32(log.Offset_in_Row) * 2,
                                                  log.RowLog_Contents_0.Length * 2,
                                                  log.RowLog_Contents_1.ToText());
                        }
                        break;
                    case "LOP_DELETE_ROWS":
                        tmpstr = log.RowLog_Contents_0.ToText();
                        break;
                }
            }
            r = tmpstr.ToByteArray();
            
            if (rechecktrantypes.Contains(DDLLogs_Tran.First().Transaction_Name) == true
                && ls.Operation == "LOP_MODIFY_ROW"
                && ls.AllocUnitName == "sys.sysschobjs.clst"
                && ls.Offset_in_Row == 36
                && (ls.Modify_Size == 1 || ls.Modify_Size == 2))
            {
                dr0 = DDL_GetColumnValue("sys", "sysschobjs", r).ToDict();
                if ((dr0["type"] == "U"
                     && DDLLogs_Tran.Any(p => p.Operation == "LOP_LOCK_XACT" && p.Lock_Information.Contains(dr0["id"])) == true
                    ) == false)
                {
                    found = false;

                    tsql = "set transaction isolation level read uncommitted; "
                         + "select top 1 t.* "
                         + "  from sys.fn_dblog(null,null) t "
                         + $" where [Current LSN]>N'{ls.Current_LSN}' "
                         + $" and [Transaction ID]>N'{ls.Transaction_ID}' "
                         + $" and [AllocUnitName]=N'{ls.AllocUnitName}' "
                         + $" and [Page ID]=N'{ls.Page_ID}' "
                         + $" and [Slot ID]={ls.Slot_ID.ToString()} "
                         + "  and [Operation] in(N'LOP_DELETE_ROWS') "
                         + "  and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.[Transaction Name]=N'DROPOBJ') "
                         + "  and exists(select 1 from sys.fn_dblog(null,null) b where b.[Transaction ID]=t.[Transaction ID] and b.[Operation]=N'LOP_COMMIT_XACT') "
                         + "  order by t.[Current LSN] ";
                    dlog = DB.Query<FLOG>(tsql, false).FirstOrDefault();
                    if (dlog != null)
                    {
                        r = dlog.RowLog_Contents_0;
                        found = true;
                    }

                    if (found == false)
                    {
                        fpage = GetPageInfo(ls.Page_ID);
                        for (i = Convert.ToInt32(ls.Slot_ID); i >= 0; i = i - 1)
                        {
                            if (fpage.SlotData.ContainsKey(i) == true)
                            {
                                dr0 = DDL_GetColumnValue("sys", "sysschobjs", fpage.SlotData[i].ToByteArray()).ToDict();
                                if (dr0["type"] == "U")
                                {
                                    r = fpage.SlotData[i].ToByteArray();
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return r;
        }

    }
}
