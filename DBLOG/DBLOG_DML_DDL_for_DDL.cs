using DBLOG.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace DBLOG
{
    public partial class DBLOG_DML_DDL
    {
        public static Dictionary<string, TableInfo> SystemTables, UserTables;
        public static Dictionary<int, string> Schemas;
        public static Dictionary<string, string> Systypes;
        private static List<Dictionary<string, string>> fsysschobjs, fsysiscols, fsyscolpars, fsysidxstats, fsysobjvalues, fsysrscols, fsysclsobjs;
        private static Dictionary<string, string> dsysschobjs, dsysiscols, dsyscolpars, dsysidxstats, dsysobjvalues, dsysrscols, dsysclsobjs;

        public List<DatabaseLog> AnalyzeDDLLog()
        {
            List<DatabaseLog> r;
            DatabaseLog dr;

            r = new List<DatabaseLog>();
            foreach (string TransactionID in DDLLogs.Where(p => DDLLogs_FTranID.Any(t => t.TransactionID == p.Transaction_ID) == false)
                                                    .Select(p => p.Transaction_ID)
                                                    .Distinct()
                                                    .OrderByDescending(p => p))
            {
                dr = AnalyzeDDLTran(TransactionID);
                r.Add(dr);
            }

            return r;
        }

        private DatabaseLog AnalyzeDDLTran(string TransactionID)
        {
            DatabaseLog ddllog;
            TransactionInfo traninfo;
            string objectname, redosql, undosql, TransactionName, BeginTime, EndTime;

#if DEBUG
            FCommon.WriteTextFile(LogFile, $"TransactionID={TransactionID} ");
#endif

            DDLLogs_Tran = DDLLogs.Where(p => p.Transaction_ID == TransactionID).OrderBy(p => p.Current_LSN).ToList();
            TransactionName = DDLLogs_Tran.First().Transaction_Name;
            BeginTime = DDLLogs_Tran.First().Begin_Time;
            EndTime = DDLLogs_Tran.Last().End_Time;

            ddllog = new DatabaseLog();
            ddllog.LSN = "";
            ddllog.Type = "DDL";
            ddllog.TransactionID = TransactionID;
            ddllog.BeginTime = DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
            ddllog.EndTime = DateTime.ParseExact(EndTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
            ddllog.Operation = TransactionName.ToUpper();
            ddllog.Message = "";

            objectname = "";
            redosql = "";
            undosql = "";
            switch (TransactionName)
            {
                case "create-schema":
                    TCreateSchema(1, out objectname, out redosql, out undosql);
                    break;
                case "DROP SCHEMA":
                    TCreateSchema(-1, out objectname, out redosql, out undosql);
                    break;
                case "CREATE TABLE":
                    TCreateTable(1, out objectname, out redosql, out undosql);
                    break;
                case "DROPOBJ":
                    TCreateTable(-1, out objectname, out redosql, out undosql);
                    break;
                    //case "CREATE INDEX":
                    //    TCreateIndex(1, out objectname, out redosql, out undosql);
                    //    break;
                    //case "DROP INDEX":
                    //    TCreateIndex(-1, out objectname, out redosql, out undosql);
                    //    break;

            }
            ddllog.ObjectName = objectname;
            ddllog.RedoSQL = redosql;
            ddllog.UndoSQL = undosql;

            traninfo = new TransactionInfo();
            traninfo.TransactionID = TransactionID;
            traninfo.TransactionType = "DDL";
            traninfo.TransactionName = TransactionName.ToUpper();
            traninfo.FTime = DateTime.ParseExact(BeginTime, "yyyy/MM/dd HH:mm:ss:fff", CultureInfo.InvariantCulture);
            traninfo.AllocUnitId = DDLLogs_Tran.Where(p => p.AllocUnitId != null)
                                               .Select(p => p.AllocUnitId.ToString())
                                               .Distinct()
                                               .ToList();
            traninfo.AllocUnitName = objectname;
            traninfo.LSNList = DDLLogs_Tran.Select(p => p.Current_LSN).ToList();

            DDLLogs_FTranID.Add(traninfo);

            return ddllog;
        }

        private void TCreateSchema(int d, out string objectname, out string redosql, out string undosql)
        {
            fsysclsobjs = TranslateSystemTable("sys.sysclsobjs.clst");
            dsysclsobjs = fsysclsobjs.First();

            objectname = dsysclsobjs["name"];
            if (d == 1)
            {
                redosql = $"create schema [{objectname}];";
                undosql = $"drop schema [{objectname}];";
            }
            else
            {
                redosql = $"drop schema [{objectname}];";
                undosql = $"create schema [{objectname}];";
            }
        }

        private void TCreateTable(int d, out string objectname, out string redosql, out string undosql)
        {
            string schemaname, columndefinition, columnname, datatype, collationname, constraint, graphtype;
            Int16 maxlength;
            long seed, increment;
            bool nullable, isidentity, iscomputed, isnode, isedge, ishidden;
            List<string> lstemp1, lstemp2, lstemp3, lstemp4, lstemp5;
            string stemp1, stemp2, stemp3, stemp4, stemp5;
            TableInfo ftabinfo;
            TableColumn fcol;
            int i;

            ftabinfo = new TableInfo();

            // sys.sysschobjs
            fsysschobjs = TranslateSystemTable("sys.sysschobjs.clst", d);

            dsysschobjs = fsysschobjs.First(p => p["type"] == "U");
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];

            isnode = ((Convert.ToInt32(dsysschobjs["status2"]) & 0x00000100) != 0 ? true : false);
            ftabinfo.IsNodeTable = isnode;

            isedge = ((Convert.ToInt32(dsysschobjs["status2"]) & 0x00000200) != 0 ? true : false);
            ftabinfo.IsEdgeTable = isedge;

            // sys.sysiscols
            fsysiscols = TranslateSystemTable("sys.sysiscols.clst", d);

            // sys.sysidxstats
            fsysidxstats = TranslateSystemTable("sys.sysidxstats.clst", d);

            // sys.sysobjvalues
            fsysobjvalues = TranslateSystemTable("sys.sysobjvalues.clst", d);

            // sys.sysrscols
            fsysrscols = TranslateSystemTable("sys.sysrscols.clst", d);

            // sys.syscolpars
            fsyscolpars = TranslateSystemTable("sys.syscolpars.clst", d);
            lstemp1 = new List<string>();
            ftabinfo.Columns = new TableColumn[fsyscolpars.Count];
            i = 0;
            foreach (Dictionary<string, string> col in fsyscolpars.OrderBy(p => Convert.ToInt32(p["colid"])))
            {
                ishidden = ((Convert.ToInt32(col["status"]) & 0x2000) != 0 ? true : false);

                dsysobjvalues = fsysobjvalues.FirstOrDefault(p => p["objid"] == col["id"]
                                                                  && p["subobjid"] == col["colid"]
                                                                  && p["valclass"] == "128" // SVC_GRAPHDB_COLUMN_TYPE
                                                                  && p["valnum"] == "0");
                graphtype = (dsysobjvalues != null ? dsysobjvalues["value"] : "");

                datatype = "";
                nullable = true;
                isidentity = false;
                maxlength = 0;

                columnname = col["name"];
                iscomputed = (fsysobjvalues.Any(p => p["objid"] == col["id"]
                                                     && p["subobjid"] == col["colid"]
                                                     && p["valclass"] == "128"
                                                     && p["valnum"] == "0") == false
                              && ((Convert.ToInt32(col["status"]) & 0x16) / 16) == 1 ? true : false); // sys.syscolumns.iscomputed

                if (iscomputed == false)
                {
                    datatype = Systypes[col["xtype"] + "_" + col["utype"]];
                    maxlength = Convert.ToInt16(col["length"]);
                    nullable = (1 - (Convert.ToInt32(col["status"]) & 0x1) == 0 ? false : true);

                    stemp1 = $"[{columnname}] {datatype}";
                    switch (datatype)
                    {
                        case "char":
                        case "nchar":
                        case "varchar":
                        case "nvarchar":
                            collationname = CollationHelper.GetCollationNameByID(Convert.ToInt32(col["collationid"]));
                            stemp1 = stemp1
                                     + $"({(maxlength == -1 ? "max" : (datatype.StartsWith("n") ? maxlength / 2 : maxlength).ToString())})"
                                     + $" collate {collationname}";
                            break;
                        case "int":
                        case "tinyint":
                        case "smallint":
                        case "bigint":
                            isidentity = ((Convert.ToInt32(col["status"]) & 0x4) == 0 ? false : true);
                            if (isidentity == true)
                            {
                                seed = BitConverter.ToInt32(col["idtval"].Replace("0x", "").ToByteArray(),
                                                            Convert.ToInt32(col["length"]) * 2);
                                increment = BitConverter.ToInt32(col["idtval"].Replace("0x", "").ToByteArray(),
                                                                 Convert.ToInt32(col["length"]));
                                stemp1 = stemp1
                                         + $" identity({seed.ToString()},{increment.ToString()})";
                            }
                            break;
                        case "decimal":
                            stemp1 = stemp1
                                     + $"({col["prec"]},{col["scale"]})";
                            break;
                    }
                    stemp1 = stemp1 + $"{(nullable ? " null" : " not null")}";
                    if (col["dflt"] != "0")
                    {
                        stemp2 = fsysschobjs.First(p => p["type"] == "D" && p["id"] == col["dflt"])["name"];
                        stemp3 = fsysobjvalues.First(p => p["objid"] == col["dflt"])["imageval"];
                        stemp4 = System.Text.Encoding.Default.GetString(stemp3.Substring(2).ToByteArray());
                        stemp1 = stemp1 + $" constraint [{stemp2}] default {stemp4}";
                    }
                }
                else
                {
                    stemp3 = fsysobjvalues.First(p => p["objid"] == col["id"] && p["subobjid"] == col["colid"])["imageval"];
                    stemp4 = System.Text.Encoding.Default.GetString(stemp3.Substring(2).ToByteArray());
                    stemp1 = $"[{columnname}] as {stemp4}";
                }

                lstemp1.Add(stemp1);

                fcol = new TableColumn();
                fcol.ColumnID = Convert.ToInt16(col["colid"]);
                fcol.ColumnName = columnname;
                fcol.DataType = datatype;
                fcol.PhysicalStorageType = GetPhysicalStorageType(datatype);
                fcol.GraphType = (string.IsNullOrEmpty(graphtype) ? -1 : Convert.ToInt32(graphtype));
                fcol.Length = maxlength;
                fcol.Precision = Convert.ToInt16(col["prec"]);
                fcol.Scale = Convert.ToInt16(col["scale"]);

                if (fsysrscols.Any(p => p["rscolid"] == col["colid"]) == true)
                {
                    fcol.LeafOffset = (short)(Convert.ToInt32(fsysrscols.First(p => p["rscolid"] == col["colid"])["offset"]) & 0xFFFF); // convert(smallint, convert(binary(2), c.offset & 0xffff))    [sys.system_internals_partition_columns]
                    fcol.LeafNullBit = (short)(Convert.ToInt32(fsysrscols.First(p => p["rscolid"] == col["colid"])["nullbit"]) & 0xFFFF); // convert(smallint, convert(binary(2), c.nullbit & 0xffff))  [sys.system_internals_partition_columns]
                }
                else
                {
                    fcol.LeafOffset = 0;
                    fcol.LeafNullBit = 0;
                }
                
                fcol.IsNullable = nullable;
                fcol.IsIdentity = isidentity;
                fcol.IsComputed = iscomputed;
                fcol.IsHidden = ishidden;
                ftabinfo.Columns[i] = fcol;

                i = i + 1;
            }
            
            columndefinition = string.Join(",\r\n ", lstemp1);

            //  primary key, unique
            if (fsysschobjs.Any(p => p["type"] == "PK" || p["type"] == "UQ") == true)
            {
                dsysschobjs = fsysschobjs.First(p => p["type"] == "PK" || p["type"] == "UQ");
                stemp4 = dsysschobjs["name"]; // constraint name
                stemp5 = (dsysschobjs["type"] == "PK" ? "primary key" : "unique"); // constraint type

                (lstemp1, lstemp2, lstemp3) = Tcreateindex_0(stemp4);
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
                    ftabinfo.IsHeapTable = false;
                }
                else
                {
                    ftabinfo.IsHeapTable = true;
                }

            }
            else
            {
                constraint = "";
            }

            objectname = $"[{schemaname}].[{objectname}]";
            if (d == 1)
            {
                redosql = $"create table {objectname}\r\n({columndefinition}\r\n{constraint}){(ftabinfo.IsNodeTable == true ? " as node" : "")}; ";
                undosql = $"drop table {objectname}; ";
            }
            else
            {
                redosql = $"drop table {objectname}; ";
                undosql = $"create table {objectname}\r\n({columndefinition}\r\n{constraint}){(ftabinfo.IsNodeTable == true ? " as node" : "")};";

                UserTables.Add($"{objectname.Replace("[", "").Replace("]", "")}", ftabinfo);
            }

        }

        private (List<string> indextype, List<string> indexcolumns, List<string> includecolumns) Tcreateindex_0(string pindexname)
        {
            List<string> indextype, indexcolumns, includecolumns;
            string columnname, sorttype;

            // type of index
            indextype = new List<string>();
            dsysidxstats = fsysidxstats.First(p => p["name"] == pindexname);
            switch (dsysidxstats["type"]) // sys.indexes.type
            {
                case "1":
                    indextype.Add("clustered");
                    break;
                case "2":
                    indextype.Add("nonclustered");
                    break;
                default:
                    break;
            }

            // index columns & include columns
            indexcolumns = new List<string>();
            includecolumns = new List<string>();
            foreach (Dictionary<string, string> c in fsysiscols
                                                     .Where(p => p["idminor"] == dsysidxstats["indid"])
                                                     .OrderBy(p => Convert.ToInt32(p["subid"])))
            {
                columnname = fsyscolpars.First(p => p["colid"] == c["intprop"])["name"]; // GetUserTableInfo(dsysidxstats["id"]).Columns.First(p => p.ColumnID.ToString() == c["intprop"]).ColumnName;
                sorttype = ((Convert.ToInt32(c["status"]) & 0x4) != 0 ? "desc" : "asc");  // sys.index_columns.is_descending_key

                if ((Convert.ToInt32(c["status"]) & 0x10) != 0)  // sys.index_columns.is_included_column
                {
                    includecolumns.Add(columnname);
                }
                else
                {
                    indexcolumns.Add($"[{columnname}] {sorttype}");
                }
            }

            return (indextype, indexcolumns, includecolumns);
        }

        private List<Dictionary<string, string>> TranslateSystemTable(string PAllocUnitName, int d = 1)
        {
            List<(TableColumn[], byte[], FLOG)> r;
            (TableColumn[], byte[], FLOG) o;
            FLOG[] plogs;
            byte[] mr;
            TableColumn[] tca;
            List<Dictionary<string, string>> r2;
            string schemaname, tablename;
            TableInfo tableinfo;

            schemaname = PAllocUnitName.Split('.')[0];
            tablename = PAllocUnitName.Split('.')[1];
            if (SystemTables.ContainsKey($"{schemaname}.{tablename}") == false)
            {
                tableinfo = GetTableInfo(schemaname, tablename, false);
                SystemTables.Add($"{schemaname}.{tablename}", tableinfo);
            }

            plogs = DDLLogs_Tran.Where(p => p.AllocUnitName == PAllocUnitName
                                            && (p.Context == "LCX_CLUSTERED" || p.Context == "LCX_MARK_AS_GHOST"))
                                .OrderBy(p => p.Current_LSN)
                                .ToArray();
            if (d == -1)
            {
                plogs = plogs.OrderByDescending(p => p.Current_LSN).ToArray();
            }

            r = new List<(TableColumn[], byte[], FLOG)>();
            foreach (FLOG ls in plogs)
            {
                switch (ls.Operation)
                {
                    case "LOP_INSERT_ROWS":
                    case "LOP_DELETE_ROWS":
                        tca = GetColumnValue(ls.AllocUnitName, ls.RowLog_Contents_0);
                        r.Add((tca, ls.RowLog_Contents_0, ls));
                        break;
                    case "LOP_MODIFY_ROW":
                        o = r.First(p => p.Item3.Page_ID == ls.Page_ID && p.Item3.Slot_ID == ls.Slot_ID);
                        mr = REDO_LOP_MODIFY_ROW(ls, o.Item2).ToByteArray();
                        tca = GetColumnValue(ls.AllocUnitName, mr);
                        r.Remove(o);
                        r.Add((tca, mr, ls));
                        break;
                }
            }
            r2 = r.Select(p => p.Item1.ToDict()).ToList();

            return r2;
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

        private TableColumn[] GetColumnValue(string PAllocUnitName, byte[] PRowLogContents0)
        {
            string schemaname, tablename;
            TableColumn[] tablecolumns2;
            TableInfo tableinfo;

            schemaname = PAllocUnitName.Split('.')[0];
            tablename = PAllocUnitName.Split('.')[1];
            tableinfo = SystemTables[$"{schemaname}.{tablename}"];

            tablecolumns2 = tableinfo.Columns.CopyToNew().Cast<TableColumn>().ToArray();
            FTableInfo = tableinfo;
            TranslateData(PRowLogContents0, tablecolumns2);

            return tablecolumns2;
        }

    }
}
