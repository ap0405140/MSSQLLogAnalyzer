using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    public class TableInfo
    {
        public List<string> PrimaryKeyColumns;
        public List<string> ClusteredIndexColumns;
        public bool IsHeapTable;
        public string AllocUnitName;
        public int TextInRow; // sp_tableoption @TableName,'text in row',@OptionValue --> When specified and @OptionValue is ON (enabled) or an integer value from 24 through 7000, new text, ntext, or image strings are stored directly in the data row. 
        public bool IsColumnStore;
        public bool IsNodeTable;
        public bool IsEdgeTable;
        public Dictionary<long, CompressionType> DataCompressionType; // key:PartitionId value:CompressionType
        public TableColumn[] Columns;
        public string ObjectID;
        public string Version;

        public TableInfo()
        {
            PrimaryKeyColumns = new List<string>();
            ClusteredIndexColumns = new List<string>();
            DataCompressionType = new Dictionary<long, CompressionType>();
            Columns = new TableColumn[] { };
            Version = "";
            IsColumnStore = false;
            IsNodeTable = false;
            IsEdgeTable = false;

        }

        public CompressionType GetCompressionType(long? partitionid)
        {
            CompressionType r;

            if (IsColumnStore == true)
            {
                r = CompressionType.COLUMNSTORE;
            }
            else
            {
                if (DataCompressionType.ContainsKey(Convert.ToInt64(partitionid)) == true)
                {
                    r = DataCompressionType[Convert.ToInt64(partitionid)];
                }
                else
                {
                    r = CompressionType.NONE;
                }
            }

            return r;
        }

    }

}
