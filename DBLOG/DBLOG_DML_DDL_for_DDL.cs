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
        public static Dictionary<string, TableInfo> SystemTables;
        public static Dictionary<string, List<TableInfo>> UserTables;
        public static Dictionary<int, string> Schemas;
        public static Dictionary<string, string> Systypes;
        private static List<Dictionary<string, string>> fsysschobjs, fsysiscols, fsyscolpars, fsysidxstats, fsysobjvalues, fsysrscols, fsysclsobjs, fsysallocunits;
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
            List<string> AllocUnitIds;

#if DEBUG
            FCommon.WriteTextFile(LogFile, $"DDL TransactionID={TransactionID} ");
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
            AllocUnitIds = new List<string>();
            switch (TransactionName)
            {
                case "create-schema":
                    TCreateSchema(1, out objectname, out redosql, out undosql);
                    break;
                case "DROP SCHEMA":
                    TCreateSchema(-1, out objectname, out redosql, out undosql);
                    break;
                case "CREATE TABLE":
                    TCreateTable(1, out objectname, out redosql, out undosql, out AllocUnitIds);
                    break;
                case "DROPOBJ":
                    TCreateTable(-1, out objectname, out redosql, out undosql, out AllocUnitIds);
                    break;
                case "user_transaction":
                    TUserTransaction(1, out objectname, out redosql, out undosql);
                    break;
                 case "ALTER TABLE":
                    TAlterTable(out objectname, out redosql, out undosql);
                    break;
                case "TRUNCATE TABLE":
                    TTruncateTable(out objectname, out redosql, out undosql);
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
            if (AllocUnitIds.Count > 0)
            {
                traninfo.AllocUnitId.AddRange(AllocUnitIds);
            }

            traninfo.AllocUnitName = objectname;
            traninfo.LSNList = DDLLogs_Tran.Select(p => p.Current_LSN).ToList();

            DDLLogs_FTranID.Add(traninfo);

#if DEBUG
            FCommon.WriteTextFile(LogFile, redosql);
#endif

            return ddllog;
        }

        private void TCreateSchema(int d, out string objectname, out string redosql, out string undosql)
        {
            int schemaid;
            List<Dictionary<string, string>> fsysclsobjs0;

            fsysclsobjs = TranslateSystemTable("sys.sysclsobjs.clst", d, out fsysclsobjs0);
            if (d == -1) { fsysclsobjs = fsysclsobjs0; }
            dsysclsobjs = fsysclsobjs.First();

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
        }

        private void TCreateTable(int d, out string objectname, out string redosql, out string undosql, out List<string> AllocUnitIds)
        {
            string schemaname, columndefinition, constraint, others, stk;
            bool isnode, isedge;
            List<string> lstemp1, lstemp2, lstemp3;
            string stemp1, stemp2, stemp3, stemp4, stemp5;
            TableInfo ftabinfo;
            TableColumn fcol;
            int i;
            List<Dictionary<string, string>> fsysiscols0, fsysidxstats0, fsyscolpars0, fsysobjvalues0, fsysrscols0, fsysallocunits0;

            ftabinfo = new TableInfo();
            AllocUnitIds = new List<string>();

            // sys.sysschobjs
            fsysschobjs = TranslateSystemTable("sys.sysschobjs.clst", d, out _);

            dsysschobjs = fsysschobjs.First(p => p["type"] == "U");
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = dsysschobjs["name"];

            isnode = ((Convert.ToInt32(dsysschobjs["status2"]) & 0x00000100) != 0 ? true : false);
            ftabinfo.IsNodeTable = isnode;

            isedge = ((Convert.ToInt32(dsysschobjs["status2"]) & 0x00000200) != 0 ? true : false);
            ftabinfo.IsEdgeTable = isedge;

            ftabinfo.ObjectID = dsysschobjs["id"];

            // sys.sysiscols
            fsysiscols = TranslateSystemTable("sys.sysiscols.clst", d, out fsysiscols0);
            if (d == -1) { fsysiscols = fsysiscols0; }

            // sys.sysidxstats
            fsysidxstats = TranslateSystemTable("sys.sysidxstats.clst", d, out fsysidxstats0);
            if (d == -1) { fsysidxstats = fsysidxstats0; }

            // sys.sysobjvalues
            fsysobjvalues = TranslateSystemTable("sys.sysobjvalues.clst", d, out fsysobjvalues0);
            if (d == -1) { fsysobjvalues = fsysobjvalues0; }

            // sys.sysrscols
            fsysrscols = TranslateSystemTable("sys.sysrscols.clst", d, out fsysrscols0);
            if (d == -1) { fsysrscols = fsysrscols0; }

            // sys.syscolpars
            fsyscolpars = TranslateSystemTable("sys.syscolpars.clst", d, out fsyscolpars0);
            if (d == -1) { fsyscolpars = fsyscolpars0; }

            // sys.sysallocunits
            fsysallocunits = TranslateSystemTable("sys.sysallocunits.clust", d, out fsysallocunits0);
            if (d == -1) { fsysallocunits = fsysallocunits0; }
            foreach (Dictionary<string, string> dr in fsysallocunits)
            {
                AllocUnitIds.Add(dr["auid"]);
            }

            lstemp1 = new List<string>();
            ftabinfo.Columns = new TableColumn[fsyscolpars.Count];
            i = 0;
            foreach (Dictionary<string, string> col in fsyscolpars.OrderBy(p => Convert.ToInt32(p["colid"])))
            {
                (stemp1, fcol) = Tgetcolumn(col, ftabinfo, d);
                if (string.IsNullOrEmpty(stemp1) == false) { lstemp1.Add(stemp1); }
                ftabinfo.Columns[i] = fcol;

                i = i + 1;
            }
            
            columndefinition = string.Join(",\r\n ", lstemp1) + ",";

            constraint = "";
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

            // clustered columnstore
            if (fsysidxstats.Any(p => p["type"] == "5") == true) // CLUSTERED COLUMNSTORE
            {
                dsysidxstats = fsysidxstats.First(p => p["type"] == "5");
                constraint = $" index [{dsysidxstats["name"]}] clustered columnstore\r\n";

                ftabinfo.IsColumnStore = true;
            }

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
            string optionname, val0, val1, schemaname;
            Dictionary<string, string> sysidxstats0, sysidxstats1, syscolpars0, syscolpars1;
            List<Dictionary<string, string>> fsysschobjs0, fsysidxstats0, fsyscolpars0;
            List<DiffColumn> diff;

            objectname = "";
            redosql = "";
            undosql = "";

            // sys.sysschobjs
            fsysschobjs = TranslateSystemTable("sys.sysschobjs.clst", d, out fsysschobjs0);

            // sys.sysidxstats
            fsysidxstats = TranslateSystemTable("sys.sysidxstats.clst", d, out fsysidxstats0);

            // sys.syscolpars
            fsyscolpars = TranslateSystemTable("sys.syscolpars.clst", 1, out fsyscolpars0);


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
                    objectname = schemaname + "." + dsysschobjs["name"];
                    optionname = "text in row";
                    val0 = diff.First(p => p.ColumnName == "intprop").OldValue;
                    val1 = diff.First(p => p.ColumnName == "intprop").NewValue;

                    redosql = $"exec sp_tableoption N'{objectname}','{optionname}','{val1}'; ";
                    undosql = $"exec sp_tableoption N'{objectname}','{optionname}','{val0}'; ";
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
                    dsysschobjs = fsysschobjs.First(p => p["id"] == syscolpars1["id"]);
                    schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
                    objectname = schemaname + "." + dsysschobjs["name"];
                    val0 = diff.First(p => p.ColumnName == "name").OldValue;
                    val1 = diff.First(p => p.ColumnName == "name").NewValue;

                    redosql = $"exec sp_rename N'{objectname}.{val0}',N'{val1}','COLUMN'; ";
                    undosql = $"exec sp_rename N'{objectname}.{val1}',N'{val0}','COLUMN'; ";
                }
            }


        }

        private void TAlterTable(out string objectname, out string redosql, out string undosql)
        {
            string coldef, schemaname;
            List<Dictionary<string, string>> fsysschobjs0, fsyscolpars0;
            List<DiffColumn> diff;
            TableInfo tableinfo;
            TableColumn tablecol;
            int i;

            redosql = "alter table ";
            undosql = "alter table ";

            // sys.sysschobjs
            fsysschobjs = TranslateSystemTable("sys.sysschobjs.clst", 1, out fsysschobjs0);
            dsysschobjs = fsysschobjs.First(p => p["type"] == "U");
            schemaname = Schemas[Convert.ToInt32(dsysschobjs["nsid"])];
            objectname = $"{dsysschobjs["name"]}";
            tableinfo = GetTableInfo(schemaname, objectname, "", true);

            redosql = redosql + $"[{schemaname}].[{objectname}] ";
            undosql = undosql + $"[{schemaname}].[{objectname}] ";

            // sys.syscolpars
            fsyscolpars = TranslateSystemTable("sys.syscolpars.clst", 1, out fsyscolpars0);

            if (fsyscolpars.Any() == true && fsyscolpars0.Any() == false)
            {
                redosql = redosql + "add ";
                undosql = undosql + "drop column ";

                for (i = 0; i <= fsyscolpars.Count - 1; i = i + 1)
                {
                    (coldef, tablecol) = Tgetcolumn(fsyscolpars[i], tableinfo, 1);
                    redosql = redosql + coldef + (i < fsyscolpars.Count - 1 ? ", " : "; ");
                    undosql = undosql + tablecol.ColumnName + (i < fsyscolpars.Count - 1 ? ", " : "; ");
                }
            }

            if (fsyscolpars.Any() == false && fsyscolpars0.Any() == true)
            {
                redosql = redosql + "drop column ";
                undosql = undosql + "add ";

                for (i = 0; i <= fsyscolpars0.Count - 1; i = i + 1)
                {
                    (coldef, tablecol) = Tgetcolumn(fsyscolpars0[i], tableinfo, -1);
                    redosql = redosql + tablecol.ColumnName + (i < fsyscolpars0.Count - 1 ? ", " : "; ");
                    undosql = undosql + coldef + (i < fsyscolpars0.Count - 1 ? ", " : "; ");
                }
            }

            if (fsyscolpars.Any() == true && fsyscolpars0.Any() == true)
            {
                redosql = redosql + "alter column ";
                undosql = undosql + "alter column ";

                for (i = 0; i <= fsyscolpars.Count - 1; i = i + 1)
                {
                    (coldef, tablecol) = Tgetcolumn(fsyscolpars[i], tableinfo, 1);
                    redosql = redosql + coldef + (i < fsyscolpars.Count - 1 ? ", " : "; ");
                }
                for (i = 0; i <= fsyscolpars0.Count - 1; i = i + 1)
                {
                    (coldef, tablecol) = Tgetcolumn(fsyscolpars0[i], tableinfo, -1);
                    undosql = undosql + coldef + (i < fsyscolpars0.Count - 1 ? ", " : "; ");
                }
            }

        }

        private void TTruncateTable(out string objectname, out string redosql, out string undosql)
        {
            string allocunitname, schemaname;
            List<Dictionary<string, string>> fsyscolpars0;

            allocunitname = DDLLogs_Tran.Select(p => p.AllocUnitName)
                                        .Where(p => string.IsNullOrEmpty(p) == false && p != "Unknown Alloc Unit" && p.StartsWith("sys.") == false)
                                        .Distinct()
                                        .FirstOrDefault();
            if (string.IsNullOrEmpty(allocunitname) == false)
            {
                schemaname = allocunitname.Split('.')[0];
                objectname = allocunitname.Split('.')[1];
            }
            else
            {
                // sys.syscolpars
                fsyscolpars = TranslateSystemTable("sys.syscolpars.clst", 1, out fsyscolpars0);
                dsyscolpars = fsyscolpars.First();

                objectname = UserTables.FirstOrDefault(p => p.Value.Any(x => x.ObjectID == dsyscolpars["id"]) == true).Key;
                schemaname = objectname.Split('.')[0];
                objectname = objectname.Split('.')[1];
            }

            redosql = $"truncate table [{schemaname}].[{objectname}]; ";
            undosql = $"";
        }

        private List<DiffColumn> DDL_Compare(Dictionary<string, string> dr0, Dictionary<string, string> dr1)
        {
            List<DiffColumn> r;
            DiffColumn f;

            r = new List<DiffColumn>();
            foreach (string key in dr0.Keys)
            {
                if (dr0[key] != dr1[key])
                {
                    f = new DiffColumn() { ColumnName = key, OldValue = dr0[key], NewValue = dr1[key] };
                    r.Add(f);
                }
            }

            return r;
        }

        private (string columndefin, TableColumn tabcol) Tgetcolumn
            (
                Dictionary<string, string> col, 
                TableInfo ftabinfo, 
                int d
            )
        {
            string columndefin, columnname, datatype, graphtype, collationname, constraintname, defaultvalue, computedcolumndefin, temp;
            short maxlength, colid;
            long seed, increment;
            bool nullable, isidentity, iscomputed, ishidden;
            TableColumn fcol;

            colid = Convert.ToInt16(col["colid"]);
            columnname = col["name"];
            datatype = "";
            nullable = true;
            isidentity = false;
            maxlength = 0;

            ishidden = ((Convert.ToInt32(col["status"]) & 0x2000) != 0 ? true : false);

            // sys.syscolumns.iscomputed
            dsysobjvalues = fsysobjvalues.FirstOrDefault(p => p["objid"] == col["id"]
                                                              && p["subobjid"] == col["colid"]
                                                              && p["valclass"] == "128" // SVC_GRAPHDB_COLUMN_TYPE
                                                              && p["valnum"] == "0");
            iscomputed = (dsysobjvalues == null
                          && 
                          ((Convert.ToInt32(col["status"]) & 0x16) / 16) == 1 ? true : false);

            // sys.syscolumns.graph_type
            graphtype = (dsysobjvalues != null ? dsysobjvalues["value"] : "");

            if (iscomputed == false)
            {
                datatype = Systypes[col["xtype"] + "_" + col["utype"]];
                maxlength = Convert.ToInt16(col["length"]);
                nullable = (1 - (Convert.ToInt32(col["status"]) & 0x1) == 0 ? false : true);

                columndefin = $"[{columnname}] {datatype}";
                switch (datatype)
                {
                    case "char":
                    case "varchar":
                    case "nchar":
                    case "nvarchar":
                        collationname = CollationHelper.GetCollationNameByID(Convert.ToInt32(col["collationid"]));
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
                                      + $"({col["scale"]})";
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
                            columndefin = columndefin
                                          + $" identity({seed.ToString()},{increment.ToString()})";
                        }
                        break;
                    case "decimal":
                    case "numeric":
                        columndefin = columndefin
                                      + $"({col["prec"]},{col["scale"]})";
                        break;
                }
                columndefin = columndefin + $"{(nullable ? " null" : " not null")}";

                if (col["dflt"] != "0")
                {
                    constraintname = fsysschobjs.First(p => p["type"] == "D" && p["id"] == col["dflt"])["name"]; // constraint name
                    temp = fsysobjvalues.First(p => p["objid"] == col["dflt"])["imageval"];
                    defaultvalue = System.Text.Encoding.Default.GetString(temp.Substring(2).ToByteArray()); // default value
                    columndefin = columndefin + $" constraint [{constraintname}] default {defaultvalue}";
                }
            }
            else
            {
                // computed column
                temp = fsysobjvalues.First(p => p["objid"] == col["id"] && p["subobjid"] == col["colid"])["imageval"];
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

            return (columndefin, fcol);
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

        private List<Dictionary<string, string>> TranslateSystemTable
            (
              string PAllocUnitName, 
              int d, 
              out List<Dictionary<string, string>> DT0
            )
        {
            List<(TableColumn[], byte[], FLOG)> r;
            (TableColumn[], byte[], FLOG) o;
            FLOG[] plogs;
            byte[] mr, slotdata;
            TableColumn[] tca;
            List<Dictionary<string, string>> r2;
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

            plogs = DDLLogs_Tran.Where(p => p.AllocUnitName == PAllocUnitName
                                            && (p.Context == "LCX_CLUSTERED" || p.Context == "LCX_MARK_AS_GHOST"))
                                .OrderBy(p => p.Current_LSN)
                                .ToArray();
            if (d == -1)
            {
                plogs = plogs.OrderByDescending(p => p.Current_LSN).ToArray();
            }

            r = new List<(TableColumn[], byte[], FLOG)>();
            DT0 = new List<Dictionary<string, string>>();
            foreach (FLOG ls in plogs)
            {
                switch (ls.Operation)
                {
                    case "LOP_INSERT_ROWS":
                    case "LOP_DELETE_ROWS":
                        tca = DDL_GetColumnValue(ls.AllocUnitName, ls.RowLog_Contents_0);

                        if (ls.Operation == "LOP_INSERT_ROWS")
                        {
                            r.Add((tca, ls.RowLog_Contents_0, ls));
                        }

                        if (ls.Operation == "LOP_DELETE_ROWS")
                        {
                            dr0 = tca.ToDict();
                            DT0.Add(dr0);
                        }
                        break;
                    case "LOP_MODIFY_ROW":
                        o = r.FirstOrDefault(p => p.Item3.Page_ID == ls.Page_ID && p.Item3.Slot_ID == ls.Slot_ID);
                        if (o.Item3 == null)
                        {
                            slotdata = DDL_GetPrevSlotData(ls);
                            dr0 = DDL_GetColumnValue(ls.AllocUnitName, slotdata).ToDict();
                            DT0.Add(dr0);
                        }
                        else                        
                        {
                            slotdata = o.Item2;
                        }   

                        mr = REDO_LOP_MODIFY_ROW(ls, slotdata).ToByteArray();
                        tca = DDL_GetColumnValue(ls.AllocUnitName, mr);
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

        private TableColumn[] DDL_GetColumnValue(string PAllocUnitName, byte[] PRowLogContents0)
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

        private byte[] DDL_GetPrevSlotData(FLOG ls)
        {
            byte[] r;
            List<FLOG> prevlogs;
            string tmpstr;

            tsql = "set transaction isolation level read uncommitted; "
                 + "select * "
                 + "  from sys.fn_dblog(null,null) t "
                 + $" where [Current LSN]<N'{ls.Current_LSN}' "
                 + $" and [Current LSN]>=(select max([Current LSN]) from sys.fn_dblog(null,null) b where b.[Current LSN]<N'{ls.Current_LSN}' and b.[Page ID]=t.[Page ID] and b.[Slot ID]=t.[Slot ID] and b.Operation=N'LOP_INSERT_ROWS') "
                 + $" and [Page ID]=N'{ls.Page_ID}' "
                 + $" and [Slot ID]={ls.Slot_ID} "
                 + "  and Operation in(N'LOP_INSERT_ROWS',N'LOP_MODIFY_ROW') "
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
                        if (prevlogs.Any(p => string.Compare(p.Current_LSN, log.Current_LSN) == -1
                                              && p.Operation == "LOP_INSERT_ROWS") == true)
                        {
                            tmpstr = tmpstr.Stuff(Convert.ToInt32(log.Offset_in_Row) * 2,
                                                  log.RowLog_Contents_0.Length * 2,
                                                  log.RowLog_Contents_1.ToText());
                        }
                        break;
                }
            }
            r = tmpstr.ToByteArray();

            return r;
        }

    }
}
