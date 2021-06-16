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

}
