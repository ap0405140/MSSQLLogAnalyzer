using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    public class TableColumn : ICloneable
    {
        public short ColumnID;
        public string ColumnName;
        public string DataType;
        public System.Data.SqlDbType PhysicalStorageType;

        public short Length = -1;
        public short Precision;
        public short Scale;

        public object Value = null;
        public string ValueHex = "";
        public string ValueHexCompression = "";
        public string LogContents = "";
        public int LogContentsStartIndex;           // LogContents的开始位置
        public int LogContentsEndIndex;             // LogContents的结束位置
        public string LogContentsEndIndexHex = "";  // LogContents的结束位置(16进制)
        public string Oth = "";

        public bool IsNull = false;       // 字段值是否为Null
        public bool IsNullable = false;   // 是否允许Null
        public bool IsIdentity;
        public bool IsComputed = false;   // 是否是计算列
        public bool IsHidden;

        public short LeafOffset;
        public short LeafNullBit;
        public int GraphType;

        public SqlDbType? VariantBaseType;
        public short? VariantScale;
        public short? VariantLength;
        public string VariantCollation;

        public TableColumn()
        {

        }

        public TableColumn(short columnid = -1, bool isexists = true)
        {
            if (isexists == false)
            {
                ColumnID = columnid;
                ColumnName = "";
            }
        }

        public bool IsExists
        {
            get
            {
                return (string.IsNullOrEmpty(ColumnName) == false ? true : false);
            }
        }

        public bool IsVarLenDataType     // 是否是变长型
        {
            get
            {
                if (PhysicalStorageType == SqlDbType.VarChar
                    || PhysicalStorageType == SqlDbType.NVarChar
                    || PhysicalStorageType == SqlDbType.VarBinary
                    || PhysicalStorageType == SqlDbType.Variant
                    || PhysicalStorageType == SqlDbType.Xml
                    || PhysicalStorageType == SqlDbType.Image
                    || PhysicalStorageType == SqlDbType.Text
                    || PhysicalStorageType == SqlDbType.NText)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

    }

}
