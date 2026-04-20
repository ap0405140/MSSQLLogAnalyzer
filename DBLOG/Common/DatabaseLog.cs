using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    [Serializable]
    public class DatabaseLog
    {
        private string _redosql,
                       _undosql;
        private byte[] _redosqlfile,
                       _undosqlfile;

        public string LSN { get; set; }
        public string Type { get; set; } // DML / DDL / DCL
        public string TransactionID { get; set; }
        public string BeginTime { get; set; }
        public string EndTime { get; set; }
        public string ObjectName { get; set; }
        public string Operation { get; set; }

        public string RedoSQL
        {
            get
            {
                return (_redosql.Length <= 1000 ? _redosql : _redosql.Substring(0, 1000) + "...");
            }
            set
            {
                _redosql = value;
            }
        }
        public byte[] RedoSQLFile
        {
            get
            {
                if (_redosqlfile == null)
                {
                    _redosqlfile = _redosql.ToFileByteArray();
                }

                return _redosqlfile;
            }
        }

        public string UndoSQL
        {
            get
            {
                return (_undosql.Length <= 1000 ? _undosql : _undosql.Substring(0, 1000) + "...");
            }
            set
            {
                _undosql = value;
            }
        }
        public byte[] UndoSQLFile
        {
            get
            {
                if (_undosqlfile == null)
                {
                    _undosqlfile = _undosql.ToFileByteArray();
                }

                return _undosqlfile;
            }
        }

        public string Message { get; set; }
    }

}
