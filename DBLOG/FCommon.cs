using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG
{
    public static class FCommon
    {
        public static string Stuff(this string x, int begin, int length, string t)
        {
            string y;

            y = x.Substring(0, begin)
                + t
                + (begin + length > x.Length ? "" : x.Substring(begin + length, x.Length - begin - length));

            return y;
        }

        public static string Reverse(this string pStr)
        {
            int i;
            string returnStr;

            returnStr = "";
            for (i = 0; i <= pStr.Length - 1; i++)
            {
                returnStr = pStr.Substring(i, 1) + returnStr;
            }

            return returnStr;
        }

        public static byte[] ToByteArray(this string ss)
        {
            int i;
            byte[] bReturn;

            bReturn = new byte[ss.Length / 2];
            for (i = 0; i <= ss.Length - 1; i = i + 2)
            {
                bReturn[i / 2] = Convert.ToByte(ss.Substring(i, 2), 16);
            }

            return bReturn;
        }

        public static string ToText(this byte[] ba)
        {
            int i;
            string r;

            r = "";
            if (ba != null)
            {
                for (i = 0; i <= ba.Length - 1; i++)
                {
                    r = r + ba[i].ToString("X2");
                }
            }

            return r;
        }


    }

    // 表信息定义
    public class TableInformation
    {
        public string PrimarykeyColumnList;
        public string ClusteredindexColumnList;
        public string IdentityColumn;
        public bool IsHeapTable;  // 是否堆表
        public string FAllocUnitName;

        public TableInformation()
        {

        }
    }

    // 表字段定义
    public class TableColumn
    {
        public short ColumnID;
        public string ColumnName;
        public System.Data.SqlDbType DataType;
        public string CSDataType;

        public short Length = -1;
        public short Precision;
        public short Scale;

        public object Value = null;
        public string ValueHex = "";
        public int ValueStartIndex;       // R0中值的开始位置
        public int ValueEndIndex;         // R0中值的结束位置
        public string EndIndex = "";      // 字段值结束位置 16进制
        public string Oth = "";

        public bool isNull = false;       // 字段值是否为Null
        public bool isNullable = false;   // 是否允许Null
        public bool isComputed = false;   // 是否计算列

        public bool isVarLenDataType;     // 是否是变长型
        public bool isExists;             // 是否存在
        public short LeafOffset;
        public short LeafNullBit;

        public TableColumn(short cid, string name, SqlDbType type, short length, short precision, short scale, short pLeafOffset, short pLeafNullBit, bool pIsNullable, bool pIsComputed)
        {
            ColumnID = cid;
            ColumnName = name;
            DataType = type;
            Length = length;
            Precision = precision;
            Scale = scale;
            LeafOffset = pLeafOffset;
            LeafNullBit = pLeafNullBit;
            isNullable = pIsNullable;
            isExists = (name.Length > 0 ? true : false);
            isComputed = pIsComputed;

            if (DataType == SqlDbType.VarChar
                || DataType == SqlDbType.NVarChar
                || DataType == SqlDbType.VarBinary
                || DataType == SqlDbType.Variant
                || DataType == SqlDbType.Xml
                || DataType == SqlDbType.Image
                || DataType == SqlDbType.Text
                || DataType == SqlDbType.NText)
            {
                isVarLenDataType = true;
            }
            else
            {
                isVarLenDataType = false;
            }

            if (DataType == SqlDbType.VarChar
                || DataType == SqlDbType.NVarChar
                || DataType == SqlDbType.Char
                || DataType == SqlDbType.NChar
                || DataType == SqlDbType.Text)
            {
                CSDataType = "System.String";
            }

            if (DataType == SqlDbType.Int
                || DataType == SqlDbType.SmallInt
                || DataType == SqlDbType.TinyInt
                || DataType == SqlDbType.BigInt)
            {
                CSDataType = "System.Int32";
            }

            if (DataType == SqlDbType.DateTime
                || DataType == SqlDbType.DateTime2
                || DataType == SqlDbType.SmallDateTime
                || DataType == SqlDbType.Date)
            {
                CSDataType = "System.DateTime";
            }

            if (DataType == SqlDbType.Binary
                || DataType == SqlDbType.VarBinary)
            {
                CSDataType = "System.Object";
            }

        }
    }

}
