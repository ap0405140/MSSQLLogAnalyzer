using DBLOG.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG
{
    public partial class DBLOG_DML_DDL
    {
        private void TranslateData_Compression(byte[] rowdata, TableColumn[] columns)
        {
            int i, j, k, offset, length, physicallength;
            string rowdatahex, colconts, colconts2, valuehex, temp;
            List<(int offset, int length, int physicallength)> cols;
            List<int> intail;
            FVarColumnInfo vc;

            rowdatahex = rowdata.ToText();
            cols = new List<(int offset, int length, int physicallength)>();

            i = (columns.Length % 2 == 0 ? columns.Length : columns.Length + 1);
            colconts = rowdatahex.Substring(4, i);

            for (j = 1, temp = ""; j <= colconts.Length - 1; j = j + 2)
            {
                temp = temp + colconts.Substring(j - 1, 2).Reverse();
            }
            colconts = temp;

            k = 2 + i / 2;
            intail = new List<int>();
            for (i = 0; i <= columns.Length - 1; i = i + 1)
            {
                temp = colconts.Substring(i, 1);
                switch (temp)
                {
                    case "0": // null
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        break;
                    case "1": // 0
                        length = columns[i].Length;
                        physicallength = 0;
                        offset = k + physicallength;
                        break;
                    case "2":
                    case "3":
                    case "4":
                    case "5":
                    case "6":
                    case "7":
                    case "8":
                    case "9":
                        length = (RowCompressionAffectsStorage.Contains(columns[i].DataType) == false ?
                                    Convert.ToInt32(temp, 16) - 1
                                    :
                                    columns[i].Length);
                        physicallength = Convert.ToInt32(temp, 16) - 1;
                        offset = k;
                        break;
                    case "A":
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        intail.Add(i);
                        break;
                    case "B":
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        break;
                    default:
                        length = 0;
                        physicallength = 0;
                        offset = 0;
                        break;
                }

                if (columns[i].DataType == "bit")
                {
                    if (temp == "0")
                    {
                        columns[i].IsNull = true;
                        columns[i].Value = null;
                    }
                    else
                    {
                        columns[i].IsNull = false;
                        columns[i].Value = (temp == "B" ? 1 : 0);
                    }
                }

                cols.Add((offset, length, physicallength));
                k = k + physicallength;
            }

            if (intail.Count > 0)
            {
                colconts2 = rowdatahex.Substring(k * 2, (2 + intail.Count * 2 + 1) * 2);

                offset = k + (2 + intail.Count * 2 + 1);
                for (i = 0, j = 0; i <= intail.Count - 1; i = i + 1)
                {
                    temp = colconts2.Substring((2 + i * 2) * 2, 2 * 2);
                    if (temp.StartsWith("8") == true) { temp = "0" + temp.Substring(1); }
                    length = physicallength = Convert.ToInt32(temp, 16) - j;

                    cols[intail[i]] = (offset, length, physicallength);
                    j = j + length;
                    offset = offset + length;
                }
            }

            for (i = 0; i <= columns.Length - 1; i = i + 1)
            {
                if (columns[i].DataType == "bit") { continue; }

                valuehex = rowdatahex.Substring(cols[i].offset * 2, cols[i].physicallength * 2);
                columns[i].ValueHexCompression = valuehex;
                columns[i].IsNull = (cols[i].offset == 0 && cols[i].length == 0 ? true : false);

                if (columns[i].IsNull == false)
                {
                    if (RowCompressionAffectsStorage.Contains(columns[i].DataType) == false)
                    {
                        columns[i].ValueHex = valuehex;
                        switch (columns[i].DataType)
                        {
                            case "tinyint":
                                columns[i].Value = Convert.ToInt32(valuehex.ToByteArray()[0]);
                                break;
                            case "smalldatetime":
                                columns[i].Value = TranslateData_SmallDateTime(valuehex.ToByteArray(), 0);
                                break;
                            case "date":
                                columns[i].Value = TranslateData_Date(valuehex.ToByteArray(), 0);
                                break;
                            case "time":
                                columns[i].Value = TranslateData_Time(valuehex.ToByteArray(), 0, columns[i].Length, columns[i].Scale);
                                break;
                            case "varchar":
                                vc = new FVarColumnInfo() { InRow = !valuehex.StartsWith("0"), FLogContents = valuehex };
                                (columns[i].ValueHex, columns[i].Value) = TranslateData_VarChar(valuehex.ToByteArray(), vc, false);
                                break;
                            case "nvarchar":
                                vc = new FVarColumnInfo() { InRow = !valuehex.StartsWith("0"), FLogContents = valuehex };
                                (columns[i].ValueHex, columns[i].Value) = TranslateData_VarChar(valuehex.ToByteArray(), vc, true);
                                break;
                            case "xml":
                                vc = new FVarColumnInfo() { FLogContents = valuehex };
                                (_, columns[i].Value) = TranslateData_XML(vc);
                                break;
                        }
                    }
                    else
                    {
                        switch (columns[i].DataType)
                        {
                            case "smallint":
                                columns[i].ValueHex = UnCompression_SMALLINT(valuehex);
                                columns[i].Value = BitConverter.ToInt16(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "int":
                                columns[i].ValueHex = UnCompression_INT(valuehex);
                                columns[i].Value = BitConverter.ToInt32(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "bigint":
                                columns[i].ValueHex = UnCompression_BIGINT(valuehex);
                                columns[i].Value = BitConverter.ToInt64(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "decimal":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = TranslateData_VarDecimal(valuehex);
                                break;
                            case "bit":
                                break;
                            case "smallmoney":
                                columns[i].ValueHex = UnCompression_SMALLMONEY(valuehex);
                                columns[i].Value = TranslateData_SmallMoney(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "money":
                                columns[i].ValueHex = UnCompression_MONEY(valuehex);
                                columns[i].Value = TranslateData_Money(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "float":
                                columns[i].ValueHex = UnCompression_FLOAT(valuehex, columns[i].Length);
                                columns[i].Value = TranslateData_Float(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length);
                                break;
                            case "real":
                                columns[i].ValueHex = UnCompression_REAL(valuehex, columns[i].Length);
                                columns[i].Value = TranslateData_Real(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length);
                                break;
                            case "datetime":
                                columns[i].ValueHex = UnCompression_DATETIME(valuehex);
                                columns[i].Value = TranslateData_DateTime(columns[i].ValueHex.ToByteArray(), 0);
                                break;
                            case "datetime2":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = TranslateData_DateTime2(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length, columns[i].Scale);
                                break;
                            case "datetimeoffset":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = TranslateData_DateTimeOffset(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length, columns[i].Scale);
                                break;
                            case "char":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = System.Text.Encoding.Default.GetString(columns[i].ValueHex.ToByteArray(), 0, valuehex.Length / 2).TrimEnd();
                                break;
                            case "nchar":
                                columns[i].ValueHex = UnCompression_NCHAR(valuehex);
                                columns[i].Value = System.Text.Encoding.Unicode.GetString(columns[i].ValueHex.ToByteArray(), 0, columns[i].ValueHex.Length / 2).TrimEnd();
                                break;
                            case "binary":
                                columns[i].ValueHex = UnCompression_BINARY(valuehex, columns[i].Length);
                                columns[i].Value = TranslateData_Binary(columns[i].ValueHex.ToByteArray(), 0, columns[i].Length);
                                break;
                            case "timestamp":
                                columns[i].ValueHex = valuehex;
                                columns[i].Value = "null";
                                break;
                            default:
                                columns[i].ValueHex = valuehex;
                                break;
                        }
                    }
                }
            }

        }

        #region UnCompression
        private string UnCompression_SMALLINT(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(2); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(2 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_INT(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(4); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(4 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_BIGINT(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(8); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(8 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_SMALLMONEY(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(4); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(4 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_MONEY(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(8); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(8 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_FLOAT(string pcvalue, short len)
        {
            string rvalue;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(len); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                rvalue = "00".Replicate(len - pcvalue.Length / 2) + pcvalue;
            }

            return rvalue;
        }

        private string UnCompression_REAL(string pcvalue, short len)
        {
            string rvalue;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(len); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                rvalue = "00".Replicate(len - pcvalue.Length / 2) + pcvalue;
            }

            return rvalue;
        }

        private string UnCompression_DATETIME(string pcvalue)
        {
            string rvalue, sg;

            if (pcvalue == "")
            {
                rvalue = "00".Replicate(8); // Note: NULL and 0 values across all data types are optimized and take no bytes.
            }
            else
            {
                sg = (pcvalue.ToBinaryString().StartsWith("1") ? "0" : "1");
                rvalue = pcvalue.ToBinaryString().Stuff(0, 1, sg).ToHexString();
                rvalue = rvalue.ToByteArray().Reverse().ToArray().ToText();
                rvalue = rvalue + (pcvalue.ToBinaryString().StartsWith("1") ? "00" : "FF").Replicate(8 - pcvalue.Length / 2);
            }

            return rvalue;
        }

        private string UnCompression_NCHAR(string pcvalue)
        {
            string rvalue, t, n;
            int i;

            if ((pcvalue.Length / 2) % 2 == 0)
            {
                rvalue = pcvalue;
            }
            else
            {
                rvalue = "";
                for (i = 0; i <= pcvalue.Length - 2; i = i + 2)
                {
                    t = pcvalue.Substring(i, 2);
                    n = (pcvalue.Length - 1 > i + 2 ? pcvalue.Substring(i + 2, 2) : "");

                    if (t == "10" && i == pcvalue.Length - 2)
                    {
                        break;
                    }

                    if (t != "00" && n == "00")
                    {
                        rvalue = rvalue + t + n;
                        i = i + 2;
                        continue;
                    }

                    if (t == "0E")
                    {
                        i = i + 2;
                        t = pcvalue.Substring(i, 2);
                        n = pcvalue.Substring(i + 2, 2);

                        rvalue = rvalue + n + t;
                        i = i + 2;

                        continue;
                    }

                    rvalue = rvalue + t + "00";
                }
            }

            return rvalue;
        }

        private string UnCompression_BINARY(string pcvalue, short len)
        {
            string rvalue;

            rvalue = pcvalue + "00".Replicate(len - pcvalue.Length / 2);

            return rvalue;
        }
        #endregion UnCompression

    }
}
