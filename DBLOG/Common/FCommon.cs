using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DBLOG.Common
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

        public static string Replicate(this string pstr, int t)
        {
            string r;

            if (t <= 0)
            {
                r = "";
            }
            else
            {
                r = new StringBuilder(pstr.Length * t).Insert(0, pstr, t).ToString();
            }

            return r;
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

        public static string ToHexString(this string binary)
        {
            StringBuilder result;

            result = new StringBuilder(binary.Length / 8);
            for (int i = 0; i < binary.Length; i += 8)
            {
                string eightBits = binary.Substring(i, 8);
                result.AppendFormat("{0:X2}", Convert.ToByte(eightBits, 2));
            }

            return result.ToString();
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
                if (TargetType.IsEnum == false)
                {
                    if (TargetType.IsGenericType == true
                        && TargetType.GetGenericTypeDefinition() == typeof(Nullable<>))
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
                else
                {
                    y = Enum.Parse(TargetType, x.ToString());
                }
                
            }

            return y;
        }

        public static object[] CopyToNew(this object[] x)
        {
            object[] y;

            y = Array.ConvertAll<object, object>(x, new Converter<object, object>(FCopyToNew));

            return y;
        }

        private static object FCopyToNew(object x)
        {
            object y;

            if (x is ICloneable)
            {
                y = (x as ICloneable).Clone();
            }
            else
            {
                y = Activator.CreateInstance(x.GetType());
                foreach (PropertyInfo p in x.GetType().GetProperties())
                {
                    if (p.CanRead && p.CanWrite)
                    {
                        p.SetValue(y, p.GetValue(x));
                    }
                }
                foreach (FieldInfo f in x.GetType().GetFields())
                {
                    f.SetValue(y, f.GetValue(x));
                }
            }

            return y;
        }

        public static T FCopy<T>(this T x)
        {
            T y;
            string json;
            //JsonSerializerSettings settings;
            JsonSerializerOptions settings;

            if (ReferenceEquals(x, null))
            {
                y = default;
            }
            else
            {
                //settings = new JsonSerializerSettings
                //{
                //    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                //    ObjectCreationHandling = ObjectCreationHandling.Replace
                //};
                //json = JsonConvert.SerializeObject(x, settings);
                //y = JsonConvert.DeserializeObject<T>(json, settings);

                settings = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
                    IncludeFields = true,
                    PropertyNameCaseInsensitive = true
                };
                json = JsonSerializer.Serialize(x, settings);
                y = JsonSerializer.Deserialize<T>(json, settings);
            }

            return y;
        }

        //public static void WriteTextFile(string pFileName, string pContent, bool pAppend = true, bool pLogTime = true)
        //{
        //    FileStream fs;
        //    StreamWriter writer;
        //    FileMode fm;

        //    if (pLogTime == true)
        //    {
        //        pContent = $"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff")}: {pContent}";
        //    }

        //    if (System.IO.File.Exists(pFileName) == true && pAppend == true)
        //    {
        //        fm = FileMode.Append;
        //    }
        //    else
        //    {
        //        fm = FileMode.Create;
        //    }
        //    fs = new FileStream(pFileName,
        //                        fm,
        //                        FileAccess.Write,
        //                        FileShare.None);
        //    writer = new StreamWriter(fs, Encoding.Unicode);
        //    writer.WriteLine(pContent);

        //    writer.Close();
        //    fs.Close();
        //    writer.Dispose();
        //    fs.Dispose();
        //}

        public static Dictionary<string, string> ToDict(this TableColumn[] tablecolumns)
        {
            Dictionary<string, string> r;

            r = new Dictionary<string, string>();
            foreach (TableColumn c in tablecolumns)
            {
                r.Add(c.ColumnName, (c.Value != null ? c.Value.ToString() : null));
            }

            return r;
        }


    }
    
    public enum CompressionType
    {
        NONE, ROW, PAGE, COLUMNSTORE, COLUMNSTORE_ARCHIVE
    }
    
    
}
