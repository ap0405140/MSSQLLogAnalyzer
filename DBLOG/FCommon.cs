using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

        // Hex string to Binary string
        public static string ToBinaryString(this string phex)
        {
            string bs;

            bs = String.Join(String.Empty,
                             phex.Select(p => Convert.ToString(Convert.ToInt32(p.ToString(), 16), 2).PadLeft(4, '0'))
                            );

            return bs;
        }

        public static byte[] ToFileByteArray(this string ptext)
        {
            FileStream fs;
            StreamWriter writer;
            FileMode fm;
            string tfilename;
            byte[] filedata;

            tfilename = Guid.NewGuid().ToString().Replace("-","") + ".txt";
            fm = FileMode.Create;
            fs = new FileStream(tfilename,fm,FileAccess.Write,FileShare.None);
            writer = new StreamWriter(fs, Encoding.Unicode);
            writer.WriteLine(ptext);

            writer.Close();
            fs.Close();
            writer.Dispose();
            fs.Dispose();

            Thread.Sleep(10);
            filedata = File.ReadAllBytes(tfilename);

            Thread.Sleep(10);
            File.Delete(tfilename);

            return filedata;
        }

        // 字节转二进制数格式(8位)
        public static string ToBinaryString(this byte pByte)
        {
            string r;

            r = Convert.ToString(pByte, 2);
            r = new string('0', 8 - r.Length) + r;

            return r;
        }

        public static void ToFile(this byte[] filedata, string tfile)
        {
            FileStream fs;
            string filepath;

            filepath = Path.GetDirectoryName(tfile);
            if (Directory.Exists(filepath) == false)
            {
                Directory.CreateDirectory(filepath);
            }

            if (File.Exists(tfile) == true)
            {
                File.Delete(tfile);
            }

            fs = new FileStream(tfile, FileMode.OpenOrCreate, FileAccess.Write);
            fs.Write(filedata, 0, filedata.Length);
            fs.Close();
            fs.Dispose();
        }

        public static object ToSpecifiedType(this object x, Type TargetType)
        {
            object y;
            TypeCode typecode;

            if (x == null)
            {
                y = null;
            }
            else
            {
                if (TargetType.IsGenericType == true && TargetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    typecode = Type.GetTypeCode(TargetType.GetGenericArguments()[0]);
                }
                else
                {
                    typecode = Type.GetTypeCode(TargetType);
                }

                switch (typecode)
                {
                    case TypeCode.Boolean:
                        y = Convert.ToBoolean(x);
                        break;
                    case TypeCode.Char:
                        y = Convert.ToChar(x);
                        break;
                    case TypeCode.SByte:
                        y = Convert.ToSByte(x);
                        break;
                    case TypeCode.Byte:
                        y = Convert.ToByte(x);
                        break;
                    case TypeCode.Int16:
                        y = Convert.ToInt16(x);
                        break;
                    case TypeCode.UInt16:
                        y = Convert.ToUInt16(x);
                        break;
                    case TypeCode.Int32:
                        y = Convert.ToInt32(x);
                        break;
                    case TypeCode.UInt32:
                        y = Convert.ToUInt32(x);
                        break;
                    case TypeCode.Int64:
                        y = Convert.ToInt64(x);
                        break;
                    case TypeCode.UInt64:
                        y = Convert.ToUInt64(x);
                        break;
                    case TypeCode.Single:
                        y = Convert.ToSingle(x);
                        break;
                    case TypeCode.Double:
                        y = Convert.ToDouble(x);
                        break;
                    case TypeCode.Decimal:
                        y = Convert.ToDecimal(x);
                        break;
                    case TypeCode.DateTime:
                        y = Convert.ToDateTime(x);
                        break;
                    case TypeCode.String:
                        y = Convert.ToString(x);
                        break;
                    default:
                        y = x;
                        break;
                }
            }

            return y;
        }
    }

    // 表信息定义
    public class TableInformation
    {
        private string _IdentityColumn;

        public List<string> PrimaryKeyColumns;
        public List<string> ClusteredIndexColumns;
        public string IdentityColumn
        {
            get
            {
                return _IdentityColumn;
            }
            set
            {
                _IdentityColumn = (value == null ? "" : value);
            }
        }
        public bool IsHeapTable;  // 是否堆表
        public string AllocUnitName;
        public int TextInRow; // sp_tableoption @TableName,'text in row',@OptionValue --> When specified and @OptionValue is ON (enabled) or an integer value from 24 through 7000, new text, ntext, or image strings are stored directly in the data row. 

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
        public string LogContents = "";
        public int LogContentsStartIndex;           // LogContents的开始位置
        public int LogContentsEndIndex;             // LogContents的结束位置
        public string LogContentsEndIndexHex = "";  // LogContents的结束位置(16进制)
        public string Oth = "";

        public bool isNull = false;       // 字段值是否为Null
        public bool isNullable = false;   // 是否允许Null
        public bool isComputed = false;   // 是否是计算列

        public bool isVarLenDataType;     // 是否是变长型
        public bool isExists;             // 是否存在
        public short LeafOffset;
        public short LeafNullBit;

        public SqlDbType? VariantBaseType;
        public short? VariantScale;
        public short? VariantLength;
        public string VariantCollation;

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

    public class FLOG
    {
        [Column("Current LSN", Order = 0)]
        [StringLength(23)]
        public string Current_LSN { get; set; }

        
        [Column(Order = 1)]
        [StringLength(31)]
        public string Operation { get; set; }

        
        [Column(Order = 2)]
        [StringLength(31)]
        public string Context { get; set; }

        
        [Column("Transaction ID", Order = 3)]
        [StringLength(14)]
        public string Transaction_ID { get; set; }

        
        [Column(Order = 4)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long LogBlockGeneration { get; set; }

        
        [Column("Tag Bits", Order = 5)]
        [MaxLength(2)]
        public byte[] Tag_Bits { get; set; }

        
        [Column("Log Record Fixed Length", Order = 6)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short Log_Record_Fixed_Length { get; set; }

        
        [Column("Log Record Length", Order = 7)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short Log_Record_Length { get; set; }

        
        [Column("Previous LSN", Order = 8)]
        [StringLength(23)]
        public string Previous_LSN { get; set; }

        
        [Column("Flag Bits", Order = 9)]
        [MaxLength(2)]
        public byte[] Flag_Bits { get; set; }

        
        [Column("Log Reserve", Order = 10)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Log_Reserve { get; set; }

        public long? AllocUnitId { get; set; }

        [StringLength(387)]
        public string AllocUnitName { get; set; }

        [Column("Page ID")]
        [StringLength(14)]
        public string Page_ID { get; set; }

        [Column("Slot ID")]
        public int? Slot_ID { get; set; }

        [Column("Previous Page LSN")]
        [StringLength(23)]
        public string Previous_Page_LSN { get; set; }

        public long? PartitionId { get; set; }

        public short? RowFlags { get; set; }

        [Column("Num Elements")]
        public short? Num_Elements { get; set; }

        [Column("Offset in Row")]
        public short? Offset_in_Row { get; set; }

        [Column("Modify Size")]
        public short? Modify_Size { get; set; }

        [Column("Checkpoint Begin")]
        [StringLength(24)]
        public string Checkpoint_Begin { get; set; }

        [Column("CHKPT Begin DB Version")]
        public short? CHKPT_Begin_DB_Version { get; set; }

        [Column("Max XDESID")]
        [StringLength(14)]
        public string Max_XDESID { get; set; }

        [Column("Num Transactions")]
        public short? Num_Transactions { get; set; }

        [Column("Checkpoint End")]
        [StringLength(24)]
        public string Checkpoint_End { get; set; }

        [Column("CHKPT End DB Version")]
        public short? CHKPT_End_DB_Version { get; set; }

        [Column("Minimum LSN")]
        [StringLength(23)]
        public string Minimum_LSN { get; set; }

        [Column("Dirty Pages")]
        public int? Dirty_Pages { get; set; }

        [Column("Oldest Replicated Begin LSN")]
        [StringLength(23)]
        public string Oldest_Replicated_Begin_LSN { get; set; }

        [Column("Next Replicated End LSN")]
        [StringLength(23)]
        public string Next_Replicated_End_LSN { get; set; }

        [Column("Last Distributed Backup End LSN")]
        [StringLength(23)]
        public string Last_Distributed_Backup_End_LSN { get; set; }

        [Column("Last Distributed End LSN")]
        [StringLength(23)]
        public string Last_Distributed_End_LSN { get; set; }

        [Column("Repl Min Hold LSN")]
        [StringLength(23)]
        public string Repl_Min_Hold_LSN { get; set; }

        [Column("Server UID")]
        public int? Server_UID { get; set; }

        public int? SPID { get; set; }

        [Column("Beginlog Status")]
        [MaxLength(4)]
        public byte[] Beginlog_Status { get; set; }

        [Column("Xact Type")]
        public int? Xact_Type { get; set; }

        [Column("Begin Time")]
        [StringLength(24)]
        public string Begin_Time { get; set; }

        [Column("Transaction Name")]
        [StringLength(33)]
        public string Transaction_Name { get; set; }

        [Column("Transaction SID")]
        [MaxLength(85)]
        public byte[] Transaction_SID { get; set; }

        [Column("Parent Transaction ID")]
        [StringLength(14)]
        public string Parent_Transaction_ID { get; set; }

        [Column("Oldest Active Transaction ID")]
        [StringLength(14)]
        public string Oldest_Active_Transaction_ID { get; set; }

        [Column("Xact ID")]
        public long? Xact_ID { get; set; }

        [Column("Xact Node ID")]
        public int? Xact_Node_ID { get; set; }

        [Column("Xact Node Local ID")]
        public int? Xact_Node_Local_ID { get; set; }

        [Column("End AGE")]
        public long? End_AGE { get; set; }

        [Column("End Time")]
        [StringLength(24)]
        public string End_Time { get; set; }

        [Column("Transaction Begin")]
        [StringLength(23)]
        public string Transaction_Begin { get; set; }

        [Column("Replicated Records")]
        public long? Replicated_Records { get; set; }

        [Column("Oldest Active LSN")]
        [StringLength(23)]
        public string Oldest_Active_LSN { get; set; }

        [Column("Server Name")]
        [StringLength(129)]
        public string Server_Name { get; set; }

        [Column("Database Name")]
        [StringLength(129)]
        public string Database_Name { get; set; }

        [Column("Mark Name")]
        [StringLength(33)]
        public string Mark_Name { get; set; }

        [Column("Repl Partition ID")]
        public int? Repl_Partition_ID { get; set; }

        [Column("Repl Epoch")]
        public int? Repl_Epoch { get; set; }

        [Column("Repl CSN")]
        public long? Repl_CSN { get; set; }

        [Column("Repl Flags")]
        public int? Repl_Flags { get; set; }

        [Column("Repl Msg")]
        [MaxLength(8000)]
        public byte[] Repl_Msg { get; set; }

        [Column("Repl Source Commit Time")]
        [StringLength(24)]
        public string Repl_Source_Commit_Time { get; set; }

        [Column("Master XDESID")]
        [StringLength(14)]
        public string Master_XDESID { get; set; }

        [Column("Master DBID")]
        public int? Master_DBID { get; set; }

        [Column("Preplog Begin LSN")]
        [StringLength(23)]
        public string Preplog_Begin_LSN { get; set; }

        [Column("Prepare Time")]
        [StringLength(24)]
        public string Prepare_Time { get; set; }

        [Column("Virtual Clock")]
        public long? Virtual_Clock { get; set; }

        [Column("Previous Savepoint")]
        [StringLength(23)]
        public string Previous_Savepoint { get; set; }

        [Column("Savepoint Name")]
        [StringLength(33)]
        public string Savepoint_Name { get; set; }

        [Column("Rowbits First Bit")]
        public short? Rowbits_First_Bit { get; set; }

        [Column("Rowbits Bit Count")]
        public short? Rowbits_Bit_Count { get; set; }

        [Column("Rowbits Bit Value")]
        [MaxLength(1)]
        public byte[] Rowbits_Bit_Value { get; set; }

        [Column("Number of Locks")]
        public short? Number_of_Locks { get; set; }

        [Column("Lock Information")]
        [StringLength(256)]
        public string Lock_Information { get; set; }

        [Column("LSN before writes")]
        [StringLength(23)]
        public string LSN_before_writes { get; set; }

        [Column("Pages Written")]
        public short? Pages_Written { get; set; }

        [Column("Command Type")]
        public int? Command_Type { get; set; }

        [Column("Publication ID")]
        public int? Publication_ID { get; set; }

        [Column("Article ID")]
        public int? Article_ID { get; set; }

        [Column("Partial Status")]
        public int? Partial_Status { get; set; }

        [StringLength(26)]
        public string Command { get; set; }

        [Column("Byte Offset")]
        public short? Byte_Offset { get; set; }

        [Column("New Value")]
        [MaxLength(1)]
        public byte[] New_Value { get; set; }

        [Column("Old Value")]
        [MaxLength(1)]
        public byte[] Old_Value { get; set; }

        [Column("New Split Page")]
        [StringLength(14)]
        public string New_Split_Page { get; set; }

        [Column("Rows Deleted")]
        public short? Rows_Deleted { get; set; }

        [Column("Bytes Freed")]
        public short? Bytes_Freed { get; set; }

        [Column("CI Table Id")]
        public int? CI_Table_Id { get; set; }

        [Column("CI Index Id")]
        public short? CI_Index_Id { get; set; }

        public long? NewAllocUnitId { get; set; }

        [Column("FileGroup ID")]
        public short? FileGroup_ID { get; set; }

        [Column("Meta Status")]
        [MaxLength(4)]
        public byte[] Meta_Status { get; set; }

        [Column("File Status")]
        [MaxLength(4)]
        public byte[] File_Status { get; set; }

        [Column("File ID")]
        public short? File_ID { get; set; }

        [Column("Physical Name")]
        [StringLength(261)]
        public string Physical_Name { get; set; }

        [Column("Logical Name")]
        [StringLength(129)]
        public string Logical_Name { get; set; }

        [Column("Format LSN")]
        [StringLength(23)]
        public string Format_LSN { get; set; }

        public long? RowsetId { get; set; }

        [MaxLength(16)]
        public byte[] TextPtr { get; set; }

        [Column("Column Offset")]
        public int? Column_Offset { get; set; }

        public int? Flags { get; set; }

        [Column("Text Size")]
        public long? Text_Size { get; set; }

        public long? Offset { get; set; }

        [Column("Old Size")]
        public long? Old_Size { get; set; }

        [Column("New Size")]
        public long? New_Size { get; set; }

        
        [Column(Order = 11)]
        [StringLength(256)]
        public string Description { get; set; }

        [Column("Bulk allocated extent count")]
        public int? Bulk_allocated_extent_count { get; set; }

        [Column("Bulk RowsetId")]
        public long? Bulk_RowsetId { get; set; }

        [Column("Bulk AllocUnitId")]
        public long? Bulk_AllocUnitId { get; set; }

        [Column("Bulk allocation first IAM Page ID")]
        [StringLength(14)]
        public string Bulk_allocation_first_IAM_Page_ID { get; set; }

        [Column("Bulk allocated extent ids")]
        [StringLength(961)]
        public string Bulk_allocated_extent_ids { get; set; }

        [Column("VLFs added")]
        [StringLength(688)]
        public string VLFs_added { get; set; }

        [Column("InvalidateCache Id")]
        public int? InvalidateCache_Id { get; set; }

        [Column("InvalidateCache keys")]
        [StringLength(401)]
        public string InvalidateCache_keys { get; set; }

        [Column("CopyVerionInfo Source Page Id")]
        [StringLength(14)]
        public string CopyVerionInfo_Source_Page_Id { get; set; }

        [Column("CopyVerionInfo Source Page LSN")]
        [StringLength(23)]
        public string CopyVerionInfo_Source_Page_LSN { get; set; }

        [Column("CopyVerionInfo Source Slot Id")]
        public int? CopyVerionInfo_Source_Slot_Id { get; set; }

        [Column("CopyVerionInfo Source Slot Count")]
        public int? CopyVerionInfo_Source_Slot_Count { get; set; }

        [Column("RowLog Contents 0")]
        [MaxLength(8000)]
        public byte[] RowLog_Contents_0 { get; set; }

        [Column("RowLog Contents 1")]
        [MaxLength(8000)]
        public byte[] RowLog_Contents_1 { get; set; }

        [Column("RowLog Contents 2")]
        [MaxLength(8000)]
        public byte[] RowLog_Contents_2 { get; set; }

        [Column("RowLog Contents 3")]
        [MaxLength(8000)]
        public byte[] RowLog_Contents_3 { get; set; }

        [Column("RowLog Contents 4")]
        [MaxLength(8000)]
        public byte[] RowLog_Contents_4 { get; set; }

        [Column("RowLog Contents 5")]
        [MaxLength(8000)]
        public byte[] RowLog_Contents_5 { get; set; }

        [Column("Compression Log Type")]
        public short? Compression_Log_Type { get; set; }

        [Column("Compression Info")]
        [MaxLength(8000)]
        public byte[] Compression_Info { get; set; }

        [Column("PageFormat PageType")]
        public short? PageFormat_PageType { get; set; }

        [Column("PageFormat PageFlags")]
        public short? PageFormat_PageFlags { get; set; }

        [Column("PageFormat PageLevel")]
        public short? PageFormat_PageLevel { get; set; }

        [Column("PageFormat PageStat")]
        public short? PageFormat_PageStat { get; set; }

        [Column("PageFormat FormatOption")]
        public short? PageFormat_FormatOption { get; set; }

        
        [Column("Log Record", Order = 12)]
        [MaxLength(8000)]
        public byte[] Log_Record { get; set; }
    }

    public static class CollationHelper
    {
        private static Dictionary<string, string> ls;

        private static void Init()
        {
            if (ls == null)
            {
                ls = new Dictionary<string, string>();
                ls.Add("18000100", "Albanian_BIN");
                ls.Add("18080000", "Albanian_BIN2");
                ls.Add("18F00000", "Albanian_CI_AI");
                ls.Add("18700000", "Albanian_CI_AI_WS");
                ls.Add("18B00000", "Albanian_CI_AI_KS");
                ls.Add("18300000", "Albanian_CI_AI_KS_WS");
                ls.Add("18D00000", "Albanian_CI_AS");
                ls.Add("18500000", "Albanian_CI_AS_WS");
                ls.Add("18900000", "Albanian_CI_AS_KS");
                ls.Add("18100000", "Albanian_CI_AS_KS_WS");
                ls.Add("18E00000", "Albanian_CS_AI");
                ls.Add("18600000", "Albanian_CS_AI_WS");
                ls.Add("18A00000", "Albanian_CS_AI_KS");
                ls.Add("18200000", "Albanian_CS_AI_KS_WS");
                ls.Add("18C00000", "Albanian_CS_AS");
                ls.Add("18400000", "Albanian_CS_AS_WS");
                ls.Add("18800000", "Albanian_CS_AS_KS");
                ls.Add("18000000", "Albanian_CS_AS_KS_WS");
                ls.Add("18000500", "Albanian_100_BIN");
                ls.Add("18080400", "Albanian_100_BIN2");
                ls.Add("18F00400", "Albanian_100_CI_AI");
                ls.Add("18700400", "Albanian_100_CI_AI_WS");
                ls.Add("18B00400", "Albanian_100_CI_AI_KS");
                ls.Add("18300400", "Albanian_100_CI_AI_KS_WS");
                ls.Add("18D00400", "Albanian_100_CI_AS");
                ls.Add("18500400", "Albanian_100_CI_AS_WS");
                ls.Add("18900400", "Albanian_100_CI_AS_KS");
                ls.Add("18100400", "Albanian_100_CI_AS_KS_WS");
                ls.Add("18E00400", "Albanian_100_CS_AI");
                ls.Add("18600400", "Albanian_100_CS_AI_WS");
                ls.Add("18A00400", "Albanian_100_CS_AI_KS");
                ls.Add("18200400", "Albanian_100_CS_AI_KS_WS");
                ls.Add("18C00400", "Albanian_100_CS_AS");
                ls.Add("18400400", "Albanian_100_CS_AS_WS");
                ls.Add("18800400", "Albanian_100_CS_AS_KS");
                ls.Add("18000400", "Albanian_100_CS_AS_KS_WS");
                ls.Add("18F10400", "Albanian_100_CI_AI_SC");
                ls.Add("18710400", "Albanian_100_CI_AI_WS_SC");
                ls.Add("18B10400", "Albanian_100_CI_AI_KS_SC");
                ls.Add("18310400", "Albanian_100_CI_AI_KS_WS_SC");
                ls.Add("18D10400", "Albanian_100_CI_AS_SC");
                ls.Add("18510400", "Albanian_100_CI_AS_WS_SC");
                ls.Add("18910400", "Albanian_100_CI_AS_KS_SC");
                ls.Add("18110400", "Albanian_100_CI_AS_KS_WS_SC");
                ls.Add("18E10400", "Albanian_100_CS_AI_SC");
                ls.Add("18610400", "Albanian_100_CS_AI_WS_SC");
                ls.Add("18A10400", "Albanian_100_CS_AI_KS_SC");
                ls.Add("18210400", "Albanian_100_CS_AI_KS_WS_SC");
                ls.Add("18C10400", "Albanian_100_CS_AS_SC");
                ls.Add("18410400", "Albanian_100_CS_AS_WS_SC");
                ls.Add("18810400", "Albanian_100_CS_AS_KS_SC");
                ls.Add("18010400", "Albanian_100_CS_AS_KS_WS_SC");
                ls.Add("01000100", "Arabic_BIN");
                ls.Add("01080000", "Arabic_BIN2");
                ls.Add("01F00000", "Arabic_CI_AI");
                ls.Add("01700000", "Arabic_CI_AI_WS");
                ls.Add("01B00000", "Arabic_CI_AI_KS");
                ls.Add("01300000", "Arabic_CI_AI_KS_WS");
                ls.Add("01D00000", "Arabic_CI_AS");
                ls.Add("01500000", "Arabic_CI_AS_WS");
                ls.Add("01900000", "Arabic_CI_AS_KS");
                ls.Add("01100000", "Arabic_CI_AS_KS_WS");
                ls.Add("01E00000", "Arabic_CS_AI");
                ls.Add("01600000", "Arabic_CS_AI_WS");
                ls.Add("01A00000", "Arabic_CS_AI_KS");
                ls.Add("01200000", "Arabic_CS_AI_KS_WS");
                ls.Add("01C00000", "Arabic_CS_AS");
                ls.Add("01400000", "Arabic_CS_AS_WS");
                ls.Add("01800000", "Arabic_CS_AS_KS");
                ls.Add("01000000", "Arabic_CS_AS_KS_WS");
                ls.Add("01000500", "Arabic_100_BIN");
                ls.Add("01080400", "Arabic_100_BIN2");
                ls.Add("01F00400", "Arabic_100_CI_AI");
                ls.Add("01700400", "Arabic_100_CI_AI_WS");
                ls.Add("01B00400", "Arabic_100_CI_AI_KS");
                ls.Add("01300400", "Arabic_100_CI_AI_KS_WS");
                ls.Add("01D00400", "Arabic_100_CI_AS");
                ls.Add("01500400", "Arabic_100_CI_AS_WS");
                ls.Add("01900400", "Arabic_100_CI_AS_KS");
                ls.Add("01100400", "Arabic_100_CI_AS_KS_WS");
                ls.Add("01E00400", "Arabic_100_CS_AI");
                ls.Add("01600400", "Arabic_100_CS_AI_WS");
                ls.Add("01A00400", "Arabic_100_CS_AI_KS");
                ls.Add("01200400", "Arabic_100_CS_AI_KS_WS");
                ls.Add("01C00400", "Arabic_100_CS_AS");
                ls.Add("01400400", "Arabic_100_CS_AS_WS");
                ls.Add("01800400", "Arabic_100_CS_AS_KS");
                ls.Add("01000400", "Arabic_100_CS_AS_KS_WS");
                ls.Add("01F10400", "Arabic_100_CI_AI_SC");
                ls.Add("01710400", "Arabic_100_CI_AI_WS_SC");
                ls.Add("01B10400", "Arabic_100_CI_AI_KS_SC");
                ls.Add("01310400", "Arabic_100_CI_AI_KS_WS_SC");
                ls.Add("01D10400", "Arabic_100_CI_AS_SC");
                ls.Add("01510400", "Arabic_100_CI_AS_WS_SC");
                ls.Add("01910400", "Arabic_100_CI_AS_KS_SC");
                ls.Add("01110400", "Arabic_100_CI_AS_KS_WS_SC");
                ls.Add("01E10400", "Arabic_100_CS_AI_SC");
                ls.Add("01610400", "Arabic_100_CS_AI_WS_SC");
                ls.Add("01A10400", "Arabic_100_CS_AI_KS_SC");
                ls.Add("01210400", "Arabic_100_CS_AI_KS_WS_SC");
                ls.Add("01C10400", "Arabic_100_CS_AS_SC");
                ls.Add("01410400", "Arabic_100_CS_AS_WS_SC");
                ls.Add("01810400", "Arabic_100_CS_AS_KS_SC");
                ls.Add("01010400", "Arabic_100_CS_AS_KS_WS_SC");
                ls.Add("5A000500", "Assamese_100_BIN");
                ls.Add("5A080400", "Assamese_100_BIN2");
                ls.Add("5AF00400", "Assamese_100_CI_AI");
                ls.Add("5A700400", "Assamese_100_CI_AI_WS");
                ls.Add("5AB00400", "Assamese_100_CI_AI_KS");
                ls.Add("5A300400", "Assamese_100_CI_AI_KS_WS");
                ls.Add("5AD00400", "Assamese_100_CI_AS");
                ls.Add("5A500400", "Assamese_100_CI_AS_WS");
                ls.Add("5A900400", "Assamese_100_CI_AS_KS");
                ls.Add("5A100400", "Assamese_100_CI_AS_KS_WS");
                ls.Add("5AE00400", "Assamese_100_CS_AI");
                ls.Add("5A600400", "Assamese_100_CS_AI_WS");
                ls.Add("5AA00400", "Assamese_100_CS_AI_KS");
                ls.Add("5A200400", "Assamese_100_CS_AI_KS_WS");
                ls.Add("5AC00400", "Assamese_100_CS_AS");
                ls.Add("5A400400", "Assamese_100_CS_AS_WS");
                ls.Add("5A800400", "Assamese_100_CS_AS_KS");
                ls.Add("5A000400", "Assamese_100_CS_AS_KS_WS");
                ls.Add("5AF10400", "Assamese_100_CI_AI_SC");
                ls.Add("5A710400", "Assamese_100_CI_AI_WS_SC");
                ls.Add("5AB10400", "Assamese_100_CI_AI_KS_SC");
                ls.Add("5A310400", "Assamese_100_CI_AI_KS_WS_SC");
                ls.Add("5AD10400", "Assamese_100_CI_AS_SC");
                ls.Add("5A510400", "Assamese_100_CI_AS_WS_SC");
                ls.Add("5A910400", "Assamese_100_CI_AS_KS_SC");
                ls.Add("5A110400", "Assamese_100_CI_AS_KS_WS_SC");
                ls.Add("5AE10400", "Assamese_100_CS_AI_SC");
                ls.Add("5A610400", "Assamese_100_CS_AI_WS_SC");
                ls.Add("5AA10400", "Assamese_100_CS_AI_KS_SC");
                ls.Add("5A210400", "Assamese_100_CS_AI_KS_WS_SC");
                ls.Add("5AC10400", "Assamese_100_CS_AS_SC");
                ls.Add("5A410400", "Assamese_100_CS_AS_WS_SC");
                ls.Add("5A810400", "Assamese_100_CS_AS_KS_SC");
                ls.Add("5A010400", "Assamese_100_CS_AS_KS_WS_SC");
                ls.Add("64000500", "Azeri_Cyrillic_100_BIN");
                ls.Add("64080400", "Azeri_Cyrillic_100_BIN2");
                ls.Add("64F00400", "Azeri_Cyrillic_100_CI_AI");
                ls.Add("64700400", "Azeri_Cyrillic_100_CI_AI_WS");
                ls.Add("64B00400", "Azeri_Cyrillic_100_CI_AI_KS");
                ls.Add("64300400", "Azeri_Cyrillic_100_CI_AI_KS_WS");
                ls.Add("64D00400", "Azeri_Cyrillic_100_CI_AS");
                ls.Add("64500400", "Azeri_Cyrillic_100_CI_AS_WS");
                ls.Add("64900400", "Azeri_Cyrillic_100_CI_AS_KS");
                ls.Add("64100400", "Azeri_Cyrillic_100_CI_AS_KS_WS");
                ls.Add("64E00400", "Azeri_Cyrillic_100_CS_AI");
                ls.Add("64600400", "Azeri_Cyrillic_100_CS_AI_WS");
                ls.Add("64A00400", "Azeri_Cyrillic_100_CS_AI_KS");
                ls.Add("64200400", "Azeri_Cyrillic_100_CS_AI_KS_WS");
                ls.Add("64C00400", "Azeri_Cyrillic_100_CS_AS");
                ls.Add("64400400", "Azeri_Cyrillic_100_CS_AS_WS");
                ls.Add("64800400", "Azeri_Cyrillic_100_CS_AS_KS");
                ls.Add("64000400", "Azeri_Cyrillic_100_CS_AS_KS_WS");
                ls.Add("64F10400", "Azeri_Cyrillic_100_CI_AI_SC");
                ls.Add("64710400", "Azeri_Cyrillic_100_CI_AI_WS_SC");
                ls.Add("64B10400", "Azeri_Cyrillic_100_CI_AI_KS_SC");
                ls.Add("64310400", "Azeri_Cyrillic_100_CI_AI_KS_WS_SC");
                ls.Add("64D10400", "Azeri_Cyrillic_100_CI_AS_SC");
                ls.Add("64510400", "Azeri_Cyrillic_100_CI_AS_WS_SC");
                ls.Add("64910400", "Azeri_Cyrillic_100_CI_AS_KS_SC");
                ls.Add("64110400", "Azeri_Cyrillic_100_CI_AS_KS_WS_SC");
                ls.Add("64E10400", "Azeri_Cyrillic_100_CS_AI_SC");
                ls.Add("64610400", "Azeri_Cyrillic_100_CS_AI_WS_SC");
                ls.Add("64A10400", "Azeri_Cyrillic_100_CS_AI_KS_SC");
                ls.Add("64210400", "Azeri_Cyrillic_100_CS_AI_KS_WS_SC");
                ls.Add("64C10400", "Azeri_Cyrillic_100_CS_AS_SC");
                ls.Add("64410400", "Azeri_Cyrillic_100_CS_AS_WS_SC");
                ls.Add("64810400", "Azeri_Cyrillic_100_CS_AS_KS_SC");
                ls.Add("64010400", "Azeri_Cyrillic_100_CS_AS_KS_WS_SC");
                ls.Add("63000500", "Azeri_Latin_100_BIN");
                ls.Add("63080400", "Azeri_Latin_100_BIN2");
                ls.Add("63F00400", "Azeri_Latin_100_CI_AI");
                ls.Add("63700400", "Azeri_Latin_100_CI_AI_WS");
                ls.Add("63B00400", "Azeri_Latin_100_CI_AI_KS");
                ls.Add("63300400", "Azeri_Latin_100_CI_AI_KS_WS");
                ls.Add("63D00400", "Azeri_Latin_100_CI_AS");
                ls.Add("63500400", "Azeri_Latin_100_CI_AS_WS");
                ls.Add("63900400", "Azeri_Latin_100_CI_AS_KS");
                ls.Add("63100400", "Azeri_Latin_100_CI_AS_KS_WS");
                ls.Add("63E00400", "Azeri_Latin_100_CS_AI");
                ls.Add("63600400", "Azeri_Latin_100_CS_AI_WS");
                ls.Add("63A00400", "Azeri_Latin_100_CS_AI_KS");
                ls.Add("63200400", "Azeri_Latin_100_CS_AI_KS_WS");
                ls.Add("63C00400", "Azeri_Latin_100_CS_AS");
                ls.Add("63400400", "Azeri_Latin_100_CS_AS_WS");
                ls.Add("63800400", "Azeri_Latin_100_CS_AS_KS");
                ls.Add("63000400", "Azeri_Latin_100_CS_AS_KS_WS");
                ls.Add("63F10400", "Azeri_Latin_100_CI_AI_SC");
                ls.Add("63710400", "Azeri_Latin_100_CI_AI_WS_SC");
                ls.Add("63B10400", "Azeri_Latin_100_CI_AI_KS_SC");
                ls.Add("63310400", "Azeri_Latin_100_CI_AI_KS_WS_SC");
                ls.Add("63D10400", "Azeri_Latin_100_CI_AS_SC");
                ls.Add("63510400", "Azeri_Latin_100_CI_AS_WS_SC");
                ls.Add("63910400", "Azeri_Latin_100_CI_AS_KS_SC");
                ls.Add("63110400", "Azeri_Latin_100_CI_AS_KS_WS_SC");
                ls.Add("63E10400", "Azeri_Latin_100_CS_AI_SC");
                ls.Add("63610400", "Azeri_Latin_100_CS_AI_WS_SC");
                ls.Add("63A10400", "Azeri_Latin_100_CS_AI_KS_SC");
                ls.Add("63210400", "Azeri_Latin_100_CS_AI_KS_WS_SC");
                ls.Add("63C10400", "Azeri_Latin_100_CS_AS_SC");
                ls.Add("63410400", "Azeri_Latin_100_CS_AS_WS_SC");
                ls.Add("63810400", "Azeri_Latin_100_CS_AS_KS_SC");
                ls.Add("63010400", "Azeri_Latin_100_CS_AS_KS_WS_SC");
                ls.Add("54000500", "Bashkir_100_BIN");
                ls.Add("54080400", "Bashkir_100_BIN2");
                ls.Add("54F00400", "Bashkir_100_CI_AI");
                ls.Add("54700400", "Bashkir_100_CI_AI_WS");
                ls.Add("54B00400", "Bashkir_100_CI_AI_KS");
                ls.Add("54300400", "Bashkir_100_CI_AI_KS_WS");
                ls.Add("54D00400", "Bashkir_100_CI_AS");
                ls.Add("54500400", "Bashkir_100_CI_AS_WS");
                ls.Add("54900400", "Bashkir_100_CI_AS_KS");
                ls.Add("54100400", "Bashkir_100_CI_AS_KS_WS");
                ls.Add("54E00400", "Bashkir_100_CS_AI");
                ls.Add("54600400", "Bashkir_100_CS_AI_WS");
                ls.Add("54A00400", "Bashkir_100_CS_AI_KS");
                ls.Add("54200400", "Bashkir_100_CS_AI_KS_WS");
                ls.Add("54C00400", "Bashkir_100_CS_AS");
                ls.Add("54400400", "Bashkir_100_CS_AS_WS");
                ls.Add("54800400", "Bashkir_100_CS_AS_KS");
                ls.Add("54000400", "Bashkir_100_CS_AS_KS_WS");
                ls.Add("54F10400", "Bashkir_100_CI_AI_SC");
                ls.Add("54710400", "Bashkir_100_CI_AI_WS_SC");
                ls.Add("54B10400", "Bashkir_100_CI_AI_KS_SC");
                ls.Add("54310400", "Bashkir_100_CI_AI_KS_WS_SC");
                ls.Add("54D10400", "Bashkir_100_CI_AS_SC");
                ls.Add("54510400", "Bashkir_100_CI_AS_WS_SC");
                ls.Add("54910400", "Bashkir_100_CI_AS_KS_SC");
                ls.Add("54110400", "Bashkir_100_CI_AS_KS_WS_SC");
                ls.Add("54E10400", "Bashkir_100_CS_AI_SC");
                ls.Add("54610400", "Bashkir_100_CS_AI_WS_SC");
                ls.Add("54A10400", "Bashkir_100_CS_AI_KS_SC");
                ls.Add("54210400", "Bashkir_100_CS_AI_KS_WS_SC");
                ls.Add("54C10400", "Bashkir_100_CS_AS_SC");
                ls.Add("54410400", "Bashkir_100_CS_AS_WS_SC");
                ls.Add("54810400", "Bashkir_100_CS_AS_KS_SC");
                ls.Add("54010400", "Bashkir_100_CS_AS_KS_WS_SC");
                ls.Add("59000500", "Bengali_100_BIN");
                ls.Add("59080400", "Bengali_100_BIN2");
                ls.Add("59F00400", "Bengali_100_CI_AI");
                ls.Add("59700400", "Bengali_100_CI_AI_WS");
                ls.Add("59B00400", "Bengali_100_CI_AI_KS");
                ls.Add("59300400", "Bengali_100_CI_AI_KS_WS");
                ls.Add("59D00400", "Bengali_100_CI_AS");
                ls.Add("59500400", "Bengali_100_CI_AS_WS");
                ls.Add("59900400", "Bengali_100_CI_AS_KS");
                ls.Add("59100400", "Bengali_100_CI_AS_KS_WS");
                ls.Add("59E00400", "Bengali_100_CS_AI");
                ls.Add("59600400", "Bengali_100_CS_AI_WS");
                ls.Add("59A00400", "Bengali_100_CS_AI_KS");
                ls.Add("59200400", "Bengali_100_CS_AI_KS_WS");
                ls.Add("59C00400", "Bengali_100_CS_AS");
                ls.Add("59400400", "Bengali_100_CS_AS_WS");
                ls.Add("59800400", "Bengali_100_CS_AS_KS");
                ls.Add("59000400", "Bengali_100_CS_AS_KS_WS");
                ls.Add("59F10400", "Bengali_100_CI_AI_SC");
                ls.Add("59710400", "Bengali_100_CI_AI_WS_SC");
                ls.Add("59B10400", "Bengali_100_CI_AI_KS_SC");
                ls.Add("59310400", "Bengali_100_CI_AI_KS_WS_SC");
                ls.Add("59D10400", "Bengali_100_CI_AS_SC");
                ls.Add("59510400", "Bengali_100_CI_AS_WS_SC");
                ls.Add("59910400", "Bengali_100_CI_AS_KS_SC");
                ls.Add("59110400", "Bengali_100_CI_AS_KS_WS_SC");
                ls.Add("59E10400", "Bengali_100_CS_AI_SC");
                ls.Add("59610400", "Bengali_100_CS_AI_WS_SC");
                ls.Add("59A10400", "Bengali_100_CS_AI_KS_SC");
                ls.Add("59210400", "Bengali_100_CS_AI_KS_WS_SC");
                ls.Add("59C10400", "Bengali_100_CS_AS_SC");
                ls.Add("59410400", "Bengali_100_CS_AS_WS_SC");
                ls.Add("59810400", "Bengali_100_CS_AS_KS_SC");
                ls.Add("59010400", "Bengali_100_CS_AS_KS_WS_SC");
                ls.Add("4F000500", "Bosnian_Cyrillic_100_BIN");
                ls.Add("4F080400", "Bosnian_Cyrillic_100_BIN2");
                ls.Add("4FF00400", "Bosnian_Cyrillic_100_CI_AI");
                ls.Add("4F700400", "Bosnian_Cyrillic_100_CI_AI_WS");
                ls.Add("4FB00400", "Bosnian_Cyrillic_100_CI_AI_KS");
                ls.Add("4F300400", "Bosnian_Cyrillic_100_CI_AI_KS_WS");
                ls.Add("4FD00400", "Bosnian_Cyrillic_100_CI_AS");
                ls.Add("4F500400", "Bosnian_Cyrillic_100_CI_AS_WS");
                ls.Add("4F900400", "Bosnian_Cyrillic_100_CI_AS_KS");
                ls.Add("4F100400", "Bosnian_Cyrillic_100_CI_AS_KS_WS");
                ls.Add("4FE00400", "Bosnian_Cyrillic_100_CS_AI");
                ls.Add("4F600400", "Bosnian_Cyrillic_100_CS_AI_WS");
                ls.Add("4FA00400", "Bosnian_Cyrillic_100_CS_AI_KS");
                ls.Add("4F200400", "Bosnian_Cyrillic_100_CS_AI_KS_WS");
                ls.Add("4FC00400", "Bosnian_Cyrillic_100_CS_AS");
                ls.Add("4F400400", "Bosnian_Cyrillic_100_CS_AS_WS");
                ls.Add("4F800400", "Bosnian_Cyrillic_100_CS_AS_KS");
                ls.Add("4F000400", "Bosnian_Cyrillic_100_CS_AS_KS_WS");
                ls.Add("4FF10400", "Bosnian_Cyrillic_100_CI_AI_SC");
                ls.Add("4F710400", "Bosnian_Cyrillic_100_CI_AI_WS_SC");
                ls.Add("4FB10400", "Bosnian_Cyrillic_100_CI_AI_KS_SC");
                ls.Add("4F310400", "Bosnian_Cyrillic_100_CI_AI_KS_WS_SC");
                ls.Add("4FD10400", "Bosnian_Cyrillic_100_CI_AS_SC");
                ls.Add("4F510400", "Bosnian_Cyrillic_100_CI_AS_WS_SC");
                ls.Add("4F910400", "Bosnian_Cyrillic_100_CI_AS_KS_SC");
                ls.Add("4F110400", "Bosnian_Cyrillic_100_CI_AS_KS_WS_SC");
                ls.Add("4FE10400", "Bosnian_Cyrillic_100_CS_AI_SC");
                ls.Add("4F610400", "Bosnian_Cyrillic_100_CS_AI_WS_SC");
                ls.Add("4FA10400", "Bosnian_Cyrillic_100_CS_AI_KS_SC");
                ls.Add("4F210400", "Bosnian_Cyrillic_100_CS_AI_KS_WS_SC");
                ls.Add("4FC10400", "Bosnian_Cyrillic_100_CS_AS_SC");
                ls.Add("4F410400", "Bosnian_Cyrillic_100_CS_AS_WS_SC");
                ls.Add("4F810400", "Bosnian_Cyrillic_100_CS_AS_KS_SC");
                ls.Add("4F010400", "Bosnian_Cyrillic_100_CS_AS_KS_WS_SC");
                ls.Add("4E000500", "Bosnian_Latin_100_BIN");
                ls.Add("4E080400", "Bosnian_Latin_100_BIN2");
                ls.Add("4EF00400", "Bosnian_Latin_100_CI_AI");
                ls.Add("4E700400", "Bosnian_Latin_100_CI_AI_WS");
                ls.Add("4EB00400", "Bosnian_Latin_100_CI_AI_KS");
                ls.Add("4E300400", "Bosnian_Latin_100_CI_AI_KS_WS");
                ls.Add("4ED00400", "Bosnian_Latin_100_CI_AS");
                ls.Add("4E500400", "Bosnian_Latin_100_CI_AS_WS");
                ls.Add("4E900400", "Bosnian_Latin_100_CI_AS_KS");
                ls.Add("4E100400", "Bosnian_Latin_100_CI_AS_KS_WS");
                ls.Add("4EE00400", "Bosnian_Latin_100_CS_AI");
                ls.Add("4E600400", "Bosnian_Latin_100_CS_AI_WS");
                ls.Add("4EA00400", "Bosnian_Latin_100_CS_AI_KS");
                ls.Add("4E200400", "Bosnian_Latin_100_CS_AI_KS_WS");
                ls.Add("4EC00400", "Bosnian_Latin_100_CS_AS");
                ls.Add("4E400400", "Bosnian_Latin_100_CS_AS_WS");
                ls.Add("4E800400", "Bosnian_Latin_100_CS_AS_KS");
                ls.Add("4E000400", "Bosnian_Latin_100_CS_AS_KS_WS");
                ls.Add("4EF10400", "Bosnian_Latin_100_CI_AI_SC");
                ls.Add("4E710400", "Bosnian_Latin_100_CI_AI_WS_SC");
                ls.Add("4EB10400", "Bosnian_Latin_100_CI_AI_KS_SC");
                ls.Add("4E310400", "Bosnian_Latin_100_CI_AI_KS_WS_SC");
                ls.Add("4ED10400", "Bosnian_Latin_100_CI_AS_SC");
                ls.Add("4E510400", "Bosnian_Latin_100_CI_AS_WS_SC");
                ls.Add("4E910400", "Bosnian_Latin_100_CI_AS_KS_SC");
                ls.Add("4E110400", "Bosnian_Latin_100_CI_AS_KS_WS_SC");
                ls.Add("4EE10400", "Bosnian_Latin_100_CS_AI_SC");
                ls.Add("4E610400", "Bosnian_Latin_100_CS_AI_WS_SC");
                ls.Add("4EA10400", "Bosnian_Latin_100_CS_AI_KS_SC");
                ls.Add("4E210400", "Bosnian_Latin_100_CS_AI_KS_WS_SC");
                ls.Add("4EC10400", "Bosnian_Latin_100_CS_AS_SC");
                ls.Add("4E410400", "Bosnian_Latin_100_CS_AS_WS_SC");
                ls.Add("4E810400", "Bosnian_Latin_100_CS_AS_KS_SC");
                ls.Add("4E010400", "Bosnian_Latin_100_CS_AS_KS_WS_SC");
                ls.Add("26000500", "Breton_100_BIN");
                ls.Add("26080400", "Breton_100_BIN2");
                ls.Add("26F00400", "Breton_100_CI_AI");
                ls.Add("26700400", "Breton_100_CI_AI_WS");
                ls.Add("26B00400", "Breton_100_CI_AI_KS");
                ls.Add("26300400", "Breton_100_CI_AI_KS_WS");
                ls.Add("26D00400", "Breton_100_CI_AS");
                ls.Add("26500400", "Breton_100_CI_AS_WS");
                ls.Add("26900400", "Breton_100_CI_AS_KS");
                ls.Add("26100400", "Breton_100_CI_AS_KS_WS");
                ls.Add("26E00400", "Breton_100_CS_AI");
                ls.Add("26600400", "Breton_100_CS_AI_WS");
                ls.Add("26A00400", "Breton_100_CS_AI_KS");
                ls.Add("26200400", "Breton_100_CS_AI_KS_WS");
                ls.Add("26C00400", "Breton_100_CS_AS");
                ls.Add("26400400", "Breton_100_CS_AS_WS");
                ls.Add("26800400", "Breton_100_CS_AS_KS");
                ls.Add("26000400", "Breton_100_CS_AS_KS_WS");
                ls.Add("26F10400", "Breton_100_CI_AI_SC");
                ls.Add("26710400", "Breton_100_CI_AI_WS_SC");
                ls.Add("26B10400", "Breton_100_CI_AI_KS_SC");
                ls.Add("26310400", "Breton_100_CI_AI_KS_WS_SC");
                ls.Add("26D10400", "Breton_100_CI_AS_SC");
                ls.Add("26510400", "Breton_100_CI_AS_WS_SC");
                ls.Add("26910400", "Breton_100_CI_AS_KS_SC");
                ls.Add("26110400", "Breton_100_CI_AS_KS_WS_SC");
                ls.Add("26E10400", "Breton_100_CS_AI_SC");
                ls.Add("26610400", "Breton_100_CS_AI_WS_SC");
                ls.Add("26A10400", "Breton_100_CS_AI_KS_SC");
                ls.Add("26210400", "Breton_100_CS_AI_KS_WS_SC");
                ls.Add("26C10400", "Breton_100_CS_AS_SC");
                ls.Add("26410400", "Breton_100_CS_AS_WS_SC");
                ls.Add("26810400", "Breton_100_CS_AS_KS_SC");
                ls.Add("26010400", "Breton_100_CS_AS_KS_WS_SC");
                ls.Add("3E000300", "Chinese_Hong_Kong_Stroke_90_BIN");
                ls.Add("3E080200", "Chinese_Hong_Kong_Stroke_90_BIN2");
                ls.Add("3EF00200", "Chinese_Hong_Kong_Stroke_90_CI_AI");
                ls.Add("3E700200", "Chinese_Hong_Kong_Stroke_90_CI_AI_WS");
                ls.Add("3EB00200", "Chinese_Hong_Kong_Stroke_90_CI_AI_KS");
                ls.Add("3E300200", "Chinese_Hong_Kong_Stroke_90_CI_AI_KS_WS");
                ls.Add("3ED00200", "Chinese_Hong_Kong_Stroke_90_CI_AS");
                ls.Add("3E500200", "Chinese_Hong_Kong_Stroke_90_CI_AS_WS");
                ls.Add("3E900200", "Chinese_Hong_Kong_Stroke_90_CI_AS_KS");
                ls.Add("3E100200", "Chinese_Hong_Kong_Stroke_90_CI_AS_KS_WS");
                ls.Add("3EE00200", "Chinese_Hong_Kong_Stroke_90_CS_AI");
                ls.Add("3E600200", "Chinese_Hong_Kong_Stroke_90_CS_AI_WS");
                ls.Add("3EA00200", "Chinese_Hong_Kong_Stroke_90_CS_AI_KS");
                ls.Add("3E200200", "Chinese_Hong_Kong_Stroke_90_CS_AI_KS_WS");
                ls.Add("3EC00200", "Chinese_Hong_Kong_Stroke_90_CS_AS");
                ls.Add("3E400200", "Chinese_Hong_Kong_Stroke_90_CS_AS_WS");
                ls.Add("3E800200", "Chinese_Hong_Kong_Stroke_90_CS_AS_KS");
                ls.Add("3E000200", "Chinese_Hong_Kong_Stroke_90_CS_AS_KS_WS");
                ls.Add("3EF10200", "Chinese_Hong_Kong_Stroke_90_CI_AI_SC");
                ls.Add("3E710200", "Chinese_Hong_Kong_Stroke_90_CI_AI_WS_SC");
                ls.Add("3EB10200", "Chinese_Hong_Kong_Stroke_90_CI_AI_KS_SC");
                ls.Add("3E310200", "Chinese_Hong_Kong_Stroke_90_CI_AI_KS_WS_SC");
                ls.Add("3ED10200", "Chinese_Hong_Kong_Stroke_90_CI_AS_SC");
                ls.Add("3E510200", "Chinese_Hong_Kong_Stroke_90_CI_AS_WS_SC");
                ls.Add("3E910200", "Chinese_Hong_Kong_Stroke_90_CI_AS_KS_SC");
                ls.Add("3E110200", "Chinese_Hong_Kong_Stroke_90_CI_AS_KS_WS_SC");
                ls.Add("3EE10200", "Chinese_Hong_Kong_Stroke_90_CS_AI_SC");
                ls.Add("3E610200", "Chinese_Hong_Kong_Stroke_90_CS_AI_WS_SC");
                ls.Add("3EA10200", "Chinese_Hong_Kong_Stroke_90_CS_AI_KS_SC");
                ls.Add("3E210200", "Chinese_Hong_Kong_Stroke_90_CS_AI_KS_WS_SC");
                ls.Add("3EC10200", "Chinese_Hong_Kong_Stroke_90_CS_AS_SC");
                ls.Add("3E410200", "Chinese_Hong_Kong_Stroke_90_CS_AS_WS_SC");
                ls.Add("3E810200", "Chinese_Hong_Kong_Stroke_90_CS_AS_KS_SC");
                ls.Add("3E010200", "Chinese_Hong_Kong_Stroke_90_CS_AS_KS_WS_SC");
                ls.Add("24000100", "Chinese_PRC_BIN");
                ls.Add("24080000", "Chinese_PRC_BIN2");
                ls.Add("24F00000", "Chinese_PRC_CI_AI");
                ls.Add("24700000", "Chinese_PRC_CI_AI_WS");
                ls.Add("24B00000", "Chinese_PRC_CI_AI_KS");
                ls.Add("24300000", "Chinese_PRC_CI_AI_KS_WS");
                ls.Add("24D00000", "Chinese_PRC_CI_AS");
                ls.Add("24500000", "Chinese_PRC_CI_AS_WS");
                ls.Add("24900000", "Chinese_PRC_CI_AS_KS");
                ls.Add("24100000", "Chinese_PRC_CI_AS_KS_WS");
                ls.Add("24E00000", "Chinese_PRC_CS_AI");
                ls.Add("24600000", "Chinese_PRC_CS_AI_WS");
                ls.Add("24A00000", "Chinese_PRC_CS_AI_KS");
                ls.Add("24200000", "Chinese_PRC_CS_AI_KS_WS");
                ls.Add("24C00000", "Chinese_PRC_CS_AS");
                ls.Add("24400000", "Chinese_PRC_CS_AS_WS");
                ls.Add("24800000", "Chinese_PRC_CS_AS_KS");
                ls.Add("24000000", "Chinese_PRC_CS_AS_KS_WS");
                ls.Add("31000300", "Chinese_PRC_90_BIN");
                ls.Add("31080200", "Chinese_PRC_90_BIN2");
                ls.Add("31F00200", "Chinese_PRC_90_CI_AI");
                ls.Add("31700200", "Chinese_PRC_90_CI_AI_WS");
                ls.Add("31B00200", "Chinese_PRC_90_CI_AI_KS");
                ls.Add("31300200", "Chinese_PRC_90_CI_AI_KS_WS");
                ls.Add("31D00200", "Chinese_PRC_90_CI_AS");
                ls.Add("31500200", "Chinese_PRC_90_CI_AS_WS");
                ls.Add("31900200", "Chinese_PRC_90_CI_AS_KS");
                ls.Add("31100200", "Chinese_PRC_90_CI_AS_KS_WS");
                ls.Add("31E00200", "Chinese_PRC_90_CS_AI");
                ls.Add("31600200", "Chinese_PRC_90_CS_AI_WS");
                ls.Add("31A00200", "Chinese_PRC_90_CS_AI_KS");
                ls.Add("31200200", "Chinese_PRC_90_CS_AI_KS_WS");
                ls.Add("31C00200", "Chinese_PRC_90_CS_AS");
                ls.Add("31400200", "Chinese_PRC_90_CS_AS_WS");
                ls.Add("31800200", "Chinese_PRC_90_CS_AS_KS");
                ls.Add("31000200", "Chinese_PRC_90_CS_AS_KS_WS");
                ls.Add("31F10200", "Chinese_PRC_90_CI_AI_SC");
                ls.Add("31710200", "Chinese_PRC_90_CI_AI_WS_SC");
                ls.Add("31B10200", "Chinese_PRC_90_CI_AI_KS_SC");
                ls.Add("31310200", "Chinese_PRC_90_CI_AI_KS_WS_SC");
                ls.Add("31D10200", "Chinese_PRC_90_CI_AS_SC");
                ls.Add("31510200", "Chinese_PRC_90_CI_AS_WS_SC");
                ls.Add("31910200", "Chinese_PRC_90_CI_AS_KS_SC");
                ls.Add("31110200", "Chinese_PRC_90_CI_AS_KS_WS_SC");
                ls.Add("31E10200", "Chinese_PRC_90_CS_AI_SC");
                ls.Add("31610200", "Chinese_PRC_90_CS_AI_WS_SC");
                ls.Add("31A10200", "Chinese_PRC_90_CS_AI_KS_SC");
                ls.Add("31210200", "Chinese_PRC_90_CS_AI_KS_WS_SC");
                ls.Add("31C10200", "Chinese_PRC_90_CS_AS_SC");
                ls.Add("31410200", "Chinese_PRC_90_CS_AS_WS_SC");
                ls.Add("31810200", "Chinese_PRC_90_CS_AS_KS_SC");
                ls.Add("31010200", "Chinese_PRC_90_CS_AS_KS_WS_SC");
                ls.Add("2E000100", "Chinese_PRC_Stroke_BIN");
                ls.Add("2E080000", "Chinese_PRC_Stroke_BIN2");
                ls.Add("2EF00000", "Chinese_PRC_Stroke_CI_AI");
                ls.Add("2E700000", "Chinese_PRC_Stroke_CI_AI_WS");
                ls.Add("2EB00000", "Chinese_PRC_Stroke_CI_AI_KS");
                ls.Add("2E300000", "Chinese_PRC_Stroke_CI_AI_KS_WS");
                ls.Add("2ED00000", "Chinese_PRC_Stroke_CI_AS");
                ls.Add("2E500000", "Chinese_PRC_Stroke_CI_AS_WS");
                ls.Add("2E900000", "Chinese_PRC_Stroke_CI_AS_KS");
                ls.Add("2E100000", "Chinese_PRC_Stroke_CI_AS_KS_WS");
                ls.Add("2EE00000", "Chinese_PRC_Stroke_CS_AI");
                ls.Add("2E600000", "Chinese_PRC_Stroke_CS_AI_WS");
                ls.Add("2EA00000", "Chinese_PRC_Stroke_CS_AI_KS");
                ls.Add("2E200000", "Chinese_PRC_Stroke_CS_AI_KS_WS");
                ls.Add("2EC00000", "Chinese_PRC_Stroke_CS_AS");
                ls.Add("2E400000", "Chinese_PRC_Stroke_CS_AS_WS");
                ls.Add("2E800000", "Chinese_PRC_Stroke_CS_AS_KS");
                ls.Add("2E000000", "Chinese_PRC_Stroke_CS_AS_KS_WS");
                ls.Add("32000300", "Chinese_PRC_Stroke_90_BIN");
                ls.Add("32080200", "Chinese_PRC_Stroke_90_BIN2");
                ls.Add("32F00200", "Chinese_PRC_Stroke_90_CI_AI");
                ls.Add("32700200", "Chinese_PRC_Stroke_90_CI_AI_WS");
                ls.Add("32B00200", "Chinese_PRC_Stroke_90_CI_AI_KS");
                ls.Add("32300200", "Chinese_PRC_Stroke_90_CI_AI_KS_WS");
                ls.Add("32D00200", "Chinese_PRC_Stroke_90_CI_AS");
                ls.Add("32500200", "Chinese_PRC_Stroke_90_CI_AS_WS");
                ls.Add("32900200", "Chinese_PRC_Stroke_90_CI_AS_KS");
                ls.Add("32100200", "Chinese_PRC_Stroke_90_CI_AS_KS_WS");
                ls.Add("32E00200", "Chinese_PRC_Stroke_90_CS_AI");
                ls.Add("32600200", "Chinese_PRC_Stroke_90_CS_AI_WS");
                ls.Add("32A00200", "Chinese_PRC_Stroke_90_CS_AI_KS");
                ls.Add("32200200", "Chinese_PRC_Stroke_90_CS_AI_KS_WS");
                ls.Add("32C00200", "Chinese_PRC_Stroke_90_CS_AS");
                ls.Add("32400200", "Chinese_PRC_Stroke_90_CS_AS_WS");
                ls.Add("32800200", "Chinese_PRC_Stroke_90_CS_AS_KS");
                ls.Add("32000200", "Chinese_PRC_Stroke_90_CS_AS_KS_WS");
                ls.Add("32F10200", "Chinese_PRC_Stroke_90_CI_AI_SC");
                ls.Add("32710200", "Chinese_PRC_Stroke_90_CI_AI_WS_SC");
                ls.Add("32B10200", "Chinese_PRC_Stroke_90_CI_AI_KS_SC");
                ls.Add("32310200", "Chinese_PRC_Stroke_90_CI_AI_KS_WS_SC");
                ls.Add("32D10200", "Chinese_PRC_Stroke_90_CI_AS_SC");
                ls.Add("32510200", "Chinese_PRC_Stroke_90_CI_AS_WS_SC");
                ls.Add("32910200", "Chinese_PRC_Stroke_90_CI_AS_KS_SC");
                ls.Add("32110200", "Chinese_PRC_Stroke_90_CI_AS_KS_WS_SC");
                ls.Add("32E10200", "Chinese_PRC_Stroke_90_CS_AI_SC");
                ls.Add("32610200", "Chinese_PRC_Stroke_90_CS_AI_WS_SC");
                ls.Add("32A10200", "Chinese_PRC_Stroke_90_CS_AI_KS_SC");
                ls.Add("32210200", "Chinese_PRC_Stroke_90_CS_AI_KS_WS_SC");
                ls.Add("32C10200", "Chinese_PRC_Stroke_90_CS_AS_SC");
                ls.Add("32410200", "Chinese_PRC_Stroke_90_CS_AS_WS_SC");
                ls.Add("32810200", "Chinese_PRC_Stroke_90_CS_AS_KS_SC");
                ls.Add("32010200", "Chinese_PRC_Stroke_90_CS_AS_KS_WS_SC");
                ls.Add("43000500", "Chinese_Simplified_Pinyin_100_BIN");
                ls.Add("43080400", "Chinese_Simplified_Pinyin_100_BIN2");
                ls.Add("43F00400", "Chinese_Simplified_Pinyin_100_CI_AI");
                ls.Add("43700400", "Chinese_Simplified_Pinyin_100_CI_AI_WS");
                ls.Add("43B00400", "Chinese_Simplified_Pinyin_100_CI_AI_KS");
                ls.Add("43300400", "Chinese_Simplified_Pinyin_100_CI_AI_KS_WS");
                ls.Add("43D00400", "Chinese_Simplified_Pinyin_100_CI_AS");
                ls.Add("43500400", "Chinese_Simplified_Pinyin_100_CI_AS_WS");
                ls.Add("43900400", "Chinese_Simplified_Pinyin_100_CI_AS_KS");
                ls.Add("43100400", "Chinese_Simplified_Pinyin_100_CI_AS_KS_WS");
                ls.Add("43E00400", "Chinese_Simplified_Pinyin_100_CS_AI");
                ls.Add("43600400", "Chinese_Simplified_Pinyin_100_CS_AI_WS");
                ls.Add("43A00400", "Chinese_Simplified_Pinyin_100_CS_AI_KS");
                ls.Add("43200400", "Chinese_Simplified_Pinyin_100_CS_AI_KS_WS");
                ls.Add("43C00400", "Chinese_Simplified_Pinyin_100_CS_AS");
                ls.Add("43400400", "Chinese_Simplified_Pinyin_100_CS_AS_WS");
                ls.Add("43800400", "Chinese_Simplified_Pinyin_100_CS_AS_KS");
                ls.Add("43000400", "Chinese_Simplified_Pinyin_100_CS_AS_KS_WS");
                ls.Add("43F10400", "Chinese_Simplified_Pinyin_100_CI_AI_SC");
                ls.Add("43710400", "Chinese_Simplified_Pinyin_100_CI_AI_WS_SC");
                ls.Add("43B10400", "Chinese_Simplified_Pinyin_100_CI_AI_KS_SC");
                ls.Add("43310400", "Chinese_Simplified_Pinyin_100_CI_AI_KS_WS_SC");
                ls.Add("43D10400", "Chinese_Simplified_Pinyin_100_CI_AS_SC");
                ls.Add("43510400", "Chinese_Simplified_Pinyin_100_CI_AS_WS_SC");
                ls.Add("43910400", "Chinese_Simplified_Pinyin_100_CI_AS_KS_SC");
                ls.Add("43110400", "Chinese_Simplified_Pinyin_100_CI_AS_KS_WS_SC");
                ls.Add("43E10400", "Chinese_Simplified_Pinyin_100_CS_AI_SC");
                ls.Add("43610400", "Chinese_Simplified_Pinyin_100_CS_AI_WS_SC");
                ls.Add("43A10400", "Chinese_Simplified_Pinyin_100_CS_AI_KS_SC");
                ls.Add("43210400", "Chinese_Simplified_Pinyin_100_CS_AI_KS_WS_SC");
                ls.Add("43C10400", "Chinese_Simplified_Pinyin_100_CS_AS_SC");
                ls.Add("43410400", "Chinese_Simplified_Pinyin_100_CS_AS_WS_SC");
                ls.Add("43810400", "Chinese_Simplified_Pinyin_100_CS_AS_KS_SC");
                ls.Add("43010400", "Chinese_Simplified_Pinyin_100_CS_AS_KS_WS_SC");
                ls.Add("44000500", "Chinese_Simplified_Stroke_Order_100_BIN");
                ls.Add("44080400", "Chinese_Simplified_Stroke_Order_100_BIN2");
                ls.Add("44F00400", "Chinese_Simplified_Stroke_Order_100_CI_AI");
                ls.Add("44700400", "Chinese_Simplified_Stroke_Order_100_CI_AI_WS");
                ls.Add("44B00400", "Chinese_Simplified_Stroke_Order_100_CI_AI_KS");
                ls.Add("44300400", "Chinese_Simplified_Stroke_Order_100_CI_AI_KS_WS");
                ls.Add("44D00400", "Chinese_Simplified_Stroke_Order_100_CI_AS");
                ls.Add("44500400", "Chinese_Simplified_Stroke_Order_100_CI_AS_WS");
                ls.Add("44900400", "Chinese_Simplified_Stroke_Order_100_CI_AS_KS");
                ls.Add("44100400", "Chinese_Simplified_Stroke_Order_100_CI_AS_KS_WS");
                ls.Add("44E00400", "Chinese_Simplified_Stroke_Order_100_CS_AI");
                ls.Add("44600400", "Chinese_Simplified_Stroke_Order_100_CS_AI_WS");
                ls.Add("44A00400", "Chinese_Simplified_Stroke_Order_100_CS_AI_KS");
                ls.Add("44200400", "Chinese_Simplified_Stroke_Order_100_CS_AI_KS_WS");
                ls.Add("44C00400", "Chinese_Simplified_Stroke_Order_100_CS_AS");
                ls.Add("44400400", "Chinese_Simplified_Stroke_Order_100_CS_AS_WS");
                ls.Add("44800400", "Chinese_Simplified_Stroke_Order_100_CS_AS_KS");
                ls.Add("44000400", "Chinese_Simplified_Stroke_Order_100_CS_AS_KS_WS");
                ls.Add("44F10400", "Chinese_Simplified_Stroke_Order_100_CI_AI_SC");
                ls.Add("44710400", "Chinese_Simplified_Stroke_Order_100_CI_AI_WS_SC");
                ls.Add("44B10400", "Chinese_Simplified_Stroke_Order_100_CI_AI_KS_SC");
                ls.Add("44310400", "Chinese_Simplified_Stroke_Order_100_CI_AI_KS_WS_SC");
                ls.Add("44D10400", "Chinese_Simplified_Stroke_Order_100_CI_AS_SC");
                ls.Add("44510400", "Chinese_Simplified_Stroke_Order_100_CI_AS_WS_SC");
                ls.Add("44910400", "Chinese_Simplified_Stroke_Order_100_CI_AS_KS_SC");
                ls.Add("44110400", "Chinese_Simplified_Stroke_Order_100_CI_AS_KS_WS_SC");
                ls.Add("44E10400", "Chinese_Simplified_Stroke_Order_100_CS_AI_SC");
                ls.Add("44610400", "Chinese_Simplified_Stroke_Order_100_CS_AI_WS_SC");
                ls.Add("44A10400", "Chinese_Simplified_Stroke_Order_100_CS_AI_KS_SC");
                ls.Add("44210400", "Chinese_Simplified_Stroke_Order_100_CS_AI_KS_WS_SC");
                ls.Add("44C10400", "Chinese_Simplified_Stroke_Order_100_CS_AS_SC");
                ls.Add("44410400", "Chinese_Simplified_Stroke_Order_100_CS_AS_WS_SC");
                ls.Add("44810400", "Chinese_Simplified_Stroke_Order_100_CS_AS_KS_SC");
                ls.Add("44010400", "Chinese_Simplified_Stroke_Order_100_CS_AS_KS_WS_SC");
                ls.Add("2F000100", "Chinese_Taiwan_Bopomofo_BIN");
                ls.Add("2F080000", "Chinese_Taiwan_Bopomofo_BIN2");
                ls.Add("2FF00000", "Chinese_Taiwan_Bopomofo_CI_AI");
                ls.Add("2F700000", "Chinese_Taiwan_Bopomofo_CI_AI_WS");
                ls.Add("2FB00000", "Chinese_Taiwan_Bopomofo_CI_AI_KS");
                ls.Add("2F300000", "Chinese_Taiwan_Bopomofo_CI_AI_KS_WS");
                ls.Add("2FD00000", "Chinese_Taiwan_Bopomofo_CI_AS");
                ls.Add("2F500000", "Chinese_Taiwan_Bopomofo_CI_AS_WS");
                ls.Add("2F900000", "Chinese_Taiwan_Bopomofo_CI_AS_KS");
                ls.Add("2F100000", "Chinese_Taiwan_Bopomofo_CI_AS_KS_WS");
                ls.Add("2FE00000", "Chinese_Taiwan_Bopomofo_CS_AI");
                ls.Add("2F600000", "Chinese_Taiwan_Bopomofo_CS_AI_WS");
                ls.Add("2FA00000", "Chinese_Taiwan_Bopomofo_CS_AI_KS");
                ls.Add("2F200000", "Chinese_Taiwan_Bopomofo_CS_AI_KS_WS");
                ls.Add("2FC00000", "Chinese_Taiwan_Bopomofo_CS_AS");
                ls.Add("2F400000", "Chinese_Taiwan_Bopomofo_CS_AS_WS");
                ls.Add("2F800000", "Chinese_Taiwan_Bopomofo_CS_AS_KS");
                ls.Add("2F000000", "Chinese_Taiwan_Bopomofo_CS_AS_KS_WS");
                ls.Add("33000300", "Chinese_Taiwan_Bopomofo_90_BIN");
                ls.Add("33080200", "Chinese_Taiwan_Bopomofo_90_BIN2");
                ls.Add("33F00200", "Chinese_Taiwan_Bopomofo_90_CI_AI");
                ls.Add("33700200", "Chinese_Taiwan_Bopomofo_90_CI_AI_WS");
                ls.Add("33B00200", "Chinese_Taiwan_Bopomofo_90_CI_AI_KS");
                ls.Add("33300200", "Chinese_Taiwan_Bopomofo_90_CI_AI_KS_WS");
                ls.Add("33D00200", "Chinese_Taiwan_Bopomofo_90_CI_AS");
                ls.Add("33500200", "Chinese_Taiwan_Bopomofo_90_CI_AS_WS");
                ls.Add("33900200", "Chinese_Taiwan_Bopomofo_90_CI_AS_KS");
                ls.Add("33100200", "Chinese_Taiwan_Bopomofo_90_CI_AS_KS_WS");
                ls.Add("33E00200", "Chinese_Taiwan_Bopomofo_90_CS_AI");
                ls.Add("33600200", "Chinese_Taiwan_Bopomofo_90_CS_AI_WS");
                ls.Add("33A00200", "Chinese_Taiwan_Bopomofo_90_CS_AI_KS");
                ls.Add("33200200", "Chinese_Taiwan_Bopomofo_90_CS_AI_KS_WS");
                ls.Add("33C00200", "Chinese_Taiwan_Bopomofo_90_CS_AS");
                ls.Add("33400200", "Chinese_Taiwan_Bopomofo_90_CS_AS_WS");
                ls.Add("33800200", "Chinese_Taiwan_Bopomofo_90_CS_AS_KS");
                ls.Add("33000200", "Chinese_Taiwan_Bopomofo_90_CS_AS_KS_WS");
                ls.Add("33F10200", "Chinese_Taiwan_Bopomofo_90_CI_AI_SC");
                ls.Add("33710200", "Chinese_Taiwan_Bopomofo_90_CI_AI_WS_SC");
                ls.Add("33B10200", "Chinese_Taiwan_Bopomofo_90_CI_AI_KS_SC");
                ls.Add("33310200", "Chinese_Taiwan_Bopomofo_90_CI_AI_KS_WS_SC");
                ls.Add("33D10200", "Chinese_Taiwan_Bopomofo_90_CI_AS_SC");
                ls.Add("33510200", "Chinese_Taiwan_Bopomofo_90_CI_AS_WS_SC");
                ls.Add("33910200", "Chinese_Taiwan_Bopomofo_90_CI_AS_KS_SC");
                ls.Add("33110200", "Chinese_Taiwan_Bopomofo_90_CI_AS_KS_WS_SC");
                ls.Add("33E10200", "Chinese_Taiwan_Bopomofo_90_CS_AI_SC");
                ls.Add("33610200", "Chinese_Taiwan_Bopomofo_90_CS_AI_WS_SC");
                ls.Add("33A10200", "Chinese_Taiwan_Bopomofo_90_CS_AI_KS_SC");
                ls.Add("33210200", "Chinese_Taiwan_Bopomofo_90_CS_AI_KS_WS_SC");
                ls.Add("33C10200", "Chinese_Taiwan_Bopomofo_90_CS_AS_SC");
                ls.Add("33410200", "Chinese_Taiwan_Bopomofo_90_CS_AS_WS_SC");
                ls.Add("33810200", "Chinese_Taiwan_Bopomofo_90_CS_AS_KS_SC");
                ls.Add("33010200", "Chinese_Taiwan_Bopomofo_90_CS_AS_KS_WS_SC");
                ls.Add("03000100", "Chinese_Taiwan_Stroke_BIN");
                ls.Add("03080000", "Chinese_Taiwan_Stroke_BIN2");
                ls.Add("03F00000", "Chinese_Taiwan_Stroke_CI_AI");
                ls.Add("03700000", "Chinese_Taiwan_Stroke_CI_AI_WS");
                ls.Add("03B00000", "Chinese_Taiwan_Stroke_CI_AI_KS");
                ls.Add("03300000", "Chinese_Taiwan_Stroke_CI_AI_KS_WS");
                ls.Add("03D00000", "Chinese_Taiwan_Stroke_CI_AS");
                ls.Add("03500000", "Chinese_Taiwan_Stroke_CI_AS_WS");
                ls.Add("03900000", "Chinese_Taiwan_Stroke_CI_AS_KS");
                ls.Add("03100000", "Chinese_Taiwan_Stroke_CI_AS_KS_WS");
                ls.Add("03E00000", "Chinese_Taiwan_Stroke_CS_AI");
                ls.Add("03600000", "Chinese_Taiwan_Stroke_CS_AI_WS");
                ls.Add("03A00000", "Chinese_Taiwan_Stroke_CS_AI_KS");
                ls.Add("03200000", "Chinese_Taiwan_Stroke_CS_AI_KS_WS");
                ls.Add("03C00000", "Chinese_Taiwan_Stroke_CS_AS");
                ls.Add("03400000", "Chinese_Taiwan_Stroke_CS_AS_WS");
                ls.Add("03800000", "Chinese_Taiwan_Stroke_CS_AS_KS");
                ls.Add("03000000", "Chinese_Taiwan_Stroke_CS_AS_KS_WS");
                ls.Add("34000300", "Chinese_Taiwan_Stroke_90_BIN");
                ls.Add("34080200", "Chinese_Taiwan_Stroke_90_BIN2");
                ls.Add("34F00200", "Chinese_Taiwan_Stroke_90_CI_AI");
                ls.Add("34700200", "Chinese_Taiwan_Stroke_90_CI_AI_WS");
                ls.Add("34B00200", "Chinese_Taiwan_Stroke_90_CI_AI_KS");
                ls.Add("34300200", "Chinese_Taiwan_Stroke_90_CI_AI_KS_WS");
                ls.Add("34D00200", "Chinese_Taiwan_Stroke_90_CI_AS");
                ls.Add("34500200", "Chinese_Taiwan_Stroke_90_CI_AS_WS");
                ls.Add("34900200", "Chinese_Taiwan_Stroke_90_CI_AS_KS");
                ls.Add("34100200", "Chinese_Taiwan_Stroke_90_CI_AS_KS_WS");
                ls.Add("34E00200", "Chinese_Taiwan_Stroke_90_CS_AI");
                ls.Add("34600200", "Chinese_Taiwan_Stroke_90_CS_AI_WS");
                ls.Add("34A00200", "Chinese_Taiwan_Stroke_90_CS_AI_KS");
                ls.Add("34200200", "Chinese_Taiwan_Stroke_90_CS_AI_KS_WS");
                ls.Add("34C00200", "Chinese_Taiwan_Stroke_90_CS_AS");
                ls.Add("34400200", "Chinese_Taiwan_Stroke_90_CS_AS_WS");
                ls.Add("34800200", "Chinese_Taiwan_Stroke_90_CS_AS_KS");
                ls.Add("34000200", "Chinese_Taiwan_Stroke_90_CS_AS_KS_WS");
                ls.Add("34F10200", "Chinese_Taiwan_Stroke_90_CI_AI_SC");
                ls.Add("34710200", "Chinese_Taiwan_Stroke_90_CI_AI_WS_SC");
                ls.Add("34B10200", "Chinese_Taiwan_Stroke_90_CI_AI_KS_SC");
                ls.Add("34310200", "Chinese_Taiwan_Stroke_90_CI_AI_KS_WS_SC");
                ls.Add("34D10200", "Chinese_Taiwan_Stroke_90_CI_AS_SC");
                ls.Add("34510200", "Chinese_Taiwan_Stroke_90_CI_AS_WS_SC");
                ls.Add("34910200", "Chinese_Taiwan_Stroke_90_CI_AS_KS_SC");
                ls.Add("34110200", "Chinese_Taiwan_Stroke_90_CI_AS_KS_WS_SC");
                ls.Add("34E10200", "Chinese_Taiwan_Stroke_90_CS_AI_SC");
                ls.Add("34610200", "Chinese_Taiwan_Stroke_90_CS_AI_WS_SC");
                ls.Add("34A10200", "Chinese_Taiwan_Stroke_90_CS_AI_KS_SC");
                ls.Add("34210200", "Chinese_Taiwan_Stroke_90_CS_AI_KS_WS_SC");
                ls.Add("34C10200", "Chinese_Taiwan_Stroke_90_CS_AS_SC");
                ls.Add("34410200", "Chinese_Taiwan_Stroke_90_CS_AS_WS_SC");
                ls.Add("34810200", "Chinese_Taiwan_Stroke_90_CS_AS_KS_SC");
                ls.Add("34010200", "Chinese_Taiwan_Stroke_90_CS_AS_KS_WS_SC");
                ls.Add("42000500", "Chinese_Traditional_Bopomofo_100_BIN");
                ls.Add("42080400", "Chinese_Traditional_Bopomofo_100_BIN2");
                ls.Add("42F00400", "Chinese_Traditional_Bopomofo_100_CI_AI");
                ls.Add("42700400", "Chinese_Traditional_Bopomofo_100_CI_AI_WS");
                ls.Add("42B00400", "Chinese_Traditional_Bopomofo_100_CI_AI_KS");
                ls.Add("42300400", "Chinese_Traditional_Bopomofo_100_CI_AI_KS_WS");
                ls.Add("42D00400", "Chinese_Traditional_Bopomofo_100_CI_AS");
                ls.Add("42500400", "Chinese_Traditional_Bopomofo_100_CI_AS_WS");
                ls.Add("42900400", "Chinese_Traditional_Bopomofo_100_CI_AS_KS");
                ls.Add("42100400", "Chinese_Traditional_Bopomofo_100_CI_AS_KS_WS");
                ls.Add("42E00400", "Chinese_Traditional_Bopomofo_100_CS_AI");
                ls.Add("42600400", "Chinese_Traditional_Bopomofo_100_CS_AI_WS");
                ls.Add("42A00400", "Chinese_Traditional_Bopomofo_100_CS_AI_KS");
                ls.Add("42200400", "Chinese_Traditional_Bopomofo_100_CS_AI_KS_WS");
                ls.Add("42C00400", "Chinese_Traditional_Bopomofo_100_CS_AS");
                ls.Add("42400400", "Chinese_Traditional_Bopomofo_100_CS_AS_WS");
                ls.Add("42800400", "Chinese_Traditional_Bopomofo_100_CS_AS_KS");
                ls.Add("42000400", "Chinese_Traditional_Bopomofo_100_CS_AS_KS_WS");
                ls.Add("42F10400", "Chinese_Traditional_Bopomofo_100_CI_AI_SC");
                ls.Add("42710400", "Chinese_Traditional_Bopomofo_100_CI_AI_WS_SC");
                ls.Add("42B10400", "Chinese_Traditional_Bopomofo_100_CI_AI_KS_SC");
                ls.Add("42310400", "Chinese_Traditional_Bopomofo_100_CI_AI_KS_WS_SC");
                ls.Add("42D10400", "Chinese_Traditional_Bopomofo_100_CI_AS_SC");
                ls.Add("42510400", "Chinese_Traditional_Bopomofo_100_CI_AS_WS_SC");
                ls.Add("42910400", "Chinese_Traditional_Bopomofo_100_CI_AS_KS_SC");
                ls.Add("42110400", "Chinese_Traditional_Bopomofo_100_CI_AS_KS_WS_SC");
                ls.Add("42E10400", "Chinese_Traditional_Bopomofo_100_CS_AI_SC");
                ls.Add("42610400", "Chinese_Traditional_Bopomofo_100_CS_AI_WS_SC");
                ls.Add("42A10400", "Chinese_Traditional_Bopomofo_100_CS_AI_KS_SC");
                ls.Add("42210400", "Chinese_Traditional_Bopomofo_100_CS_AI_KS_WS_SC");
                ls.Add("42C10400", "Chinese_Traditional_Bopomofo_100_CS_AS_SC");
                ls.Add("42410400", "Chinese_Traditional_Bopomofo_100_CS_AS_WS_SC");
                ls.Add("42810400", "Chinese_Traditional_Bopomofo_100_CS_AS_KS_SC");
                ls.Add("42010400", "Chinese_Traditional_Bopomofo_100_CS_AS_KS_WS_SC");
                ls.Add("45000500", "Chinese_Traditional_Pinyin_100_BIN");
                ls.Add("45080400", "Chinese_Traditional_Pinyin_100_BIN2");
                ls.Add("45F00400", "Chinese_Traditional_Pinyin_100_CI_AI");
                ls.Add("45700400", "Chinese_Traditional_Pinyin_100_CI_AI_WS");
                ls.Add("45B00400", "Chinese_Traditional_Pinyin_100_CI_AI_KS");
                ls.Add("45300400", "Chinese_Traditional_Pinyin_100_CI_AI_KS_WS");
                ls.Add("45D00400", "Chinese_Traditional_Pinyin_100_CI_AS");
                ls.Add("45500400", "Chinese_Traditional_Pinyin_100_CI_AS_WS");
                ls.Add("45900400", "Chinese_Traditional_Pinyin_100_CI_AS_KS");
                ls.Add("45100400", "Chinese_Traditional_Pinyin_100_CI_AS_KS_WS");
                ls.Add("45E00400", "Chinese_Traditional_Pinyin_100_CS_AI");
                ls.Add("45600400", "Chinese_Traditional_Pinyin_100_CS_AI_WS");
                ls.Add("45A00400", "Chinese_Traditional_Pinyin_100_CS_AI_KS");
                ls.Add("45200400", "Chinese_Traditional_Pinyin_100_CS_AI_KS_WS");
                ls.Add("45C00400", "Chinese_Traditional_Pinyin_100_CS_AS");
                ls.Add("45400400", "Chinese_Traditional_Pinyin_100_CS_AS_WS");
                ls.Add("45800400", "Chinese_Traditional_Pinyin_100_CS_AS_KS");
                ls.Add("45000400", "Chinese_Traditional_Pinyin_100_CS_AS_KS_WS");
                ls.Add("45F10400", "Chinese_Traditional_Pinyin_100_CI_AI_SC");
                ls.Add("45710400", "Chinese_Traditional_Pinyin_100_CI_AI_WS_SC");
                ls.Add("45B10400", "Chinese_Traditional_Pinyin_100_CI_AI_KS_SC");
                ls.Add("45310400", "Chinese_Traditional_Pinyin_100_CI_AI_KS_WS_SC");
                ls.Add("45D10400", "Chinese_Traditional_Pinyin_100_CI_AS_SC");
                ls.Add("45510400", "Chinese_Traditional_Pinyin_100_CI_AS_WS_SC");
                ls.Add("45910400", "Chinese_Traditional_Pinyin_100_CI_AS_KS_SC");
                ls.Add("45110400", "Chinese_Traditional_Pinyin_100_CI_AS_KS_WS_SC");
                ls.Add("45E10400", "Chinese_Traditional_Pinyin_100_CS_AI_SC");
                ls.Add("45610400", "Chinese_Traditional_Pinyin_100_CS_AI_WS_SC");
                ls.Add("45A10400", "Chinese_Traditional_Pinyin_100_CS_AI_KS_SC");
                ls.Add("45210400", "Chinese_Traditional_Pinyin_100_CS_AI_KS_WS_SC");
                ls.Add("45C10400", "Chinese_Traditional_Pinyin_100_CS_AS_SC");
                ls.Add("45410400", "Chinese_Traditional_Pinyin_100_CS_AS_WS_SC");
                ls.Add("45810400", "Chinese_Traditional_Pinyin_100_CS_AS_KS_SC");
                ls.Add("45010400", "Chinese_Traditional_Pinyin_100_CS_AS_KS_WS_SC");
                ls.Add("41000500", "Chinese_Traditional_Stroke_Count_100_BIN");
                ls.Add("41080400", "Chinese_Traditional_Stroke_Count_100_BIN2");
                ls.Add("41F00400", "Chinese_Traditional_Stroke_Count_100_CI_AI");
                ls.Add("41700400", "Chinese_Traditional_Stroke_Count_100_CI_AI_WS");
                ls.Add("41B00400", "Chinese_Traditional_Stroke_Count_100_CI_AI_KS");
                ls.Add("41300400", "Chinese_Traditional_Stroke_Count_100_CI_AI_KS_WS");
                ls.Add("41D00400", "Chinese_Traditional_Stroke_Count_100_CI_AS");
                ls.Add("41500400", "Chinese_Traditional_Stroke_Count_100_CI_AS_WS");
                ls.Add("41900400", "Chinese_Traditional_Stroke_Count_100_CI_AS_KS");
                ls.Add("41100400", "Chinese_Traditional_Stroke_Count_100_CI_AS_KS_WS");
                ls.Add("41E00400", "Chinese_Traditional_Stroke_Count_100_CS_AI");
                ls.Add("41600400", "Chinese_Traditional_Stroke_Count_100_CS_AI_WS");
                ls.Add("41A00400", "Chinese_Traditional_Stroke_Count_100_CS_AI_KS");
                ls.Add("41200400", "Chinese_Traditional_Stroke_Count_100_CS_AI_KS_WS");
                ls.Add("41C00400", "Chinese_Traditional_Stroke_Count_100_CS_AS");
                ls.Add("41400400", "Chinese_Traditional_Stroke_Count_100_CS_AS_WS");
                ls.Add("41800400", "Chinese_Traditional_Stroke_Count_100_CS_AS_KS");
                ls.Add("41000400", "Chinese_Traditional_Stroke_Count_100_CS_AS_KS_WS");
                ls.Add("41F10400", "Chinese_Traditional_Stroke_Count_100_CI_AI_SC");
                ls.Add("41710400", "Chinese_Traditional_Stroke_Count_100_CI_AI_WS_SC");
                ls.Add("41B10400", "Chinese_Traditional_Stroke_Count_100_CI_AI_KS_SC");
                ls.Add("41310400", "Chinese_Traditional_Stroke_Count_100_CI_AI_KS_WS_SC");
                ls.Add("41D10400", "Chinese_Traditional_Stroke_Count_100_CI_AS_SC");
                ls.Add("41510400", "Chinese_Traditional_Stroke_Count_100_CI_AS_WS_SC");
                ls.Add("41910400", "Chinese_Traditional_Stroke_Count_100_CI_AS_KS_SC");
                ls.Add("41110400", "Chinese_Traditional_Stroke_Count_100_CI_AS_KS_WS_SC");
                ls.Add("41E10400", "Chinese_Traditional_Stroke_Count_100_CS_AI_SC");
                ls.Add("41610400", "Chinese_Traditional_Stroke_Count_100_CS_AI_WS_SC");
                ls.Add("41A10400", "Chinese_Traditional_Stroke_Count_100_CS_AI_KS_SC");
                ls.Add("41210400", "Chinese_Traditional_Stroke_Count_100_CS_AI_KS_WS_SC");
                ls.Add("41C10400", "Chinese_Traditional_Stroke_Count_100_CS_AS_SC");
                ls.Add("41410400", "Chinese_Traditional_Stroke_Count_100_CS_AS_WS_SC");
                ls.Add("41810400", "Chinese_Traditional_Stroke_Count_100_CS_AS_KS_SC");
                ls.Add("41010400", "Chinese_Traditional_Stroke_Count_100_CS_AS_KS_WS_SC");
                ls.Add("46000500", "Chinese_Traditional_Stroke_Order_100_BIN");
                ls.Add("46080400", "Chinese_Traditional_Stroke_Order_100_BIN2");
                ls.Add("46F00400", "Chinese_Traditional_Stroke_Order_100_CI_AI");
                ls.Add("46700400", "Chinese_Traditional_Stroke_Order_100_CI_AI_WS");
                ls.Add("46B00400", "Chinese_Traditional_Stroke_Order_100_CI_AI_KS");
                ls.Add("46300400", "Chinese_Traditional_Stroke_Order_100_CI_AI_KS_WS");
                ls.Add("46D00400", "Chinese_Traditional_Stroke_Order_100_CI_AS");
                ls.Add("46500400", "Chinese_Traditional_Stroke_Order_100_CI_AS_WS");
                ls.Add("46900400", "Chinese_Traditional_Stroke_Order_100_CI_AS_KS");
                ls.Add("46100400", "Chinese_Traditional_Stroke_Order_100_CI_AS_KS_WS");
                ls.Add("46E00400", "Chinese_Traditional_Stroke_Order_100_CS_AI");
                ls.Add("46600400", "Chinese_Traditional_Stroke_Order_100_CS_AI_WS");
                ls.Add("46A00400", "Chinese_Traditional_Stroke_Order_100_CS_AI_KS");
                ls.Add("46200400", "Chinese_Traditional_Stroke_Order_100_CS_AI_KS_WS");
                ls.Add("46C00400", "Chinese_Traditional_Stroke_Order_100_CS_AS");
                ls.Add("46400400", "Chinese_Traditional_Stroke_Order_100_CS_AS_WS");
                ls.Add("46800400", "Chinese_Traditional_Stroke_Order_100_CS_AS_KS");
                ls.Add("46000400", "Chinese_Traditional_Stroke_Order_100_CS_AS_KS_WS");
                ls.Add("46F10400", "Chinese_Traditional_Stroke_Order_100_CI_AI_SC");
                ls.Add("46710400", "Chinese_Traditional_Stroke_Order_100_CI_AI_WS_SC");
                ls.Add("46B10400", "Chinese_Traditional_Stroke_Order_100_CI_AI_KS_SC");
                ls.Add("46310400", "Chinese_Traditional_Stroke_Order_100_CI_AI_KS_WS_SC");
                ls.Add("46D10400", "Chinese_Traditional_Stroke_Order_100_CI_AS_SC");
                ls.Add("46510400", "Chinese_Traditional_Stroke_Order_100_CI_AS_WS_SC");
                ls.Add("46910400", "Chinese_Traditional_Stroke_Order_100_CI_AS_KS_SC");
                ls.Add("46110400", "Chinese_Traditional_Stroke_Order_100_CI_AS_KS_WS_SC");
                ls.Add("46E10400", "Chinese_Traditional_Stroke_Order_100_CS_AI_SC");
                ls.Add("46610400", "Chinese_Traditional_Stroke_Order_100_CS_AI_WS_SC");
                ls.Add("46A10400", "Chinese_Traditional_Stroke_Order_100_CS_AI_KS_SC");
                ls.Add("46210400", "Chinese_Traditional_Stroke_Order_100_CS_AI_KS_WS_SC");
                ls.Add("46C10400", "Chinese_Traditional_Stroke_Order_100_CS_AS_SC");
                ls.Add("46410400", "Chinese_Traditional_Stroke_Order_100_CS_AS_WS_SC");
                ls.Add("46810400", "Chinese_Traditional_Stroke_Order_100_CS_AS_KS_SC");
                ls.Add("46010400", "Chinese_Traditional_Stroke_Order_100_CS_AS_KS_WS_SC");
                ls.Add("0F000500", "Corsican_100_BIN");
                ls.Add("0F080400", "Corsican_100_BIN2");
                ls.Add("0FF00400", "Corsican_100_CI_AI");
                ls.Add("0F700400", "Corsican_100_CI_AI_WS");
                ls.Add("0FB00400", "Corsican_100_CI_AI_KS");
                ls.Add("0F300400", "Corsican_100_CI_AI_KS_WS");
                ls.Add("0FD00400", "Corsican_100_CI_AS");
                ls.Add("0F500400", "Corsican_100_CI_AS_WS");
                ls.Add("0F900400", "Corsican_100_CI_AS_KS");
                ls.Add("0F100400", "Corsican_100_CI_AS_KS_WS");
                ls.Add("0FE00400", "Corsican_100_CS_AI");
                ls.Add("0F600400", "Corsican_100_CS_AI_WS");
                ls.Add("0FA00400", "Corsican_100_CS_AI_KS");
                ls.Add("0F200400", "Corsican_100_CS_AI_KS_WS");
                ls.Add("0FC00400", "Corsican_100_CS_AS");
                ls.Add("0F400400", "Corsican_100_CS_AS_WS");
                ls.Add("0F800400", "Corsican_100_CS_AS_KS");
                ls.Add("0F000400", "Corsican_100_CS_AS_KS_WS");
                ls.Add("0FF10400", "Corsican_100_CI_AI_SC");
                ls.Add("0F710400", "Corsican_100_CI_AI_WS_SC");
                ls.Add("0FB10400", "Corsican_100_CI_AI_KS_SC");
                ls.Add("0F310400", "Corsican_100_CI_AI_KS_WS_SC");
                ls.Add("0FD10400", "Corsican_100_CI_AS_SC");
                ls.Add("0F510400", "Corsican_100_CI_AS_WS_SC");
                ls.Add("0F910400", "Corsican_100_CI_AS_KS_SC");
                ls.Add("0F110400", "Corsican_100_CI_AS_KS_WS_SC");
                ls.Add("0FE10400", "Corsican_100_CS_AI_SC");
                ls.Add("0F610400", "Corsican_100_CS_AI_WS_SC");
                ls.Add("0FA10400", "Corsican_100_CS_AI_KS_SC");
                ls.Add("0F210400", "Corsican_100_CS_AI_KS_WS_SC");
                ls.Add("0FC10400", "Corsican_100_CS_AS_SC");
                ls.Add("0F410400", "Corsican_100_CS_AS_WS_SC");
                ls.Add("0F810400", "Corsican_100_CS_AS_KS_SC");
                ls.Add("0F010400", "Corsican_100_CS_AS_KS_WS_SC");
                ls.Add("16000100", "Croatian_BIN");
                ls.Add("16080000", "Croatian_BIN2");
                ls.Add("16F00000", "Croatian_CI_AI");
                ls.Add("16700000", "Croatian_CI_AI_WS");
                ls.Add("16B00000", "Croatian_CI_AI_KS");
                ls.Add("16300000", "Croatian_CI_AI_KS_WS");
                ls.Add("16D00000", "Croatian_CI_AS");
                ls.Add("16500000", "Croatian_CI_AS_WS");
                ls.Add("16900000", "Croatian_CI_AS_KS");
                ls.Add("16100000", "Croatian_CI_AS_KS_WS");
                ls.Add("16E00000", "Croatian_CS_AI");
                ls.Add("16600000", "Croatian_CS_AI_WS");
                ls.Add("16A00000", "Croatian_CS_AI_KS");
                ls.Add("16200000", "Croatian_CS_AI_KS_WS");
                ls.Add("16C00000", "Croatian_CS_AS");
                ls.Add("16400000", "Croatian_CS_AS_WS");
                ls.Add("16800000", "Croatian_CS_AS_KS");
                ls.Add("16000000", "Croatian_CS_AS_KS_WS");
                ls.Add("16000500", "Croatian_100_BIN");
                ls.Add("16080400", "Croatian_100_BIN2");
                ls.Add("16F00400", "Croatian_100_CI_AI");
                ls.Add("16700400", "Croatian_100_CI_AI_WS");
                ls.Add("16B00400", "Croatian_100_CI_AI_KS");
                ls.Add("16300400", "Croatian_100_CI_AI_KS_WS");
                ls.Add("16D00400", "Croatian_100_CI_AS");
                ls.Add("16500400", "Croatian_100_CI_AS_WS");
                ls.Add("16900400", "Croatian_100_CI_AS_KS");
                ls.Add("16100400", "Croatian_100_CI_AS_KS_WS");
                ls.Add("16E00400", "Croatian_100_CS_AI");
                ls.Add("16600400", "Croatian_100_CS_AI_WS");
                ls.Add("16A00400", "Croatian_100_CS_AI_KS");
                ls.Add("16200400", "Croatian_100_CS_AI_KS_WS");
                ls.Add("16C00400", "Croatian_100_CS_AS");
                ls.Add("16400400", "Croatian_100_CS_AS_WS");
                ls.Add("16800400", "Croatian_100_CS_AS_KS");
                ls.Add("16000400", "Croatian_100_CS_AS_KS_WS");
                ls.Add("16F10400", "Croatian_100_CI_AI_SC");
                ls.Add("16710400", "Croatian_100_CI_AI_WS_SC");
                ls.Add("16B10400", "Croatian_100_CI_AI_KS_SC");
                ls.Add("16310400", "Croatian_100_CI_AI_KS_WS_SC");
                ls.Add("16D10400", "Croatian_100_CI_AS_SC");
                ls.Add("16510400", "Croatian_100_CI_AS_WS_SC");
                ls.Add("16910400", "Croatian_100_CI_AS_KS_SC");
                ls.Add("16110400", "Croatian_100_CI_AS_KS_WS_SC");
                ls.Add("16E10400", "Croatian_100_CS_AI_SC");
                ls.Add("16610400", "Croatian_100_CS_AI_WS_SC");
                ls.Add("16A10400", "Croatian_100_CS_AI_KS_SC");
                ls.Add("16210400", "Croatian_100_CS_AI_KS_WS_SC");
                ls.Add("16C10400", "Croatian_100_CS_AS_SC");
                ls.Add("16410400", "Croatian_100_CS_AS_WS_SC");
                ls.Add("16810400", "Croatian_100_CS_AS_KS_SC");
                ls.Add("16010400", "Croatian_100_CS_AS_KS_WS_SC");
                ls.Add("15000100", "Cyrillic_General_BIN");
                ls.Add("15080000", "Cyrillic_General_BIN2");
                ls.Add("15F00000", "Cyrillic_General_CI_AI");
                ls.Add("15700000", "Cyrillic_General_CI_AI_WS");
                ls.Add("15B00000", "Cyrillic_General_CI_AI_KS");
                ls.Add("15300000", "Cyrillic_General_CI_AI_KS_WS");
                ls.Add("15D00000", "Cyrillic_General_CI_AS");
                ls.Add("15500000", "Cyrillic_General_CI_AS_WS");
                ls.Add("15900000", "Cyrillic_General_CI_AS_KS");
                ls.Add("15100000", "Cyrillic_General_CI_AS_KS_WS");
                ls.Add("15E00000", "Cyrillic_General_CS_AI");
                ls.Add("15600000", "Cyrillic_General_CS_AI_WS");
                ls.Add("15A00000", "Cyrillic_General_CS_AI_KS");
                ls.Add("15200000", "Cyrillic_General_CS_AI_KS_WS");
                ls.Add("15C00000", "Cyrillic_General_CS_AS");
                ls.Add("15400000", "Cyrillic_General_CS_AS_WS");
                ls.Add("15800000", "Cyrillic_General_CS_AS_KS");
                ls.Add("15000000", "Cyrillic_General_CS_AS_KS_WS");
                ls.Add("15000500", "Cyrillic_General_100_BIN");
                ls.Add("15080400", "Cyrillic_General_100_BIN2");
                ls.Add("15F00400", "Cyrillic_General_100_CI_AI");
                ls.Add("15700400", "Cyrillic_General_100_CI_AI_WS");
                ls.Add("15B00400", "Cyrillic_General_100_CI_AI_KS");
                ls.Add("15300400", "Cyrillic_General_100_CI_AI_KS_WS");
                ls.Add("15D00400", "Cyrillic_General_100_CI_AS");
                ls.Add("15500400", "Cyrillic_General_100_CI_AS_WS");
                ls.Add("15900400", "Cyrillic_General_100_CI_AS_KS");
                ls.Add("15100400", "Cyrillic_General_100_CI_AS_KS_WS");
                ls.Add("15E00400", "Cyrillic_General_100_CS_AI");
                ls.Add("15600400", "Cyrillic_General_100_CS_AI_WS");
                ls.Add("15A00400", "Cyrillic_General_100_CS_AI_KS");
                ls.Add("15200400", "Cyrillic_General_100_CS_AI_KS_WS");
                ls.Add("15C00400", "Cyrillic_General_100_CS_AS");
                ls.Add("15400400", "Cyrillic_General_100_CS_AS_WS");
                ls.Add("15800400", "Cyrillic_General_100_CS_AS_KS");
                ls.Add("15000400", "Cyrillic_General_100_CS_AS_KS_WS");
                ls.Add("15F10400", "Cyrillic_General_100_CI_AI_SC");
                ls.Add("15710400", "Cyrillic_General_100_CI_AI_WS_SC");
                ls.Add("15B10400", "Cyrillic_General_100_CI_AI_KS_SC");
                ls.Add("15310400", "Cyrillic_General_100_CI_AI_KS_WS_SC");
                ls.Add("15D10400", "Cyrillic_General_100_CI_AS_SC");
                ls.Add("15510400", "Cyrillic_General_100_CI_AS_WS_SC");
                ls.Add("15910400", "Cyrillic_General_100_CI_AS_KS_SC");
                ls.Add("15110400", "Cyrillic_General_100_CI_AS_KS_WS_SC");
                ls.Add("15E10400", "Cyrillic_General_100_CS_AI_SC");
                ls.Add("15610400", "Cyrillic_General_100_CS_AI_WS_SC");
                ls.Add("15A10400", "Cyrillic_General_100_CS_AI_KS_SC");
                ls.Add("15210400", "Cyrillic_General_100_CS_AI_KS_WS_SC");
                ls.Add("15C10400", "Cyrillic_General_100_CS_AS_SC");
                ls.Add("15410400", "Cyrillic_General_100_CS_AS_WS_SC");
                ls.Add("15810400", "Cyrillic_General_100_CS_AS_KS_SC");
                ls.Add("15010400", "Cyrillic_General_100_CS_AS_KS_WS_SC");
                ls.Add("04000100", "Czech_BIN");
                ls.Add("04080000", "Czech_BIN2");
                ls.Add("04F00000", "Czech_CI_AI");
                ls.Add("04700000", "Czech_CI_AI_WS");
                ls.Add("04B00000", "Czech_CI_AI_KS");
                ls.Add("04300000", "Czech_CI_AI_KS_WS");
                ls.Add("04D00000", "Czech_CI_AS");
                ls.Add("04500000", "Czech_CI_AS_WS");
                ls.Add("04900000", "Czech_CI_AS_KS");
                ls.Add("04100000", "Czech_CI_AS_KS_WS");
                ls.Add("04E00000", "Czech_CS_AI");
                ls.Add("04600000", "Czech_CS_AI_WS");
                ls.Add("04A00000", "Czech_CS_AI_KS");
                ls.Add("04200000", "Czech_CS_AI_KS_WS");
                ls.Add("04C00000", "Czech_CS_AS");
                ls.Add("04400000", "Czech_CS_AS_WS");
                ls.Add("04800000", "Czech_CS_AS_KS");
                ls.Add("04000000", "Czech_CS_AS_KS_WS");
                ls.Add("04000500", "Czech_100_BIN");
                ls.Add("04080400", "Czech_100_BIN2");
                ls.Add("04F00400", "Czech_100_CI_AI");
                ls.Add("04700400", "Czech_100_CI_AI_WS");
                ls.Add("04B00400", "Czech_100_CI_AI_KS");
                ls.Add("04300400", "Czech_100_CI_AI_KS_WS");
                ls.Add("04D00400", "Czech_100_CI_AS");
                ls.Add("04500400", "Czech_100_CI_AS_WS");
                ls.Add("04900400", "Czech_100_CI_AS_KS");
                ls.Add("04100400", "Czech_100_CI_AS_KS_WS");
                ls.Add("04E00400", "Czech_100_CS_AI");
                ls.Add("04600400", "Czech_100_CS_AI_WS");
                ls.Add("04A00400", "Czech_100_CS_AI_KS");
                ls.Add("04200400", "Czech_100_CS_AI_KS_WS");
                ls.Add("04C00400", "Czech_100_CS_AS");
                ls.Add("04400400", "Czech_100_CS_AS_WS");
                ls.Add("04800400", "Czech_100_CS_AS_KS");
                ls.Add("04000400", "Czech_100_CS_AS_KS_WS");
                ls.Add("04F10400", "Czech_100_CI_AI_SC");
                ls.Add("04710400", "Czech_100_CI_AI_WS_SC");
                ls.Add("04B10400", "Czech_100_CI_AI_KS_SC");
                ls.Add("04310400", "Czech_100_CI_AI_KS_WS_SC");
                ls.Add("04D10400", "Czech_100_CI_AS_SC");
                ls.Add("04510400", "Czech_100_CI_AS_WS_SC");
                ls.Add("04910400", "Czech_100_CI_AS_KS_SC");
                ls.Add("04110400", "Czech_100_CI_AS_KS_WS_SC");
                ls.Add("04E10400", "Czech_100_CS_AI_SC");
                ls.Add("04610400", "Czech_100_CS_AI_WS_SC");
                ls.Add("04A10400", "Czech_100_CS_AI_KS_SC");
                ls.Add("04210400", "Czech_100_CS_AI_KS_WS_SC");
                ls.Add("04C10400", "Czech_100_CS_AS_SC");
                ls.Add("04410400", "Czech_100_CS_AS_WS_SC");
                ls.Add("04810400", "Czech_100_CS_AS_KS_SC");
                ls.Add("04010400", "Czech_100_CS_AS_KS_WS_SC");
                ls.Add("47000500", "Danish_Greenlandic_100_BIN");
                ls.Add("47080400", "Danish_Greenlandic_100_BIN2");
                ls.Add("47F00400", "Danish_Greenlandic_100_CI_AI");
                ls.Add("47700400", "Danish_Greenlandic_100_CI_AI_WS");
                ls.Add("47B00400", "Danish_Greenlandic_100_CI_AI_KS");
                ls.Add("47300400", "Danish_Greenlandic_100_CI_AI_KS_WS");
                ls.Add("47D00400", "Danish_Greenlandic_100_CI_AS");
                ls.Add("47500400", "Danish_Greenlandic_100_CI_AS_WS");
                ls.Add("47900400", "Danish_Greenlandic_100_CI_AS_KS");
                ls.Add("47100400", "Danish_Greenlandic_100_CI_AS_KS_WS");
                ls.Add("47E00400", "Danish_Greenlandic_100_CS_AI");
                ls.Add("47600400", "Danish_Greenlandic_100_CS_AI_WS");
                ls.Add("47A00400", "Danish_Greenlandic_100_CS_AI_KS");
                ls.Add("47200400", "Danish_Greenlandic_100_CS_AI_KS_WS");
                ls.Add("47C00400", "Danish_Greenlandic_100_CS_AS");
                ls.Add("47400400", "Danish_Greenlandic_100_CS_AS_WS");
                ls.Add("47800400", "Danish_Greenlandic_100_CS_AS_KS");
                ls.Add("47000400", "Danish_Greenlandic_100_CS_AS_KS_WS");
                ls.Add("47F10400", "Danish_Greenlandic_100_CI_AI_SC");
                ls.Add("47710400", "Danish_Greenlandic_100_CI_AI_WS_SC");
                ls.Add("47B10400", "Danish_Greenlandic_100_CI_AI_KS_SC");
                ls.Add("47310400", "Danish_Greenlandic_100_CI_AI_KS_WS_SC");
                ls.Add("47D10400", "Danish_Greenlandic_100_CI_AS_SC");
                ls.Add("47510400", "Danish_Greenlandic_100_CI_AS_WS_SC");
                ls.Add("47910400", "Danish_Greenlandic_100_CI_AS_KS_SC");
                ls.Add("47110400", "Danish_Greenlandic_100_CI_AS_KS_WS_SC");
                ls.Add("47E10400", "Danish_Greenlandic_100_CS_AI_SC");
                ls.Add("47610400", "Danish_Greenlandic_100_CS_AI_WS_SC");
                ls.Add("47A10400", "Danish_Greenlandic_100_CS_AI_KS_SC");
                ls.Add("47210400", "Danish_Greenlandic_100_CS_AI_KS_WS_SC");
                ls.Add("47C10400", "Danish_Greenlandic_100_CS_AS_SC");
                ls.Add("47410400", "Danish_Greenlandic_100_CS_AS_WS_SC");
                ls.Add("47810400", "Danish_Greenlandic_100_CS_AS_KS_SC");
                ls.Add("47010400", "Danish_Greenlandic_100_CS_AS_KS_WS_SC");
                ls.Add("05000100", "Danish_Norwegian_BIN");
                ls.Add("05080000", "Danish_Norwegian_BIN2");
                ls.Add("05F00000", "Danish_Norwegian_CI_AI");
                ls.Add("05700000", "Danish_Norwegian_CI_AI_WS");
                ls.Add("05B00000", "Danish_Norwegian_CI_AI_KS");
                ls.Add("05300000", "Danish_Norwegian_CI_AI_KS_WS");
                ls.Add("05D00000", "Danish_Norwegian_CI_AS");
                ls.Add("05500000", "Danish_Norwegian_CI_AS_WS");
                ls.Add("05900000", "Danish_Norwegian_CI_AS_KS");
                ls.Add("05100000", "Danish_Norwegian_CI_AS_KS_WS");
                ls.Add("05E00000", "Danish_Norwegian_CS_AI");
                ls.Add("05600000", "Danish_Norwegian_CS_AI_WS");
                ls.Add("05A00000", "Danish_Norwegian_CS_AI_KS");
                ls.Add("05200000", "Danish_Norwegian_CS_AI_KS_WS");
                ls.Add("05C00000", "Danish_Norwegian_CS_AS");
                ls.Add("05400000", "Danish_Norwegian_CS_AS_WS");
                ls.Add("05800000", "Danish_Norwegian_CS_AS_KS");
                ls.Add("05000000", "Danish_Norwegian_CS_AS_KS_WS");
                ls.Add("02000500", "Dari_100_BIN");
                ls.Add("02080400", "Dari_100_BIN2");
                ls.Add("02F00400", "Dari_100_CI_AI");
                ls.Add("02700400", "Dari_100_CI_AI_WS");
                ls.Add("02B00400", "Dari_100_CI_AI_KS");
                ls.Add("02300400", "Dari_100_CI_AI_KS_WS");
                ls.Add("02D00400", "Dari_100_CI_AS");
                ls.Add("02500400", "Dari_100_CI_AS_WS");
                ls.Add("02900400", "Dari_100_CI_AS_KS");
                ls.Add("02100400", "Dari_100_CI_AS_KS_WS");
                ls.Add("02E00400", "Dari_100_CS_AI");
                ls.Add("02600400", "Dari_100_CS_AI_WS");
                ls.Add("02A00400", "Dari_100_CS_AI_KS");
                ls.Add("02200400", "Dari_100_CS_AI_KS_WS");
                ls.Add("02C00400", "Dari_100_CS_AS");
                ls.Add("02400400", "Dari_100_CS_AS_WS");
                ls.Add("02800400", "Dari_100_CS_AS_KS");
                ls.Add("02000400", "Dari_100_CS_AS_KS_WS");
                ls.Add("02F10400", "Dari_100_CI_AI_SC");
                ls.Add("02710400", "Dari_100_CI_AI_WS_SC");
                ls.Add("02B10400", "Dari_100_CI_AI_KS_SC");
                ls.Add("02310400", "Dari_100_CI_AI_KS_WS_SC");
                ls.Add("02D10400", "Dari_100_CI_AS_SC");
                ls.Add("02510400", "Dari_100_CI_AS_WS_SC");
                ls.Add("02910400", "Dari_100_CI_AS_KS_SC");
                ls.Add("02110400", "Dari_100_CI_AS_KS_WS_SC");
                ls.Add("02E10400", "Dari_100_CS_AI_SC");
                ls.Add("02610400", "Dari_100_CS_AI_WS_SC");
                ls.Add("02A10400", "Dari_100_CS_AI_KS_SC");
                ls.Add("02210400", "Dari_100_CS_AI_KS_WS_SC");
                ls.Add("02C10400", "Dari_100_CS_AS_SC");
                ls.Add("02410400", "Dari_100_CS_AS_WS_SC");
                ls.Add("02810400", "Dari_100_CS_AS_KS_SC");
                ls.Add("02010400", "Dari_100_CS_AS_KS_WS_SC");
                ls.Add("3C000300", "Divehi_90_BIN");
                ls.Add("3C080200", "Divehi_90_BIN2");
                ls.Add("3CF00200", "Divehi_90_CI_AI");
                ls.Add("3C700200", "Divehi_90_CI_AI_WS");
                ls.Add("3CB00200", "Divehi_90_CI_AI_KS");
                ls.Add("3C300200", "Divehi_90_CI_AI_KS_WS");
                ls.Add("3CD00200", "Divehi_90_CI_AS");
                ls.Add("3C500200", "Divehi_90_CI_AS_WS");
                ls.Add("3C900200", "Divehi_90_CI_AS_KS");
                ls.Add("3C100200", "Divehi_90_CI_AS_KS_WS");
                ls.Add("3CE00200", "Divehi_90_CS_AI");
                ls.Add("3C600200", "Divehi_90_CS_AI_WS");
                ls.Add("3CA00200", "Divehi_90_CS_AI_KS");
                ls.Add("3C200200", "Divehi_90_CS_AI_KS_WS");
                ls.Add("3CC00200", "Divehi_90_CS_AS");
                ls.Add("3C400200", "Divehi_90_CS_AS_WS");
                ls.Add("3C800200", "Divehi_90_CS_AS_KS");
                ls.Add("3C000200", "Divehi_90_CS_AS_KS_WS");
                ls.Add("3CF10200", "Divehi_90_CI_AI_SC");
                ls.Add("3C710200", "Divehi_90_CI_AI_WS_SC");
                ls.Add("3CB10200", "Divehi_90_CI_AI_KS_SC");
                ls.Add("3C310200", "Divehi_90_CI_AI_KS_WS_SC");
                ls.Add("3CD10200", "Divehi_90_CI_AS_SC");
                ls.Add("3C510200", "Divehi_90_CI_AS_WS_SC");
                ls.Add("3C910200", "Divehi_90_CI_AS_KS_SC");
                ls.Add("3C110200", "Divehi_90_CI_AS_KS_WS_SC");
                ls.Add("3CE10200", "Divehi_90_CS_AI_SC");
                ls.Add("3C610200", "Divehi_90_CS_AI_WS_SC");
                ls.Add("3CA10200", "Divehi_90_CS_AI_KS_SC");
                ls.Add("3C210200", "Divehi_90_CS_AI_KS_WS_SC");
                ls.Add("3CC10200", "Divehi_90_CS_AS_SC");
                ls.Add("3C410200", "Divehi_90_CS_AS_WS_SC");
                ls.Add("3C810200", "Divehi_90_CS_AS_KS_SC");
                ls.Add("3C010200", "Divehi_90_CS_AS_KS_WS_SC");
                ls.Add("3C000500", "Divehi_100_BIN");
                ls.Add("3C080400", "Divehi_100_BIN2");
                ls.Add("3CF00400", "Divehi_100_CI_AI");
                ls.Add("3C700400", "Divehi_100_CI_AI_WS");
                ls.Add("3CB00400", "Divehi_100_CI_AI_KS");
                ls.Add("3C300400", "Divehi_100_CI_AI_KS_WS");
                ls.Add("3CD00400", "Divehi_100_CI_AS");
                ls.Add("3C500400", "Divehi_100_CI_AS_WS");
                ls.Add("3C900400", "Divehi_100_CI_AS_KS");
                ls.Add("3C100400", "Divehi_100_CI_AS_KS_WS");
                ls.Add("3CE00400", "Divehi_100_CS_AI");
                ls.Add("3C600400", "Divehi_100_CS_AI_WS");
                ls.Add("3CA00400", "Divehi_100_CS_AI_KS");
                ls.Add("3C200400", "Divehi_100_CS_AI_KS_WS");
                ls.Add("3CC00400", "Divehi_100_CS_AS");
                ls.Add("3C400400", "Divehi_100_CS_AS_WS");
                ls.Add("3C800400", "Divehi_100_CS_AS_KS");
                ls.Add("3C000400", "Divehi_100_CS_AS_KS_WS");
                ls.Add("3CF10400", "Divehi_100_CI_AI_SC");
                ls.Add("3C710400", "Divehi_100_CI_AI_WS_SC");
                ls.Add("3CB10400", "Divehi_100_CI_AI_KS_SC");
                ls.Add("3C310400", "Divehi_100_CI_AI_KS_WS_SC");
                ls.Add("3CD10400", "Divehi_100_CI_AS_SC");
                ls.Add("3C510400", "Divehi_100_CI_AS_WS_SC");
                ls.Add("3C910400", "Divehi_100_CI_AS_KS_SC");
                ls.Add("3C110400", "Divehi_100_CI_AS_KS_WS_SC");
                ls.Add("3CE10400", "Divehi_100_CS_AI_SC");
                ls.Add("3C610400", "Divehi_100_CS_AI_WS_SC");
                ls.Add("3CA10400", "Divehi_100_CS_AI_KS_SC");
                ls.Add("3C210400", "Divehi_100_CS_AI_KS_WS_SC");
                ls.Add("3CC10400", "Divehi_100_CS_AS_SC");
                ls.Add("3C410400", "Divehi_100_CS_AS_WS_SC");
                ls.Add("3C810400", "Divehi_100_CS_AS_KS_SC");
                ls.Add("3C010400", "Divehi_100_CS_AS_KS_WS_SC");
                ls.Add("1D000100", "Estonian_BIN");
                ls.Add("1D080000", "Estonian_BIN2");
                ls.Add("1DF00000", "Estonian_CI_AI");
                ls.Add("1D700000", "Estonian_CI_AI_WS");
                ls.Add("1DB00000", "Estonian_CI_AI_KS");
                ls.Add("1D300000", "Estonian_CI_AI_KS_WS");
                ls.Add("1DD00000", "Estonian_CI_AS");
                ls.Add("1D500000", "Estonian_CI_AS_WS");
                ls.Add("1D900000", "Estonian_CI_AS_KS");
                ls.Add("1D100000", "Estonian_CI_AS_KS_WS");
                ls.Add("1DE00000", "Estonian_CS_AI");
                ls.Add("1D600000", "Estonian_CS_AI_WS");
                ls.Add("1DA00000", "Estonian_CS_AI_KS");
                ls.Add("1D200000", "Estonian_CS_AI_KS_WS");
                ls.Add("1DC00000", "Estonian_CS_AS");
                ls.Add("1D400000", "Estonian_CS_AS_WS");
                ls.Add("1D800000", "Estonian_CS_AS_KS");
                ls.Add("1D000000", "Estonian_CS_AS_KS_WS");
                ls.Add("1D000500", "Estonian_100_BIN");
                ls.Add("1D080400", "Estonian_100_BIN2");
                ls.Add("1DF00400", "Estonian_100_CI_AI");
                ls.Add("1D700400", "Estonian_100_CI_AI_WS");
                ls.Add("1DB00400", "Estonian_100_CI_AI_KS");
                ls.Add("1D300400", "Estonian_100_CI_AI_KS_WS");
                ls.Add("1DD00400", "Estonian_100_CI_AS");
                ls.Add("1D500400", "Estonian_100_CI_AS_WS");
                ls.Add("1D900400", "Estonian_100_CI_AS_KS");
                ls.Add("1D100400", "Estonian_100_CI_AS_KS_WS");
                ls.Add("1DE00400", "Estonian_100_CS_AI");
                ls.Add("1D600400", "Estonian_100_CS_AI_WS");
                ls.Add("1DA00400", "Estonian_100_CS_AI_KS");
                ls.Add("1D200400", "Estonian_100_CS_AI_KS_WS");
                ls.Add("1DC00400", "Estonian_100_CS_AS");
                ls.Add("1D400400", "Estonian_100_CS_AS_WS");
                ls.Add("1D800400", "Estonian_100_CS_AS_KS");
                ls.Add("1D000400", "Estonian_100_CS_AS_KS_WS");
                ls.Add("1DF10400", "Estonian_100_CI_AI_SC");
                ls.Add("1D710400", "Estonian_100_CI_AI_WS_SC");
                ls.Add("1DB10400", "Estonian_100_CI_AI_KS_SC");
                ls.Add("1D310400", "Estonian_100_CI_AI_KS_WS_SC");
                ls.Add("1DD10400", "Estonian_100_CI_AS_SC");
                ls.Add("1D510400", "Estonian_100_CI_AS_WS_SC");
                ls.Add("1D910400", "Estonian_100_CI_AS_KS_SC");
                ls.Add("1D110400", "Estonian_100_CI_AS_KS_WS_SC");
                ls.Add("1DE10400", "Estonian_100_CS_AI_SC");
                ls.Add("1D610400", "Estonian_100_CS_AI_WS_SC");
                ls.Add("1DA10400", "Estonian_100_CS_AI_KS_SC");
                ls.Add("1D210400", "Estonian_100_CS_AI_KS_WS_SC");
                ls.Add("1DC10400", "Estonian_100_CS_AS_SC");
                ls.Add("1D410400", "Estonian_100_CS_AS_WS_SC");
                ls.Add("1D810400", "Estonian_100_CS_AS_KS_SC");
                ls.Add("1D010400", "Estonian_100_CS_AS_KS_WS_SC");
                ls.Add("0A000100", "Finnish_Swedish_BIN");
                ls.Add("0A080000", "Finnish_Swedish_BIN2");
                ls.Add("0AF00000", "Finnish_Swedish_CI_AI");
                ls.Add("0A700000", "Finnish_Swedish_CI_AI_WS");
                ls.Add("0AB00000", "Finnish_Swedish_CI_AI_KS");
                ls.Add("0A300000", "Finnish_Swedish_CI_AI_KS_WS");
                ls.Add("0AD00000", "Finnish_Swedish_CI_AS");
                ls.Add("0A500000", "Finnish_Swedish_CI_AS_WS");
                ls.Add("0A900000", "Finnish_Swedish_CI_AS_KS");
                ls.Add("0A100000", "Finnish_Swedish_CI_AS_KS_WS");
                ls.Add("0AE00000", "Finnish_Swedish_CS_AI");
                ls.Add("0A600000", "Finnish_Swedish_CS_AI_WS");
                ls.Add("0AA00000", "Finnish_Swedish_CS_AI_KS");
                ls.Add("0A200000", "Finnish_Swedish_CS_AI_KS_WS");
                ls.Add("0AC00000", "Finnish_Swedish_CS_AS");
                ls.Add("0A400000", "Finnish_Swedish_CS_AS_WS");
                ls.Add("0A800000", "Finnish_Swedish_CS_AS_KS");
                ls.Add("0A000000", "Finnish_Swedish_CS_AS_KS_WS");
                ls.Add("0A000500", "Finnish_Swedish_100_BIN");
                ls.Add("0A080400", "Finnish_Swedish_100_BIN2");
                ls.Add("0AF00400", "Finnish_Swedish_100_CI_AI");
                ls.Add("0A700400", "Finnish_Swedish_100_CI_AI_WS");
                ls.Add("0AB00400", "Finnish_Swedish_100_CI_AI_KS");
                ls.Add("0A300400", "Finnish_Swedish_100_CI_AI_KS_WS");
                ls.Add("0AD00400", "Finnish_Swedish_100_CI_AS");
                ls.Add("0A500400", "Finnish_Swedish_100_CI_AS_WS");
                ls.Add("0A900400", "Finnish_Swedish_100_CI_AS_KS");
                ls.Add("0A100400", "Finnish_Swedish_100_CI_AS_KS_WS");
                ls.Add("0AE00400", "Finnish_Swedish_100_CS_AI");
                ls.Add("0A600400", "Finnish_Swedish_100_CS_AI_WS");
                ls.Add("0AA00400", "Finnish_Swedish_100_CS_AI_KS");
                ls.Add("0A200400", "Finnish_Swedish_100_CS_AI_KS_WS");
                ls.Add("0AC00400", "Finnish_Swedish_100_CS_AS");
                ls.Add("0A400400", "Finnish_Swedish_100_CS_AS_WS");
                ls.Add("0A800400", "Finnish_Swedish_100_CS_AS_KS");
                ls.Add("0A000400", "Finnish_Swedish_100_CS_AS_KS_WS");
                ls.Add("0AF10400", "Finnish_Swedish_100_CI_AI_SC");
                ls.Add("0A710400", "Finnish_Swedish_100_CI_AI_WS_SC");
                ls.Add("0AB10400", "Finnish_Swedish_100_CI_AI_KS_SC");
                ls.Add("0A310400", "Finnish_Swedish_100_CI_AI_KS_WS_SC");
                ls.Add("0AD10400", "Finnish_Swedish_100_CI_AS_SC");
                ls.Add("0A510400", "Finnish_Swedish_100_CI_AS_WS_SC");
                ls.Add("0A910400", "Finnish_Swedish_100_CI_AS_KS_SC");
                ls.Add("0A110400", "Finnish_Swedish_100_CI_AS_KS_WS_SC");
                ls.Add("0AE10400", "Finnish_Swedish_100_CS_AI_SC");
                ls.Add("0A610400", "Finnish_Swedish_100_CS_AI_WS_SC");
                ls.Add("0AA10400", "Finnish_Swedish_100_CS_AI_KS_SC");
                ls.Add("0A210400", "Finnish_Swedish_100_CS_AI_KS_WS_SC");
                ls.Add("0AC10400", "Finnish_Swedish_100_CS_AS_SC");
                ls.Add("0A410400", "Finnish_Swedish_100_CS_AS_WS_SC");
                ls.Add("0A810400", "Finnish_Swedish_100_CS_AS_KS_SC");
                ls.Add("0A010400", "Finnish_Swedish_100_CS_AS_KS_WS_SC");
                ls.Add("0B000100", "French_BIN");
                ls.Add("0B080000", "French_BIN2");
                ls.Add("0BF00000", "French_CI_AI");
                ls.Add("0B700000", "French_CI_AI_WS");
                ls.Add("0BB00000", "French_CI_AI_KS");
                ls.Add("0B300000", "French_CI_AI_KS_WS");
                ls.Add("0BD00000", "French_CI_AS");
                ls.Add("0B500000", "French_CI_AS_WS");
                ls.Add("0B900000", "French_CI_AS_KS");
                ls.Add("0B100000", "French_CI_AS_KS_WS");
                ls.Add("0BE00000", "French_CS_AI");
                ls.Add("0B600000", "French_CS_AI_WS");
                ls.Add("0BA00000", "French_CS_AI_KS");
                ls.Add("0B200000", "French_CS_AI_KS_WS");
                ls.Add("0BC00000", "French_CS_AS");
                ls.Add("0B400000", "French_CS_AS_WS");
                ls.Add("0B800000", "French_CS_AS_KS");
                ls.Add("0B000000", "French_CS_AS_KS_WS");
                ls.Add("0B000500", "French_100_BIN");
                ls.Add("0B080400", "French_100_BIN2");
                ls.Add("0BF00400", "French_100_CI_AI");
                ls.Add("0B700400", "French_100_CI_AI_WS");
                ls.Add("0BB00400", "French_100_CI_AI_KS");
                ls.Add("0B300400", "French_100_CI_AI_KS_WS");
                ls.Add("0BD00400", "French_100_CI_AS");
                ls.Add("0B500400", "French_100_CI_AS_WS");
                ls.Add("0B900400", "French_100_CI_AS_KS");
                ls.Add("0B100400", "French_100_CI_AS_KS_WS");
                ls.Add("0BE00400", "French_100_CS_AI");
                ls.Add("0B600400", "French_100_CS_AI_WS");
                ls.Add("0BA00400", "French_100_CS_AI_KS");
                ls.Add("0B200400", "French_100_CS_AI_KS_WS");
                ls.Add("0BC00400", "French_100_CS_AS");
                ls.Add("0B400400", "French_100_CS_AS_WS");
                ls.Add("0B800400", "French_100_CS_AS_KS");
                ls.Add("0B000400", "French_100_CS_AS_KS_WS");
                ls.Add("0BF10400", "French_100_CI_AI_SC");
                ls.Add("0B710400", "French_100_CI_AI_WS_SC");
                ls.Add("0BB10400", "French_100_CI_AI_KS_SC");
                ls.Add("0B310400", "French_100_CI_AI_KS_WS_SC");
                ls.Add("0BD10400", "French_100_CI_AS_SC");
                ls.Add("0B510400", "French_100_CI_AS_WS_SC");
                ls.Add("0B910400", "French_100_CI_AS_KS_SC");
                ls.Add("0B110400", "French_100_CI_AS_KS_WS_SC");
                ls.Add("0BE10400", "French_100_CS_AI_SC");
                ls.Add("0B610400", "French_100_CS_AI_WS_SC");
                ls.Add("0BA10400", "French_100_CS_AI_KS_SC");
                ls.Add("0B210400", "French_100_CS_AI_KS_WS_SC");
                ls.Add("0BC10400", "French_100_CS_AS_SC");
                ls.Add("0B410400", "French_100_CS_AS_WS_SC");
                ls.Add("0B810400", "French_100_CS_AS_KS_SC");
                ls.Add("0B010400", "French_100_CS_AS_KS_WS_SC");
                ls.Add("60000500", "Frisian_100_BIN");
                ls.Add("60080400", "Frisian_100_BIN2");
                ls.Add("60F00400", "Frisian_100_CI_AI");
                ls.Add("60700400", "Frisian_100_CI_AI_WS");
                ls.Add("60B00400", "Frisian_100_CI_AI_KS");
                ls.Add("60300400", "Frisian_100_CI_AI_KS_WS");
                ls.Add("60D00400", "Frisian_100_CI_AS");
                ls.Add("60500400", "Frisian_100_CI_AS_WS");
                ls.Add("60900400", "Frisian_100_CI_AS_KS");
                ls.Add("60100400", "Frisian_100_CI_AS_KS_WS");
                ls.Add("60E00400", "Frisian_100_CS_AI");
                ls.Add("60600400", "Frisian_100_CS_AI_WS");
                ls.Add("60A00400", "Frisian_100_CS_AI_KS");
                ls.Add("60200400", "Frisian_100_CS_AI_KS_WS");
                ls.Add("60C00400", "Frisian_100_CS_AS");
                ls.Add("60400400", "Frisian_100_CS_AS_WS");
                ls.Add("60800400", "Frisian_100_CS_AS_KS");
                ls.Add("60000400", "Frisian_100_CS_AS_KS_WS");
                ls.Add("60F10400", "Frisian_100_CI_AI_SC");
                ls.Add("60710400", "Frisian_100_CI_AI_WS_SC");
                ls.Add("60B10400", "Frisian_100_CI_AI_KS_SC");
                ls.Add("60310400", "Frisian_100_CI_AI_KS_WS_SC");
                ls.Add("60D10400", "Frisian_100_CI_AS_SC");
                ls.Add("60510400", "Frisian_100_CI_AS_WS_SC");
                ls.Add("60910400", "Frisian_100_CI_AS_KS_SC");
                ls.Add("60110400", "Frisian_100_CI_AS_KS_WS_SC");
                ls.Add("60E10400", "Frisian_100_CS_AI_SC");
                ls.Add("60610400", "Frisian_100_CS_AI_WS_SC");
                ls.Add("60A10400", "Frisian_100_CS_AI_KS_SC");
                ls.Add("60210400", "Frisian_100_CS_AI_KS_WS_SC");
                ls.Add("60C10400", "Frisian_100_CS_AS_SC");
                ls.Add("60410400", "Frisian_100_CS_AS_WS_SC");
                ls.Add("60810400", "Frisian_100_CS_AS_KS_SC");
                ls.Add("60010400", "Frisian_100_CS_AS_KS_WS_SC");
                ls.Add("2D000100", "Georgian_Modern_Sort_BIN");
                ls.Add("2D080000", "Georgian_Modern_Sort_BIN2");
                ls.Add("2DF00000", "Georgian_Modern_Sort_CI_AI");
                ls.Add("2D700000", "Georgian_Modern_Sort_CI_AI_WS");
                ls.Add("2DB00000", "Georgian_Modern_Sort_CI_AI_KS");
                ls.Add("2D300000", "Georgian_Modern_Sort_CI_AI_KS_WS");
                ls.Add("2DD00000", "Georgian_Modern_Sort_CI_AS");
                ls.Add("2D500000", "Georgian_Modern_Sort_CI_AS_WS");
                ls.Add("2D900000", "Georgian_Modern_Sort_CI_AS_KS");
                ls.Add("2D100000", "Georgian_Modern_Sort_CI_AS_KS_WS");
                ls.Add("2DE00000", "Georgian_Modern_Sort_CS_AI");
                ls.Add("2D600000", "Georgian_Modern_Sort_CS_AI_WS");
                ls.Add("2DA00000", "Georgian_Modern_Sort_CS_AI_KS");
                ls.Add("2D200000", "Georgian_Modern_Sort_CS_AI_KS_WS");
                ls.Add("2DC00000", "Georgian_Modern_Sort_CS_AS");
                ls.Add("2D400000", "Georgian_Modern_Sort_CS_AS_WS");
                ls.Add("2D800000", "Georgian_Modern_Sort_CS_AS_KS");
                ls.Add("2D000000", "Georgian_Modern_Sort_CS_AS_KS_WS");
                ls.Add("2D000500", "Georgian_Modern_Sort_100_BIN");
                ls.Add("2D080400", "Georgian_Modern_Sort_100_BIN2");
                ls.Add("2DF00400", "Georgian_Modern_Sort_100_CI_AI");
                ls.Add("2D700400", "Georgian_Modern_Sort_100_CI_AI_WS");
                ls.Add("2DB00400", "Georgian_Modern_Sort_100_CI_AI_KS");
                ls.Add("2D300400", "Georgian_Modern_Sort_100_CI_AI_KS_WS");
                ls.Add("2DD00400", "Georgian_Modern_Sort_100_CI_AS");
                ls.Add("2D500400", "Georgian_Modern_Sort_100_CI_AS_WS");
                ls.Add("2D900400", "Georgian_Modern_Sort_100_CI_AS_KS");
                ls.Add("2D100400", "Georgian_Modern_Sort_100_CI_AS_KS_WS");
                ls.Add("2DE00400", "Georgian_Modern_Sort_100_CS_AI");
                ls.Add("2D600400", "Georgian_Modern_Sort_100_CS_AI_WS");
                ls.Add("2DA00400", "Georgian_Modern_Sort_100_CS_AI_KS");
                ls.Add("2D200400", "Georgian_Modern_Sort_100_CS_AI_KS_WS");
                ls.Add("2DC00400", "Georgian_Modern_Sort_100_CS_AS");
                ls.Add("2D400400", "Georgian_Modern_Sort_100_CS_AS_WS");
                ls.Add("2D800400", "Georgian_Modern_Sort_100_CS_AS_KS");
                ls.Add("2D000400", "Georgian_Modern_Sort_100_CS_AS_KS_WS");
                ls.Add("2DF10400", "Georgian_Modern_Sort_100_CI_AI_SC");
                ls.Add("2D710400", "Georgian_Modern_Sort_100_CI_AI_WS_SC");
                ls.Add("2DB10400", "Georgian_Modern_Sort_100_CI_AI_KS_SC");
                ls.Add("2D310400", "Georgian_Modern_Sort_100_CI_AI_KS_WS_SC");
                ls.Add("2DD10400", "Georgian_Modern_Sort_100_CI_AS_SC");
                ls.Add("2D510400", "Georgian_Modern_Sort_100_CI_AS_WS_SC");
                ls.Add("2D910400", "Georgian_Modern_Sort_100_CI_AS_KS_SC");
                ls.Add("2D110400", "Georgian_Modern_Sort_100_CI_AS_KS_WS_SC");
                ls.Add("2DE10400", "Georgian_Modern_Sort_100_CS_AI_SC");
                ls.Add("2D610400", "Georgian_Modern_Sort_100_CS_AI_WS_SC");
                ls.Add("2DA10400", "Georgian_Modern_Sort_100_CS_AI_KS_SC");
                ls.Add("2D210400", "Georgian_Modern_Sort_100_CS_AI_KS_WS_SC");
                ls.Add("2DC10400", "Georgian_Modern_Sort_100_CS_AS_SC");
                ls.Add("2D410400", "Georgian_Modern_Sort_100_CS_AS_WS_SC");
                ls.Add("2D810400", "Georgian_Modern_Sort_100_CS_AS_KS_SC");
                ls.Add("2D010400", "Georgian_Modern_Sort_100_CS_AS_KS_WS_SC");
                ls.Add("29000100", "German_PhoneBook_BIN");
                ls.Add("29080000", "German_PhoneBook_BIN2");
                ls.Add("29F00000", "German_PhoneBook_CI_AI");
                ls.Add("29700000", "German_PhoneBook_CI_AI_WS");
                ls.Add("29B00000", "German_PhoneBook_CI_AI_KS");
                ls.Add("29300000", "German_PhoneBook_CI_AI_KS_WS");
                ls.Add("29D00000", "German_PhoneBook_CI_AS");
                ls.Add("29500000", "German_PhoneBook_CI_AS_WS");
                ls.Add("29900000", "German_PhoneBook_CI_AS_KS");
                ls.Add("29100000", "German_PhoneBook_CI_AS_KS_WS");
                ls.Add("29E00000", "German_PhoneBook_CS_AI");
                ls.Add("29600000", "German_PhoneBook_CS_AI_WS");
                ls.Add("29A00000", "German_PhoneBook_CS_AI_KS");
                ls.Add("29200000", "German_PhoneBook_CS_AI_KS_WS");
                ls.Add("29C00000", "German_PhoneBook_CS_AS");
                ls.Add("29400000", "German_PhoneBook_CS_AS_WS");
                ls.Add("29800000", "German_PhoneBook_CS_AS_KS");
                ls.Add("29000000", "German_PhoneBook_CS_AS_KS_WS");
                ls.Add("29000500", "German_PhoneBook_100_BIN");
                ls.Add("29080400", "German_PhoneBook_100_BIN2");
                ls.Add("29F00400", "German_PhoneBook_100_CI_AI");
                ls.Add("29700400", "German_PhoneBook_100_CI_AI_WS");
                ls.Add("29B00400", "German_PhoneBook_100_CI_AI_KS");
                ls.Add("29300400", "German_PhoneBook_100_CI_AI_KS_WS");
                ls.Add("29D00400", "German_PhoneBook_100_CI_AS");
                ls.Add("29500400", "German_PhoneBook_100_CI_AS_WS");
                ls.Add("29900400", "German_PhoneBook_100_CI_AS_KS");
                ls.Add("29100400", "German_PhoneBook_100_CI_AS_KS_WS");
                ls.Add("29E00400", "German_PhoneBook_100_CS_AI");
                ls.Add("29600400", "German_PhoneBook_100_CS_AI_WS");
                ls.Add("29A00400", "German_PhoneBook_100_CS_AI_KS");
                ls.Add("29200400", "German_PhoneBook_100_CS_AI_KS_WS");
                ls.Add("29C00400", "German_PhoneBook_100_CS_AS");
                ls.Add("29400400", "German_PhoneBook_100_CS_AS_WS");
                ls.Add("29800400", "German_PhoneBook_100_CS_AS_KS");
                ls.Add("29000400", "German_PhoneBook_100_CS_AS_KS_WS");
                ls.Add("29F10400", "German_PhoneBook_100_CI_AI_SC");
                ls.Add("29710400", "German_PhoneBook_100_CI_AI_WS_SC");
                ls.Add("29B10400", "German_PhoneBook_100_CI_AI_KS_SC");
                ls.Add("29310400", "German_PhoneBook_100_CI_AI_KS_WS_SC");
                ls.Add("29D10400", "German_PhoneBook_100_CI_AS_SC");
                ls.Add("29510400", "German_PhoneBook_100_CI_AS_WS_SC");
                ls.Add("29910400", "German_PhoneBook_100_CI_AS_KS_SC");
                ls.Add("29110400", "German_PhoneBook_100_CI_AS_KS_WS_SC");
                ls.Add("29E10400", "German_PhoneBook_100_CS_AI_SC");
                ls.Add("29610400", "German_PhoneBook_100_CS_AI_WS_SC");
                ls.Add("29A10400", "German_PhoneBook_100_CS_AI_KS_SC");
                ls.Add("29210400", "German_PhoneBook_100_CS_AI_KS_WS_SC");
                ls.Add("29C10400", "German_PhoneBook_100_CS_AS_SC");
                ls.Add("29410400", "German_PhoneBook_100_CS_AS_WS_SC");
                ls.Add("29810400", "German_PhoneBook_100_CS_AS_KS_SC");
                ls.Add("29010400", "German_PhoneBook_100_CS_AS_KS_WS_SC");
                ls.Add("07000100", "Greek_BIN");
                ls.Add("07080000", "Greek_BIN2");
                ls.Add("07F00000", "Greek_CI_AI");
                ls.Add("07700000", "Greek_CI_AI_WS");
                ls.Add("07B00000", "Greek_CI_AI_KS");
                ls.Add("07300000", "Greek_CI_AI_KS_WS");
                ls.Add("07D00000", "Greek_CI_AS");
                ls.Add("07500000", "Greek_CI_AS_WS");
                ls.Add("07900000", "Greek_CI_AS_KS");
                ls.Add("07100000", "Greek_CI_AS_KS_WS");
                ls.Add("07E00000", "Greek_CS_AI");
                ls.Add("07600000", "Greek_CS_AI_WS");
                ls.Add("07A00000", "Greek_CS_AI_KS");
                ls.Add("07200000", "Greek_CS_AI_KS_WS");
                ls.Add("07C00000", "Greek_CS_AS");
                ls.Add("07400000", "Greek_CS_AS_WS");
                ls.Add("07800000", "Greek_CS_AS_KS");
                ls.Add("07000000", "Greek_CS_AS_KS_WS");
                ls.Add("07000500", "Greek_100_BIN");
                ls.Add("07080400", "Greek_100_BIN2");
                ls.Add("07F00400", "Greek_100_CI_AI");
                ls.Add("07700400", "Greek_100_CI_AI_WS");
                ls.Add("07B00400", "Greek_100_CI_AI_KS");
                ls.Add("07300400", "Greek_100_CI_AI_KS_WS");
                ls.Add("07D00400", "Greek_100_CI_AS");
                ls.Add("07500400", "Greek_100_CI_AS_WS");
                ls.Add("07900400", "Greek_100_CI_AS_KS");
                ls.Add("07100400", "Greek_100_CI_AS_KS_WS");
                ls.Add("07E00400", "Greek_100_CS_AI");
                ls.Add("07600400", "Greek_100_CS_AI_WS");
                ls.Add("07A00400", "Greek_100_CS_AI_KS");
                ls.Add("07200400", "Greek_100_CS_AI_KS_WS");
                ls.Add("07C00400", "Greek_100_CS_AS");
                ls.Add("07400400", "Greek_100_CS_AS_WS");
                ls.Add("07800400", "Greek_100_CS_AS_KS");
                ls.Add("07000400", "Greek_100_CS_AS_KS_WS");
                ls.Add("07F10400", "Greek_100_CI_AI_SC");
                ls.Add("07710400", "Greek_100_CI_AI_WS_SC");
                ls.Add("07B10400", "Greek_100_CI_AI_KS_SC");
                ls.Add("07310400", "Greek_100_CI_AI_KS_WS_SC");
                ls.Add("07D10400", "Greek_100_CI_AS_SC");
                ls.Add("07510400", "Greek_100_CI_AS_WS_SC");
                ls.Add("07910400", "Greek_100_CI_AS_KS_SC");
                ls.Add("07110400", "Greek_100_CI_AS_KS_WS_SC");
                ls.Add("07E10400", "Greek_100_CS_AI_SC");
                ls.Add("07610400", "Greek_100_CS_AI_WS_SC");
                ls.Add("07A10400", "Greek_100_CS_AI_KS_SC");
                ls.Add("07210400", "Greek_100_CS_AI_KS_WS_SC");
                ls.Add("07C10400", "Greek_100_CS_AS_SC");
                ls.Add("07410400", "Greek_100_CS_AS_WS_SC");
                ls.Add("07810400", "Greek_100_CS_AS_KS_SC");
                ls.Add("07010400", "Greek_100_CS_AS_KS_WS_SC");
                ls.Add("0C000100", "Hebrew_BIN");
                ls.Add("0C080000", "Hebrew_BIN2");
                ls.Add("0CF00000", "Hebrew_CI_AI");
                ls.Add("0C700000", "Hebrew_CI_AI_WS");
                ls.Add("0CB00000", "Hebrew_CI_AI_KS");
                ls.Add("0C300000", "Hebrew_CI_AI_KS_WS");
                ls.Add("0CD00000", "Hebrew_CI_AS");
                ls.Add("0C500000", "Hebrew_CI_AS_WS");
                ls.Add("0C900000", "Hebrew_CI_AS_KS");
                ls.Add("0C100000", "Hebrew_CI_AS_KS_WS");
                ls.Add("0CE00000", "Hebrew_CS_AI");
                ls.Add("0C600000", "Hebrew_CS_AI_WS");
                ls.Add("0CA00000", "Hebrew_CS_AI_KS");
                ls.Add("0C200000", "Hebrew_CS_AI_KS_WS");
                ls.Add("0CC00000", "Hebrew_CS_AS");
                ls.Add("0C400000", "Hebrew_CS_AS_WS");
                ls.Add("0C800000", "Hebrew_CS_AS_KS");
                ls.Add("0C000000", "Hebrew_CS_AS_KS_WS");
                ls.Add("0C000500", "Hebrew_100_BIN");
                ls.Add("0C080400", "Hebrew_100_BIN2");
                ls.Add("0CF00400", "Hebrew_100_CI_AI");
                ls.Add("0C700400", "Hebrew_100_CI_AI_WS");
                ls.Add("0CB00400", "Hebrew_100_CI_AI_KS");
                ls.Add("0C300400", "Hebrew_100_CI_AI_KS_WS");
                ls.Add("0CD00400", "Hebrew_100_CI_AS");
                ls.Add("0C500400", "Hebrew_100_CI_AS_WS");
                ls.Add("0C900400", "Hebrew_100_CI_AS_KS");
                ls.Add("0C100400", "Hebrew_100_CI_AS_KS_WS");
                ls.Add("0CE00400", "Hebrew_100_CS_AI");
                ls.Add("0C600400", "Hebrew_100_CS_AI_WS");
                ls.Add("0CA00400", "Hebrew_100_CS_AI_KS");
                ls.Add("0C200400", "Hebrew_100_CS_AI_KS_WS");
                ls.Add("0CC00400", "Hebrew_100_CS_AS");
                ls.Add("0C400400", "Hebrew_100_CS_AS_WS");
                ls.Add("0C800400", "Hebrew_100_CS_AS_KS");
                ls.Add("0C000400", "Hebrew_100_CS_AS_KS_WS");
                ls.Add("0CF10400", "Hebrew_100_CI_AI_SC");
                ls.Add("0C710400", "Hebrew_100_CI_AI_WS_SC");
                ls.Add("0CB10400", "Hebrew_100_CI_AI_KS_SC");
                ls.Add("0C310400", "Hebrew_100_CI_AI_KS_WS_SC");
                ls.Add("0CD10400", "Hebrew_100_CI_AS_SC");
                ls.Add("0C510400", "Hebrew_100_CI_AS_WS_SC");
                ls.Add("0C910400", "Hebrew_100_CI_AS_KS_SC");
                ls.Add("0C110400", "Hebrew_100_CI_AS_KS_WS_SC");
                ls.Add("0CE10400", "Hebrew_100_CS_AI_SC");
                ls.Add("0C610400", "Hebrew_100_CS_AI_WS_SC");
                ls.Add("0CA10400", "Hebrew_100_CS_AI_KS_SC");
                ls.Add("0C210400", "Hebrew_100_CS_AI_KS_WS_SC");
                ls.Add("0CC10400", "Hebrew_100_CS_AS_SC");
                ls.Add("0C410400", "Hebrew_100_CS_AS_WS_SC");
                ls.Add("0C810400", "Hebrew_100_CS_AS_KS_SC");
                ls.Add("0C010400", "Hebrew_100_CS_AS_KS_WS_SC");
                ls.Add("0D000100", "Hungarian_BIN");
                ls.Add("0D080000", "Hungarian_BIN2");
                ls.Add("0DF00000", "Hungarian_CI_AI");
                ls.Add("0D700000", "Hungarian_CI_AI_WS");
                ls.Add("0DB00000", "Hungarian_CI_AI_KS");
                ls.Add("0D300000", "Hungarian_CI_AI_KS_WS");
                ls.Add("0DD00000", "Hungarian_CI_AS");
                ls.Add("0D500000", "Hungarian_CI_AS_WS");
                ls.Add("0D900000", "Hungarian_CI_AS_KS");
                ls.Add("0D100000", "Hungarian_CI_AS_KS_WS");
                ls.Add("0DE00000", "Hungarian_CS_AI");
                ls.Add("0D600000", "Hungarian_CS_AI_WS");
                ls.Add("0DA00000", "Hungarian_CS_AI_KS");
                ls.Add("0D200000", "Hungarian_CS_AI_KS_WS");
                ls.Add("0DC00000", "Hungarian_CS_AS");
                ls.Add("0D400000", "Hungarian_CS_AS_WS");
                ls.Add("0D800000", "Hungarian_CS_AS_KS");
                ls.Add("0D000000", "Hungarian_CS_AS_KS_WS");
                ls.Add("0D000500", "Hungarian_100_BIN");
                ls.Add("0D080400", "Hungarian_100_BIN2");
                ls.Add("0DF00400", "Hungarian_100_CI_AI");
                ls.Add("0D700400", "Hungarian_100_CI_AI_WS");
                ls.Add("0DB00400", "Hungarian_100_CI_AI_KS");
                ls.Add("0D300400", "Hungarian_100_CI_AI_KS_WS");
                ls.Add("0DD00400", "Hungarian_100_CI_AS");
                ls.Add("0D500400", "Hungarian_100_CI_AS_WS");
                ls.Add("0D900400", "Hungarian_100_CI_AS_KS");
                ls.Add("0D100400", "Hungarian_100_CI_AS_KS_WS");
                ls.Add("0DE00400", "Hungarian_100_CS_AI");
                ls.Add("0D600400", "Hungarian_100_CS_AI_WS");
                ls.Add("0DA00400", "Hungarian_100_CS_AI_KS");
                ls.Add("0D200400", "Hungarian_100_CS_AI_KS_WS");
                ls.Add("0DC00400", "Hungarian_100_CS_AS");
                ls.Add("0D400400", "Hungarian_100_CS_AS_WS");
                ls.Add("0D800400", "Hungarian_100_CS_AS_KS");
                ls.Add("0D000400", "Hungarian_100_CS_AS_KS_WS");
                ls.Add("0DF10400", "Hungarian_100_CI_AI_SC");
                ls.Add("0D710400", "Hungarian_100_CI_AI_WS_SC");
                ls.Add("0DB10400", "Hungarian_100_CI_AI_KS_SC");
                ls.Add("0D310400", "Hungarian_100_CI_AI_KS_WS_SC");
                ls.Add("0DD10400", "Hungarian_100_CI_AS_SC");
                ls.Add("0D510400", "Hungarian_100_CI_AS_WS_SC");
                ls.Add("0D910400", "Hungarian_100_CI_AS_KS_SC");
                ls.Add("0D110400", "Hungarian_100_CI_AS_KS_WS_SC");
                ls.Add("0DE10400", "Hungarian_100_CS_AI_SC");
                ls.Add("0D610400", "Hungarian_100_CS_AI_WS_SC");
                ls.Add("0DA10400", "Hungarian_100_CS_AI_KS_SC");
                ls.Add("0D210400", "Hungarian_100_CS_AI_KS_WS_SC");
                ls.Add("0DC10400", "Hungarian_100_CS_AS_SC");
                ls.Add("0D410400", "Hungarian_100_CS_AS_WS_SC");
                ls.Add("0D810400", "Hungarian_100_CS_AS_KS_SC");
                ls.Add("0D010400", "Hungarian_100_CS_AS_KS_WS_SC");
                ls.Add("2A000100", "Hungarian_Technical_BIN");
                ls.Add("2A080000", "Hungarian_Technical_BIN2");
                ls.Add("2AF00000", "Hungarian_Technical_CI_AI");
                ls.Add("2A700000", "Hungarian_Technical_CI_AI_WS");
                ls.Add("2AB00000", "Hungarian_Technical_CI_AI_KS");
                ls.Add("2A300000", "Hungarian_Technical_CI_AI_KS_WS");
                ls.Add("2AD00000", "Hungarian_Technical_CI_AS");
                ls.Add("2A500000", "Hungarian_Technical_CI_AS_WS");
                ls.Add("2A900000", "Hungarian_Technical_CI_AS_KS");
                ls.Add("2A100000", "Hungarian_Technical_CI_AS_KS_WS");
                ls.Add("2AE00000", "Hungarian_Technical_CS_AI");
                ls.Add("2A600000", "Hungarian_Technical_CS_AI_WS");
                ls.Add("2AA00000", "Hungarian_Technical_CS_AI_KS");
                ls.Add("2A200000", "Hungarian_Technical_CS_AI_KS_WS");
                ls.Add("2AC00000", "Hungarian_Technical_CS_AS");
                ls.Add("2A400000", "Hungarian_Technical_CS_AS_WS");
                ls.Add("2A800000", "Hungarian_Technical_CS_AS_KS");
                ls.Add("2A000000", "Hungarian_Technical_CS_AS_KS_WS");
                ls.Add("2A000500", "Hungarian_Technical_100_BIN");
                ls.Add("2A080400", "Hungarian_Technical_100_BIN2");
                ls.Add("2AF00400", "Hungarian_Technical_100_CI_AI");
                ls.Add("2A700400", "Hungarian_Technical_100_CI_AI_WS");
                ls.Add("2AB00400", "Hungarian_Technical_100_CI_AI_KS");
                ls.Add("2A300400", "Hungarian_Technical_100_CI_AI_KS_WS");
                ls.Add("2AD00400", "Hungarian_Technical_100_CI_AS");
                ls.Add("2A500400", "Hungarian_Technical_100_CI_AS_WS");
                ls.Add("2A900400", "Hungarian_Technical_100_CI_AS_KS");
                ls.Add("2A100400", "Hungarian_Technical_100_CI_AS_KS_WS");
                ls.Add("2AE00400", "Hungarian_Technical_100_CS_AI");
                ls.Add("2A600400", "Hungarian_Technical_100_CS_AI_WS");
                ls.Add("2AA00400", "Hungarian_Technical_100_CS_AI_KS");
                ls.Add("2A200400", "Hungarian_Technical_100_CS_AI_KS_WS");
                ls.Add("2AC00400", "Hungarian_Technical_100_CS_AS");
                ls.Add("2A400400", "Hungarian_Technical_100_CS_AS_WS");
                ls.Add("2A800400", "Hungarian_Technical_100_CS_AS_KS");
                ls.Add("2A000400", "Hungarian_Technical_100_CS_AS_KS_WS");
                ls.Add("2AF10400", "Hungarian_Technical_100_CI_AI_SC");
                ls.Add("2A710400", "Hungarian_Technical_100_CI_AI_WS_SC");
                ls.Add("2AB10400", "Hungarian_Technical_100_CI_AI_KS_SC");
                ls.Add("2A310400", "Hungarian_Technical_100_CI_AI_KS_WS_SC");
                ls.Add("2AD10400", "Hungarian_Technical_100_CI_AS_SC");
                ls.Add("2A510400", "Hungarian_Technical_100_CI_AS_WS_SC");
                ls.Add("2A910400", "Hungarian_Technical_100_CI_AS_KS_SC");
                ls.Add("2A110400", "Hungarian_Technical_100_CI_AS_KS_WS_SC");
                ls.Add("2AE10400", "Hungarian_Technical_100_CS_AI_SC");
                ls.Add("2A610400", "Hungarian_Technical_100_CS_AI_WS_SC");
                ls.Add("2AA10400", "Hungarian_Technical_100_CS_AI_KS_SC");
                ls.Add("2A210400", "Hungarian_Technical_100_CS_AI_KS_WS_SC");
                ls.Add("2AC10400", "Hungarian_Technical_100_CS_AS_SC");
                ls.Add("2A410400", "Hungarian_Technical_100_CS_AS_WS_SC");
                ls.Add("2A810400", "Hungarian_Technical_100_CS_AS_KS_SC");
                ls.Add("2A010400", "Hungarian_Technical_100_CS_AS_KS_WS_SC");
                ls.Add("0E000100", "Icelandic_BIN");
                ls.Add("0E080000", "Icelandic_BIN2");
                ls.Add("0EF00000", "Icelandic_CI_AI");
                ls.Add("0E700000", "Icelandic_CI_AI_WS");
                ls.Add("0EB00000", "Icelandic_CI_AI_KS");
                ls.Add("0E300000", "Icelandic_CI_AI_KS_WS");
                ls.Add("0ED00000", "Icelandic_CI_AS");
                ls.Add("0E500000", "Icelandic_CI_AS_WS");
                ls.Add("0E900000", "Icelandic_CI_AS_KS");
                ls.Add("0E100000", "Icelandic_CI_AS_KS_WS");
                ls.Add("0EE00000", "Icelandic_CS_AI");
                ls.Add("0E600000", "Icelandic_CS_AI_WS");
                ls.Add("0EA00000", "Icelandic_CS_AI_KS");
                ls.Add("0E200000", "Icelandic_CS_AI_KS_WS");
                ls.Add("0EC00000", "Icelandic_CS_AS");
                ls.Add("0E400000", "Icelandic_CS_AS_WS");
                ls.Add("0E800000", "Icelandic_CS_AS_KS");
                ls.Add("0E000000", "Icelandic_CS_AS_KS_WS");
                ls.Add("0E000500", "Icelandic_100_BIN");
                ls.Add("0E080400", "Icelandic_100_BIN2");
                ls.Add("0EF00400", "Icelandic_100_CI_AI");
                ls.Add("0E700400", "Icelandic_100_CI_AI_WS");
                ls.Add("0EB00400", "Icelandic_100_CI_AI_KS");
                ls.Add("0E300400", "Icelandic_100_CI_AI_KS_WS");
                ls.Add("0ED00400", "Icelandic_100_CI_AS");
                ls.Add("0E500400", "Icelandic_100_CI_AS_WS");
                ls.Add("0E900400", "Icelandic_100_CI_AS_KS");
                ls.Add("0E100400", "Icelandic_100_CI_AS_KS_WS");
                ls.Add("0EE00400", "Icelandic_100_CS_AI");
                ls.Add("0E600400", "Icelandic_100_CS_AI_WS");
                ls.Add("0EA00400", "Icelandic_100_CS_AI_KS");
                ls.Add("0E200400", "Icelandic_100_CS_AI_KS_WS");
                ls.Add("0EC00400", "Icelandic_100_CS_AS");
                ls.Add("0E400400", "Icelandic_100_CS_AS_WS");
                ls.Add("0E800400", "Icelandic_100_CS_AS_KS");
                ls.Add("0E000400", "Icelandic_100_CS_AS_KS_WS");
                ls.Add("0EF10400", "Icelandic_100_CI_AI_SC");
                ls.Add("0E710400", "Icelandic_100_CI_AI_WS_SC");
                ls.Add("0EB10400", "Icelandic_100_CI_AI_KS_SC");
                ls.Add("0E310400", "Icelandic_100_CI_AI_KS_WS_SC");
                ls.Add("0ED10400", "Icelandic_100_CI_AS_SC");
                ls.Add("0E510400", "Icelandic_100_CI_AS_WS_SC");
                ls.Add("0E910400", "Icelandic_100_CI_AS_KS_SC");
                ls.Add("0E110400", "Icelandic_100_CI_AS_KS_WS_SC");
                ls.Add("0EE10400", "Icelandic_100_CS_AI_SC");
                ls.Add("0E610400", "Icelandic_100_CS_AI_WS_SC");
                ls.Add("0EA10400", "Icelandic_100_CS_AI_KS_SC");
                ls.Add("0E210400", "Icelandic_100_CS_AI_KS_WS_SC");
                ls.Add("0EC10400", "Icelandic_100_CS_AS_SC");
                ls.Add("0E410400", "Icelandic_100_CS_AS_WS_SC");
                ls.Add("0E810400", "Icelandic_100_CS_AS_KS_SC");
                ls.Add("0E010400", "Icelandic_100_CS_AS_KS_WS_SC");
                ls.Add("35000300", "Indic_General_90_BIN");
                ls.Add("35080200", "Indic_General_90_BIN2");
                ls.Add("35F00200", "Indic_General_90_CI_AI");
                ls.Add("35700200", "Indic_General_90_CI_AI_WS");
                ls.Add("35B00200", "Indic_General_90_CI_AI_KS");
                ls.Add("35300200", "Indic_General_90_CI_AI_KS_WS");
                ls.Add("35D00200", "Indic_General_90_CI_AS");
                ls.Add("35500200", "Indic_General_90_CI_AS_WS");
                ls.Add("35900200", "Indic_General_90_CI_AS_KS");
                ls.Add("35100200", "Indic_General_90_CI_AS_KS_WS");
                ls.Add("35E00200", "Indic_General_90_CS_AI");
                ls.Add("35600200", "Indic_General_90_CS_AI_WS");
                ls.Add("35A00200", "Indic_General_90_CS_AI_KS");
                ls.Add("35200200", "Indic_General_90_CS_AI_KS_WS");
                ls.Add("35C00200", "Indic_General_90_CS_AS");
                ls.Add("35400200", "Indic_General_90_CS_AS_WS");
                ls.Add("35800200", "Indic_General_90_CS_AS_KS");
                ls.Add("35000200", "Indic_General_90_CS_AS_KS_WS");
                ls.Add("35F10200", "Indic_General_90_CI_AI_SC");
                ls.Add("35710200", "Indic_General_90_CI_AI_WS_SC");
                ls.Add("35B10200", "Indic_General_90_CI_AI_KS_SC");
                ls.Add("35310200", "Indic_General_90_CI_AI_KS_WS_SC");
                ls.Add("35D10200", "Indic_General_90_CI_AS_SC");
                ls.Add("35510200", "Indic_General_90_CI_AS_WS_SC");
                ls.Add("35910200", "Indic_General_90_CI_AS_KS_SC");
                ls.Add("35110200", "Indic_General_90_CI_AS_KS_WS_SC");
                ls.Add("35E10200", "Indic_General_90_CS_AI_SC");
                ls.Add("35610200", "Indic_General_90_CS_AI_WS_SC");
                ls.Add("35A10200", "Indic_General_90_CS_AI_KS_SC");
                ls.Add("35210200", "Indic_General_90_CS_AI_KS_WS_SC");
                ls.Add("35C10200", "Indic_General_90_CS_AS_SC");
                ls.Add("35410200", "Indic_General_90_CS_AS_WS_SC");
                ls.Add("35810200", "Indic_General_90_CS_AS_KS_SC");
                ls.Add("35010200", "Indic_General_90_CS_AS_KS_WS_SC");
                ls.Add("35000500", "Indic_General_100_BIN");
                ls.Add("35080400", "Indic_General_100_BIN2");
                ls.Add("35F00400", "Indic_General_100_CI_AI");
                ls.Add("35700400", "Indic_General_100_CI_AI_WS");
                ls.Add("35B00400", "Indic_General_100_CI_AI_KS");
                ls.Add("35300400", "Indic_General_100_CI_AI_KS_WS");
                ls.Add("35D00400", "Indic_General_100_CI_AS");
                ls.Add("35500400", "Indic_General_100_CI_AS_WS");
                ls.Add("35900400", "Indic_General_100_CI_AS_KS");
                ls.Add("35100400", "Indic_General_100_CI_AS_KS_WS");
                ls.Add("35E00400", "Indic_General_100_CS_AI");
                ls.Add("35600400", "Indic_General_100_CS_AI_WS");
                ls.Add("35A00400", "Indic_General_100_CS_AI_KS");
                ls.Add("35200400", "Indic_General_100_CS_AI_KS_WS");
                ls.Add("35C00400", "Indic_General_100_CS_AS");
                ls.Add("35400400", "Indic_General_100_CS_AS_WS");
                ls.Add("35800400", "Indic_General_100_CS_AS_KS");
                ls.Add("35000400", "Indic_General_100_CS_AS_KS_WS");
                ls.Add("35F10400", "Indic_General_100_CI_AI_SC");
                ls.Add("35710400", "Indic_General_100_CI_AI_WS_SC");
                ls.Add("35B10400", "Indic_General_100_CI_AI_KS_SC");
                ls.Add("35310400", "Indic_General_100_CI_AI_KS_WS_SC");
                ls.Add("35D10400", "Indic_General_100_CI_AS_SC");
                ls.Add("35510400", "Indic_General_100_CI_AS_WS_SC");
                ls.Add("35910400", "Indic_General_100_CI_AS_KS_SC");
                ls.Add("35110400", "Indic_General_100_CI_AS_KS_WS_SC");
                ls.Add("35E10400", "Indic_General_100_CS_AI_SC");
                ls.Add("35610400", "Indic_General_100_CS_AI_WS_SC");
                ls.Add("35A10400", "Indic_General_100_CS_AI_KS_SC");
                ls.Add("35210400", "Indic_General_100_CS_AI_KS_WS_SC");
                ls.Add("35C10400", "Indic_General_100_CS_AS_SC");
                ls.Add("35410400", "Indic_General_100_CS_AS_WS_SC");
                ls.Add("35810400", "Indic_General_100_CS_AS_KS_SC");
                ls.Add("35010400", "Indic_General_100_CS_AS_KS_WS_SC");
                ls.Add("10000100", "Japanese_BIN");
                ls.Add("10080000", "Japanese_BIN2");
                ls.Add("10F00000", "Japanese_CI_AI");
                ls.Add("10700000", "Japanese_CI_AI_WS");
                ls.Add("10B00000", "Japanese_CI_AI_KS");
                ls.Add("10300000", "Japanese_CI_AI_KS_WS");
                ls.Add("10D00000", "Japanese_CI_AS");
                ls.Add("10500000", "Japanese_CI_AS_WS");
                ls.Add("10900000", "Japanese_CI_AS_KS");
                ls.Add("10100000", "Japanese_CI_AS_KS_WS");
                ls.Add("10E00000", "Japanese_CS_AI");
                ls.Add("10600000", "Japanese_CS_AI_WS");
                ls.Add("10A00000", "Japanese_CS_AI_KS");
                ls.Add("10200000", "Japanese_CS_AI_KS_WS");
                ls.Add("10C00000", "Japanese_CS_AS");
                ls.Add("10400000", "Japanese_CS_AS_WS");
                ls.Add("10800000", "Japanese_CS_AS_KS");
                ls.Add("10000000", "Japanese_CS_AS_KS_WS");
                ls.Add("30000300", "Japanese_90_BIN");
                ls.Add("30080200", "Japanese_90_BIN2");
                ls.Add("30F00200", "Japanese_90_CI_AI");
                ls.Add("30700200", "Japanese_90_CI_AI_WS");
                ls.Add("30B00200", "Japanese_90_CI_AI_KS");
                ls.Add("30300200", "Japanese_90_CI_AI_KS_WS");
                ls.Add("30D00200", "Japanese_90_CI_AS");
                ls.Add("30500200", "Japanese_90_CI_AS_WS");
                ls.Add("30900200", "Japanese_90_CI_AS_KS");
                ls.Add("30100200", "Japanese_90_CI_AS_KS_WS");
                ls.Add("30E00200", "Japanese_90_CS_AI");
                ls.Add("30600200", "Japanese_90_CS_AI_WS");
                ls.Add("30A00200", "Japanese_90_CS_AI_KS");
                ls.Add("30200200", "Japanese_90_CS_AI_KS_WS");
                ls.Add("30C00200", "Japanese_90_CS_AS");
                ls.Add("30400200", "Japanese_90_CS_AS_WS");
                ls.Add("30800200", "Japanese_90_CS_AS_KS");
                ls.Add("30000200", "Japanese_90_CS_AS_KS_WS");
                ls.Add("30F10200", "Japanese_90_CI_AI_SC");
                ls.Add("30710200", "Japanese_90_CI_AI_WS_SC");
                ls.Add("30B10200", "Japanese_90_CI_AI_KS_SC");
                ls.Add("30310200", "Japanese_90_CI_AI_KS_WS_SC");
                ls.Add("30D10200", "Japanese_90_CI_AS_SC");
                ls.Add("30510200", "Japanese_90_CI_AS_WS_SC");
                ls.Add("30910200", "Japanese_90_CI_AS_KS_SC");
                ls.Add("30110200", "Japanese_90_CI_AS_KS_WS_SC");
                ls.Add("30E10200", "Japanese_90_CS_AI_SC");
                ls.Add("30610200", "Japanese_90_CS_AI_WS_SC");
                ls.Add("30A10200", "Japanese_90_CS_AI_KS_SC");
                ls.Add("30210200", "Japanese_90_CS_AI_KS_WS_SC");
                ls.Add("30C10200", "Japanese_90_CS_AS_SC");
                ls.Add("30410200", "Japanese_90_CS_AS_WS_SC");
                ls.Add("30810200", "Japanese_90_CS_AS_KS_SC");
                ls.Add("30010200", "Japanese_90_CS_AS_KS_WS_SC");
                ls.Add("49000500", "Japanese_Bushu_Kakusu_100_BIN");
                ls.Add("49080400", "Japanese_Bushu_Kakusu_100_BIN2");
                ls.Add("49F00400", "Japanese_Bushu_Kakusu_100_CI_AI");
                ls.Add("49700400", "Japanese_Bushu_Kakusu_100_CI_AI_WS");
                ls.Add("49B00400", "Japanese_Bushu_Kakusu_100_CI_AI_KS");
                ls.Add("49300400", "Japanese_Bushu_Kakusu_100_CI_AI_KS_WS");
                ls.Add("49D00400", "Japanese_Bushu_Kakusu_100_CI_AS");
                ls.Add("49500400", "Japanese_Bushu_Kakusu_100_CI_AS_WS");
                ls.Add("49900400", "Japanese_Bushu_Kakusu_100_CI_AS_KS");
                ls.Add("49100400", "Japanese_Bushu_Kakusu_100_CI_AS_KS_WS");
                ls.Add("49E00400", "Japanese_Bushu_Kakusu_100_CS_AI");
                ls.Add("49600400", "Japanese_Bushu_Kakusu_100_CS_AI_WS");
                ls.Add("49A00400", "Japanese_Bushu_Kakusu_100_CS_AI_KS");
                ls.Add("49200400", "Japanese_Bushu_Kakusu_100_CS_AI_KS_WS");
                ls.Add("49C00400", "Japanese_Bushu_Kakusu_100_CS_AS");
                ls.Add("49400400", "Japanese_Bushu_Kakusu_100_CS_AS_WS");
                ls.Add("49800400", "Japanese_Bushu_Kakusu_100_CS_AS_KS");
                ls.Add("49000400", "Japanese_Bushu_Kakusu_100_CS_AS_KS_WS");
                ls.Add("49F10400", "Japanese_Bushu_Kakusu_100_CI_AI_SC");
                ls.Add("49710400", "Japanese_Bushu_Kakusu_100_CI_AI_WS_SC");
                ls.Add("49B10400", "Japanese_Bushu_Kakusu_100_CI_AI_KS_SC");
                ls.Add("49310400", "Japanese_Bushu_Kakusu_100_CI_AI_KS_WS_SC");
                ls.Add("49D10400", "Japanese_Bushu_Kakusu_100_CI_AS_SC");
                ls.Add("49510400", "Japanese_Bushu_Kakusu_100_CI_AS_WS_SC");
                ls.Add("49910400", "Japanese_Bushu_Kakusu_100_CI_AS_KS_SC");
                ls.Add("49110400", "Japanese_Bushu_Kakusu_100_CI_AS_KS_WS_SC");
                ls.Add("49E10400", "Japanese_Bushu_Kakusu_100_CS_AI_SC");
                ls.Add("49610400", "Japanese_Bushu_Kakusu_100_CS_AI_WS_SC");
                ls.Add("49A10400", "Japanese_Bushu_Kakusu_100_CS_AI_KS_SC");
                ls.Add("49210400", "Japanese_Bushu_Kakusu_100_CS_AI_KS_WS_SC");
                ls.Add("49C10400", "Japanese_Bushu_Kakusu_100_CS_AS_SC");
                ls.Add("49410400", "Japanese_Bushu_Kakusu_100_CS_AS_WS_SC");
                ls.Add("49810400", "Japanese_Bushu_Kakusu_100_CS_AS_KS_SC");
                ls.Add("49010400", "Japanese_Bushu_Kakusu_100_CS_AS_KS_WS_SC");
                ls.Add("49000700", "Japanese_Bushu_Kakusu_140_BIN");
                ls.Add("49080600", "Japanese_Bushu_Kakusu_140_BIN2");
                ls.Add("49F10600", "Japanese_Bushu_Kakusu_140_CI_AI_VSS");
                ls.Add("49710600", "Japanese_Bushu_Kakusu_140_CI_AI_WS_VSS");
                ls.Add("49B10600", "Japanese_Bushu_Kakusu_140_CI_AI_KS_VSS");
                ls.Add("49310600", "Japanese_Bushu_Kakusu_140_CI_AI_KS_WS_VSS");
                ls.Add("49D10600", "Japanese_Bushu_Kakusu_140_CI_AS_VSS");
                ls.Add("49510600", "Japanese_Bushu_Kakusu_140_CI_AS_WS_VSS");
                ls.Add("49910600", "Japanese_Bushu_Kakusu_140_CI_AS_KS_VSS");
                ls.Add("49110600", "Japanese_Bushu_Kakusu_140_CI_AS_KS_WS_VSS");
                ls.Add("49E10600", "Japanese_Bushu_Kakusu_140_CS_AI_VSS");
                ls.Add("49610600", "Japanese_Bushu_Kakusu_140_CS_AI_WS_VSS");
                ls.Add("49A10600", "Japanese_Bushu_Kakusu_140_CS_AI_KS_VSS");
                ls.Add("49210600", "Japanese_Bushu_Kakusu_140_CS_AI_KS_WS_VSS");
                ls.Add("49C10600", "Japanese_Bushu_Kakusu_140_CS_AS_VSS");
                ls.Add("49410600", "Japanese_Bushu_Kakusu_140_CS_AS_WS_VSS");
                ls.Add("49810600", "Japanese_Bushu_Kakusu_140_CS_AS_KS_VSS");
                ls.Add("49010600", "Japanese_Bushu_Kakusu_140_CS_AS_KS_WS_VSS");
                ls.Add("49F30600", "Japanese_Bushu_Kakusu_140_CI_AI");
                ls.Add("49730600", "Japanese_Bushu_Kakusu_140_CI_AI_WS");
                ls.Add("49B30600", "Japanese_Bushu_Kakusu_140_CI_AI_KS");
                ls.Add("49330600", "Japanese_Bushu_Kakusu_140_CI_AI_KS_WS");
                ls.Add("49D30600", "Japanese_Bushu_Kakusu_140_CI_AS");
                ls.Add("49530600", "Japanese_Bushu_Kakusu_140_CI_AS_WS");
                ls.Add("49930600", "Japanese_Bushu_Kakusu_140_CI_AS_KS");
                ls.Add("49130600", "Japanese_Bushu_Kakusu_140_CI_AS_KS_WS");
                ls.Add("49E30600", "Japanese_Bushu_Kakusu_140_CS_AI");
                ls.Add("49630600", "Japanese_Bushu_Kakusu_140_CS_AI_WS");
                ls.Add("49A30600", "Japanese_Bushu_Kakusu_140_CS_AI_KS");
                ls.Add("49230600", "Japanese_Bushu_Kakusu_140_CS_AI_KS_WS");
                ls.Add("49C30600", "Japanese_Bushu_Kakusu_140_CS_AS");
                ls.Add("49430600", "Japanese_Bushu_Kakusu_140_CS_AS_WS");
                ls.Add("49830600", "Japanese_Bushu_Kakusu_140_CS_AS_KS");
                ls.Add("49030600", "Japanese_Bushu_Kakusu_140_CS_AS_KS_WS");
                ls.Add("2B000100", "Japanese_Unicode_BIN");
                ls.Add("2B080000", "Japanese_Unicode_BIN2");
                ls.Add("2BF00000", "Japanese_Unicode_CI_AI");
                ls.Add("2B700000", "Japanese_Unicode_CI_AI_WS");
                ls.Add("2BB00000", "Japanese_Unicode_CI_AI_KS");
                ls.Add("2B300000", "Japanese_Unicode_CI_AI_KS_WS");
                ls.Add("2BD00000", "Japanese_Unicode_CI_AS");
                ls.Add("2B500000", "Japanese_Unicode_CI_AS_WS");
                ls.Add("2B900000", "Japanese_Unicode_CI_AS_KS");
                ls.Add("2B100000", "Japanese_Unicode_CI_AS_KS_WS");
                ls.Add("2BE00000", "Japanese_Unicode_CS_AI");
                ls.Add("2B600000", "Japanese_Unicode_CS_AI_WS");
                ls.Add("2BA00000", "Japanese_Unicode_CS_AI_KS");
                ls.Add("2B200000", "Japanese_Unicode_CS_AI_KS_WS");
                ls.Add("2BC00000", "Japanese_Unicode_CS_AS");
                ls.Add("2B400000", "Japanese_Unicode_CS_AS_WS");
                ls.Add("2B800000", "Japanese_Unicode_CS_AS_KS");
                ls.Add("2B000000", "Japanese_Unicode_CS_AS_KS_WS");
                ls.Add("48000500", "Japanese_XJIS_100_BIN");
                ls.Add("48080400", "Japanese_XJIS_100_BIN2");
                ls.Add("48F00400", "Japanese_XJIS_100_CI_AI");
                ls.Add("48700400", "Japanese_XJIS_100_CI_AI_WS");
                ls.Add("48B00400", "Japanese_XJIS_100_CI_AI_KS");
                ls.Add("48300400", "Japanese_XJIS_100_CI_AI_KS_WS");
                ls.Add("48D00400", "Japanese_XJIS_100_CI_AS");
                ls.Add("48500400", "Japanese_XJIS_100_CI_AS_WS");
                ls.Add("48900400", "Japanese_XJIS_100_CI_AS_KS");
                ls.Add("48100400", "Japanese_XJIS_100_CI_AS_KS_WS");
                ls.Add("48E00400", "Japanese_XJIS_100_CS_AI");
                ls.Add("48600400", "Japanese_XJIS_100_CS_AI_WS");
                ls.Add("48A00400", "Japanese_XJIS_100_CS_AI_KS");
                ls.Add("48200400", "Japanese_XJIS_100_CS_AI_KS_WS");
                ls.Add("48C00400", "Japanese_XJIS_100_CS_AS");
                ls.Add("48400400", "Japanese_XJIS_100_CS_AS_WS");
                ls.Add("48800400", "Japanese_XJIS_100_CS_AS_KS");
                ls.Add("48000400", "Japanese_XJIS_100_CS_AS_KS_WS");
                ls.Add("48F10400", "Japanese_XJIS_100_CI_AI_SC");
                ls.Add("48710400", "Japanese_XJIS_100_CI_AI_WS_SC");
                ls.Add("48B10400", "Japanese_XJIS_100_CI_AI_KS_SC");
                ls.Add("48310400", "Japanese_XJIS_100_CI_AI_KS_WS_SC");
                ls.Add("48D10400", "Japanese_XJIS_100_CI_AS_SC");
                ls.Add("48510400", "Japanese_XJIS_100_CI_AS_WS_SC");
                ls.Add("48910400", "Japanese_XJIS_100_CI_AS_KS_SC");
                ls.Add("48110400", "Japanese_XJIS_100_CI_AS_KS_WS_SC");
                ls.Add("48E10400", "Japanese_XJIS_100_CS_AI_SC");
                ls.Add("48610400", "Japanese_XJIS_100_CS_AI_WS_SC");
                ls.Add("48A10400", "Japanese_XJIS_100_CS_AI_KS_SC");
                ls.Add("48210400", "Japanese_XJIS_100_CS_AI_KS_WS_SC");
                ls.Add("48C10400", "Japanese_XJIS_100_CS_AS_SC");
                ls.Add("48410400", "Japanese_XJIS_100_CS_AS_WS_SC");
                ls.Add("48810400", "Japanese_XJIS_100_CS_AS_KS_SC");
                ls.Add("48010400", "Japanese_XJIS_100_CS_AS_KS_WS_SC");
                ls.Add("48000700", "Japanese_XJIS_140_BIN");
                ls.Add("48080600", "Japanese_XJIS_140_BIN2");
                ls.Add("48F10600", "Japanese_XJIS_140_CI_AI_VSS");
                ls.Add("48710600", "Japanese_XJIS_140_CI_AI_WS_VSS");
                ls.Add("48B10600", "Japanese_XJIS_140_CI_AI_KS_VSS");
                ls.Add("48310600", "Japanese_XJIS_140_CI_AI_KS_WS_VSS");
                ls.Add("48D10600", "Japanese_XJIS_140_CI_AS_VSS");
                ls.Add("48510600", "Japanese_XJIS_140_CI_AS_WS_VSS");
                ls.Add("48910600", "Japanese_XJIS_140_CI_AS_KS_VSS");
                ls.Add("48110600", "Japanese_XJIS_140_CI_AS_KS_WS_VSS");
                ls.Add("48E10600", "Japanese_XJIS_140_CS_AI_VSS");
                ls.Add("48610600", "Japanese_XJIS_140_CS_AI_WS_VSS");
                ls.Add("48A10600", "Japanese_XJIS_140_CS_AI_KS_VSS");
                ls.Add("48210600", "Japanese_XJIS_140_CS_AI_KS_WS_VSS");
                ls.Add("48C10600", "Japanese_XJIS_140_CS_AS_VSS");
                ls.Add("48410600", "Japanese_XJIS_140_CS_AS_WS_VSS");
                ls.Add("48810600", "Japanese_XJIS_140_CS_AS_KS_VSS");
                ls.Add("48010600", "Japanese_XJIS_140_CS_AS_KS_WS_VSS");
                ls.Add("48F30600", "Japanese_XJIS_140_CI_AI");
                ls.Add("48730600", "Japanese_XJIS_140_CI_AI_WS");
                ls.Add("48B30600", "Japanese_XJIS_140_CI_AI_KS");
                ls.Add("48330600", "Japanese_XJIS_140_CI_AI_KS_WS");
                ls.Add("48D30600", "Japanese_XJIS_140_CI_AS");
                ls.Add("48530600", "Japanese_XJIS_140_CI_AS_WS");
                ls.Add("48930600", "Japanese_XJIS_140_CI_AS_KS");
                ls.Add("48130600", "Japanese_XJIS_140_CI_AS_KS_WS");
                ls.Add("48E30600", "Japanese_XJIS_140_CS_AI");
                ls.Add("48630600", "Japanese_XJIS_140_CS_AI_WS");
                ls.Add("48A30600", "Japanese_XJIS_140_CS_AI_KS");
                ls.Add("48230600", "Japanese_XJIS_140_CS_AI_KS_WS");
                ls.Add("48C30600", "Japanese_XJIS_140_CS_AS");
                ls.Add("48430600", "Japanese_XJIS_140_CS_AS_WS");
                ls.Add("48830600", "Japanese_XJIS_140_CS_AS_KS");
                ls.Add("48030600", "Japanese_XJIS_140_CS_AS_KS_WS");
                ls.Add("37000300", "Kazakh_90_BIN");
                ls.Add("37080200", "Kazakh_90_BIN2");
                ls.Add("37F00200", "Kazakh_90_CI_AI");
                ls.Add("37700200", "Kazakh_90_CI_AI_WS");
                ls.Add("37B00200", "Kazakh_90_CI_AI_KS");
                ls.Add("37300200", "Kazakh_90_CI_AI_KS_WS");
                ls.Add("37D00200", "Kazakh_90_CI_AS");
                ls.Add("37500200", "Kazakh_90_CI_AS_WS");
                ls.Add("37900200", "Kazakh_90_CI_AS_KS");
                ls.Add("37100200", "Kazakh_90_CI_AS_KS_WS");
                ls.Add("37E00200", "Kazakh_90_CS_AI");
                ls.Add("37600200", "Kazakh_90_CS_AI_WS");
                ls.Add("37A00200", "Kazakh_90_CS_AI_KS");
                ls.Add("37200200", "Kazakh_90_CS_AI_KS_WS");
                ls.Add("37C00200", "Kazakh_90_CS_AS");
                ls.Add("37400200", "Kazakh_90_CS_AS_WS");
                ls.Add("37800200", "Kazakh_90_CS_AS_KS");
                ls.Add("37000200", "Kazakh_90_CS_AS_KS_WS");
                ls.Add("37F10200", "Kazakh_90_CI_AI_SC");
                ls.Add("37710200", "Kazakh_90_CI_AI_WS_SC");
                ls.Add("37B10200", "Kazakh_90_CI_AI_KS_SC");
                ls.Add("37310200", "Kazakh_90_CI_AI_KS_WS_SC");
                ls.Add("37D10200", "Kazakh_90_CI_AS_SC");
                ls.Add("37510200", "Kazakh_90_CI_AS_WS_SC");
                ls.Add("37910200", "Kazakh_90_CI_AS_KS_SC");
                ls.Add("37110200", "Kazakh_90_CI_AS_KS_WS_SC");
                ls.Add("37E10200", "Kazakh_90_CS_AI_SC");
                ls.Add("37610200", "Kazakh_90_CS_AI_WS_SC");
                ls.Add("37A10200", "Kazakh_90_CS_AI_KS_SC");
                ls.Add("37210200", "Kazakh_90_CS_AI_KS_WS_SC");
                ls.Add("37C10200", "Kazakh_90_CS_AS_SC");
                ls.Add("37410200", "Kazakh_90_CS_AS_WS_SC");
                ls.Add("37810200", "Kazakh_90_CS_AS_KS_SC");
                ls.Add("37010200", "Kazakh_90_CS_AS_KS_WS_SC");
                ls.Add("37000500", "Kazakh_100_BIN");
                ls.Add("37080400", "Kazakh_100_BIN2");
                ls.Add("37F00400", "Kazakh_100_CI_AI");
                ls.Add("37700400", "Kazakh_100_CI_AI_WS");
                ls.Add("37B00400", "Kazakh_100_CI_AI_KS");
                ls.Add("37300400", "Kazakh_100_CI_AI_KS_WS");
                ls.Add("37D00400", "Kazakh_100_CI_AS");
                ls.Add("37500400", "Kazakh_100_CI_AS_WS");
                ls.Add("37900400", "Kazakh_100_CI_AS_KS");
                ls.Add("37100400", "Kazakh_100_CI_AS_KS_WS");
                ls.Add("37E00400", "Kazakh_100_CS_AI");
                ls.Add("37600400", "Kazakh_100_CS_AI_WS");
                ls.Add("37A00400", "Kazakh_100_CS_AI_KS");
                ls.Add("37200400", "Kazakh_100_CS_AI_KS_WS");
                ls.Add("37C00400", "Kazakh_100_CS_AS");
                ls.Add("37400400", "Kazakh_100_CS_AS_WS");
                ls.Add("37800400", "Kazakh_100_CS_AS_KS");
                ls.Add("37000400", "Kazakh_100_CS_AS_KS_WS");
                ls.Add("37F10400", "Kazakh_100_CI_AI_SC");
                ls.Add("37710400", "Kazakh_100_CI_AI_WS_SC");
                ls.Add("37B10400", "Kazakh_100_CI_AI_KS_SC");
                ls.Add("37310400", "Kazakh_100_CI_AI_KS_WS_SC");
                ls.Add("37D10400", "Kazakh_100_CI_AS_SC");
                ls.Add("37510400", "Kazakh_100_CI_AS_WS_SC");
                ls.Add("37910400", "Kazakh_100_CI_AS_KS_SC");
                ls.Add("37110400", "Kazakh_100_CI_AS_KS_WS_SC");
                ls.Add("37E10400", "Kazakh_100_CS_AI_SC");
                ls.Add("37610400", "Kazakh_100_CS_AI_WS_SC");
                ls.Add("37A10400", "Kazakh_100_CS_AI_KS_SC");
                ls.Add("37210400", "Kazakh_100_CS_AI_KS_WS_SC");
                ls.Add("37C10400", "Kazakh_100_CS_AS_SC");
                ls.Add("37410400", "Kazakh_100_CS_AS_WS_SC");
                ls.Add("37810400", "Kazakh_100_CS_AS_KS_SC");
                ls.Add("37010400", "Kazakh_100_CS_AS_KS_WS_SC");
                ls.Add("5E000500", "Khmer_100_BIN");
                ls.Add("5E080400", "Khmer_100_BIN2");
                ls.Add("5EF00400", "Khmer_100_CI_AI");
                ls.Add("5E700400", "Khmer_100_CI_AI_WS");
                ls.Add("5EB00400", "Khmer_100_CI_AI_KS");
                ls.Add("5E300400", "Khmer_100_CI_AI_KS_WS");
                ls.Add("5ED00400", "Khmer_100_CI_AS");
                ls.Add("5E500400", "Khmer_100_CI_AS_WS");
                ls.Add("5E900400", "Khmer_100_CI_AS_KS");
                ls.Add("5E100400", "Khmer_100_CI_AS_KS_WS");
                ls.Add("5EE00400", "Khmer_100_CS_AI");
                ls.Add("5E600400", "Khmer_100_CS_AI_WS");
                ls.Add("5EA00400", "Khmer_100_CS_AI_KS");
                ls.Add("5E200400", "Khmer_100_CS_AI_KS_WS");
                ls.Add("5EC00400", "Khmer_100_CS_AS");
                ls.Add("5E400400", "Khmer_100_CS_AS_WS");
                ls.Add("5E800400", "Khmer_100_CS_AS_KS");
                ls.Add("5E000400", "Khmer_100_CS_AS_KS_WS");
                ls.Add("5EF10400", "Khmer_100_CI_AI_SC");
                ls.Add("5E710400", "Khmer_100_CI_AI_WS_SC");
                ls.Add("5EB10400", "Khmer_100_CI_AI_KS_SC");
                ls.Add("5E310400", "Khmer_100_CI_AI_KS_WS_SC");
                ls.Add("5ED10400", "Khmer_100_CI_AS_SC");
                ls.Add("5E510400", "Khmer_100_CI_AS_WS_SC");
                ls.Add("5E910400", "Khmer_100_CI_AS_KS_SC");
                ls.Add("5E110400", "Khmer_100_CI_AS_KS_WS_SC");
                ls.Add("5EE10400", "Khmer_100_CS_AI_SC");
                ls.Add("5E610400", "Khmer_100_CS_AI_WS_SC");
                ls.Add("5EA10400", "Khmer_100_CS_AI_KS_SC");
                ls.Add("5E210400", "Khmer_100_CS_AI_KS_WS_SC");
                ls.Add("5EC10400", "Khmer_100_CS_AS_SC");
                ls.Add("5E410400", "Khmer_100_CS_AS_WS_SC");
                ls.Add("5E810400", "Khmer_100_CS_AS_KS_SC");
                ls.Add("5E010400", "Khmer_100_CS_AS_KS_WS_SC");
                ls.Add("40000300", "Korean_90_BIN");
                ls.Add("40080200", "Korean_90_BIN2");
                ls.Add("40F00200", "Korean_90_CI_AI");
                ls.Add("40700200", "Korean_90_CI_AI_WS");
                ls.Add("40B00200", "Korean_90_CI_AI_KS");
                ls.Add("40300200", "Korean_90_CI_AI_KS_WS");
                ls.Add("40D00200", "Korean_90_CI_AS");
                ls.Add("40500200", "Korean_90_CI_AS_WS");
                ls.Add("40900200", "Korean_90_CI_AS_KS");
                ls.Add("40100200", "Korean_90_CI_AS_KS_WS");
                ls.Add("40E00200", "Korean_90_CS_AI");
                ls.Add("40600200", "Korean_90_CS_AI_WS");
                ls.Add("40A00200", "Korean_90_CS_AI_KS");
                ls.Add("40200200", "Korean_90_CS_AI_KS_WS");
                ls.Add("40C00200", "Korean_90_CS_AS");
                ls.Add("40400200", "Korean_90_CS_AS_WS");
                ls.Add("40800200", "Korean_90_CS_AS_KS");
                ls.Add("40000200", "Korean_90_CS_AS_KS_WS");
                ls.Add("40F10200", "Korean_90_CI_AI_SC");
                ls.Add("40710200", "Korean_90_CI_AI_WS_SC");
                ls.Add("40B10200", "Korean_90_CI_AI_KS_SC");
                ls.Add("40310200", "Korean_90_CI_AI_KS_WS_SC");
                ls.Add("40D10200", "Korean_90_CI_AS_SC");
                ls.Add("40510200", "Korean_90_CI_AS_WS_SC");
                ls.Add("40910200", "Korean_90_CI_AS_KS_SC");
                ls.Add("40110200", "Korean_90_CI_AS_KS_WS_SC");
                ls.Add("40E10200", "Korean_90_CS_AI_SC");
                ls.Add("40610200", "Korean_90_CS_AI_WS_SC");
                ls.Add("40A10200", "Korean_90_CS_AI_KS_SC");
                ls.Add("40210200", "Korean_90_CS_AI_KS_WS_SC");
                ls.Add("40C10200", "Korean_90_CS_AS_SC");
                ls.Add("40410200", "Korean_90_CS_AS_WS_SC");
                ls.Add("40810200", "Korean_90_CS_AS_KS_SC");
                ls.Add("40010200", "Korean_90_CS_AS_KS_WS_SC");
                ls.Add("40000500", "Korean_100_BIN");
                ls.Add("40080400", "Korean_100_BIN2");
                ls.Add("40F00400", "Korean_100_CI_AI");
                ls.Add("40700400", "Korean_100_CI_AI_WS");
                ls.Add("40B00400", "Korean_100_CI_AI_KS");
                ls.Add("40300400", "Korean_100_CI_AI_KS_WS");
                ls.Add("40D00400", "Korean_100_CI_AS");
                ls.Add("40500400", "Korean_100_CI_AS_WS");
                ls.Add("40900400", "Korean_100_CI_AS_KS");
                ls.Add("40100400", "Korean_100_CI_AS_KS_WS");
                ls.Add("40E00400", "Korean_100_CS_AI");
                ls.Add("40600400", "Korean_100_CS_AI_WS");
                ls.Add("40A00400", "Korean_100_CS_AI_KS");
                ls.Add("40200400", "Korean_100_CS_AI_KS_WS");
                ls.Add("40C00400", "Korean_100_CS_AS");
                ls.Add("40400400", "Korean_100_CS_AS_WS");
                ls.Add("40800400", "Korean_100_CS_AS_KS");
                ls.Add("40000400", "Korean_100_CS_AS_KS_WS");
                ls.Add("40F10400", "Korean_100_CI_AI_SC");
                ls.Add("40710400", "Korean_100_CI_AI_WS_SC");
                ls.Add("40B10400", "Korean_100_CI_AI_KS_SC");
                ls.Add("40310400", "Korean_100_CI_AI_KS_WS_SC");
                ls.Add("40D10400", "Korean_100_CI_AS_SC");
                ls.Add("40510400", "Korean_100_CI_AS_WS_SC");
                ls.Add("40910400", "Korean_100_CI_AS_KS_SC");
                ls.Add("40110400", "Korean_100_CI_AS_KS_WS_SC");
                ls.Add("40E10400", "Korean_100_CS_AI_SC");
                ls.Add("40610400", "Korean_100_CS_AI_WS_SC");
                ls.Add("40A10400", "Korean_100_CS_AI_KS_SC");
                ls.Add("40210400", "Korean_100_CS_AI_KS_WS_SC");
                ls.Add("40C10400", "Korean_100_CS_AS_SC");
                ls.Add("40410400", "Korean_100_CS_AS_WS_SC");
                ls.Add("40810400", "Korean_100_CS_AS_KS_SC");
                ls.Add("40010400", "Korean_100_CS_AS_KS_WS_SC");
                ls.Add("11000100", "Korean_Wansung_BIN");
                ls.Add("11080000", "Korean_Wansung_BIN2");
                ls.Add("11F00000", "Korean_Wansung_CI_AI");
                ls.Add("11700000", "Korean_Wansung_CI_AI_WS");
                ls.Add("11B00000", "Korean_Wansung_CI_AI_KS");
                ls.Add("11300000", "Korean_Wansung_CI_AI_KS_WS");
                ls.Add("11D00000", "Korean_Wansung_CI_AS");
                ls.Add("11500000", "Korean_Wansung_CI_AS_WS");
                ls.Add("11900000", "Korean_Wansung_CI_AS_KS");
                ls.Add("11100000", "Korean_Wansung_CI_AS_KS_WS");
                ls.Add("11E00000", "Korean_Wansung_CS_AI");
                ls.Add("11600000", "Korean_Wansung_CS_AI_WS");
                ls.Add("11A00000", "Korean_Wansung_CS_AI_KS");
                ls.Add("11200000", "Korean_Wansung_CS_AI_KS_WS");
                ls.Add("11C00000", "Korean_Wansung_CS_AS");
                ls.Add("11400000", "Korean_Wansung_CS_AS_WS");
                ls.Add("11800000", "Korean_Wansung_CS_AS_KS");
                ls.Add("11000000", "Korean_Wansung_CS_AS_KS_WS");
                ls.Add("5F000500", "Lao_100_BIN");
                ls.Add("5F080400", "Lao_100_BIN2");
                ls.Add("5FF00400", "Lao_100_CI_AI");
                ls.Add("5F700400", "Lao_100_CI_AI_WS");
                ls.Add("5FB00400", "Lao_100_CI_AI_KS");
                ls.Add("5F300400", "Lao_100_CI_AI_KS_WS");
                ls.Add("5FD00400", "Lao_100_CI_AS");
                ls.Add("5F500400", "Lao_100_CI_AS_WS");
                ls.Add("5F900400", "Lao_100_CI_AS_KS");
                ls.Add("5F100400", "Lao_100_CI_AS_KS_WS");
                ls.Add("5FE00400", "Lao_100_CS_AI");
                ls.Add("5F600400", "Lao_100_CS_AI_WS");
                ls.Add("5FA00400", "Lao_100_CS_AI_KS");
                ls.Add("5F200400", "Lao_100_CS_AI_KS_WS");
                ls.Add("5FC00400", "Lao_100_CS_AS");
                ls.Add("5F400400", "Lao_100_CS_AS_WS");
                ls.Add("5F800400", "Lao_100_CS_AS_KS");
                ls.Add("5F000400", "Lao_100_CS_AS_KS_WS");
                ls.Add("5FF10400", "Lao_100_CI_AI_SC");
                ls.Add("5F710400", "Lao_100_CI_AI_WS_SC");
                ls.Add("5FB10400", "Lao_100_CI_AI_KS_SC");
                ls.Add("5F310400", "Lao_100_CI_AI_KS_WS_SC");
                ls.Add("5FD10400", "Lao_100_CI_AS_SC");
                ls.Add("5F510400", "Lao_100_CI_AS_WS_SC");
                ls.Add("5F910400", "Lao_100_CI_AS_KS_SC");
                ls.Add("5F110400", "Lao_100_CI_AS_KS_WS_SC");
                ls.Add("5FE10400", "Lao_100_CS_AI_SC");
                ls.Add("5F610400", "Lao_100_CS_AI_WS_SC");
                ls.Add("5FA10400", "Lao_100_CS_AI_KS_SC");
                ls.Add("5F210400", "Lao_100_CS_AI_KS_WS_SC");
                ls.Add("5FC10400", "Lao_100_CS_AS_SC");
                ls.Add("5F410400", "Lao_100_CS_AS_WS_SC");
                ls.Add("5F810400", "Lao_100_CS_AS_KS_SC");
                ls.Add("5F010400", "Lao_100_CS_AS_KS_WS_SC");
                ls.Add("08000100", "Latin1_General_BIN");
                ls.Add("08080000", "Latin1_General_BIN2");
                ls.Add("08F00000", "Latin1_General_CI_AI");
                ls.Add("08700000", "Latin1_General_CI_AI_WS");
                ls.Add("08B00000", "Latin1_General_CI_AI_KS");
                ls.Add("08300000", "Latin1_General_CI_AI_KS_WS");
                ls.Add("08D00000", "Latin1_General_CI_AS");
                ls.Add("08500000", "Latin1_General_CI_AS_WS");
                ls.Add("08900000", "Latin1_General_CI_AS_KS");
                ls.Add("08100000", "Latin1_General_CI_AS_KS_WS");
                ls.Add("08E00000", "Latin1_General_CS_AI");
                ls.Add("08600000", "Latin1_General_CS_AI_WS");
                ls.Add("08A00000", "Latin1_General_CS_AI_KS");
                ls.Add("08200000", "Latin1_General_CS_AI_KS_WS");
                ls.Add("08C00000", "Latin1_General_CS_AS");
                ls.Add("08400000", "Latin1_General_CS_AS_WS");
                ls.Add("08800000", "Latin1_General_CS_AS_KS");
                ls.Add("08000000", "Latin1_General_CS_AS_KS_WS");
                ls.Add("08000500", "Latin1_General_100_BIN");
                ls.Add("08080400", "Latin1_General_100_BIN2");
                ls.Add("08F00400", "Latin1_General_100_CI_AI");
                ls.Add("08700400", "Latin1_General_100_CI_AI_WS");
                ls.Add("08B00400", "Latin1_General_100_CI_AI_KS");
                ls.Add("08300400", "Latin1_General_100_CI_AI_KS_WS");
                ls.Add("08D00400", "Latin1_General_100_CI_AS");
                ls.Add("08500400", "Latin1_General_100_CI_AS_WS");
                ls.Add("08900400", "Latin1_General_100_CI_AS_KS");
                ls.Add("08100400", "Latin1_General_100_CI_AS_KS_WS");
                ls.Add("08E00400", "Latin1_General_100_CS_AI");
                ls.Add("08600400", "Latin1_General_100_CS_AI_WS");
                ls.Add("08A00400", "Latin1_General_100_CS_AI_KS");
                ls.Add("08200400", "Latin1_General_100_CS_AI_KS_WS");
                ls.Add("08C00400", "Latin1_General_100_CS_AS");
                ls.Add("08400400", "Latin1_General_100_CS_AS_WS");
                ls.Add("08800400", "Latin1_General_100_CS_AS_KS");
                ls.Add("08000400", "Latin1_General_100_CS_AS_KS_WS");
                ls.Add("08F10400", "Latin1_General_100_CI_AI_SC");
                ls.Add("08710400", "Latin1_General_100_CI_AI_WS_SC");
                ls.Add("08B10400", "Latin1_General_100_CI_AI_KS_SC");
                ls.Add("08310400", "Latin1_General_100_CI_AI_KS_WS_SC");
                ls.Add("08D10400", "Latin1_General_100_CI_AS_SC");
                ls.Add("08510400", "Latin1_General_100_CI_AS_WS_SC");
                ls.Add("08910400", "Latin1_General_100_CI_AS_KS_SC");
                ls.Add("08110400", "Latin1_General_100_CI_AS_KS_WS_SC");
                ls.Add("08E10400", "Latin1_General_100_CS_AI_SC");
                ls.Add("08610400", "Latin1_General_100_CS_AI_WS_SC");
                ls.Add("08A10400", "Latin1_General_100_CS_AI_KS_SC");
                ls.Add("08210400", "Latin1_General_100_CS_AI_KS_WS_SC");
                ls.Add("08C10400", "Latin1_General_100_CS_AS_SC");
                ls.Add("08410400", "Latin1_General_100_CS_AS_WS_SC");
                ls.Add("08810400", "Latin1_General_100_CS_AS_KS_SC");
                ls.Add("08010400", "Latin1_General_100_CS_AS_KS_WS_SC");
                ls.Add("1E000100", "Latvian_BIN");
                ls.Add("1E080000", "Latvian_BIN2");
                ls.Add("1EF00000", "Latvian_CI_AI");
                ls.Add("1E700000", "Latvian_CI_AI_WS");
                ls.Add("1EB00000", "Latvian_CI_AI_KS");
                ls.Add("1E300000", "Latvian_CI_AI_KS_WS");
                ls.Add("1ED00000", "Latvian_CI_AS");
                ls.Add("1E500000", "Latvian_CI_AS_WS");
                ls.Add("1E900000", "Latvian_CI_AS_KS");
                ls.Add("1E100000", "Latvian_CI_AS_KS_WS");
                ls.Add("1EE00000", "Latvian_CS_AI");
                ls.Add("1E600000", "Latvian_CS_AI_WS");
                ls.Add("1EA00000", "Latvian_CS_AI_KS");
                ls.Add("1E200000", "Latvian_CS_AI_KS_WS");
                ls.Add("1EC00000", "Latvian_CS_AS");
                ls.Add("1E400000", "Latvian_CS_AS_WS");
                ls.Add("1E800000", "Latvian_CS_AS_KS");
                ls.Add("1E000000", "Latvian_CS_AS_KS_WS");
                ls.Add("1E000500", "Latvian_100_BIN");
                ls.Add("1E080400", "Latvian_100_BIN2");
                ls.Add("1EF00400", "Latvian_100_CI_AI");
                ls.Add("1E700400", "Latvian_100_CI_AI_WS");
                ls.Add("1EB00400", "Latvian_100_CI_AI_KS");
                ls.Add("1E300400", "Latvian_100_CI_AI_KS_WS");
                ls.Add("1ED00400", "Latvian_100_CI_AS");
                ls.Add("1E500400", "Latvian_100_CI_AS_WS");
                ls.Add("1E900400", "Latvian_100_CI_AS_KS");
                ls.Add("1E100400", "Latvian_100_CI_AS_KS_WS");
                ls.Add("1EE00400", "Latvian_100_CS_AI");
                ls.Add("1E600400", "Latvian_100_CS_AI_WS");
                ls.Add("1EA00400", "Latvian_100_CS_AI_KS");
                ls.Add("1E200400", "Latvian_100_CS_AI_KS_WS");
                ls.Add("1EC00400", "Latvian_100_CS_AS");
                ls.Add("1E400400", "Latvian_100_CS_AS_WS");
                ls.Add("1E800400", "Latvian_100_CS_AS_KS");
                ls.Add("1E000400", "Latvian_100_CS_AS_KS_WS");
                ls.Add("1EF10400", "Latvian_100_CI_AI_SC");
                ls.Add("1E710400", "Latvian_100_CI_AI_WS_SC");
                ls.Add("1EB10400", "Latvian_100_CI_AI_KS_SC");
                ls.Add("1E310400", "Latvian_100_CI_AI_KS_WS_SC");
                ls.Add("1ED10400", "Latvian_100_CI_AS_SC");
                ls.Add("1E510400", "Latvian_100_CI_AS_WS_SC");
                ls.Add("1E910400", "Latvian_100_CI_AS_KS_SC");
                ls.Add("1E110400", "Latvian_100_CI_AS_KS_WS_SC");
                ls.Add("1EE10400", "Latvian_100_CS_AI_SC");
                ls.Add("1E610400", "Latvian_100_CS_AI_WS_SC");
                ls.Add("1EA10400", "Latvian_100_CS_AI_KS_SC");
                ls.Add("1E210400", "Latvian_100_CS_AI_KS_WS_SC");
                ls.Add("1EC10400", "Latvian_100_CS_AS_SC");
                ls.Add("1E410400", "Latvian_100_CS_AS_WS_SC");
                ls.Add("1E810400", "Latvian_100_CS_AS_KS_SC");
                ls.Add("1E010400", "Latvian_100_CS_AS_KS_WS_SC");
                ls.Add("1F000100", "Lithuanian_BIN");
                ls.Add("1F080000", "Lithuanian_BIN2");
                ls.Add("1FF00000", "Lithuanian_CI_AI");
                ls.Add("1F700000", "Lithuanian_CI_AI_WS");
                ls.Add("1FB00000", "Lithuanian_CI_AI_KS");
                ls.Add("1F300000", "Lithuanian_CI_AI_KS_WS");
                ls.Add("1FD00000", "Lithuanian_CI_AS");
                ls.Add("1F500000", "Lithuanian_CI_AS_WS");
                ls.Add("1F900000", "Lithuanian_CI_AS_KS");
                ls.Add("1F100000", "Lithuanian_CI_AS_KS_WS");
                ls.Add("1FE00000", "Lithuanian_CS_AI");
                ls.Add("1F600000", "Lithuanian_CS_AI_WS");
                ls.Add("1FA00000", "Lithuanian_CS_AI_KS");
                ls.Add("1F200000", "Lithuanian_CS_AI_KS_WS");
                ls.Add("1FC00000", "Lithuanian_CS_AS");
                ls.Add("1F400000", "Lithuanian_CS_AS_WS");
                ls.Add("1F800000", "Lithuanian_CS_AS_KS");
                ls.Add("1F000000", "Lithuanian_CS_AS_KS_WS");
                ls.Add("1F000500", "Lithuanian_100_BIN");
                ls.Add("1F080400", "Lithuanian_100_BIN2");
                ls.Add("1FF00400", "Lithuanian_100_CI_AI");
                ls.Add("1F700400", "Lithuanian_100_CI_AI_WS");
                ls.Add("1FB00400", "Lithuanian_100_CI_AI_KS");
                ls.Add("1F300400", "Lithuanian_100_CI_AI_KS_WS");
                ls.Add("1FD00400", "Lithuanian_100_CI_AS");
                ls.Add("1F500400", "Lithuanian_100_CI_AS_WS");
                ls.Add("1F900400", "Lithuanian_100_CI_AS_KS");
                ls.Add("1F100400", "Lithuanian_100_CI_AS_KS_WS");
                ls.Add("1FE00400", "Lithuanian_100_CS_AI");
                ls.Add("1F600400", "Lithuanian_100_CS_AI_WS");
                ls.Add("1FA00400", "Lithuanian_100_CS_AI_KS");
                ls.Add("1F200400", "Lithuanian_100_CS_AI_KS_WS");
                ls.Add("1FC00400", "Lithuanian_100_CS_AS");
                ls.Add("1F400400", "Lithuanian_100_CS_AS_WS");
                ls.Add("1F800400", "Lithuanian_100_CS_AS_KS");
                ls.Add("1F000400", "Lithuanian_100_CS_AS_KS_WS");
                ls.Add("1FF10400", "Lithuanian_100_CI_AI_SC");
                ls.Add("1F710400", "Lithuanian_100_CI_AI_WS_SC");
                ls.Add("1FB10400", "Lithuanian_100_CI_AI_KS_SC");
                ls.Add("1F310400", "Lithuanian_100_CI_AI_KS_WS_SC");
                ls.Add("1FD10400", "Lithuanian_100_CI_AS_SC");
                ls.Add("1F510400", "Lithuanian_100_CI_AS_WS_SC");
                ls.Add("1F910400", "Lithuanian_100_CI_AS_KS_SC");
                ls.Add("1F110400", "Lithuanian_100_CI_AS_KS_WS_SC");
                ls.Add("1FE10400", "Lithuanian_100_CS_AI_SC");
                ls.Add("1F610400", "Lithuanian_100_CS_AI_WS_SC");
                ls.Add("1FA10400", "Lithuanian_100_CS_AI_KS_SC");
                ls.Add("1F210400", "Lithuanian_100_CS_AI_KS_WS_SC");
                ls.Add("1FC10400", "Lithuanian_100_CS_AS_SC");
                ls.Add("1F410400", "Lithuanian_100_CS_AS_WS_SC");
                ls.Add("1F810400", "Lithuanian_100_CS_AS_KS_SC");
                ls.Add("1F010400", "Lithuanian_100_CS_AS_KS_WS_SC");
                ls.Add("3A000300", "Macedonian_FYROM_90_BIN");
                ls.Add("3A080200", "Macedonian_FYROM_90_BIN2");
                ls.Add("3AF00200", "Macedonian_FYROM_90_CI_AI");
                ls.Add("3A700200", "Macedonian_FYROM_90_CI_AI_WS");
                ls.Add("3AB00200", "Macedonian_FYROM_90_CI_AI_KS");
                ls.Add("3A300200", "Macedonian_FYROM_90_CI_AI_KS_WS");
                ls.Add("3AD00200", "Macedonian_FYROM_90_CI_AS");
                ls.Add("3A500200", "Macedonian_FYROM_90_CI_AS_WS");
                ls.Add("3A900200", "Macedonian_FYROM_90_CI_AS_KS");
                ls.Add("3A100200", "Macedonian_FYROM_90_CI_AS_KS_WS");
                ls.Add("3AE00200", "Macedonian_FYROM_90_CS_AI");
                ls.Add("3A600200", "Macedonian_FYROM_90_CS_AI_WS");
                ls.Add("3AA00200", "Macedonian_FYROM_90_CS_AI_KS");
                ls.Add("3A200200", "Macedonian_FYROM_90_CS_AI_KS_WS");
                ls.Add("3AC00200", "Macedonian_FYROM_90_CS_AS");
                ls.Add("3A400200", "Macedonian_FYROM_90_CS_AS_WS");
                ls.Add("3A800200", "Macedonian_FYROM_90_CS_AS_KS");
                ls.Add("3A000200", "Macedonian_FYROM_90_CS_AS_KS_WS");
                ls.Add("3AF10200", "Macedonian_FYROM_90_CI_AI_SC");
                ls.Add("3A710200", "Macedonian_FYROM_90_CI_AI_WS_SC");
                ls.Add("3AB10200", "Macedonian_FYROM_90_CI_AI_KS_SC");
                ls.Add("3A310200", "Macedonian_FYROM_90_CI_AI_KS_WS_SC");
                ls.Add("3AD10200", "Macedonian_FYROM_90_CI_AS_SC");
                ls.Add("3A510200", "Macedonian_FYROM_90_CI_AS_WS_SC");
                ls.Add("3A910200", "Macedonian_FYROM_90_CI_AS_KS_SC");
                ls.Add("3A110200", "Macedonian_FYROM_90_CI_AS_KS_WS_SC");
                ls.Add("3AE10200", "Macedonian_FYROM_90_CS_AI_SC");
                ls.Add("3A610200", "Macedonian_FYROM_90_CS_AI_WS_SC");
                ls.Add("3AA10200", "Macedonian_FYROM_90_CS_AI_KS_SC");
                ls.Add("3A210200", "Macedonian_FYROM_90_CS_AI_KS_WS_SC");
                ls.Add("3AC10200", "Macedonian_FYROM_90_CS_AS_SC");
                ls.Add("3A410200", "Macedonian_FYROM_90_CS_AS_WS_SC");
                ls.Add("3A810200", "Macedonian_FYROM_90_CS_AS_KS_SC");
                ls.Add("3A010200", "Macedonian_FYROM_90_CS_AS_KS_WS_SC");
                ls.Add("3A000500", "Macedonian_FYROM_100_BIN");
                ls.Add("3A080400", "Macedonian_FYROM_100_BIN2");
                ls.Add("3AF00400", "Macedonian_FYROM_100_CI_AI");
                ls.Add("3A700400", "Macedonian_FYROM_100_CI_AI_WS");
                ls.Add("3AB00400", "Macedonian_FYROM_100_CI_AI_KS");
                ls.Add("3A300400", "Macedonian_FYROM_100_CI_AI_KS_WS");
                ls.Add("3AD00400", "Macedonian_FYROM_100_CI_AS");
                ls.Add("3A500400", "Macedonian_FYROM_100_CI_AS_WS");
                ls.Add("3A900400", "Macedonian_FYROM_100_CI_AS_KS");
                ls.Add("3A100400", "Macedonian_FYROM_100_CI_AS_KS_WS");
                ls.Add("3AE00400", "Macedonian_FYROM_100_CS_AI");
                ls.Add("3A600400", "Macedonian_FYROM_100_CS_AI_WS");
                ls.Add("3AA00400", "Macedonian_FYROM_100_CS_AI_KS");
                ls.Add("3A200400", "Macedonian_FYROM_100_CS_AI_KS_WS");
                ls.Add("3AC00400", "Macedonian_FYROM_100_CS_AS");
                ls.Add("3A400400", "Macedonian_FYROM_100_CS_AS_WS");
                ls.Add("3A800400", "Macedonian_FYROM_100_CS_AS_KS");
                ls.Add("3A000400", "Macedonian_FYROM_100_CS_AS_KS_WS");
                ls.Add("3AF10400", "Macedonian_FYROM_100_CI_AI_SC");
                ls.Add("3A710400", "Macedonian_FYROM_100_CI_AI_WS_SC");
                ls.Add("3AB10400", "Macedonian_FYROM_100_CI_AI_KS_SC");
                ls.Add("3A310400", "Macedonian_FYROM_100_CI_AI_KS_WS_SC");
                ls.Add("3AD10400", "Macedonian_FYROM_100_CI_AS_SC");
                ls.Add("3A510400", "Macedonian_FYROM_100_CI_AS_WS_SC");
                ls.Add("3A910400", "Macedonian_FYROM_100_CI_AS_KS_SC");
                ls.Add("3A110400", "Macedonian_FYROM_100_CI_AS_KS_WS_SC");
                ls.Add("3AE10400", "Macedonian_FYROM_100_CS_AI_SC");
                ls.Add("3A610400", "Macedonian_FYROM_100_CS_AI_WS_SC");
                ls.Add("3AA10400", "Macedonian_FYROM_100_CS_AI_KS_SC");
                ls.Add("3A210400", "Macedonian_FYROM_100_CS_AI_KS_WS_SC");
                ls.Add("3AC10400", "Macedonian_FYROM_100_CS_AS_SC");
                ls.Add("3A410400", "Macedonian_FYROM_100_CS_AS_WS_SC");
                ls.Add("3A810400", "Macedonian_FYROM_100_CS_AS_KS_SC");
                ls.Add("3A010400", "Macedonian_FYROM_100_CS_AS_KS_WS_SC");
                ls.Add("55000500", "Maltese_100_BIN");
                ls.Add("55080400", "Maltese_100_BIN2");
                ls.Add("55F00400", "Maltese_100_CI_AI");
                ls.Add("55700400", "Maltese_100_CI_AI_WS");
                ls.Add("55B00400", "Maltese_100_CI_AI_KS");
                ls.Add("55300400", "Maltese_100_CI_AI_KS_WS");
                ls.Add("55D00400", "Maltese_100_CI_AS");
                ls.Add("55500400", "Maltese_100_CI_AS_WS");
                ls.Add("55900400", "Maltese_100_CI_AS_KS");
                ls.Add("55100400", "Maltese_100_CI_AS_KS_WS");
                ls.Add("55E00400", "Maltese_100_CS_AI");
                ls.Add("55600400", "Maltese_100_CS_AI_WS");
                ls.Add("55A00400", "Maltese_100_CS_AI_KS");
                ls.Add("55200400", "Maltese_100_CS_AI_KS_WS");
                ls.Add("55C00400", "Maltese_100_CS_AS");
                ls.Add("55400400", "Maltese_100_CS_AS_WS");
                ls.Add("55800400", "Maltese_100_CS_AS_KS");
                ls.Add("55000400", "Maltese_100_CS_AS_KS_WS");
                ls.Add("55F10400", "Maltese_100_CI_AI_SC");
                ls.Add("55710400", "Maltese_100_CI_AI_WS_SC");
                ls.Add("55B10400", "Maltese_100_CI_AI_KS_SC");
                ls.Add("55310400", "Maltese_100_CI_AI_KS_WS_SC");
                ls.Add("55D10400", "Maltese_100_CI_AS_SC");
                ls.Add("55510400", "Maltese_100_CI_AS_WS_SC");
                ls.Add("55910400", "Maltese_100_CI_AS_KS_SC");
                ls.Add("55110400", "Maltese_100_CI_AS_KS_WS_SC");
                ls.Add("55E10400", "Maltese_100_CS_AI_SC");
                ls.Add("55610400", "Maltese_100_CS_AI_WS_SC");
                ls.Add("55A10400", "Maltese_100_CS_AI_KS_SC");
                ls.Add("55210400", "Maltese_100_CS_AI_KS_WS_SC");
                ls.Add("55C10400", "Maltese_100_CS_AS_SC");
                ls.Add("55410400", "Maltese_100_CS_AS_WS_SC");
                ls.Add("55810400", "Maltese_100_CS_AS_KS_SC");
                ls.Add("55010400", "Maltese_100_CS_AS_KS_WS_SC");
                ls.Add("12000500", "Maori_100_BIN");
                ls.Add("12080400", "Maori_100_BIN2");
                ls.Add("12F00400", "Maori_100_CI_AI");
                ls.Add("12700400", "Maori_100_CI_AI_WS");
                ls.Add("12B00400", "Maori_100_CI_AI_KS");
                ls.Add("12300400", "Maori_100_CI_AI_KS_WS");
                ls.Add("12D00400", "Maori_100_CI_AS");
                ls.Add("12500400", "Maori_100_CI_AS_WS");
                ls.Add("12900400", "Maori_100_CI_AS_KS");
                ls.Add("12100400", "Maori_100_CI_AS_KS_WS");
                ls.Add("12E00400", "Maori_100_CS_AI");
                ls.Add("12600400", "Maori_100_CS_AI_WS");
                ls.Add("12A00400", "Maori_100_CS_AI_KS");
                ls.Add("12200400", "Maori_100_CS_AI_KS_WS");
                ls.Add("12C00400", "Maori_100_CS_AS");
                ls.Add("12400400", "Maori_100_CS_AS_WS");
                ls.Add("12800400", "Maori_100_CS_AS_KS");
                ls.Add("12000400", "Maori_100_CS_AS_KS_WS");
                ls.Add("12F10400", "Maori_100_CI_AI_SC");
                ls.Add("12710400", "Maori_100_CI_AI_WS_SC");
                ls.Add("12B10400", "Maori_100_CI_AI_KS_SC");
                ls.Add("12310400", "Maori_100_CI_AI_KS_WS_SC");
                ls.Add("12D10400", "Maori_100_CI_AS_SC");
                ls.Add("12510400", "Maori_100_CI_AS_WS_SC");
                ls.Add("12910400", "Maori_100_CI_AS_KS_SC");
                ls.Add("12110400", "Maori_100_CI_AS_KS_WS_SC");
                ls.Add("12E10400", "Maori_100_CS_AI_SC");
                ls.Add("12610400", "Maori_100_CS_AI_WS_SC");
                ls.Add("12A10400", "Maori_100_CS_AI_KS_SC");
                ls.Add("12210400", "Maori_100_CS_AI_KS_WS_SC");
                ls.Add("12C10400", "Maori_100_CS_AS_SC");
                ls.Add("12410400", "Maori_100_CS_AS_WS_SC");
                ls.Add("12810400", "Maori_100_CS_AS_KS_SC");
                ls.Add("12010400", "Maori_100_CS_AS_KS_WS_SC");
                ls.Add("52000500", "Mapudungan_100_BIN");
                ls.Add("52080400", "Mapudungan_100_BIN2");
                ls.Add("52F00400", "Mapudungan_100_CI_AI");
                ls.Add("52700400", "Mapudungan_100_CI_AI_WS");
                ls.Add("52B00400", "Mapudungan_100_CI_AI_KS");
                ls.Add("52300400", "Mapudungan_100_CI_AI_KS_WS");
                ls.Add("52D00400", "Mapudungan_100_CI_AS");
                ls.Add("52500400", "Mapudungan_100_CI_AS_WS");
                ls.Add("52900400", "Mapudungan_100_CI_AS_KS");
                ls.Add("52100400", "Mapudungan_100_CI_AS_KS_WS");
                ls.Add("52E00400", "Mapudungan_100_CS_AI");
                ls.Add("52600400", "Mapudungan_100_CS_AI_WS");
                ls.Add("52A00400", "Mapudungan_100_CS_AI_KS");
                ls.Add("52200400", "Mapudungan_100_CS_AI_KS_WS");
                ls.Add("52C00400", "Mapudungan_100_CS_AS");
                ls.Add("52400400", "Mapudungan_100_CS_AS_WS");
                ls.Add("52800400", "Mapudungan_100_CS_AS_KS");
                ls.Add("52000400", "Mapudungan_100_CS_AS_KS_WS");
                ls.Add("52F10400", "Mapudungan_100_CI_AI_SC");
                ls.Add("52710400", "Mapudungan_100_CI_AI_WS_SC");
                ls.Add("52B10400", "Mapudungan_100_CI_AI_KS_SC");
                ls.Add("52310400", "Mapudungan_100_CI_AI_KS_WS_SC");
                ls.Add("52D10400", "Mapudungan_100_CI_AS_SC");
                ls.Add("52510400", "Mapudungan_100_CI_AS_WS_SC");
                ls.Add("52910400", "Mapudungan_100_CI_AS_KS_SC");
                ls.Add("52110400", "Mapudungan_100_CI_AS_KS_WS_SC");
                ls.Add("52E10400", "Mapudungan_100_CS_AI_SC");
                ls.Add("52610400", "Mapudungan_100_CS_AI_WS_SC");
                ls.Add("52A10400", "Mapudungan_100_CS_AI_KS_SC");
                ls.Add("52210400", "Mapudungan_100_CS_AI_KS_WS_SC");
                ls.Add("52C10400", "Mapudungan_100_CS_AS_SC");
                ls.Add("52410400", "Mapudungan_100_CS_AS_WS_SC");
                ls.Add("52810400", "Mapudungan_100_CS_AS_KS_SC");
                ls.Add("52010400", "Mapudungan_100_CS_AS_KS_WS_SC");
                ls.Add("28000100", "Modern_Spanish_BIN");
                ls.Add("28080000", "Modern_Spanish_BIN2");
                ls.Add("28F00000", "Modern_Spanish_CI_AI");
                ls.Add("28700000", "Modern_Spanish_CI_AI_WS");
                ls.Add("28B00000", "Modern_Spanish_CI_AI_KS");
                ls.Add("28300000", "Modern_Spanish_CI_AI_KS_WS");
                ls.Add("28D00000", "Modern_Spanish_CI_AS");
                ls.Add("28500000", "Modern_Spanish_CI_AS_WS");
                ls.Add("28900000", "Modern_Spanish_CI_AS_KS");
                ls.Add("28100000", "Modern_Spanish_CI_AS_KS_WS");
                ls.Add("28E00000", "Modern_Spanish_CS_AI");
                ls.Add("28600000", "Modern_Spanish_CS_AI_WS");
                ls.Add("28A00000", "Modern_Spanish_CS_AI_KS");
                ls.Add("28200000", "Modern_Spanish_CS_AI_KS_WS");
                ls.Add("28C00000", "Modern_Spanish_CS_AS");
                ls.Add("28400000", "Modern_Spanish_CS_AS_WS");
                ls.Add("28800000", "Modern_Spanish_CS_AS_KS");
                ls.Add("28000000", "Modern_Spanish_CS_AS_KS_WS");
                ls.Add("28000500", "Modern_Spanish_100_BIN");
                ls.Add("28080400", "Modern_Spanish_100_BIN2");
                ls.Add("28F00400", "Modern_Spanish_100_CI_AI");
                ls.Add("28700400", "Modern_Spanish_100_CI_AI_WS");
                ls.Add("28B00400", "Modern_Spanish_100_CI_AI_KS");
                ls.Add("28300400", "Modern_Spanish_100_CI_AI_KS_WS");
                ls.Add("28D00400", "Modern_Spanish_100_CI_AS");
                ls.Add("28500400", "Modern_Spanish_100_CI_AS_WS");
                ls.Add("28900400", "Modern_Spanish_100_CI_AS_KS");
                ls.Add("28100400", "Modern_Spanish_100_CI_AS_KS_WS");
                ls.Add("28E00400", "Modern_Spanish_100_CS_AI");
                ls.Add("28600400", "Modern_Spanish_100_CS_AI_WS");
                ls.Add("28A00400", "Modern_Spanish_100_CS_AI_KS");
                ls.Add("28200400", "Modern_Spanish_100_CS_AI_KS_WS");
                ls.Add("28C00400", "Modern_Spanish_100_CS_AS");
                ls.Add("28400400", "Modern_Spanish_100_CS_AS_WS");
                ls.Add("28800400", "Modern_Spanish_100_CS_AS_KS");
                ls.Add("28000400", "Modern_Spanish_100_CS_AS_KS_WS");
                ls.Add("28F10400", "Modern_Spanish_100_CI_AI_SC");
                ls.Add("28710400", "Modern_Spanish_100_CI_AI_WS_SC");
                ls.Add("28B10400", "Modern_Spanish_100_CI_AI_KS_SC");
                ls.Add("28310400", "Modern_Spanish_100_CI_AI_KS_WS_SC");
                ls.Add("28D10400", "Modern_Spanish_100_CI_AS_SC");
                ls.Add("28510400", "Modern_Spanish_100_CI_AS_WS_SC");
                ls.Add("28910400", "Modern_Spanish_100_CI_AS_KS_SC");
                ls.Add("28110400", "Modern_Spanish_100_CI_AS_KS_WS_SC");
                ls.Add("28E10400", "Modern_Spanish_100_CS_AI_SC");
                ls.Add("28610400", "Modern_Spanish_100_CS_AI_WS_SC");
                ls.Add("28A10400", "Modern_Spanish_100_CS_AI_KS_SC");
                ls.Add("28210400", "Modern_Spanish_100_CS_AI_KS_WS_SC");
                ls.Add("28C10400", "Modern_Spanish_100_CS_AS_SC");
                ls.Add("28410400", "Modern_Spanish_100_CS_AS_WS_SC");
                ls.Add("28810400", "Modern_Spanish_100_CS_AS_KS_SC");
                ls.Add("28010400", "Modern_Spanish_100_CS_AS_KS_WS_SC");
                ls.Add("3F000500", "Mohawk_100_BIN");
                ls.Add("3F080400", "Mohawk_100_BIN2");
                ls.Add("3FF00400", "Mohawk_100_CI_AI");
                ls.Add("3F700400", "Mohawk_100_CI_AI_WS");
                ls.Add("3FB00400", "Mohawk_100_CI_AI_KS");
                ls.Add("3F300400", "Mohawk_100_CI_AI_KS_WS");
                ls.Add("3FD00400", "Mohawk_100_CI_AS");
                ls.Add("3F500400", "Mohawk_100_CI_AS_WS");
                ls.Add("3F900400", "Mohawk_100_CI_AS_KS");
                ls.Add("3F100400", "Mohawk_100_CI_AS_KS_WS");
                ls.Add("3FE00400", "Mohawk_100_CS_AI");
                ls.Add("3F600400", "Mohawk_100_CS_AI_WS");
                ls.Add("3FA00400", "Mohawk_100_CS_AI_KS");
                ls.Add("3F200400", "Mohawk_100_CS_AI_KS_WS");
                ls.Add("3FC00400", "Mohawk_100_CS_AS");
                ls.Add("3F400400", "Mohawk_100_CS_AS_WS");
                ls.Add("3F800400", "Mohawk_100_CS_AS_KS");
                ls.Add("3F000400", "Mohawk_100_CS_AS_KS_WS");
                ls.Add("3FF10400", "Mohawk_100_CI_AI_SC");
                ls.Add("3F710400", "Mohawk_100_CI_AI_WS_SC");
                ls.Add("3FB10400", "Mohawk_100_CI_AI_KS_SC");
                ls.Add("3F310400", "Mohawk_100_CI_AI_KS_WS_SC");
                ls.Add("3FD10400", "Mohawk_100_CI_AS_SC");
                ls.Add("3F510400", "Mohawk_100_CI_AS_WS_SC");
                ls.Add("3F910400", "Mohawk_100_CI_AS_KS_SC");
                ls.Add("3F110400", "Mohawk_100_CI_AS_KS_WS_SC");
                ls.Add("3FE10400", "Mohawk_100_CS_AI_SC");
                ls.Add("3F610400", "Mohawk_100_CS_AI_WS_SC");
                ls.Add("3FA10400", "Mohawk_100_CS_AI_KS_SC");
                ls.Add("3F210400", "Mohawk_100_CS_AI_KS_WS_SC");
                ls.Add("3FC10400", "Mohawk_100_CS_AS_SC");
                ls.Add("3F410400", "Mohawk_100_CS_AS_WS_SC");
                ls.Add("3F810400", "Mohawk_100_CS_AS_KS_SC");
                ls.Add("3F010400", "Mohawk_100_CS_AS_KS_WS_SC");
                ls.Add("62000500", "Nepali_100_BIN");
                ls.Add("62080400", "Nepali_100_BIN2");
                ls.Add("62F00400", "Nepali_100_CI_AI");
                ls.Add("62700400", "Nepali_100_CI_AI_WS");
                ls.Add("62B00400", "Nepali_100_CI_AI_KS");
                ls.Add("62300400", "Nepali_100_CI_AI_KS_WS");
                ls.Add("62D00400", "Nepali_100_CI_AS");
                ls.Add("62500400", "Nepali_100_CI_AS_WS");
                ls.Add("62900400", "Nepali_100_CI_AS_KS");
                ls.Add("62100400", "Nepali_100_CI_AS_KS_WS");
                ls.Add("62E00400", "Nepali_100_CS_AI");
                ls.Add("62600400", "Nepali_100_CS_AI_WS");
                ls.Add("62A00400", "Nepali_100_CS_AI_KS");
                ls.Add("62200400", "Nepali_100_CS_AI_KS_WS");
                ls.Add("62C00400", "Nepali_100_CS_AS");
                ls.Add("62400400", "Nepali_100_CS_AS_WS");
                ls.Add("62800400", "Nepali_100_CS_AS_KS");
                ls.Add("62000400", "Nepali_100_CS_AS_KS_WS");
                ls.Add("62F10400", "Nepali_100_CI_AI_SC");
                ls.Add("62710400", "Nepali_100_CI_AI_WS_SC");
                ls.Add("62B10400", "Nepali_100_CI_AI_KS_SC");
                ls.Add("62310400", "Nepali_100_CI_AI_KS_WS_SC");
                ls.Add("62D10400", "Nepali_100_CI_AS_SC");
                ls.Add("62510400", "Nepali_100_CI_AS_WS_SC");
                ls.Add("62910400", "Nepali_100_CI_AS_KS_SC");
                ls.Add("62110400", "Nepali_100_CI_AS_KS_WS_SC");
                ls.Add("62E10400", "Nepali_100_CS_AI_SC");
                ls.Add("62610400", "Nepali_100_CS_AI_WS_SC");
                ls.Add("62A10400", "Nepali_100_CS_AI_KS_SC");
                ls.Add("62210400", "Nepali_100_CS_AI_KS_WS_SC");
                ls.Add("62C10400", "Nepali_100_CS_AS_SC");
                ls.Add("62410400", "Nepali_100_CS_AS_WS_SC");
                ls.Add("62810400", "Nepali_100_CS_AS_KS_SC");
                ls.Add("62010400", "Nepali_100_CS_AS_KS_WS_SC");
                ls.Add("4A000500", "Norwegian_100_BIN");
                ls.Add("4A080400", "Norwegian_100_BIN2");
                ls.Add("4AF00400", "Norwegian_100_CI_AI");
                ls.Add("4A700400", "Norwegian_100_CI_AI_WS");
                ls.Add("4AB00400", "Norwegian_100_CI_AI_KS");
                ls.Add("4A300400", "Norwegian_100_CI_AI_KS_WS");
                ls.Add("4AD00400", "Norwegian_100_CI_AS");
                ls.Add("4A500400", "Norwegian_100_CI_AS_WS");
                ls.Add("4A900400", "Norwegian_100_CI_AS_KS");
                ls.Add("4A100400", "Norwegian_100_CI_AS_KS_WS");
                ls.Add("4AE00400", "Norwegian_100_CS_AI");
                ls.Add("4A600400", "Norwegian_100_CS_AI_WS");
                ls.Add("4AA00400", "Norwegian_100_CS_AI_KS");
                ls.Add("4A200400", "Norwegian_100_CS_AI_KS_WS");
                ls.Add("4AC00400", "Norwegian_100_CS_AS");
                ls.Add("4A400400", "Norwegian_100_CS_AS_WS");
                ls.Add("4A800400", "Norwegian_100_CS_AS_KS");
                ls.Add("4A000400", "Norwegian_100_CS_AS_KS_WS");
                ls.Add("4AF10400", "Norwegian_100_CI_AI_SC");
                ls.Add("4A710400", "Norwegian_100_CI_AI_WS_SC");
                ls.Add("4AB10400", "Norwegian_100_CI_AI_KS_SC");
                ls.Add("4A310400", "Norwegian_100_CI_AI_KS_WS_SC");
                ls.Add("4AD10400", "Norwegian_100_CI_AS_SC");
                ls.Add("4A510400", "Norwegian_100_CI_AS_WS_SC");
                ls.Add("4A910400", "Norwegian_100_CI_AS_KS_SC");
                ls.Add("4A110400", "Norwegian_100_CI_AS_KS_WS_SC");
                ls.Add("4AE10400", "Norwegian_100_CS_AI_SC");
                ls.Add("4A610400", "Norwegian_100_CS_AI_WS_SC");
                ls.Add("4AA10400", "Norwegian_100_CS_AI_KS_SC");
                ls.Add("4A210400", "Norwegian_100_CS_AI_KS_WS_SC");
                ls.Add("4AC10400", "Norwegian_100_CS_AS_SC");
                ls.Add("4A410400", "Norwegian_100_CS_AS_WS_SC");
                ls.Add("4A810400", "Norwegian_100_CS_AS_KS_SC");
                ls.Add("4A010400", "Norwegian_100_CS_AS_KS_WS_SC");
                ls.Add("5B000500", "Pashto_100_BIN");
                ls.Add("5B080400", "Pashto_100_BIN2");
                ls.Add("5BF00400", "Pashto_100_CI_AI");
                ls.Add("5B700400", "Pashto_100_CI_AI_WS");
                ls.Add("5BB00400", "Pashto_100_CI_AI_KS");
                ls.Add("5B300400", "Pashto_100_CI_AI_KS_WS");
                ls.Add("5BD00400", "Pashto_100_CI_AS");
                ls.Add("5B500400", "Pashto_100_CI_AS_WS");
                ls.Add("5B900400", "Pashto_100_CI_AS_KS");
                ls.Add("5B100400", "Pashto_100_CI_AS_KS_WS");
                ls.Add("5BE00400", "Pashto_100_CS_AI");
                ls.Add("5B600400", "Pashto_100_CS_AI_WS");
                ls.Add("5BA00400", "Pashto_100_CS_AI_KS");
                ls.Add("5B200400", "Pashto_100_CS_AI_KS_WS");
                ls.Add("5BC00400", "Pashto_100_CS_AS");
                ls.Add("5B400400", "Pashto_100_CS_AS_WS");
                ls.Add("5B800400", "Pashto_100_CS_AS_KS");
                ls.Add("5B000400", "Pashto_100_CS_AS_KS_WS");
                ls.Add("5BF10400", "Pashto_100_CI_AI_SC");
                ls.Add("5B710400", "Pashto_100_CI_AI_WS_SC");
                ls.Add("5BB10400", "Pashto_100_CI_AI_KS_SC");
                ls.Add("5B310400", "Pashto_100_CI_AI_KS_WS_SC");
                ls.Add("5BD10400", "Pashto_100_CI_AS_SC");
                ls.Add("5B510400", "Pashto_100_CI_AS_WS_SC");
                ls.Add("5B910400", "Pashto_100_CI_AS_KS_SC");
                ls.Add("5B110400", "Pashto_100_CI_AS_KS_WS_SC");
                ls.Add("5BE10400", "Pashto_100_CS_AI_SC");
                ls.Add("5B610400", "Pashto_100_CS_AI_WS_SC");
                ls.Add("5BA10400", "Pashto_100_CS_AI_KS_SC");
                ls.Add("5B210400", "Pashto_100_CS_AI_KS_WS_SC");
                ls.Add("5BC10400", "Pashto_100_CS_AS_SC");
                ls.Add("5B410400", "Pashto_100_CS_AS_WS_SC");
                ls.Add("5B810400", "Pashto_100_CS_AS_KS_SC");
                ls.Add("5B010400", "Pashto_100_CS_AS_KS_WS_SC");
                ls.Add("51000500", "Persian_100_BIN");
                ls.Add("51080400", "Persian_100_BIN2");
                ls.Add("51F00400", "Persian_100_CI_AI");
                ls.Add("51700400", "Persian_100_CI_AI_WS");
                ls.Add("51B00400", "Persian_100_CI_AI_KS");
                ls.Add("51300400", "Persian_100_CI_AI_KS_WS");
                ls.Add("51D00400", "Persian_100_CI_AS");
                ls.Add("51500400", "Persian_100_CI_AS_WS");
                ls.Add("51900400", "Persian_100_CI_AS_KS");
                ls.Add("51100400", "Persian_100_CI_AS_KS_WS");
                ls.Add("51E00400", "Persian_100_CS_AI");
                ls.Add("51600400", "Persian_100_CS_AI_WS");
                ls.Add("51A00400", "Persian_100_CS_AI_KS");
                ls.Add("51200400", "Persian_100_CS_AI_KS_WS");
                ls.Add("51C00400", "Persian_100_CS_AS");
                ls.Add("51400400", "Persian_100_CS_AS_WS");
                ls.Add("51800400", "Persian_100_CS_AS_KS");
                ls.Add("51000400", "Persian_100_CS_AS_KS_WS");
                ls.Add("51F10400", "Persian_100_CI_AI_SC");
                ls.Add("51710400", "Persian_100_CI_AI_WS_SC");
                ls.Add("51B10400", "Persian_100_CI_AI_KS_SC");
                ls.Add("51310400", "Persian_100_CI_AI_KS_WS_SC");
                ls.Add("51D10400", "Persian_100_CI_AS_SC");
                ls.Add("51510400", "Persian_100_CI_AS_WS_SC");
                ls.Add("51910400", "Persian_100_CI_AS_KS_SC");
                ls.Add("51110400", "Persian_100_CI_AS_KS_WS_SC");
                ls.Add("51E10400", "Persian_100_CS_AI_SC");
                ls.Add("51610400", "Persian_100_CS_AI_WS_SC");
                ls.Add("51A10400", "Persian_100_CS_AI_KS_SC");
                ls.Add("51210400", "Persian_100_CS_AI_KS_WS_SC");
                ls.Add("51C10400", "Persian_100_CS_AS_SC");
                ls.Add("51410400", "Persian_100_CS_AS_WS_SC");
                ls.Add("51810400", "Persian_100_CS_AS_KS_SC");
                ls.Add("51010400", "Persian_100_CS_AS_KS_WS_SC");
                ls.Add("13000100", "Polish_BIN");
                ls.Add("13080000", "Polish_BIN2");
                ls.Add("13F00000", "Polish_CI_AI");
                ls.Add("13700000", "Polish_CI_AI_WS");
                ls.Add("13B00000", "Polish_CI_AI_KS");
                ls.Add("13300000", "Polish_CI_AI_KS_WS");
                ls.Add("13D00000", "Polish_CI_AS");
                ls.Add("13500000", "Polish_CI_AS_WS");
                ls.Add("13900000", "Polish_CI_AS_KS");
                ls.Add("13100000", "Polish_CI_AS_KS_WS");
                ls.Add("13E00000", "Polish_CS_AI");
                ls.Add("13600000", "Polish_CS_AI_WS");
                ls.Add("13A00000", "Polish_CS_AI_KS");
                ls.Add("13200000", "Polish_CS_AI_KS_WS");
                ls.Add("13C00000", "Polish_CS_AS");
                ls.Add("13400000", "Polish_CS_AS_WS");
                ls.Add("13800000", "Polish_CS_AS_KS");
                ls.Add("13000000", "Polish_CS_AS_KS_WS");
                ls.Add("13000500", "Polish_100_BIN");
                ls.Add("13080400", "Polish_100_BIN2");
                ls.Add("13F00400", "Polish_100_CI_AI");
                ls.Add("13700400", "Polish_100_CI_AI_WS");
                ls.Add("13B00400", "Polish_100_CI_AI_KS");
                ls.Add("13300400", "Polish_100_CI_AI_KS_WS");
                ls.Add("13D00400", "Polish_100_CI_AS");
                ls.Add("13500400", "Polish_100_CI_AS_WS");
                ls.Add("13900400", "Polish_100_CI_AS_KS");
                ls.Add("13100400", "Polish_100_CI_AS_KS_WS");
                ls.Add("13E00400", "Polish_100_CS_AI");
                ls.Add("13600400", "Polish_100_CS_AI_WS");
                ls.Add("13A00400", "Polish_100_CS_AI_KS");
                ls.Add("13200400", "Polish_100_CS_AI_KS_WS");
                ls.Add("13C00400", "Polish_100_CS_AS");
                ls.Add("13400400", "Polish_100_CS_AS_WS");
                ls.Add("13800400", "Polish_100_CS_AS_KS");
                ls.Add("13000400", "Polish_100_CS_AS_KS_WS");
                ls.Add("13F10400", "Polish_100_CI_AI_SC");
                ls.Add("13710400", "Polish_100_CI_AI_WS_SC");
                ls.Add("13B10400", "Polish_100_CI_AI_KS_SC");
                ls.Add("13310400", "Polish_100_CI_AI_KS_WS_SC");
                ls.Add("13D10400", "Polish_100_CI_AS_SC");
                ls.Add("13510400", "Polish_100_CI_AS_WS_SC");
                ls.Add("13910400", "Polish_100_CI_AS_KS_SC");
                ls.Add("13110400", "Polish_100_CI_AS_KS_WS_SC");
                ls.Add("13E10400", "Polish_100_CS_AI_SC");
                ls.Add("13610400", "Polish_100_CS_AI_WS_SC");
                ls.Add("13A10400", "Polish_100_CS_AI_KS_SC");
                ls.Add("13210400", "Polish_100_CS_AI_KS_WS_SC");
                ls.Add("13C10400", "Polish_100_CS_AS_SC");
                ls.Add("13410400", "Polish_100_CS_AS_WS_SC");
                ls.Add("13810400", "Polish_100_CS_AS_KS_SC");
                ls.Add("13010400", "Polish_100_CS_AS_KS_WS_SC");
                ls.Add("14000100", "Romanian_BIN");
                ls.Add("14080000", "Romanian_BIN2");
                ls.Add("14F00000", "Romanian_CI_AI");
                ls.Add("14700000", "Romanian_CI_AI_WS");
                ls.Add("14B00000", "Romanian_CI_AI_KS");
                ls.Add("14300000", "Romanian_CI_AI_KS_WS");
                ls.Add("14D00000", "Romanian_CI_AS");
                ls.Add("14500000", "Romanian_CI_AS_WS");
                ls.Add("14900000", "Romanian_CI_AS_KS");
                ls.Add("14100000", "Romanian_CI_AS_KS_WS");
                ls.Add("14E00000", "Romanian_CS_AI");
                ls.Add("14600000", "Romanian_CS_AI_WS");
                ls.Add("14A00000", "Romanian_CS_AI_KS");
                ls.Add("14200000", "Romanian_CS_AI_KS_WS");
                ls.Add("14C00000", "Romanian_CS_AS");
                ls.Add("14400000", "Romanian_CS_AS_WS");
                ls.Add("14800000", "Romanian_CS_AS_KS");
                ls.Add("14000000", "Romanian_CS_AS_KS_WS");
                ls.Add("14000500", "Romanian_100_BIN");
                ls.Add("14080400", "Romanian_100_BIN2");
                ls.Add("14F00400", "Romanian_100_CI_AI");
                ls.Add("14700400", "Romanian_100_CI_AI_WS");
                ls.Add("14B00400", "Romanian_100_CI_AI_KS");
                ls.Add("14300400", "Romanian_100_CI_AI_KS_WS");
                ls.Add("14D00400", "Romanian_100_CI_AS");
                ls.Add("14500400", "Romanian_100_CI_AS_WS");
                ls.Add("14900400", "Romanian_100_CI_AS_KS");
                ls.Add("14100400", "Romanian_100_CI_AS_KS_WS");
                ls.Add("14E00400", "Romanian_100_CS_AI");
                ls.Add("14600400", "Romanian_100_CS_AI_WS");
                ls.Add("14A00400", "Romanian_100_CS_AI_KS");
                ls.Add("14200400", "Romanian_100_CS_AI_KS_WS");
                ls.Add("14C00400", "Romanian_100_CS_AS");
                ls.Add("14400400", "Romanian_100_CS_AS_WS");
                ls.Add("14800400", "Romanian_100_CS_AS_KS");
                ls.Add("14000400", "Romanian_100_CS_AS_KS_WS");
                ls.Add("14F10400", "Romanian_100_CI_AI_SC");
                ls.Add("14710400", "Romanian_100_CI_AI_WS_SC");
                ls.Add("14B10400", "Romanian_100_CI_AI_KS_SC");
                ls.Add("14310400", "Romanian_100_CI_AI_KS_WS_SC");
                ls.Add("14D10400", "Romanian_100_CI_AS_SC");
                ls.Add("14510400", "Romanian_100_CI_AS_WS_SC");
                ls.Add("14910400", "Romanian_100_CI_AS_KS_SC");
                ls.Add("14110400", "Romanian_100_CI_AS_KS_WS_SC");
                ls.Add("14E10400", "Romanian_100_CS_AI_SC");
                ls.Add("14610400", "Romanian_100_CS_AI_WS_SC");
                ls.Add("14A10400", "Romanian_100_CS_AI_KS_SC");
                ls.Add("14210400", "Romanian_100_CS_AI_KS_WS_SC");
                ls.Add("14C10400", "Romanian_100_CS_AS_SC");
                ls.Add("14410400", "Romanian_100_CS_AS_WS_SC");
                ls.Add("14810400", "Romanian_100_CS_AS_KS_SC");
                ls.Add("14010400", "Romanian_100_CS_AS_KS_WS_SC");
                ls.Add("4B000500", "Romansh_100_BIN");
                ls.Add("4B080400", "Romansh_100_BIN2");
                ls.Add("4BF00400", "Romansh_100_CI_AI");
                ls.Add("4B700400", "Romansh_100_CI_AI_WS");
                ls.Add("4BB00400", "Romansh_100_CI_AI_KS");
                ls.Add("4B300400", "Romansh_100_CI_AI_KS_WS");
                ls.Add("4BD00400", "Romansh_100_CI_AS");
                ls.Add("4B500400", "Romansh_100_CI_AS_WS");
                ls.Add("4B900400", "Romansh_100_CI_AS_KS");
                ls.Add("4B100400", "Romansh_100_CI_AS_KS_WS");
                ls.Add("4BE00400", "Romansh_100_CS_AI");
                ls.Add("4B600400", "Romansh_100_CS_AI_WS");
                ls.Add("4BA00400", "Romansh_100_CS_AI_KS");
                ls.Add("4B200400", "Romansh_100_CS_AI_KS_WS");
                ls.Add("4BC00400", "Romansh_100_CS_AS");
                ls.Add("4B400400", "Romansh_100_CS_AS_WS");
                ls.Add("4B800400", "Romansh_100_CS_AS_KS");
                ls.Add("4B000400", "Romansh_100_CS_AS_KS_WS");
                ls.Add("4BF10400", "Romansh_100_CI_AI_SC");
                ls.Add("4B710400", "Romansh_100_CI_AI_WS_SC");
                ls.Add("4BB10400", "Romansh_100_CI_AI_KS_SC");
                ls.Add("4B310400", "Romansh_100_CI_AI_KS_WS_SC");
                ls.Add("4BD10400", "Romansh_100_CI_AS_SC");
                ls.Add("4B510400", "Romansh_100_CI_AS_WS_SC");
                ls.Add("4B910400", "Romansh_100_CI_AS_KS_SC");
                ls.Add("4B110400", "Romansh_100_CI_AS_KS_WS_SC");
                ls.Add("4BE10400", "Romansh_100_CS_AI_SC");
                ls.Add("4B610400", "Romansh_100_CS_AI_WS_SC");
                ls.Add("4BA10400", "Romansh_100_CS_AI_KS_SC");
                ls.Add("4B210400", "Romansh_100_CS_AI_KS_WS_SC");
                ls.Add("4BC10400", "Romansh_100_CS_AS_SC");
                ls.Add("4B410400", "Romansh_100_CS_AS_WS_SC");
                ls.Add("4B810400", "Romansh_100_CS_AS_KS_SC");
                ls.Add("4B010400", "Romansh_100_CS_AS_KS_WS_SC");
                ls.Add("56000500", "Sami_Norway_100_BIN");
                ls.Add("56080400", "Sami_Norway_100_BIN2");
                ls.Add("56F00400", "Sami_Norway_100_CI_AI");
                ls.Add("56700400", "Sami_Norway_100_CI_AI_WS");
                ls.Add("56B00400", "Sami_Norway_100_CI_AI_KS");
                ls.Add("56300400", "Sami_Norway_100_CI_AI_KS_WS");
                ls.Add("56D00400", "Sami_Norway_100_CI_AS");
                ls.Add("56500400", "Sami_Norway_100_CI_AS_WS");
                ls.Add("56900400", "Sami_Norway_100_CI_AS_KS");
                ls.Add("56100400", "Sami_Norway_100_CI_AS_KS_WS");
                ls.Add("56E00400", "Sami_Norway_100_CS_AI");
                ls.Add("56600400", "Sami_Norway_100_CS_AI_WS");
                ls.Add("56A00400", "Sami_Norway_100_CS_AI_KS");
                ls.Add("56200400", "Sami_Norway_100_CS_AI_KS_WS");
                ls.Add("56C00400", "Sami_Norway_100_CS_AS");
                ls.Add("56400400", "Sami_Norway_100_CS_AS_WS");
                ls.Add("56800400", "Sami_Norway_100_CS_AS_KS");
                ls.Add("56000400", "Sami_Norway_100_CS_AS_KS_WS");
                ls.Add("56F10400", "Sami_Norway_100_CI_AI_SC");
                ls.Add("56710400", "Sami_Norway_100_CI_AI_WS_SC");
                ls.Add("56B10400", "Sami_Norway_100_CI_AI_KS_SC");
                ls.Add("56310400", "Sami_Norway_100_CI_AI_KS_WS_SC");
                ls.Add("56D10400", "Sami_Norway_100_CI_AS_SC");
                ls.Add("56510400", "Sami_Norway_100_CI_AS_WS_SC");
                ls.Add("56910400", "Sami_Norway_100_CI_AS_KS_SC");
                ls.Add("56110400", "Sami_Norway_100_CI_AS_KS_WS_SC");
                ls.Add("56E10400", "Sami_Norway_100_CS_AI_SC");
                ls.Add("56610400", "Sami_Norway_100_CS_AI_WS_SC");
                ls.Add("56A10400", "Sami_Norway_100_CS_AI_KS_SC");
                ls.Add("56210400", "Sami_Norway_100_CS_AI_KS_WS_SC");
                ls.Add("56C10400", "Sami_Norway_100_CS_AS_SC");
                ls.Add("56410400", "Sami_Norway_100_CS_AS_WS_SC");
                ls.Add("56810400", "Sami_Norway_100_CS_AS_KS_SC");
                ls.Add("56010400", "Sami_Norway_100_CS_AS_KS_WS_SC");
                ls.Add("57000500", "Sami_Sweden_Finland_100_BIN");
                ls.Add("57080400", "Sami_Sweden_Finland_100_BIN2");
                ls.Add("57F00400", "Sami_Sweden_Finland_100_CI_AI");
                ls.Add("57700400", "Sami_Sweden_Finland_100_CI_AI_WS");
                ls.Add("57B00400", "Sami_Sweden_Finland_100_CI_AI_KS");
                ls.Add("57300400", "Sami_Sweden_Finland_100_CI_AI_KS_WS");
                ls.Add("57D00400", "Sami_Sweden_Finland_100_CI_AS");
                ls.Add("57500400", "Sami_Sweden_Finland_100_CI_AS_WS");
                ls.Add("57900400", "Sami_Sweden_Finland_100_CI_AS_KS");
                ls.Add("57100400", "Sami_Sweden_Finland_100_CI_AS_KS_WS");
                ls.Add("57E00400", "Sami_Sweden_Finland_100_CS_AI");
                ls.Add("57600400", "Sami_Sweden_Finland_100_CS_AI_WS");
                ls.Add("57A00400", "Sami_Sweden_Finland_100_CS_AI_KS");
                ls.Add("57200400", "Sami_Sweden_Finland_100_CS_AI_KS_WS");
                ls.Add("57C00400", "Sami_Sweden_Finland_100_CS_AS");
                ls.Add("57400400", "Sami_Sweden_Finland_100_CS_AS_WS");
                ls.Add("57800400", "Sami_Sweden_Finland_100_CS_AS_KS");
                ls.Add("57000400", "Sami_Sweden_Finland_100_CS_AS_KS_WS");
                ls.Add("57F10400", "Sami_Sweden_Finland_100_CI_AI_SC");
                ls.Add("57710400", "Sami_Sweden_Finland_100_CI_AI_WS_SC");
                ls.Add("57B10400", "Sami_Sweden_Finland_100_CI_AI_KS_SC");
                ls.Add("57310400", "Sami_Sweden_Finland_100_CI_AI_KS_WS_SC");
                ls.Add("57D10400", "Sami_Sweden_Finland_100_CI_AS_SC");
                ls.Add("57510400", "Sami_Sweden_Finland_100_CI_AS_WS_SC");
                ls.Add("57910400", "Sami_Sweden_Finland_100_CI_AS_KS_SC");
                ls.Add("57110400", "Sami_Sweden_Finland_100_CI_AS_KS_WS_SC");
                ls.Add("57E10400", "Sami_Sweden_Finland_100_CS_AI_SC");
                ls.Add("57610400", "Sami_Sweden_Finland_100_CS_AI_WS_SC");
                ls.Add("57A10400", "Sami_Sweden_Finland_100_CS_AI_KS_SC");
                ls.Add("57210400", "Sami_Sweden_Finland_100_CS_AI_KS_WS_SC");
                ls.Add("57C10400", "Sami_Sweden_Finland_100_CS_AS_SC");
                ls.Add("57410400", "Sami_Sweden_Finland_100_CS_AS_WS_SC");
                ls.Add("57810400", "Sami_Sweden_Finland_100_CS_AS_KS_SC");
                ls.Add("57010400", "Sami_Sweden_Finland_100_CS_AS_KS_WS_SC");
                ls.Add("4D000500", "Serbian_Cyrillic_100_BIN");
                ls.Add("4D080400", "Serbian_Cyrillic_100_BIN2");
                ls.Add("4DF00400", "Serbian_Cyrillic_100_CI_AI");
                ls.Add("4D700400", "Serbian_Cyrillic_100_CI_AI_WS");
                ls.Add("4DB00400", "Serbian_Cyrillic_100_CI_AI_KS");
                ls.Add("4D300400", "Serbian_Cyrillic_100_CI_AI_KS_WS");
                ls.Add("4DD00400", "Serbian_Cyrillic_100_CI_AS");
                ls.Add("4D500400", "Serbian_Cyrillic_100_CI_AS_WS");
                ls.Add("4D900400", "Serbian_Cyrillic_100_CI_AS_KS");
                ls.Add("4D100400", "Serbian_Cyrillic_100_CI_AS_KS_WS");
                ls.Add("4DE00400", "Serbian_Cyrillic_100_CS_AI");
                ls.Add("4D600400", "Serbian_Cyrillic_100_CS_AI_WS");
                ls.Add("4DA00400", "Serbian_Cyrillic_100_CS_AI_KS");
                ls.Add("4D200400", "Serbian_Cyrillic_100_CS_AI_KS_WS");
                ls.Add("4DC00400", "Serbian_Cyrillic_100_CS_AS");
                ls.Add("4D400400", "Serbian_Cyrillic_100_CS_AS_WS");
                ls.Add("4D800400", "Serbian_Cyrillic_100_CS_AS_KS");
                ls.Add("4D000400", "Serbian_Cyrillic_100_CS_AS_KS_WS");
                ls.Add("4DF10400", "Serbian_Cyrillic_100_CI_AI_SC");
                ls.Add("4D710400", "Serbian_Cyrillic_100_CI_AI_WS_SC");
                ls.Add("4DB10400", "Serbian_Cyrillic_100_CI_AI_KS_SC");
                ls.Add("4D310400", "Serbian_Cyrillic_100_CI_AI_KS_WS_SC");
                ls.Add("4DD10400", "Serbian_Cyrillic_100_CI_AS_SC");
                ls.Add("4D510400", "Serbian_Cyrillic_100_CI_AS_WS_SC");
                ls.Add("4D910400", "Serbian_Cyrillic_100_CI_AS_KS_SC");
                ls.Add("4D110400", "Serbian_Cyrillic_100_CI_AS_KS_WS_SC");
                ls.Add("4DE10400", "Serbian_Cyrillic_100_CS_AI_SC");
                ls.Add("4D610400", "Serbian_Cyrillic_100_CS_AI_WS_SC");
                ls.Add("4DA10400", "Serbian_Cyrillic_100_CS_AI_KS_SC");
                ls.Add("4D210400", "Serbian_Cyrillic_100_CS_AI_KS_WS_SC");
                ls.Add("4DC10400", "Serbian_Cyrillic_100_CS_AS_SC");
                ls.Add("4D410400", "Serbian_Cyrillic_100_CS_AS_WS_SC");
                ls.Add("4D810400", "Serbian_Cyrillic_100_CS_AS_KS_SC");
                ls.Add("4D010400", "Serbian_Cyrillic_100_CS_AS_KS_WS_SC");
                ls.Add("4C000500", "Serbian_Latin_100_BIN");
                ls.Add("4C080400", "Serbian_Latin_100_BIN2");
                ls.Add("4CF00400", "Serbian_Latin_100_CI_AI");
                ls.Add("4C700400", "Serbian_Latin_100_CI_AI_WS");
                ls.Add("4CB00400", "Serbian_Latin_100_CI_AI_KS");
                ls.Add("4C300400", "Serbian_Latin_100_CI_AI_KS_WS");
                ls.Add("4CD00400", "Serbian_Latin_100_CI_AS");
                ls.Add("4C500400", "Serbian_Latin_100_CI_AS_WS");
                ls.Add("4C900400", "Serbian_Latin_100_CI_AS_KS");
                ls.Add("4C100400", "Serbian_Latin_100_CI_AS_KS_WS");
                ls.Add("4CE00400", "Serbian_Latin_100_CS_AI");
                ls.Add("4C600400", "Serbian_Latin_100_CS_AI_WS");
                ls.Add("4CA00400", "Serbian_Latin_100_CS_AI_KS");
                ls.Add("4C200400", "Serbian_Latin_100_CS_AI_KS_WS");
                ls.Add("4CC00400", "Serbian_Latin_100_CS_AS");
                ls.Add("4C400400", "Serbian_Latin_100_CS_AS_WS");
                ls.Add("4C800400", "Serbian_Latin_100_CS_AS_KS");
                ls.Add("4C000400", "Serbian_Latin_100_CS_AS_KS_WS");
                ls.Add("4CF10400", "Serbian_Latin_100_CI_AI_SC");
                ls.Add("4C710400", "Serbian_Latin_100_CI_AI_WS_SC");
                ls.Add("4CB10400", "Serbian_Latin_100_CI_AI_KS_SC");
                ls.Add("4C310400", "Serbian_Latin_100_CI_AI_KS_WS_SC");
                ls.Add("4CD10400", "Serbian_Latin_100_CI_AS_SC");
                ls.Add("4C510400", "Serbian_Latin_100_CI_AS_WS_SC");
                ls.Add("4C910400", "Serbian_Latin_100_CI_AS_KS_SC");
                ls.Add("4C110400", "Serbian_Latin_100_CI_AS_KS_WS_SC");
                ls.Add("4CE10400", "Serbian_Latin_100_CS_AI_SC");
                ls.Add("4C610400", "Serbian_Latin_100_CS_AI_WS_SC");
                ls.Add("4CA10400", "Serbian_Latin_100_CS_AI_KS_SC");
                ls.Add("4C210400", "Serbian_Latin_100_CS_AI_KS_WS_SC");
                ls.Add("4CC10400", "Serbian_Latin_100_CS_AS_SC");
                ls.Add("4C410400", "Serbian_Latin_100_CS_AS_WS_SC");
                ls.Add("4C810400", "Serbian_Latin_100_CS_AS_KS_SC");
                ls.Add("4C010400", "Serbian_Latin_100_CS_AS_KS_WS_SC");
                ls.Add("17000100", "Slovak_BIN");
                ls.Add("17080000", "Slovak_BIN2");
                ls.Add("17F00000", "Slovak_CI_AI");
                ls.Add("17700000", "Slovak_CI_AI_WS");
                ls.Add("17B00000", "Slovak_CI_AI_KS");
                ls.Add("17300000", "Slovak_CI_AI_KS_WS");
                ls.Add("17D00000", "Slovak_CI_AS");
                ls.Add("17500000", "Slovak_CI_AS_WS");
                ls.Add("17900000", "Slovak_CI_AS_KS");
                ls.Add("17100000", "Slovak_CI_AS_KS_WS");
                ls.Add("17E00000", "Slovak_CS_AI");
                ls.Add("17600000", "Slovak_CS_AI_WS");
                ls.Add("17A00000", "Slovak_CS_AI_KS");
                ls.Add("17200000", "Slovak_CS_AI_KS_WS");
                ls.Add("17C00000", "Slovak_CS_AS");
                ls.Add("17400000", "Slovak_CS_AS_WS");
                ls.Add("17800000", "Slovak_CS_AS_KS");
                ls.Add("17000000", "Slovak_CS_AS_KS_WS");
                ls.Add("17000500", "Slovak_100_BIN");
                ls.Add("17080400", "Slovak_100_BIN2");
                ls.Add("17F00400", "Slovak_100_CI_AI");
                ls.Add("17700400", "Slovak_100_CI_AI_WS");
                ls.Add("17B00400", "Slovak_100_CI_AI_KS");
                ls.Add("17300400", "Slovak_100_CI_AI_KS_WS");
                ls.Add("17D00400", "Slovak_100_CI_AS");
                ls.Add("17500400", "Slovak_100_CI_AS_WS");
                ls.Add("17900400", "Slovak_100_CI_AS_KS");
                ls.Add("17100400", "Slovak_100_CI_AS_KS_WS");
                ls.Add("17E00400", "Slovak_100_CS_AI");
                ls.Add("17600400", "Slovak_100_CS_AI_WS");
                ls.Add("17A00400", "Slovak_100_CS_AI_KS");
                ls.Add("17200400", "Slovak_100_CS_AI_KS_WS");
                ls.Add("17C00400", "Slovak_100_CS_AS");
                ls.Add("17400400", "Slovak_100_CS_AS_WS");
                ls.Add("17800400", "Slovak_100_CS_AS_KS");
                ls.Add("17000400", "Slovak_100_CS_AS_KS_WS");
                ls.Add("17F10400", "Slovak_100_CI_AI_SC");
                ls.Add("17710400", "Slovak_100_CI_AI_WS_SC");
                ls.Add("17B10400", "Slovak_100_CI_AI_KS_SC");
                ls.Add("17310400", "Slovak_100_CI_AI_KS_WS_SC");
                ls.Add("17D10400", "Slovak_100_CI_AS_SC");
                ls.Add("17510400", "Slovak_100_CI_AS_WS_SC");
                ls.Add("17910400", "Slovak_100_CI_AS_KS_SC");
                ls.Add("17110400", "Slovak_100_CI_AS_KS_WS_SC");
                ls.Add("17E10400", "Slovak_100_CS_AI_SC");
                ls.Add("17610400", "Slovak_100_CS_AI_WS_SC");
                ls.Add("17A10400", "Slovak_100_CS_AI_KS_SC");
                ls.Add("17210400", "Slovak_100_CS_AI_KS_WS_SC");
                ls.Add("17C10400", "Slovak_100_CS_AS_SC");
                ls.Add("17410400", "Slovak_100_CS_AS_WS_SC");
                ls.Add("17810400", "Slovak_100_CS_AS_KS_SC");
                ls.Add("17010400", "Slovak_100_CS_AS_KS_WS_SC");
                ls.Add("1C000100", "Slovenian_BIN");
                ls.Add("1C080000", "Slovenian_BIN2");
                ls.Add("1CF00000", "Slovenian_CI_AI");
                ls.Add("1C700000", "Slovenian_CI_AI_WS");
                ls.Add("1CB00000", "Slovenian_CI_AI_KS");
                ls.Add("1C300000", "Slovenian_CI_AI_KS_WS");
                ls.Add("1CD00000", "Slovenian_CI_AS");
                ls.Add("1C500000", "Slovenian_CI_AS_WS");
                ls.Add("1C900000", "Slovenian_CI_AS_KS");
                ls.Add("1C100000", "Slovenian_CI_AS_KS_WS");
                ls.Add("1CE00000", "Slovenian_CS_AI");
                ls.Add("1C600000", "Slovenian_CS_AI_WS");
                ls.Add("1CA00000", "Slovenian_CS_AI_KS");
                ls.Add("1C200000", "Slovenian_CS_AI_KS_WS");
                ls.Add("1CC00000", "Slovenian_CS_AS");
                ls.Add("1C400000", "Slovenian_CS_AS_WS");
                ls.Add("1C800000", "Slovenian_CS_AS_KS");
                ls.Add("1C000000", "Slovenian_CS_AS_KS_WS");
                ls.Add("1C000500", "Slovenian_100_BIN");
                ls.Add("1C080400", "Slovenian_100_BIN2");
                ls.Add("1CF00400", "Slovenian_100_CI_AI");
                ls.Add("1C700400", "Slovenian_100_CI_AI_WS");
                ls.Add("1CB00400", "Slovenian_100_CI_AI_KS");
                ls.Add("1C300400", "Slovenian_100_CI_AI_KS_WS");
                ls.Add("1CD00400", "Slovenian_100_CI_AS");
                ls.Add("1C500400", "Slovenian_100_CI_AS_WS");
                ls.Add("1C900400", "Slovenian_100_CI_AS_KS");
                ls.Add("1C100400", "Slovenian_100_CI_AS_KS_WS");
                ls.Add("1CE00400", "Slovenian_100_CS_AI");
                ls.Add("1C600400", "Slovenian_100_CS_AI_WS");
                ls.Add("1CA00400", "Slovenian_100_CS_AI_KS");
                ls.Add("1C200400", "Slovenian_100_CS_AI_KS_WS");
                ls.Add("1CC00400", "Slovenian_100_CS_AS");
                ls.Add("1C400400", "Slovenian_100_CS_AS_WS");
                ls.Add("1C800400", "Slovenian_100_CS_AS_KS");
                ls.Add("1C000400", "Slovenian_100_CS_AS_KS_WS");
                ls.Add("1CF10400", "Slovenian_100_CI_AI_SC");
                ls.Add("1C710400", "Slovenian_100_CI_AI_WS_SC");
                ls.Add("1CB10400", "Slovenian_100_CI_AI_KS_SC");
                ls.Add("1C310400", "Slovenian_100_CI_AI_KS_WS_SC");
                ls.Add("1CD10400", "Slovenian_100_CI_AS_SC");
                ls.Add("1C510400", "Slovenian_100_CI_AS_WS_SC");
                ls.Add("1C910400", "Slovenian_100_CI_AS_KS_SC");
                ls.Add("1C110400", "Slovenian_100_CI_AS_KS_WS_SC");
                ls.Add("1CE10400", "Slovenian_100_CS_AI_SC");
                ls.Add("1C610400", "Slovenian_100_CS_AI_WS_SC");
                ls.Add("1CA10400", "Slovenian_100_CS_AI_KS_SC");
                ls.Add("1C210400", "Slovenian_100_CS_AI_KS_WS_SC");
                ls.Add("1CC10400", "Slovenian_100_CS_AS_SC");
                ls.Add("1C410400", "Slovenian_100_CS_AS_WS_SC");
                ls.Add("1C810400", "Slovenian_100_CS_AS_KS_SC");
                ls.Add("1C010400", "Slovenian_100_CS_AS_KS_WS_SC");
                ls.Add("3B000300", "Syriac_90_BIN");
                ls.Add("3B080200", "Syriac_90_BIN2");
                ls.Add("3BF00200", "Syriac_90_CI_AI");
                ls.Add("3B700200", "Syriac_90_CI_AI_WS");
                ls.Add("3BB00200", "Syriac_90_CI_AI_KS");
                ls.Add("3B300200", "Syriac_90_CI_AI_KS_WS");
                ls.Add("3BD00200", "Syriac_90_CI_AS");
                ls.Add("3B500200", "Syriac_90_CI_AS_WS");
                ls.Add("3B900200", "Syriac_90_CI_AS_KS");
                ls.Add("3B100200", "Syriac_90_CI_AS_KS_WS");
                ls.Add("3BE00200", "Syriac_90_CS_AI");
                ls.Add("3B600200", "Syriac_90_CS_AI_WS");
                ls.Add("3BA00200", "Syriac_90_CS_AI_KS");
                ls.Add("3B200200", "Syriac_90_CS_AI_KS_WS");
                ls.Add("3BC00200", "Syriac_90_CS_AS");
                ls.Add("3B400200", "Syriac_90_CS_AS_WS");
                ls.Add("3B800200", "Syriac_90_CS_AS_KS");
                ls.Add("3B000200", "Syriac_90_CS_AS_KS_WS");
                ls.Add("3BF10200", "Syriac_90_CI_AI_SC");
                ls.Add("3B710200", "Syriac_90_CI_AI_WS_SC");
                ls.Add("3BB10200", "Syriac_90_CI_AI_KS_SC");
                ls.Add("3B310200", "Syriac_90_CI_AI_KS_WS_SC");
                ls.Add("3BD10200", "Syriac_90_CI_AS_SC");
                ls.Add("3B510200", "Syriac_90_CI_AS_WS_SC");
                ls.Add("3B910200", "Syriac_90_CI_AS_KS_SC");
                ls.Add("3B110200", "Syriac_90_CI_AS_KS_WS_SC");
                ls.Add("3BE10200", "Syriac_90_CS_AI_SC");
                ls.Add("3B610200", "Syriac_90_CS_AI_WS_SC");
                ls.Add("3BA10200", "Syriac_90_CS_AI_KS_SC");
                ls.Add("3B210200", "Syriac_90_CS_AI_KS_WS_SC");
                ls.Add("3BC10200", "Syriac_90_CS_AS_SC");
                ls.Add("3B410200", "Syriac_90_CS_AS_WS_SC");
                ls.Add("3B810200", "Syriac_90_CS_AS_KS_SC");
                ls.Add("3B010200", "Syriac_90_CS_AS_KS_WS_SC");
                ls.Add("3B000500", "Syriac_100_BIN");
                ls.Add("3B080400", "Syriac_100_BIN2");
                ls.Add("3BF00400", "Syriac_100_CI_AI");
                ls.Add("3B700400", "Syriac_100_CI_AI_WS");
                ls.Add("3BB00400", "Syriac_100_CI_AI_KS");
                ls.Add("3B300400", "Syriac_100_CI_AI_KS_WS");
                ls.Add("3BD00400", "Syriac_100_CI_AS");
                ls.Add("3B500400", "Syriac_100_CI_AS_WS");
                ls.Add("3B900400", "Syriac_100_CI_AS_KS");
                ls.Add("3B100400", "Syriac_100_CI_AS_KS_WS");
                ls.Add("3BE00400", "Syriac_100_CS_AI");
                ls.Add("3B600400", "Syriac_100_CS_AI_WS");
                ls.Add("3BA00400", "Syriac_100_CS_AI_KS");
                ls.Add("3B200400", "Syriac_100_CS_AI_KS_WS");
                ls.Add("3BC00400", "Syriac_100_CS_AS");
                ls.Add("3B400400", "Syriac_100_CS_AS_WS");
                ls.Add("3B800400", "Syriac_100_CS_AS_KS");
                ls.Add("3B000400", "Syriac_100_CS_AS_KS_WS");
                ls.Add("3BF10400", "Syriac_100_CI_AI_SC");
                ls.Add("3B710400", "Syriac_100_CI_AI_WS_SC");
                ls.Add("3BB10400", "Syriac_100_CI_AI_KS_SC");
                ls.Add("3B310400", "Syriac_100_CI_AI_KS_WS_SC");
                ls.Add("3BD10400", "Syriac_100_CI_AS_SC");
                ls.Add("3B510400", "Syriac_100_CI_AS_WS_SC");
                ls.Add("3B910400", "Syriac_100_CI_AS_KS_SC");
                ls.Add("3B110400", "Syriac_100_CI_AS_KS_WS_SC");
                ls.Add("3BE10400", "Syriac_100_CS_AI_SC");
                ls.Add("3B610400", "Syriac_100_CS_AI_WS_SC");
                ls.Add("3BA10400", "Syriac_100_CS_AI_KS_SC");
                ls.Add("3B210400", "Syriac_100_CS_AI_KS_WS_SC");
                ls.Add("3BC10400", "Syriac_100_CS_AS_SC");
                ls.Add("3B410400", "Syriac_100_CS_AS_WS_SC");
                ls.Add("3B810400", "Syriac_100_CS_AS_KS_SC");
                ls.Add("3B010400", "Syriac_100_CS_AS_KS_WS_SC");
                ls.Add("61000500", "Tamazight_100_BIN");
                ls.Add("61080400", "Tamazight_100_BIN2");
                ls.Add("61F00400", "Tamazight_100_CI_AI");
                ls.Add("61700400", "Tamazight_100_CI_AI_WS");
                ls.Add("61B00400", "Tamazight_100_CI_AI_KS");
                ls.Add("61300400", "Tamazight_100_CI_AI_KS_WS");
                ls.Add("61D00400", "Tamazight_100_CI_AS");
                ls.Add("61500400", "Tamazight_100_CI_AS_WS");
                ls.Add("61900400", "Tamazight_100_CI_AS_KS");
                ls.Add("61100400", "Tamazight_100_CI_AS_KS_WS");
                ls.Add("61E00400", "Tamazight_100_CS_AI");
                ls.Add("61600400", "Tamazight_100_CS_AI_WS");
                ls.Add("61A00400", "Tamazight_100_CS_AI_KS");
                ls.Add("61200400", "Tamazight_100_CS_AI_KS_WS");
                ls.Add("61C00400", "Tamazight_100_CS_AS");
                ls.Add("61400400", "Tamazight_100_CS_AS_WS");
                ls.Add("61800400", "Tamazight_100_CS_AS_KS");
                ls.Add("61000400", "Tamazight_100_CS_AS_KS_WS");
                ls.Add("61F10400", "Tamazight_100_CI_AI_SC");
                ls.Add("61710400", "Tamazight_100_CI_AI_WS_SC");
                ls.Add("61B10400", "Tamazight_100_CI_AI_KS_SC");
                ls.Add("61310400", "Tamazight_100_CI_AI_KS_WS_SC");
                ls.Add("61D10400", "Tamazight_100_CI_AS_SC");
                ls.Add("61510400", "Tamazight_100_CI_AS_WS_SC");
                ls.Add("61910400", "Tamazight_100_CI_AS_KS_SC");
                ls.Add("61110400", "Tamazight_100_CI_AS_KS_WS_SC");
                ls.Add("61E10400", "Tamazight_100_CS_AI_SC");
                ls.Add("61610400", "Tamazight_100_CS_AI_WS_SC");
                ls.Add("61A10400", "Tamazight_100_CS_AI_KS_SC");
                ls.Add("61210400", "Tamazight_100_CS_AI_KS_WS_SC");
                ls.Add("61C10400", "Tamazight_100_CS_AS_SC");
                ls.Add("61410400", "Tamazight_100_CS_AS_WS_SC");
                ls.Add("61810400", "Tamazight_100_CS_AS_KS_SC");
                ls.Add("61010400", "Tamazight_100_CS_AS_KS_WS_SC");
                ls.Add("39000300", "Tatar_90_BIN");
                ls.Add("39080200", "Tatar_90_BIN2");
                ls.Add("39F00200", "Tatar_90_CI_AI");
                ls.Add("39700200", "Tatar_90_CI_AI_WS");
                ls.Add("39B00200", "Tatar_90_CI_AI_KS");
                ls.Add("39300200", "Tatar_90_CI_AI_KS_WS");
                ls.Add("39D00200", "Tatar_90_CI_AS");
                ls.Add("39500200", "Tatar_90_CI_AS_WS");
                ls.Add("39900200", "Tatar_90_CI_AS_KS");
                ls.Add("39100200", "Tatar_90_CI_AS_KS_WS");
                ls.Add("39E00200", "Tatar_90_CS_AI");
                ls.Add("39600200", "Tatar_90_CS_AI_WS");
                ls.Add("39A00200", "Tatar_90_CS_AI_KS");
                ls.Add("39200200", "Tatar_90_CS_AI_KS_WS");
                ls.Add("39C00200", "Tatar_90_CS_AS");
                ls.Add("39400200", "Tatar_90_CS_AS_WS");
                ls.Add("39800200", "Tatar_90_CS_AS_KS");
                ls.Add("39000200", "Tatar_90_CS_AS_KS_WS");
                ls.Add("39F10200", "Tatar_90_CI_AI_SC");
                ls.Add("39710200", "Tatar_90_CI_AI_WS_SC");
                ls.Add("39B10200", "Tatar_90_CI_AI_KS_SC");
                ls.Add("39310200", "Tatar_90_CI_AI_KS_WS_SC");
                ls.Add("39D10200", "Tatar_90_CI_AS_SC");
                ls.Add("39510200", "Tatar_90_CI_AS_WS_SC");
                ls.Add("39910200", "Tatar_90_CI_AS_KS_SC");
                ls.Add("39110200", "Tatar_90_CI_AS_KS_WS_SC");
                ls.Add("39E10200", "Tatar_90_CS_AI_SC");
                ls.Add("39610200", "Tatar_90_CS_AI_WS_SC");
                ls.Add("39A10200", "Tatar_90_CS_AI_KS_SC");
                ls.Add("39210200", "Tatar_90_CS_AI_KS_WS_SC");
                ls.Add("39C10200", "Tatar_90_CS_AS_SC");
                ls.Add("39410200", "Tatar_90_CS_AS_WS_SC");
                ls.Add("39810200", "Tatar_90_CS_AS_KS_SC");
                ls.Add("39010200", "Tatar_90_CS_AS_KS_WS_SC");
                ls.Add("39000500", "Tatar_100_BIN");
                ls.Add("39080400", "Tatar_100_BIN2");
                ls.Add("39F00400", "Tatar_100_CI_AI");
                ls.Add("39700400", "Tatar_100_CI_AI_WS");
                ls.Add("39B00400", "Tatar_100_CI_AI_KS");
                ls.Add("39300400", "Tatar_100_CI_AI_KS_WS");
                ls.Add("39D00400", "Tatar_100_CI_AS");
                ls.Add("39500400", "Tatar_100_CI_AS_WS");
                ls.Add("39900400", "Tatar_100_CI_AS_KS");
                ls.Add("39100400", "Tatar_100_CI_AS_KS_WS");
                ls.Add("39E00400", "Tatar_100_CS_AI");
                ls.Add("39600400", "Tatar_100_CS_AI_WS");
                ls.Add("39A00400", "Tatar_100_CS_AI_KS");
                ls.Add("39200400", "Tatar_100_CS_AI_KS_WS");
                ls.Add("39C00400", "Tatar_100_CS_AS");
                ls.Add("39400400", "Tatar_100_CS_AS_WS");
                ls.Add("39800400", "Tatar_100_CS_AS_KS");
                ls.Add("39000400", "Tatar_100_CS_AS_KS_WS");
                ls.Add("39F10400", "Tatar_100_CI_AI_SC");
                ls.Add("39710400", "Tatar_100_CI_AI_WS_SC");
                ls.Add("39B10400", "Tatar_100_CI_AI_KS_SC");
                ls.Add("39310400", "Tatar_100_CI_AI_KS_WS_SC");
                ls.Add("39D10400", "Tatar_100_CI_AS_SC");
                ls.Add("39510400", "Tatar_100_CI_AS_WS_SC");
                ls.Add("39910400", "Tatar_100_CI_AS_KS_SC");
                ls.Add("39110400", "Tatar_100_CI_AS_KS_WS_SC");
                ls.Add("39E10400", "Tatar_100_CS_AI_SC");
                ls.Add("39610400", "Tatar_100_CS_AI_WS_SC");
                ls.Add("39A10400", "Tatar_100_CS_AI_KS_SC");
                ls.Add("39210400", "Tatar_100_CS_AI_KS_WS_SC");
                ls.Add("39C10400", "Tatar_100_CS_AS_SC");
                ls.Add("39410400", "Tatar_100_CS_AS_WS_SC");
                ls.Add("39810400", "Tatar_100_CS_AS_KS_SC");
                ls.Add("39010400", "Tatar_100_CS_AS_KS_WS_SC");
                ls.Add("19000100", "Thai_BIN");
                ls.Add("19080000", "Thai_BIN2");
                ls.Add("19F00000", "Thai_CI_AI");
                ls.Add("19700000", "Thai_CI_AI_WS");
                ls.Add("19B00000", "Thai_CI_AI_KS");
                ls.Add("19300000", "Thai_CI_AI_KS_WS");
                ls.Add("19D00000", "Thai_CI_AS");
                ls.Add("19500000", "Thai_CI_AS_WS");
                ls.Add("19900000", "Thai_CI_AS_KS");
                ls.Add("19100000", "Thai_CI_AS_KS_WS");
                ls.Add("19E00000", "Thai_CS_AI");
                ls.Add("19600000", "Thai_CS_AI_WS");
                ls.Add("19A00000", "Thai_CS_AI_KS");
                ls.Add("19200000", "Thai_CS_AI_KS_WS");
                ls.Add("19C00000", "Thai_CS_AS");
                ls.Add("19400000", "Thai_CS_AS_WS");
                ls.Add("19800000", "Thai_CS_AS_KS");
                ls.Add("19000000", "Thai_CS_AS_KS_WS");
                ls.Add("19000500", "Thai_100_BIN");
                ls.Add("19080400", "Thai_100_BIN2");
                ls.Add("19F00400", "Thai_100_CI_AI");
                ls.Add("19700400", "Thai_100_CI_AI_WS");
                ls.Add("19B00400", "Thai_100_CI_AI_KS");
                ls.Add("19300400", "Thai_100_CI_AI_KS_WS");
                ls.Add("19D00400", "Thai_100_CI_AS");
                ls.Add("19500400", "Thai_100_CI_AS_WS");
                ls.Add("19900400", "Thai_100_CI_AS_KS");
                ls.Add("19100400", "Thai_100_CI_AS_KS_WS");
                ls.Add("19E00400", "Thai_100_CS_AI");
                ls.Add("19600400", "Thai_100_CS_AI_WS");
                ls.Add("19A00400", "Thai_100_CS_AI_KS");
                ls.Add("19200400", "Thai_100_CS_AI_KS_WS");
                ls.Add("19C00400", "Thai_100_CS_AS");
                ls.Add("19400400", "Thai_100_CS_AS_WS");
                ls.Add("19800400", "Thai_100_CS_AS_KS");
                ls.Add("19000400", "Thai_100_CS_AS_KS_WS");
                ls.Add("19F10400", "Thai_100_CI_AI_SC");
                ls.Add("19710400", "Thai_100_CI_AI_WS_SC");
                ls.Add("19B10400", "Thai_100_CI_AI_KS_SC");
                ls.Add("19310400", "Thai_100_CI_AI_KS_WS_SC");
                ls.Add("19D10400", "Thai_100_CI_AS_SC");
                ls.Add("19510400", "Thai_100_CI_AS_WS_SC");
                ls.Add("19910400", "Thai_100_CI_AS_KS_SC");
                ls.Add("19110400", "Thai_100_CI_AS_KS_WS_SC");
                ls.Add("19E10400", "Thai_100_CS_AI_SC");
                ls.Add("19610400", "Thai_100_CS_AI_WS_SC");
                ls.Add("19A10400", "Thai_100_CS_AI_KS_SC");
                ls.Add("19210400", "Thai_100_CS_AI_KS_WS_SC");
                ls.Add("19C10400", "Thai_100_CS_AS_SC");
                ls.Add("19410400", "Thai_100_CS_AS_WS_SC");
                ls.Add("19810400", "Thai_100_CS_AS_KS_SC");
                ls.Add("19010400", "Thai_100_CS_AS_KS_WS_SC");
                ls.Add("5C000500", "Tibetan_100_BIN");
                ls.Add("5C080400", "Tibetan_100_BIN2");
                ls.Add("5CF00400", "Tibetan_100_CI_AI");
                ls.Add("5C700400", "Tibetan_100_CI_AI_WS");
                ls.Add("5CB00400", "Tibetan_100_CI_AI_KS");
                ls.Add("5C300400", "Tibetan_100_CI_AI_KS_WS");
                ls.Add("5CD00400", "Tibetan_100_CI_AS");
                ls.Add("5C500400", "Tibetan_100_CI_AS_WS");
                ls.Add("5C900400", "Tibetan_100_CI_AS_KS");
                ls.Add("5C100400", "Tibetan_100_CI_AS_KS_WS");
                ls.Add("5CE00400", "Tibetan_100_CS_AI");
                ls.Add("5C600400", "Tibetan_100_CS_AI_WS");
                ls.Add("5CA00400", "Tibetan_100_CS_AI_KS");
                ls.Add("5C200400", "Tibetan_100_CS_AI_KS_WS");
                ls.Add("5CC00400", "Tibetan_100_CS_AS");
                ls.Add("5C400400", "Tibetan_100_CS_AS_WS");
                ls.Add("5C800400", "Tibetan_100_CS_AS_KS");
                ls.Add("5C000400", "Tibetan_100_CS_AS_KS_WS");
                ls.Add("5CF10400", "Tibetan_100_CI_AI_SC");
                ls.Add("5C710400", "Tibetan_100_CI_AI_WS_SC");
                ls.Add("5CB10400", "Tibetan_100_CI_AI_KS_SC");
                ls.Add("5C310400", "Tibetan_100_CI_AI_KS_WS_SC");
                ls.Add("5CD10400", "Tibetan_100_CI_AS_SC");
                ls.Add("5C510400", "Tibetan_100_CI_AS_WS_SC");
                ls.Add("5C910400", "Tibetan_100_CI_AS_KS_SC");
                ls.Add("5C110400", "Tibetan_100_CI_AS_KS_WS_SC");
                ls.Add("5CE10400", "Tibetan_100_CS_AI_SC");
                ls.Add("5C610400", "Tibetan_100_CS_AI_WS_SC");
                ls.Add("5CA10400", "Tibetan_100_CS_AI_KS_SC");
                ls.Add("5C210400", "Tibetan_100_CS_AI_KS_WS_SC");
                ls.Add("5CC10400", "Tibetan_100_CS_AS_SC");
                ls.Add("5C410400", "Tibetan_100_CS_AS_WS_SC");
                ls.Add("5C810400", "Tibetan_100_CS_AS_KS_SC");
                ls.Add("5C010400", "Tibetan_100_CS_AS_KS_WS_SC");
                ls.Add("09000100", "Traditional_Spanish_BIN");
                ls.Add("09080000", "Traditional_Spanish_BIN2");
                ls.Add("09F00000", "Traditional_Spanish_CI_AI");
                ls.Add("09700000", "Traditional_Spanish_CI_AI_WS");
                ls.Add("09B00000", "Traditional_Spanish_CI_AI_KS");
                ls.Add("09300000", "Traditional_Spanish_CI_AI_KS_WS");
                ls.Add("09D00000", "Traditional_Spanish_CI_AS");
                ls.Add("09500000", "Traditional_Spanish_CI_AS_WS");
                ls.Add("09900000", "Traditional_Spanish_CI_AS_KS");
                ls.Add("09100000", "Traditional_Spanish_CI_AS_KS_WS");
                ls.Add("09E00000", "Traditional_Spanish_CS_AI");
                ls.Add("09600000", "Traditional_Spanish_CS_AI_WS");
                ls.Add("09A00000", "Traditional_Spanish_CS_AI_KS");
                ls.Add("09200000", "Traditional_Spanish_CS_AI_KS_WS");
                ls.Add("09C00000", "Traditional_Spanish_CS_AS");
                ls.Add("09400000", "Traditional_Spanish_CS_AS_WS");
                ls.Add("09800000", "Traditional_Spanish_CS_AS_KS");
                ls.Add("09000000", "Traditional_Spanish_CS_AS_KS_WS");
                ls.Add("09000500", "Traditional_Spanish_100_BIN");
                ls.Add("09080400", "Traditional_Spanish_100_BIN2");
                ls.Add("09F00400", "Traditional_Spanish_100_CI_AI");
                ls.Add("09700400", "Traditional_Spanish_100_CI_AI_WS");
                ls.Add("09B00400", "Traditional_Spanish_100_CI_AI_KS");
                ls.Add("09300400", "Traditional_Spanish_100_CI_AI_KS_WS");
                ls.Add("09D00400", "Traditional_Spanish_100_CI_AS");
                ls.Add("09500400", "Traditional_Spanish_100_CI_AS_WS");
                ls.Add("09900400", "Traditional_Spanish_100_CI_AS_KS");
                ls.Add("09100400", "Traditional_Spanish_100_CI_AS_KS_WS");
                ls.Add("09E00400", "Traditional_Spanish_100_CS_AI");
                ls.Add("09600400", "Traditional_Spanish_100_CS_AI_WS");
                ls.Add("09A00400", "Traditional_Spanish_100_CS_AI_KS");
                ls.Add("09200400", "Traditional_Spanish_100_CS_AI_KS_WS");
                ls.Add("09C00400", "Traditional_Spanish_100_CS_AS");
                ls.Add("09400400", "Traditional_Spanish_100_CS_AS_WS");
                ls.Add("09800400", "Traditional_Spanish_100_CS_AS_KS");
                ls.Add("09000400", "Traditional_Spanish_100_CS_AS_KS_WS");
                ls.Add("09F10400", "Traditional_Spanish_100_CI_AI_SC");
                ls.Add("09710400", "Traditional_Spanish_100_CI_AI_WS_SC");
                ls.Add("09B10400", "Traditional_Spanish_100_CI_AI_KS_SC");
                ls.Add("09310400", "Traditional_Spanish_100_CI_AI_KS_WS_SC");
                ls.Add("09D10400", "Traditional_Spanish_100_CI_AS_SC");
                ls.Add("09510400", "Traditional_Spanish_100_CI_AS_WS_SC");
                ls.Add("09910400", "Traditional_Spanish_100_CI_AS_KS_SC");
                ls.Add("09110400", "Traditional_Spanish_100_CI_AS_KS_WS_SC");
                ls.Add("09E10400", "Traditional_Spanish_100_CS_AI_SC");
                ls.Add("09610400", "Traditional_Spanish_100_CS_AI_WS_SC");
                ls.Add("09A10400", "Traditional_Spanish_100_CS_AI_KS_SC");
                ls.Add("09210400", "Traditional_Spanish_100_CS_AI_KS_WS_SC");
                ls.Add("09C10400", "Traditional_Spanish_100_CS_AS_SC");
                ls.Add("09410400", "Traditional_Spanish_100_CS_AS_WS_SC");
                ls.Add("09810400", "Traditional_Spanish_100_CS_AS_KS_SC");
                ls.Add("09010400", "Traditional_Spanish_100_CS_AS_KS_WS_SC");
                ls.Add("1A000100", "Turkish_BIN");
                ls.Add("1A080000", "Turkish_BIN2");
                ls.Add("1AF00000", "Turkish_CI_AI");
                ls.Add("1A700000", "Turkish_CI_AI_WS");
                ls.Add("1AB00000", "Turkish_CI_AI_KS");
                ls.Add("1A300000", "Turkish_CI_AI_KS_WS");
                ls.Add("1AD00000", "Turkish_CI_AS");
                ls.Add("1A500000", "Turkish_CI_AS_WS");
                ls.Add("1A900000", "Turkish_CI_AS_KS");
                ls.Add("1A100000", "Turkish_CI_AS_KS_WS");
                ls.Add("1AE00000", "Turkish_CS_AI");
                ls.Add("1A600000", "Turkish_CS_AI_WS");
                ls.Add("1AA00000", "Turkish_CS_AI_KS");
                ls.Add("1A200000", "Turkish_CS_AI_KS_WS");
                ls.Add("1AC00000", "Turkish_CS_AS");
                ls.Add("1A400000", "Turkish_CS_AS_WS");
                ls.Add("1A800000", "Turkish_CS_AS_KS");
                ls.Add("1A000000", "Turkish_CS_AS_KS_WS");
                ls.Add("1A000500", "Turkish_100_BIN");
                ls.Add("1A080400", "Turkish_100_BIN2");
                ls.Add("1AF00400", "Turkish_100_CI_AI");
                ls.Add("1A700400", "Turkish_100_CI_AI_WS");
                ls.Add("1AB00400", "Turkish_100_CI_AI_KS");
                ls.Add("1A300400", "Turkish_100_CI_AI_KS_WS");
                ls.Add("1AD00400", "Turkish_100_CI_AS");
                ls.Add("1A500400", "Turkish_100_CI_AS_WS");
                ls.Add("1A900400", "Turkish_100_CI_AS_KS");
                ls.Add("1A100400", "Turkish_100_CI_AS_KS_WS");
                ls.Add("1AE00400", "Turkish_100_CS_AI");
                ls.Add("1A600400", "Turkish_100_CS_AI_WS");
                ls.Add("1AA00400", "Turkish_100_CS_AI_KS");
                ls.Add("1A200400", "Turkish_100_CS_AI_KS_WS");
                ls.Add("1AC00400", "Turkish_100_CS_AS");
                ls.Add("1A400400", "Turkish_100_CS_AS_WS");
                ls.Add("1A800400", "Turkish_100_CS_AS_KS");
                ls.Add("1A000400", "Turkish_100_CS_AS_KS_WS");
                ls.Add("1AF10400", "Turkish_100_CI_AI_SC");
                ls.Add("1A710400", "Turkish_100_CI_AI_WS_SC");
                ls.Add("1AB10400", "Turkish_100_CI_AI_KS_SC");
                ls.Add("1A310400", "Turkish_100_CI_AI_KS_WS_SC");
                ls.Add("1AD10400", "Turkish_100_CI_AS_SC");
                ls.Add("1A510400", "Turkish_100_CI_AS_WS_SC");
                ls.Add("1A910400", "Turkish_100_CI_AS_KS_SC");
                ls.Add("1A110400", "Turkish_100_CI_AS_KS_WS_SC");
                ls.Add("1AE10400", "Turkish_100_CS_AI_SC");
                ls.Add("1A610400", "Turkish_100_CS_AI_WS_SC");
                ls.Add("1AA10400", "Turkish_100_CS_AI_KS_SC");
                ls.Add("1A210400", "Turkish_100_CS_AI_KS_WS_SC");
                ls.Add("1AC10400", "Turkish_100_CS_AS_SC");
                ls.Add("1A410400", "Turkish_100_CS_AS_WS_SC");
                ls.Add("1A810400", "Turkish_100_CS_AS_KS_SC");
                ls.Add("1A010400", "Turkish_100_CS_AS_KS_WS_SC");
                ls.Add("58000500", "Turkmen_100_BIN");
                ls.Add("58080400", "Turkmen_100_BIN2");
                ls.Add("58F00400", "Turkmen_100_CI_AI");
                ls.Add("58700400", "Turkmen_100_CI_AI_WS");
                ls.Add("58B00400", "Turkmen_100_CI_AI_KS");
                ls.Add("58300400", "Turkmen_100_CI_AI_KS_WS");
                ls.Add("58D00400", "Turkmen_100_CI_AS");
                ls.Add("58500400", "Turkmen_100_CI_AS_WS");
                ls.Add("58900400", "Turkmen_100_CI_AS_KS");
                ls.Add("58100400", "Turkmen_100_CI_AS_KS_WS");
                ls.Add("58E00400", "Turkmen_100_CS_AI");
                ls.Add("58600400", "Turkmen_100_CS_AI_WS");
                ls.Add("58A00400", "Turkmen_100_CS_AI_KS");
                ls.Add("58200400", "Turkmen_100_CS_AI_KS_WS");
                ls.Add("58C00400", "Turkmen_100_CS_AS");
                ls.Add("58400400", "Turkmen_100_CS_AS_WS");
                ls.Add("58800400", "Turkmen_100_CS_AS_KS");
                ls.Add("58000400", "Turkmen_100_CS_AS_KS_WS");
                ls.Add("58F10400", "Turkmen_100_CI_AI_SC");
                ls.Add("58710400", "Turkmen_100_CI_AI_WS_SC");
                ls.Add("58B10400", "Turkmen_100_CI_AI_KS_SC");
                ls.Add("58310400", "Turkmen_100_CI_AI_KS_WS_SC");
                ls.Add("58D10400", "Turkmen_100_CI_AS_SC");
                ls.Add("58510400", "Turkmen_100_CI_AS_WS_SC");
                ls.Add("58910400", "Turkmen_100_CI_AS_KS_SC");
                ls.Add("58110400", "Turkmen_100_CI_AS_KS_WS_SC");
                ls.Add("58E10400", "Turkmen_100_CS_AI_SC");
                ls.Add("58610400", "Turkmen_100_CS_AI_WS_SC");
                ls.Add("58A10400", "Turkmen_100_CS_AI_KS_SC");
                ls.Add("58210400", "Turkmen_100_CS_AI_KS_WS_SC");
                ls.Add("58C10400", "Turkmen_100_CS_AS_SC");
                ls.Add("58410400", "Turkmen_100_CS_AS_WS_SC");
                ls.Add("58810400", "Turkmen_100_CS_AS_KS_SC");
                ls.Add("58010400", "Turkmen_100_CS_AS_KS_WS_SC");
                ls.Add("25000500", "Uighur_100_BIN");
                ls.Add("25080400", "Uighur_100_BIN2");
                ls.Add("25F00400", "Uighur_100_CI_AI");
                ls.Add("25700400", "Uighur_100_CI_AI_WS");
                ls.Add("25B00400", "Uighur_100_CI_AI_KS");
                ls.Add("25300400", "Uighur_100_CI_AI_KS_WS");
                ls.Add("25D00400", "Uighur_100_CI_AS");
                ls.Add("25500400", "Uighur_100_CI_AS_WS");
                ls.Add("25900400", "Uighur_100_CI_AS_KS");
                ls.Add("25100400", "Uighur_100_CI_AS_KS_WS");
                ls.Add("25E00400", "Uighur_100_CS_AI");
                ls.Add("25600400", "Uighur_100_CS_AI_WS");
                ls.Add("25A00400", "Uighur_100_CS_AI_KS");
                ls.Add("25200400", "Uighur_100_CS_AI_KS_WS");
                ls.Add("25C00400", "Uighur_100_CS_AS");
                ls.Add("25400400", "Uighur_100_CS_AS_WS");
                ls.Add("25800400", "Uighur_100_CS_AS_KS");
                ls.Add("25000400", "Uighur_100_CS_AS_KS_WS");
                ls.Add("25F10400", "Uighur_100_CI_AI_SC");
                ls.Add("25710400", "Uighur_100_CI_AI_WS_SC");
                ls.Add("25B10400", "Uighur_100_CI_AI_KS_SC");
                ls.Add("25310400", "Uighur_100_CI_AI_KS_WS_SC");
                ls.Add("25D10400", "Uighur_100_CI_AS_SC");
                ls.Add("25510400", "Uighur_100_CI_AS_WS_SC");
                ls.Add("25910400", "Uighur_100_CI_AS_KS_SC");
                ls.Add("25110400", "Uighur_100_CI_AS_KS_WS_SC");
                ls.Add("25E10400", "Uighur_100_CS_AI_SC");
                ls.Add("25610400", "Uighur_100_CS_AI_WS_SC");
                ls.Add("25A10400", "Uighur_100_CS_AI_KS_SC");
                ls.Add("25210400", "Uighur_100_CS_AI_KS_WS_SC");
                ls.Add("25C10400", "Uighur_100_CS_AS_SC");
                ls.Add("25410400", "Uighur_100_CS_AS_WS_SC");
                ls.Add("25810400", "Uighur_100_CS_AS_KS_SC");
                ls.Add("25010400", "Uighur_100_CS_AS_KS_WS_SC");
                ls.Add("1B000100", "Ukrainian_BIN");
                ls.Add("1B080000", "Ukrainian_BIN2");
                ls.Add("1BF00000", "Ukrainian_CI_AI");
                ls.Add("1B700000", "Ukrainian_CI_AI_WS");
                ls.Add("1BB00000", "Ukrainian_CI_AI_KS");
                ls.Add("1B300000", "Ukrainian_CI_AI_KS_WS");
                ls.Add("1BD00000", "Ukrainian_CI_AS");
                ls.Add("1B500000", "Ukrainian_CI_AS_WS");
                ls.Add("1B900000", "Ukrainian_CI_AS_KS");
                ls.Add("1B100000", "Ukrainian_CI_AS_KS_WS");
                ls.Add("1BE00000", "Ukrainian_CS_AI");
                ls.Add("1B600000", "Ukrainian_CS_AI_WS");
                ls.Add("1BA00000", "Ukrainian_CS_AI_KS");
                ls.Add("1B200000", "Ukrainian_CS_AI_KS_WS");
                ls.Add("1BC00000", "Ukrainian_CS_AS");
                ls.Add("1B400000", "Ukrainian_CS_AS_WS");
                ls.Add("1B800000", "Ukrainian_CS_AS_KS");
                ls.Add("1B000000", "Ukrainian_CS_AS_KS_WS");
                ls.Add("1B000500", "Ukrainian_100_BIN");
                ls.Add("1B080400", "Ukrainian_100_BIN2");
                ls.Add("1BF00400", "Ukrainian_100_CI_AI");
                ls.Add("1B700400", "Ukrainian_100_CI_AI_WS");
                ls.Add("1BB00400", "Ukrainian_100_CI_AI_KS");
                ls.Add("1B300400", "Ukrainian_100_CI_AI_KS_WS");
                ls.Add("1BD00400", "Ukrainian_100_CI_AS");
                ls.Add("1B500400", "Ukrainian_100_CI_AS_WS");
                ls.Add("1B900400", "Ukrainian_100_CI_AS_KS");
                ls.Add("1B100400", "Ukrainian_100_CI_AS_KS_WS");
                ls.Add("1BE00400", "Ukrainian_100_CS_AI");
                ls.Add("1B600400", "Ukrainian_100_CS_AI_WS");
                ls.Add("1BA00400", "Ukrainian_100_CS_AI_KS");
                ls.Add("1B200400", "Ukrainian_100_CS_AI_KS_WS");
                ls.Add("1BC00400", "Ukrainian_100_CS_AS");
                ls.Add("1B400400", "Ukrainian_100_CS_AS_WS");
                ls.Add("1B800400", "Ukrainian_100_CS_AS_KS");
                ls.Add("1B000400", "Ukrainian_100_CS_AS_KS_WS");
                ls.Add("1BF10400", "Ukrainian_100_CI_AI_SC");
                ls.Add("1B710400", "Ukrainian_100_CI_AI_WS_SC");
                ls.Add("1BB10400", "Ukrainian_100_CI_AI_KS_SC");
                ls.Add("1B310400", "Ukrainian_100_CI_AI_KS_WS_SC");
                ls.Add("1BD10400", "Ukrainian_100_CI_AS_SC");
                ls.Add("1B510400", "Ukrainian_100_CI_AS_WS_SC");
                ls.Add("1B910400", "Ukrainian_100_CI_AS_KS_SC");
                ls.Add("1B110400", "Ukrainian_100_CI_AS_KS_WS_SC");
                ls.Add("1BE10400", "Ukrainian_100_CS_AI_SC");
                ls.Add("1B610400", "Ukrainian_100_CS_AI_WS_SC");
                ls.Add("1BA10400", "Ukrainian_100_CS_AI_KS_SC");
                ls.Add("1B210400", "Ukrainian_100_CS_AI_KS_WS_SC");
                ls.Add("1BC10400", "Ukrainian_100_CS_AS_SC");
                ls.Add("1B410400", "Ukrainian_100_CS_AS_WS_SC");
                ls.Add("1B810400", "Ukrainian_100_CS_AS_KS_SC");
                ls.Add("1B010400", "Ukrainian_100_CS_AS_KS_WS_SC");
                ls.Add("53000500", "Upper_Sorbian_100_BIN");
                ls.Add("53080400", "Upper_Sorbian_100_BIN2");
                ls.Add("53F00400", "Upper_Sorbian_100_CI_AI");
                ls.Add("53700400", "Upper_Sorbian_100_CI_AI_WS");
                ls.Add("53B00400", "Upper_Sorbian_100_CI_AI_KS");
                ls.Add("53300400", "Upper_Sorbian_100_CI_AI_KS_WS");
                ls.Add("53D00400", "Upper_Sorbian_100_CI_AS");
                ls.Add("53500400", "Upper_Sorbian_100_CI_AS_WS");
                ls.Add("53900400", "Upper_Sorbian_100_CI_AS_KS");
                ls.Add("53100400", "Upper_Sorbian_100_CI_AS_KS_WS");
                ls.Add("53E00400", "Upper_Sorbian_100_CS_AI");
                ls.Add("53600400", "Upper_Sorbian_100_CS_AI_WS");
                ls.Add("53A00400", "Upper_Sorbian_100_CS_AI_KS");
                ls.Add("53200400", "Upper_Sorbian_100_CS_AI_KS_WS");
                ls.Add("53C00400", "Upper_Sorbian_100_CS_AS");
                ls.Add("53400400", "Upper_Sorbian_100_CS_AS_WS");
                ls.Add("53800400", "Upper_Sorbian_100_CS_AS_KS");
                ls.Add("53000400", "Upper_Sorbian_100_CS_AS_KS_WS");
                ls.Add("53F10400", "Upper_Sorbian_100_CI_AI_SC");
                ls.Add("53710400", "Upper_Sorbian_100_CI_AI_WS_SC");
                ls.Add("53B10400", "Upper_Sorbian_100_CI_AI_KS_SC");
                ls.Add("53310400", "Upper_Sorbian_100_CI_AI_KS_WS_SC");
                ls.Add("53D10400", "Upper_Sorbian_100_CI_AS_SC");
                ls.Add("53510400", "Upper_Sorbian_100_CI_AS_WS_SC");
                ls.Add("53910400", "Upper_Sorbian_100_CI_AS_KS_SC");
                ls.Add("53110400", "Upper_Sorbian_100_CI_AS_KS_WS_SC");
                ls.Add("53E10400", "Upper_Sorbian_100_CS_AI_SC");
                ls.Add("53610400", "Upper_Sorbian_100_CS_AI_WS_SC");
                ls.Add("53A10400", "Upper_Sorbian_100_CS_AI_KS_SC");
                ls.Add("53210400", "Upper_Sorbian_100_CS_AI_KS_WS_SC");
                ls.Add("53C10400", "Upper_Sorbian_100_CS_AS_SC");
                ls.Add("53410400", "Upper_Sorbian_100_CS_AS_WS_SC");
                ls.Add("53810400", "Upper_Sorbian_100_CS_AS_KS_SC");
                ls.Add("53010400", "Upper_Sorbian_100_CS_AS_KS_WS_SC");
                ls.Add("50000500", "Urdu_100_BIN");
                ls.Add("50080400", "Urdu_100_BIN2");
                ls.Add("50F00400", "Urdu_100_CI_AI");
                ls.Add("50700400", "Urdu_100_CI_AI_WS");
                ls.Add("50B00400", "Urdu_100_CI_AI_KS");
                ls.Add("50300400", "Urdu_100_CI_AI_KS_WS");
                ls.Add("50D00400", "Urdu_100_CI_AS");
                ls.Add("50500400", "Urdu_100_CI_AS_WS");
                ls.Add("50900400", "Urdu_100_CI_AS_KS");
                ls.Add("50100400", "Urdu_100_CI_AS_KS_WS");
                ls.Add("50E00400", "Urdu_100_CS_AI");
                ls.Add("50600400", "Urdu_100_CS_AI_WS");
                ls.Add("50A00400", "Urdu_100_CS_AI_KS");
                ls.Add("50200400", "Urdu_100_CS_AI_KS_WS");
                ls.Add("50C00400", "Urdu_100_CS_AS");
                ls.Add("50400400", "Urdu_100_CS_AS_WS");
                ls.Add("50800400", "Urdu_100_CS_AS_KS");
                ls.Add("50000400", "Urdu_100_CS_AS_KS_WS");
                ls.Add("50F10400", "Urdu_100_CI_AI_SC");
                ls.Add("50710400", "Urdu_100_CI_AI_WS_SC");
                ls.Add("50B10400", "Urdu_100_CI_AI_KS_SC");
                ls.Add("50310400", "Urdu_100_CI_AI_KS_WS_SC");
                ls.Add("50D10400", "Urdu_100_CI_AS_SC");
                ls.Add("50510400", "Urdu_100_CI_AS_WS_SC");
                ls.Add("50910400", "Urdu_100_CI_AS_KS_SC");
                ls.Add("50110400", "Urdu_100_CI_AS_KS_WS_SC");
                ls.Add("50E10400", "Urdu_100_CS_AI_SC");
                ls.Add("50610400", "Urdu_100_CS_AI_WS_SC");
                ls.Add("50A10400", "Urdu_100_CS_AI_KS_SC");
                ls.Add("50210400", "Urdu_100_CS_AI_KS_WS_SC");
                ls.Add("50C10400", "Urdu_100_CS_AS_SC");
                ls.Add("50410400", "Urdu_100_CS_AS_WS_SC");
                ls.Add("50810400", "Urdu_100_CS_AS_KS_SC");
                ls.Add("50010400", "Urdu_100_CS_AS_KS_WS_SC");
                ls.Add("38000300", "Uzbek_Latin_90_BIN");
                ls.Add("38080200", "Uzbek_Latin_90_BIN2");
                ls.Add("38F00200", "Uzbek_Latin_90_CI_AI");
                ls.Add("38700200", "Uzbek_Latin_90_CI_AI_WS");
                ls.Add("38B00200", "Uzbek_Latin_90_CI_AI_KS");
                ls.Add("38300200", "Uzbek_Latin_90_CI_AI_KS_WS");
                ls.Add("38D00200", "Uzbek_Latin_90_CI_AS");
                ls.Add("38500200", "Uzbek_Latin_90_CI_AS_WS");
                ls.Add("38900200", "Uzbek_Latin_90_CI_AS_KS");
                ls.Add("38100200", "Uzbek_Latin_90_CI_AS_KS_WS");
                ls.Add("38E00200", "Uzbek_Latin_90_CS_AI");
                ls.Add("38600200", "Uzbek_Latin_90_CS_AI_WS");
                ls.Add("38A00200", "Uzbek_Latin_90_CS_AI_KS");
                ls.Add("38200200", "Uzbek_Latin_90_CS_AI_KS_WS");
                ls.Add("38C00200", "Uzbek_Latin_90_CS_AS");
                ls.Add("38400200", "Uzbek_Latin_90_CS_AS_WS");
                ls.Add("38800200", "Uzbek_Latin_90_CS_AS_KS");
                ls.Add("38000200", "Uzbek_Latin_90_CS_AS_KS_WS");
                ls.Add("38F10200", "Uzbek_Latin_90_CI_AI_SC");
                ls.Add("38710200", "Uzbek_Latin_90_CI_AI_WS_SC");
                ls.Add("38B10200", "Uzbek_Latin_90_CI_AI_KS_SC");
                ls.Add("38310200", "Uzbek_Latin_90_CI_AI_KS_WS_SC");
                ls.Add("38D10200", "Uzbek_Latin_90_CI_AS_SC");
                ls.Add("38510200", "Uzbek_Latin_90_CI_AS_WS_SC");
                ls.Add("38910200", "Uzbek_Latin_90_CI_AS_KS_SC");
                ls.Add("38110200", "Uzbek_Latin_90_CI_AS_KS_WS_SC");
                ls.Add("38E10200", "Uzbek_Latin_90_CS_AI_SC");
                ls.Add("38610200", "Uzbek_Latin_90_CS_AI_WS_SC");
                ls.Add("38A10200", "Uzbek_Latin_90_CS_AI_KS_SC");
                ls.Add("38210200", "Uzbek_Latin_90_CS_AI_KS_WS_SC");
                ls.Add("38C10200", "Uzbek_Latin_90_CS_AS_SC");
                ls.Add("38410200", "Uzbek_Latin_90_CS_AS_WS_SC");
                ls.Add("38810200", "Uzbek_Latin_90_CS_AS_KS_SC");
                ls.Add("38010200", "Uzbek_Latin_90_CS_AS_KS_WS_SC");
                ls.Add("38000500", "Uzbek_Latin_100_BIN");
                ls.Add("38080400", "Uzbek_Latin_100_BIN2");
                ls.Add("38F00400", "Uzbek_Latin_100_CI_AI");
                ls.Add("38700400", "Uzbek_Latin_100_CI_AI_WS");
                ls.Add("38B00400", "Uzbek_Latin_100_CI_AI_KS");
                ls.Add("38300400", "Uzbek_Latin_100_CI_AI_KS_WS");
                ls.Add("38D00400", "Uzbek_Latin_100_CI_AS");
                ls.Add("38500400", "Uzbek_Latin_100_CI_AS_WS");
                ls.Add("38900400", "Uzbek_Latin_100_CI_AS_KS");
                ls.Add("38100400", "Uzbek_Latin_100_CI_AS_KS_WS");
                ls.Add("38E00400", "Uzbek_Latin_100_CS_AI");
                ls.Add("38600400", "Uzbek_Latin_100_CS_AI_WS");
                ls.Add("38A00400", "Uzbek_Latin_100_CS_AI_KS");
                ls.Add("38200400", "Uzbek_Latin_100_CS_AI_KS_WS");
                ls.Add("38C00400", "Uzbek_Latin_100_CS_AS");
                ls.Add("38400400", "Uzbek_Latin_100_CS_AS_WS");
                ls.Add("38800400", "Uzbek_Latin_100_CS_AS_KS");
                ls.Add("38000400", "Uzbek_Latin_100_CS_AS_KS_WS");
                ls.Add("38F10400", "Uzbek_Latin_100_CI_AI_SC");
                ls.Add("38710400", "Uzbek_Latin_100_CI_AI_WS_SC");
                ls.Add("38B10400", "Uzbek_Latin_100_CI_AI_KS_SC");
                ls.Add("38310400", "Uzbek_Latin_100_CI_AI_KS_WS_SC");
                ls.Add("38D10400", "Uzbek_Latin_100_CI_AS_SC");
                ls.Add("38510400", "Uzbek_Latin_100_CI_AS_WS_SC");
                ls.Add("38910400", "Uzbek_Latin_100_CI_AS_KS_SC");
                ls.Add("38110400", "Uzbek_Latin_100_CI_AS_KS_WS_SC");
                ls.Add("38E10400", "Uzbek_Latin_100_CS_AI_SC");
                ls.Add("38610400", "Uzbek_Latin_100_CS_AI_WS_SC");
                ls.Add("38A10400", "Uzbek_Latin_100_CS_AI_KS_SC");
                ls.Add("38210400", "Uzbek_Latin_100_CS_AI_KS_WS_SC");
                ls.Add("38C10400", "Uzbek_Latin_100_CS_AS_SC");
                ls.Add("38410400", "Uzbek_Latin_100_CS_AS_WS_SC");
                ls.Add("38810400", "Uzbek_Latin_100_CS_AS_KS_SC");
                ls.Add("38010400", "Uzbek_Latin_100_CS_AS_KS_WS_SC");
                ls.Add("20000100", "Vietnamese_BIN");
                ls.Add("20080000", "Vietnamese_BIN2");
                ls.Add("20F00000", "Vietnamese_CI_AI");
                ls.Add("20700000", "Vietnamese_CI_AI_WS");
                ls.Add("20B00000", "Vietnamese_CI_AI_KS");
                ls.Add("20300000", "Vietnamese_CI_AI_KS_WS");
                ls.Add("20D00000", "Vietnamese_CI_AS");
                ls.Add("20500000", "Vietnamese_CI_AS_WS");
                ls.Add("20900000", "Vietnamese_CI_AS_KS");
                ls.Add("20100000", "Vietnamese_CI_AS_KS_WS");
                ls.Add("20E00000", "Vietnamese_CS_AI");
                ls.Add("20600000", "Vietnamese_CS_AI_WS");
                ls.Add("20A00000", "Vietnamese_CS_AI_KS");
                ls.Add("20200000", "Vietnamese_CS_AI_KS_WS");
                ls.Add("20C00000", "Vietnamese_CS_AS");
                ls.Add("20400000", "Vietnamese_CS_AS_WS");
                ls.Add("20800000", "Vietnamese_CS_AS_KS");
                ls.Add("20000000", "Vietnamese_CS_AS_KS_WS");
                ls.Add("20000500", "Vietnamese_100_BIN");
                ls.Add("20080400", "Vietnamese_100_BIN2");
                ls.Add("20F00400", "Vietnamese_100_CI_AI");
                ls.Add("20700400", "Vietnamese_100_CI_AI_WS");
                ls.Add("20B00400", "Vietnamese_100_CI_AI_KS");
                ls.Add("20300400", "Vietnamese_100_CI_AI_KS_WS");
                ls.Add("20D00400", "Vietnamese_100_CI_AS");
                ls.Add("20500400", "Vietnamese_100_CI_AS_WS");
                ls.Add("20900400", "Vietnamese_100_CI_AS_KS");
                ls.Add("20100400", "Vietnamese_100_CI_AS_KS_WS");
                ls.Add("20E00400", "Vietnamese_100_CS_AI");
                ls.Add("20600400", "Vietnamese_100_CS_AI_WS");
                ls.Add("20A00400", "Vietnamese_100_CS_AI_KS");
                ls.Add("20200400", "Vietnamese_100_CS_AI_KS_WS");
                ls.Add("20C00400", "Vietnamese_100_CS_AS");
                ls.Add("20400400", "Vietnamese_100_CS_AS_WS");
                ls.Add("20800400", "Vietnamese_100_CS_AS_KS");
                ls.Add("20000400", "Vietnamese_100_CS_AS_KS_WS");
                ls.Add("20F10400", "Vietnamese_100_CI_AI_SC");
                ls.Add("20710400", "Vietnamese_100_CI_AI_WS_SC");
                ls.Add("20B10400", "Vietnamese_100_CI_AI_KS_SC");
                ls.Add("20310400", "Vietnamese_100_CI_AI_KS_WS_SC");
                ls.Add("20D10400", "Vietnamese_100_CI_AS_SC");
                ls.Add("20510400", "Vietnamese_100_CI_AS_WS_SC");
                ls.Add("20910400", "Vietnamese_100_CI_AS_KS_SC");
                ls.Add("20110400", "Vietnamese_100_CI_AS_KS_WS_SC");
                ls.Add("20E10400", "Vietnamese_100_CS_AI_SC");
                ls.Add("20610400", "Vietnamese_100_CS_AI_WS_SC");
                ls.Add("20A10400", "Vietnamese_100_CS_AI_KS_SC");
                ls.Add("20210400", "Vietnamese_100_CS_AI_KS_WS_SC");
                ls.Add("20C10400", "Vietnamese_100_CS_AS_SC");
                ls.Add("20410400", "Vietnamese_100_CS_AS_WS_SC");
                ls.Add("20810400", "Vietnamese_100_CS_AS_KS_SC");
                ls.Add("20010400", "Vietnamese_100_CS_AS_KS_WS_SC");
                ls.Add("5D000500", "Welsh_100_BIN");
                ls.Add("5D080400", "Welsh_100_BIN2");
                ls.Add("5DF00400", "Welsh_100_CI_AI");
                ls.Add("5D700400", "Welsh_100_CI_AI_WS");
                ls.Add("5DB00400", "Welsh_100_CI_AI_KS");
                ls.Add("5D300400", "Welsh_100_CI_AI_KS_WS");
                ls.Add("5DD00400", "Welsh_100_CI_AS");
                ls.Add("5D500400", "Welsh_100_CI_AS_WS");
                ls.Add("5D900400", "Welsh_100_CI_AS_KS");
                ls.Add("5D100400", "Welsh_100_CI_AS_KS_WS");
                ls.Add("5DE00400", "Welsh_100_CS_AI");
                ls.Add("5D600400", "Welsh_100_CS_AI_WS");
                ls.Add("5DA00400", "Welsh_100_CS_AI_KS");
                ls.Add("5D200400", "Welsh_100_CS_AI_KS_WS");
                ls.Add("5DC00400", "Welsh_100_CS_AS");
                ls.Add("5D400400", "Welsh_100_CS_AS_WS");
                ls.Add("5D800400", "Welsh_100_CS_AS_KS");
                ls.Add("5D000400", "Welsh_100_CS_AS_KS_WS");
                ls.Add("5DF10400", "Welsh_100_CI_AI_SC");
                ls.Add("5D710400", "Welsh_100_CI_AI_WS_SC");
                ls.Add("5DB10400", "Welsh_100_CI_AI_KS_SC");
                ls.Add("5D310400", "Welsh_100_CI_AI_KS_WS_SC");
                ls.Add("5DD10400", "Welsh_100_CI_AS_SC");
                ls.Add("5D510400", "Welsh_100_CI_AS_WS_SC");
                ls.Add("5D910400", "Welsh_100_CI_AS_KS_SC");
                ls.Add("5D110400", "Welsh_100_CI_AS_KS_WS_SC");
                ls.Add("5DE10400", "Welsh_100_CS_AI_SC");
                ls.Add("5D610400", "Welsh_100_CS_AI_WS_SC");
                ls.Add("5DA10400", "Welsh_100_CS_AI_KS_SC");
                ls.Add("5D210400", "Welsh_100_CS_AI_KS_WS_SC");
                ls.Add("5DC10400", "Welsh_100_CS_AS_SC");
                ls.Add("5D410400", "Welsh_100_CS_AS_WS_SC");
                ls.Add("5D810400", "Welsh_100_CS_AS_KS_SC");
                ls.Add("5D010400", "Welsh_100_CS_AS_KS_WS_SC");
                ls.Add("06000500", "Yakut_100_BIN");
                ls.Add("06080400", "Yakut_100_BIN2");
                ls.Add("06F00400", "Yakut_100_CI_AI");
                ls.Add("06700400", "Yakut_100_CI_AI_WS");
                ls.Add("06B00400", "Yakut_100_CI_AI_KS");
                ls.Add("06300400", "Yakut_100_CI_AI_KS_WS");
                ls.Add("06D00400", "Yakut_100_CI_AS");
                ls.Add("06500400", "Yakut_100_CI_AS_WS");
                ls.Add("06900400", "Yakut_100_CI_AS_KS");
                ls.Add("06100400", "Yakut_100_CI_AS_KS_WS");
                ls.Add("06E00400", "Yakut_100_CS_AI");
                ls.Add("06600400", "Yakut_100_CS_AI_WS");
                ls.Add("06A00400", "Yakut_100_CS_AI_KS");
                ls.Add("06200400", "Yakut_100_CS_AI_KS_WS");
                ls.Add("06C00400", "Yakut_100_CS_AS");
                ls.Add("06400400", "Yakut_100_CS_AS_WS");
                ls.Add("06800400", "Yakut_100_CS_AS_KS");
                ls.Add("06000400", "Yakut_100_CS_AS_KS_WS");
                ls.Add("06F10400", "Yakut_100_CI_AI_SC");
                ls.Add("06710400", "Yakut_100_CI_AI_WS_SC");
                ls.Add("06B10400", "Yakut_100_CI_AI_KS_SC");
                ls.Add("06310400", "Yakut_100_CI_AI_KS_WS_SC");
                ls.Add("06D10400", "Yakut_100_CI_AS_SC");
                ls.Add("06510400", "Yakut_100_CI_AS_WS_SC");
                ls.Add("06910400", "Yakut_100_CI_AS_KS_SC");
                ls.Add("06110400", "Yakut_100_CI_AS_KS_WS_SC");
                ls.Add("06E10400", "Yakut_100_CS_AI_SC");
                ls.Add("06610400", "Yakut_100_CS_AI_WS_SC");
                ls.Add("06A10400", "Yakut_100_CS_AI_KS_SC");
                ls.Add("06210400", "Yakut_100_CS_AI_KS_WS_SC");
                ls.Add("06C10400", "Yakut_100_CS_AS_SC");
                ls.Add("06410400", "Yakut_100_CS_AS_WS_SC");
                ls.Add("06810400", "Yakut_100_CS_AS_KS_SC");
                ls.Add("06010400", "Yakut_100_CS_AS_KS_WS_SC");
                ls.Add("08D00031", "SQL_1xCompat_CP850_CI_AS");
                ls.Add("08F00039", "SQL_AltDiction_CP850_CI_AI");
                ls.Add("08D0003D", "SQL_AltDiction_CP850_CI_AS");
                ls.Add("08C00037", "SQL_AltDiction_CP850_CS_AS");
                ls.Add("08D00038", "SQL_AltDiction_Pref_CP850_CI_AS");
                ls.Add("08C0007A", "SQL_AltDiction2_CP1253_CS_AS");
                ls.Add("16D0005C", "SQL_Croatian_CP1250_CI_AS");
                ls.Add("16C0005B", "SQL_Croatian_CP1250_CS_AS");
                ls.Add("04D00054", "SQL_Czech_CP1250_CI_AS");
                ls.Add("04C00053", "SQL_Czech_CP1250_CS_AS");
                ls.Add("05D000B7", "SQL_Danish_Pref_CP1_CI_AS");
                ls.Add("08C000D2", "SQL_EBCDIC037_CP1_CS_AS");
                ls.Add("29C000DB", "SQL_EBCDIC1141_CP1_CS_AS");
                ls.Add("29C000D3", "SQL_EBCDIC273_CP1_CS_AS");
                ls.Add("05C000DA", "SQL_EBCDIC277_2_CP1_CS_AS");
                ls.Add("05C000D4", "SQL_EBCDIC277_CP1_CS_AS");
                ls.Add("0AC000D5", "SQL_EBCDIC278_CP1_CS_AS");
                ls.Add("08C000D6", "SQL_EBCDIC280_CP1_CS_AS");
                ls.Add("28C000D7", "SQL_EBCDIC284_CP1_CS_AS");
                ls.Add("08C000D8", "SQL_EBCDIC285_CP1_CS_AS");
                ls.Add("0BC000D9", "SQL_EBCDIC297_CP1_CS_AS");
                ls.Add("1DD0009C", "SQL_Estonian_CP1257_CI_AS");
                ls.Add("1DC0009B", "SQL_Estonian_CP1257_CS_AS");
                ls.Add("0DD00056", "SQL_Hungarian_CP1250_CI_AS");
                ls.Add("0DC00055", "SQL_Hungarian_CP1250_CS_AS");
                ls.Add("0ED000BA", "SQL_Icelandic_Pref_CP1_CI_AS");
                ls.Add("08F00036", "SQL_Latin1_General_CP1_CI_AI");
                ls.Add("08D00034", "SQL_Latin1_General_CP1_CI_AS");
                ls.Add("08C00033", "SQL_Latin1_General_CP1_CS_AS");
                ls.Add("08D00052", "SQL_Latin1_General_CP1250_CI_AS");
                ls.Add("08C00051", "SQL_Latin1_General_CP1250_CS_AS");
                ls.Add("08D0006A", "SQL_Latin1_General_CP1251_CI_AS");
                ls.Add("08C00069", "SQL_Latin1_General_CP1251_CS_AS");
                ls.Add("08F0007C", "SQL_Latin1_General_CP1253_CI_AI");
                ls.Add("08D00072", "SQL_Latin1_General_CP1253_CI_AS");
                ls.Add("08C00071", "SQL_Latin1_General_CP1253_CS_AS");
                ls.Add("1AD00082", "SQL_Latin1_General_CP1254_CI_AS");
                ls.Add("1AC00081", "SQL_Latin1_General_CP1254_CS_AS");
                ls.Add("08D0008A", "SQL_Latin1_General_CP1255_CI_AS");
                ls.Add("08C00089", "SQL_Latin1_General_CP1255_CS_AS");
                ls.Add("08D00092", "SQL_Latin1_General_CP1256_CI_AS");
                ls.Add("08C00091", "SQL_Latin1_General_CP1256_CS_AS");
                ls.Add("08D0009A", "SQL_Latin1_General_CP1257_CI_AS");
                ls.Add("08C00099", "SQL_Latin1_General_CP1257_CS_AS");
                ls.Add("0800011E", "SQL_Latin1_General_CP437_BIN");
                ls.Add("0808001E", "SQL_Latin1_General_CP437_BIN2");
                ls.Add("08F00022", "SQL_Latin1_General_CP437_CI_AI");
                ls.Add("08D00020", "SQL_Latin1_General_CP437_CI_AS");
                ls.Add("08C0001F", "SQL_Latin1_General_CP437_CS_AS");
                ls.Add("08000128", "SQL_Latin1_General_CP850_BIN");
                ls.Add("08080028", "SQL_Latin1_General_CP850_BIN2");
                ls.Add("08F0002C", "SQL_Latin1_General_CP850_CI_AI");
                ls.Add("08D0002A", "SQL_Latin1_General_CP850_CI_AS");
                ls.Add("08C00029", "SQL_Latin1_General_CP850_CS_AS");
                ls.Add("08D00035", "SQL_Latin1_General_Pref_CP1_CI_AS");
                ls.Add("08D00021", "SQL_Latin1_General_Pref_CP437_CI_AS");
                ls.Add("08D0002B", "SQL_Latin1_General_Pref_CP850_CI_AS");
                ls.Add("1ED0009E", "SQL_Latvian_CP1257_CI_AS");
                ls.Add("1EC0009D", "SQL_Latvian_CP1257_CS_AS");
                ls.Add("1FD000A0", "SQL_Lithuanian_CP1257_CI_AS");
                ls.Add("1FC0009F", "SQL_Lithuanian_CP1257_CS_AS");
                ls.Add("08C00078", "SQL_MixDiction_CP1253_CS_AS");
                ls.Add("13D00058", "SQL_Polish_CP1250_CI_AS");
                ls.Add("13C00057", "SQL_Polish_CP1250_CS_AS");
                ls.Add("14D0005A", "SQL_Romanian_CP1250_CI_AS");
                ls.Add("14C00059", "SQL_Romanian_CP1250_CS_AS");
                ls.Add("0AD0003C", "SQL_Scandinavian_CP850_CI_AS");
                ls.Add("0AC0003B", "SQL_Scandinavian_CP850_CS_AS");
                ls.Add("0AD0003A", "SQL_Scandinavian_Pref_CP850_CI_AS");
                ls.Add("17D0005E", "SQL_Slovak_CP1250_CI_AS");
                ls.Add("17C0005D", "SQL_Slovak_CP1250_CS_AS");
                ls.Add("1CD00060", "SQL_Slovenian_CP1250_CI_AS");
                ls.Add("1CC0005F", "SQL_Slovenian_CP1250_CS_AS");
                ls.Add("0AD000B8", "SQL_SwedishPhone_Pref_CP1_CI_AS");
                ls.Add("0AD000B9", "SQL_SwedishStd_Pref_CP1_CI_AS");
                ls.Add("1BD0006C", "SQL_Ukrainian_CP1251_CI_AS");
                ls.Add("1BC0006B", "SQL_Ukrainian_CP1251_CS_AS");
            }
        }

        public static string GetCollationName(string pcode)
        {
            string name;

            Init();
            name = (ls.ContainsKey(pcode) ? ls[pcode] : "");

            return name;
        }
    }

}
