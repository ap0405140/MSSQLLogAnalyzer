using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DBLOG
{
    // 数据库操作类
    public class DatabaseOperation
    {
        public string ServerName,
                      DatabaseName,
                      LoginName,
                      Password,
                      ApplicationName,
                      ErrorMessage,
                      ConnectString;
        private SqlConnection scn;
        private SqlCommand scm;
        private SqlDataAdapter sda;
        private DataSet sds;

        public DatabaseOperation(string pservername, string pdatabasename, string plogin, string ppassword, string papplicationname = "")
        {
            ServerName = pservername;
            DatabaseName = pdatabasename;
            LoginName = plogin;
            Password = ppassword;
            ApplicationName = papplicationname;

            ConnectString = string.Format("server={0};database={1};uid={2};pwd={3};Application Name={4};Connection Timeout=5;Integrated Security=false;",
                                          ServerName,
                                          DatabaseName,
                                          LoginName,
                                          Password,
                                          ApplicationName);
        }

        public DatabaseOperation(string pconnectionstring)
        {
            ConnectString = pconnectionstring;
        }

        public void RefreshConnect()
        {
            try
            {
                if (scn == null || scn.State != ConnectionState.Open)
                {
                    scn = new SqlConnection();
                    scn.ConnectionString = ConnectString;
                    scn.Open();
                }
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public DataTable Query(string sTsql, bool closeconnect = true)
        {
            DataTable dt;

            try
            {
                RefreshConnect();

                sda = new SqlDataAdapter(sTsql, scn);
                sda.SelectCommand.CommandTimeout = 0;

                sds = new DataSet();
                sda.Fill(sds);

                dt = (sds != null && sds.Tables.Count > 0 ? sds.Tables[0] : null);

                return dt;
            }
            catch (Exception ex)
            {
                throw new Exception("Run SQL: \r\n" + sTsql
                                    + "\r\n\r\n" + "ExceptionSource: " + ex.Source
                                    + "\r\n\r\n" + "ExceptionMessage: " + ex.Message);
            }
            finally
            {
                if (closeconnect == true)
                {
                    if (scn.State == ConnectionState.Open)
                    {
                        scn.Close();
                    }

                    scn.Dispose();
                }
            }
        }

        public List<T> Query<T>(string sTsql, bool closeconnect = true)
        {
            DataTable dt;
            List<T> ls;
            T tt;
            object tt2;
            PropertyInfo[] props;
            FieldInfo fieldinfo;
            DataColumn[] dtcolumns;
            ColumnAttribute[] columnattributes;
            string targettype, columnname;
            int i;

            try
            {
                dt = Query(sTsql, closeconnect);
                dtcolumns = dt.Columns.Cast<DataColumn>().ToArray();
                ls = new List<T>();

                targettype = "";
                if (typeof(T).IsValueType 
                    || typeof(T).Name.ToLower().Contains("string"))
                {
                    targettype = "ValueType";
                }
                if (typeof(T).Name.StartsWith("ValueTuple"))
                {
                    targettype = "ValueTuple";
                }
                if (typeof(T).GetConstructors().Any(p => p.GetParameters().Length == 0))
                {
                    targettype = "Class";
                }

                foreach (DataRow dr in dt.Rows)
                {
                    switch (targettype)
                    {
                        case "ValueType":
                            tt = (dr[0] == DBNull.Value ? default(T) : (T)dr[0]);
                            ls.Add(tt);
                            break;

                        case "ValueTuple":
                            tt = Activator.CreateInstance<T>();
                            tt2 = tt;
                            for (i = 0; i <= dtcolumns.Length - 1; i++)
                            {
                                fieldinfo = tt2.GetType().GetField("Item" + (i + 1).ToString());
                                if (fieldinfo != null)
                                {
                                    fieldinfo.SetValue(tt2, (dr[i] == DBNull.Value ? null : dr[i].ToSpecifiedType(fieldinfo.FieldType)));
                                }
                            }
                            tt = (T)tt2;
                            ls.Add(tt);
                            break;

                        case "Class":
                            tt = (T)Activator.CreateInstance(typeof(T));
                            props = typeof(T).GetProperties();
                            foreach (PropertyInfo prop in props)
                            {
                                columnattributes = prop.GetCustomAttributes(typeof(ColumnAttribute), false).Cast<ColumnAttribute>().ToArray();
                                columnname = (columnattributes.Length > 0 && string.IsNullOrEmpty(columnattributes[0].Name) == false
                                                ?
                                                   columnattributes[0].Name
                                                :
                                                   prop.Name);
                                if (dtcolumns.Any(c => c.ColumnName == columnname))
                                {
                                    prop.SetValue(tt, (dr[columnname] == DBNull.Value ? null : dr[columnname]));
                                }
                            }
                            ls.Add(tt);
                            break;

                        default:
                            break;
                    }
                }

                return ls;
            }
            catch (Exception ex)
            {
                throw new Exception("Run SQL: \r\n" + sTsql
                                    + "\r\n\r\n" + "ExceptionSource: " + ex.Source
                                    + "\r\n\r\n" + "ExceptionMessage: " + ex.Message);
            }
            finally
            {
                //if (closeconnect == true)
                //{
                //    if (scn.State == ConnectionState.Open)
                //    {
                //        scn.Close();
                //    }

                //    scn.Dispose();
                //}
            }
        }

        public int QueryForRowcount(string sTsql)
        {
            DataTable dt;
            dt = Query(sTsql);
            return dt.Rows.Count;
        }

        public string Query11(string sTsql, bool closeconnect = true)
        {
            string sReturn;
            object oTemp;

            try
            {
                RefreshConnect();

                scm = new SqlCommand(sTsql, scn);
                oTemp = scm.ExecuteScalar();
                sReturn = (oTemp == null ? string.Empty : oTemp.ToString());
            }
            catch (Exception ex)
            {
                sReturn = null;
                throw new Exception("Run SQL: \r\n" + sTsql
                                    + "\r\n\r\n" + "ExceptionSource: " + ex.Source
                                    + "\r\n\r\n" + "ExceptionMessage: " + ex.Message);
            }
            finally
            {
                if (closeconnect == true)
                {
                    if (scn.State == ConnectionState.Open)
                    {
                        scn.Close();
                    }

                    scn.Dispose();
                }
            }

            return sReturn;
        }

        public void ExecuteSQL(string sTsql, bool closeconnect = true)
        {
            try
            {
                RefreshConnect();

                scm = new SqlCommand(sTsql, scn);
                scm.CommandTimeout = 0;
                scm.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Run SQL: \r\n" + sTsql
                                    + "\r\n\r\n" + "ExceptionSource: " + ex.Source
                                    + "\r\n\r\n" + "ExceptionMessage: " + ex.Message);
            }
            finally
            {
                if (closeconnect == true)
                {
                    if (scn.State == ConnectionState.Open)
                    {
                        scn.Close();
                    }

                    scn.Dispose();
                }
            }
        }

        public bool ExecuteSQL(string sTsql, out string sMessage, bool bFinallyClose = true)
        {
            bool y;

            try
            {
                sMessage = string.Empty;
                RefreshConnect();

                y = false;
                scm = new SqlCommand(sTsql, scn);
                scm.CommandTimeout = 0;
                scm.ExecuteNonQuery();
                y = true;
            }
            catch (Exception ex)
            {
                y = false;
                sMessage = ex.Source + " " + ex.Message;
            }
            finally
            {
                if (bFinallyClose == true)
                {
                    if (scn.State == ConnectionState.Open)
                    {
                        scn.Close();
                    }

                    scn.Dispose();
                }
            }

            return y;
        }

        public bool ExecuteSQLWithIdentityValue(string sTsql, ref string NewID, ref string sMessage)
        {
            bool s;
            object r;

            try
            {
                s = false;
                sMessage = string.Empty;
                RefreshConnect();

                sTsql = sTsql + "select NewID=@@identity; ";
                scm = new SqlCommand(sTsql, scn);
                scm.CommandTimeout = 0;
                r = scm.ExecuteScalar();

                if (r != null)
                {
                    NewID = Convert.ToInt64(r).ToString();
                    s = true;
                }
                else
                {
                    NewID = null;
                    sMessage = "ExecuteScalar return null.";
                }
            }
            catch (Exception ex)
            {
                s = false;
                NewID = null;
                sMessage = "Run SQL: \r\n" + sTsql
                           + "\r\n\r\n" + "ExceptionSource: " + ex.Source
                           + "\r\n\r\n" + "ExceptionMessage: " + ex.Message;
            }
            finally
            {
                if (scn.State == ConnectionState.Open)
                {
                    scn.Close();
                }

                scn.Dispose();
            }

            return s;
        }

        public void ExecuteSP_Parameters(string SPname,
                                         ref List<SqlParameter> pParameters)
        {
            try
            {
                RefreshConnect();

                scm = new SqlCommand(SPname, scn);
                scm.CommandType = CommandType.StoredProcedure;
                scm.Parameters.AddRange(pParameters.ToArray());
                scm.ExecuteNonQuery();
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (scn.State == ConnectionState.Open)
                {
                    scn.Close();
                }

                scn.Dispose();
            }
        }

        public DataTable ExecuteSP_Datatable(string SPname, ref Dictionary<string, SqlParameter> pParameter)
        {
            DataTable dt;
            dt = null;

            try
            {
                RefreshConnect();

                scm = new SqlCommand(SPname, scn);
                scm.CommandType = CommandType.StoredProcedure;

                foreach (SqlParameter pp in pParameter.Values)
                {
                    scm.Parameters.Add(pp);
                }

                sda = new SqlDataAdapter(scm);
                sda.SelectCommand.CommandTimeout = 0;

                sds = new DataSet();
                sda.Fill(sds);

                if (sds != null && sds.Tables.Count > 0)
                {
                    dt = sds.Tables[0];
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Run SQL: \r\n" + SPname + " " + string.Join(",", pParameter.Values.Select(p => p.Value.ToString()).ToArray())
                                    + "\r\n\r\n" + "ExceptionSource: " + ex.Source
                                    + "\r\n\r\n" + "ExceptionMessage: " + ex.Message);
            }
            finally
            {
                if (scn.State == ConnectionState.Open)
                {
                    scn.Close();
                }

                scn.Dispose();
            }

            return dt;
        }
    

    }


}
