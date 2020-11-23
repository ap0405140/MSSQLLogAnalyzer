using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
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
