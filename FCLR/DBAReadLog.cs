using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using DBLOG;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;

public partial class UserDefinedFunctions
{
    [Microsoft.SqlServer.Server.SqlFunction
     (DataAccess = DataAccessKind.Read,
      FillRowMethodName = "DBAReadLog_FillRow",
      TableDefinition = "LSN nvarchar(max),Type nvarchar(max),TransactionID nvarchar(max),BeginTime nvarchar(max),EndTime nvarchar(max),ObjectName nvarchar(max),Operation nvarchar(max),RedoSQL nvarchar(max),UndoSQL nvarchar(max),Message nvarchar(max)")]
    public static IEnumerable DBAReadLog(string pconnectionstring,
                                         string pbegintime,
                                         string pendtime,
                                         string pobjectname)
    {
        DatabaseLog[] r;
        DatabaseLogAnalyzer xc;

        if (string.IsNullOrEmpty(pbegintime))
        {
            pbegintime = DateTime.Now.AddSeconds(-5).ToString("yyyy-MM-dd HH:mm:ss");
        }
        if (string.IsNullOrEmpty(pendtime))
        {
            pendtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        xc = new DatabaseLogAnalyzer(pconnectionstring);
        r = xc.ReadLog(pbegintime, pendtime, pobjectname);
        
        return r;
    }

    public static void DBAReadLog_FillRow(object obj,
                                          out SqlString LSN,
                                          out SqlString Type,
                                          out SqlString TransactionID,
                                          out SqlString BeginTime,
                                          out SqlString EndTime,
                                          out SqlString ObjectName,
                                          out SqlString Operation,
                                          out SqlString RedoSQL,
                                          out SqlString UndoSQL,
                                          out SqlString Message)
    {
        DatabaseLog x;

        x = (DatabaseLog)obj;
        LSN = x.LSN;
        Type = x.Type;
        TransactionID = x.TransactionID;
        BeginTime = x.BeginTime;
        EndTime = x.EndTime;
        ObjectName = x.ObjectName;
        Operation = x.Operation;
        RedoSQL = x.RedoSQL;
        UndoSQL = x.UndoSQL;
        Message = x.Message;
    }

}
