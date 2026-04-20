using DBLOG.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG
{
    public partial class DBLOG_DML_DDL
    {
        private string TranslateData_VarDecimal(string pcvalue)
        {
            string rvalue, pcvalue2, sg, zs, ws, wsvs;
            int zsv, wsv, i;
            double bv;

            if (pcvalue.Length == 0)
            {
                rvalue = "0";
            }
            else
            {
                pcvalue2 = pcvalue.ToBinaryString();
                sg = (pcvalue2.StartsWith("1") ? "" : "-");
                zs = pcvalue2.Substring(1, 7);
                ws = pcvalue2.Substring(8, pcvalue2.Length - 8);

                zsv = Convert.ToInt32(zs, 2) - 64;
                ws = ws + new string('0', 10 * Convert.ToInt32(Math.Ceiling(ws.Length / 10.0)) - ws.Length);
                wsvs = "";
                for (i = 0; i <= ws.Length / 10 - 1; i = i + 1)
                {
                    wsv = Convert.ToInt32(ws.Substring(i * 10, 10), 2);
                    wsvs = wsvs + wsv.ToString().PadLeft(3, '0');
                }
                bv = Convert.ToDouble(wsvs.Insert(1, ".")) * Math.Pow(10, zsv);
                rvalue = $"{sg}{bv.ToString()}";
            }

            return rvalue;
        }

        private string TranslateData_Bit(byte[] data, TableColumn[] columns, int iCurrentIndex, string sColumnName, short sBitColumnCount, byte[] m_bBitColumnData0, short sBitColumnDataIndex0, ref int iJumpIndexLength, ref byte[] m_bBitColumnData1, ref short sBitColumnDataIndex1)
        {
            string rBit, sBitColumnData2;
            short i, sCurrentColumnIDinBit;  // 当前字段为第几个Bit类型字段

            m_bBitColumnData1 = m_bBitColumnData0;
            sBitColumnDataIndex1 = sBitColumnDataIndex0;
            sCurrentColumnIDinBit = 0;
            for (i = 0; i <= columns.Length - 1; i++)
            {
                if (columns[i].PhysicalStorageType == SqlDbType.Bit)
                {
                    sCurrentColumnIDinBit = (short)(sCurrentColumnIDinBit + 1);
                    if (columns[i].ColumnName == sColumnName) { break; }
                }
            }

            iJumpIndexLength = 0;
            if (sBitColumnDataIndex1 == -1 || (sBitColumnDataIndex1 + 1) * 8 < sCurrentColumnIDinBit)
            {
                sBitColumnDataIndex1 = (short)(sBitColumnDataIndex1 + 1);
                Array.Copy(data, iCurrentIndex, m_bBitColumnData1, sBitColumnDataIndex1, 1);  // 读入1个字节
                iJumpIndexLength = iJumpIndexLength + 1;
            }

            sBitColumnData2 = string.Empty;
            for (i = sBitColumnDataIndex1; i >= 0; i--)
            {
                sBitColumnData2 = sBitColumnData2 + m_bBitColumnData1[i].ToBinaryString();
            }

            sBitColumnData2 = sBitColumnData2.Reverse();   // 字符串反转
            rBit = sBitColumnData2.Substring(sCurrentColumnIDinBit - 1, 1);

            return rBit;
        }

        private string TranslateData_Date(byte[] data, int iCurrentIndex)
        {
            string returnDate, hDate;
            DateTime date1;
            byte[] bDate;
            int days_date;

            date1 = new DateTime(1900, 1, 1, 0, 0, 0);
            bDate = new byte[3];
            Array.Copy(data, iCurrentIndex, bDate, 0, 3);

            hDate = "";
            foreach (byte b in bDate)
            {
                hDate = b.ToString("X2") + hDate;
            }

            days_date = Convert.ToInt32(hDate, 16) - 693595;
            date1 = date1.AddDays(days_date);
            returnDate = date1.ToString("yyyy-MM-dd");

            return returnDate;
        }

        private string TranslateData_DateTime(byte[] data, int iCurrentIndex)
        {
            string sReturnDatetime;
            DateTime date0;
            int second, days;

            date0 = new DateTime(1900, 1, 1, 0, 0, 0);

            // 前四个字节  以1/300秒保存
            second = BitConverter.ToInt32(data, iCurrentIndex);
            date0 = date0.AddMilliseconds(second * 3.3333333333);
            iCurrentIndex = iCurrentIndex + 4;

            // 后四个字节  为1900-1-1后的天数
            days = BitConverter.ToInt32(data, iCurrentIndex);
            date0 = date0.AddDays(days);

            sReturnDatetime = date0.ToString("yyyy-MM-dd HH:mm:ss.fff");

            return sReturnDatetime;
        }

        private string TranslateData_Time(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sTimeHex, sTimeDec, sTimeSeconds, sTimeSeconds2, sReturnTime;
            byte[] bTime;
            System.DateTime date2;

            bTime = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bTime, 0, sLength);

            sTimeHex = "";
            foreach (byte b in bTime)
            {
                sTimeHex = b.ToString("X2") + sTimeHex;
            }

            sTimeDec = Convert.ToInt64(sTimeHex, 16).ToString();
            if (sTimeDec.Length <= sScale)
            {
                sTimeSeconds = "0";
                sTimeSeconds2 = new string('0', sScale);
                sTimeSeconds2 = sTimeSeconds2 + sTimeDec;      // 秒的小数部分
                sTimeSeconds2 = sTimeSeconds2.Substring(sTimeSeconds2.Length - sScale, sScale);
            }
            else
            {
                sTimeSeconds = sTimeDec.Substring(0, sTimeDec.Length - sScale);
                sTimeSeconds2 = sTimeDec.Substring(sTimeDec.Length - sScale, sScale);    // 秒的小数部分
            }

            date2 = new DateTime(1900, 1, 1, 0, 0, 0);
            date2 = date2.AddSeconds(Convert.ToDouble(sTimeSeconds));
            sReturnTime = date2.ToString("HH:mm:ss") + (sTimeSeconds2.Length > 0 ? "." : "") + sTimeSeconds2;

            return sReturnTime;
        }

        private string TranslateData_DateTime2(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sReturnDatetime2, sDate, sTime;
            byte[] bDatetime2;

            bDatetime2 = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDatetime2, 0, sLength);
            sTime = TranslateData_Time(bDatetime2, 0, (short)(sLength - 3), sScale);
            sDate = TranslateData_Date(bDatetime2, sLength - 3);
            sReturnDatetime2 = $"{sDate} {sTime}";

            return sReturnDatetime2;
        }

        private string TranslateData_DateTimeOffset(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            string sReturnDateTimeOffset, sDate, sTime, sOffset;
            short sSignOffset, iOffset;
            byte[] bDateTimeOffset;
            DateTime d0;

            bDateTimeOffset = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDateTimeOffset, 0, sLength);

            // offset
            sSignOffset = 1;
            iOffset = Convert.ToInt16(bDateTimeOffset[sLength - 1].ToString("X2").Substring(1, 1) + bDateTimeOffset[sLength - 2].ToString("X2"), 16);
            if (bDateTimeOffset[sLength - 1].ToBinaryString().Substring(0, 1) == "1")
            {
                sSignOffset = -1;
                iOffset = (short)(Convert.ToInt16("FFF", 16) + 1 - iOffset);
            }

            d0 = new DateTime(1900, 1, 1, 0, 0, 0);
            d0 = d0.AddMinutes(iOffset);
            sOffset = (sSignOffset == 1 ? "+" : "-") + d0.ToString("HH:mm");

            // date
            sDate = TranslateData_Date(bDateTimeOffset, sLength - 5);

            // time
            sTime = TranslateData_Time(bDateTimeOffset, 0, (short)(sLength - 5), sScale);

            // 计算offset
            d0 = new DateTime();
            d0 = DateTime.Parse(sDate + " " + sTime);
            d0 = d0.AddMinutes(sSignOffset * iOffset);

            sDate = d0.ToString("yyyy-MM-dd");
            sTime = d0.ToString("HH:mm:ss.fffffff");

            sTime = sTime.Substring(0, sTime.IndexOf(".", 0) + 1)
                    + Convert.ToInt32(sTime.Substring(sTime.IndexOf(".", 0) + 1, sTime.Length - sTime.IndexOf(".", 0) - 1).Reverse()).ToString().Reverse();

            sReturnDateTimeOffset = sDate + " " + sTime + sOffset;

            return sReturnDateTimeOffset;
        }

        private string TranslateData_SmallDateTime(byte[] data, int iCurrentIndex)
        {
            string sReturnSmallDatetime;

            byte[] bSmallDatetime = new byte[4];
            Array.Copy(data, iCurrentIndex, bSmallDatetime, 0, 4);

            System.DateTime date0 = new DateTime(1900, 1, 1, 0, 0, 0);

            // 前2个字节保存分钟数
            int iMinutes = Convert.ToInt32(bSmallDatetime[1].ToString("X2") + bSmallDatetime[0].ToString("X2"), 16);
            date0 = date0.AddMinutes(iMinutes);

            // 后2个字节为1900-1-1后的天数
            int iDays = Convert.ToInt32(bSmallDatetime[3].ToString("X2") + bSmallDatetime[2].ToString("X2"), 16);
            date0 = date0.AddDays(iDays);

            sReturnSmallDatetime = date0.ToString("yyyy-MM-dd HH:mm:ss");

            return sReturnSmallDatetime;
        }

        private string TranslateData_Money(byte[] data, int iCurrentIndex)
        {
            string sReturnMoney, sSign;
            byte[] bMoney;

            bMoney = new byte[8];
            Array.Copy(data, iCurrentIndex, bMoney, 0, 8);

            if (bMoney[7].ToBinaryString().Substring(7, 1) == "0")
            { sSign = ""; }
            else
            { sSign = "-"; }

            string sMoneyHex, sMoney, sTemp;
            short iMoney;

            sMoneyHex = "";
            for (iMoney = 7; iMoney >= 0; iMoney--)
            {
                sMoneyHex = sMoneyHex + bMoney[iMoney].ToString("X2");
            }

            sMoney = BigInteger.Parse(sMoneyHex, System.Globalization.NumberStyles.HexNumber).ToString();

            if (sSign == "")
            { // 正数

            }
            else
            { // 负数
                BigInteger bigintMoney;
                bigintMoney = BigInteger.Parse("FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber)
                              + 1
                              - BigInteger.Parse(sMoneyHex, System.Globalization.NumberStyles.HexNumber);

                sMoney = bigintMoney.ToString();
            }

            sTemp = new string('0', (sMoney.Length < 5 ? 5 - sMoney.Length : 0));
            sMoney = sTemp + sMoney;
            sMoney = sMoney.Insert(sMoney.Length - 4, ".");

            if (sSign == "-" && sMoney.StartsWith("-"))
            {
                sSign = "";
                sMoney = sMoney.Stuff(0, 1, "");
            }

            sReturnMoney = sSign + sMoney;

            return sReturnMoney;
        }

        private string TranslateData_SmallMoney(byte[] data, int iCurrentIndex)
        {
            string sReturnSmallMoney, sSign, sSmallMoneyHex, sSmallMoney;
            byte[] bSmallMoney;
            short iSmallMoney;
            BigInteger bigintSmallMoney;

            bSmallMoney = new byte[4];
            Array.Copy(data, iCurrentIndex, bSmallMoney, 0, 4);

            sSign = (bSmallMoney[3].ToBinaryString().Substring(7, 1) == "0" ? "" : "-");
            sSmallMoneyHex = "";
            for (iSmallMoney = 3; iSmallMoney >= 0; iSmallMoney--)
            {
                sSmallMoneyHex = sSmallMoneyHex + bSmallMoney[iSmallMoney].ToString("X2");
            }

            sSmallMoney = BigInteger.Parse(sSmallMoneyHex, System.Globalization.NumberStyles.HexNumber).ToString();

            if (sSign != "")
            {   // 负数
                bigintSmallMoney = BigInteger.Parse("FFFFFFFF", System.Globalization.NumberStyles.HexNumber) + 1
                                   - BigInteger.Parse(sSmallMoneyHex, System.Globalization.NumberStyles.HexNumber);
                sSmallMoney = bigintSmallMoney.ToString();
            }

            sSmallMoney = "0".Replicate((sSmallMoney.Length < 5 ? 5 - sSmallMoney.Length : 0)) + sSmallMoney;
            sSmallMoney = sSmallMoney.Insert(sSmallMoney.Length - 4, ".");

            if (sSign == "-" && sSmallMoney.StartsWith("-"))
            {
                sSign = "";
                sSmallMoney = sSmallMoney.Stuff(0, 1, "");
            }
            sReturnSmallMoney = sSign + sSmallMoney;

            return sReturnSmallMoney;
        }

        private string TranslateData_Decimal(byte[] data, int iCurrentIndex, short sLength, short sScale)
        {
            byte[] bDecimal;
            string sDecimalHex, sDecimal, sTemp;
            short sSignDecimal;
            int iDecimal;

            bDecimal = new byte[sLength];
            Array.Copy(data, iCurrentIndex, bDecimal, 0, sLength);
            sSignDecimal = Convert.ToInt16(bDecimal[0].ToString("X2") == "00" ? -1 : 1);

            sDecimalHex = "";
            for (iDecimal = 1; iDecimal <= bDecimal.Length - 1; iDecimal++)
            {
                sDecimalHex = bDecimal[iDecimal].ToString("X2") + sDecimalHex;
            }

            sDecimal = BigInteger.Parse(sDecimalHex, System.Globalization.NumberStyles.HexNumber).ToString();
            sTemp = new string('0', (sDecimal.Length < (sScale + 1) ? sScale + 1 - sDecimal.Length : 0));
            sDecimal = sTemp + sDecimal;
            sDecimal = sDecimal.Insert(sDecimal.Length - sScale, ".");
            sDecimal = (sSignDecimal == 1 ? "" : "-") + sDecimal;

            return sDecimal;
        }

        private string TranslateData_Real(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnReal, sExpReal, sFractionReal;
            byte[] bReal;
            short sSignReal;
            int iExpReal, iReal;
            double dFractionReal;

            bReal = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bReal, 0, sLenth);

            sSignReal = Convert.ToInt16(bReal[3].ToBinaryString().Substring(0, 1) == "1" ? -1 : 1);

            // 指数
            sExpReal = bReal[3].ToBinaryString().Substring(1, 7)
                       + bReal[2].ToBinaryString().Substring(0, 1);
            iExpReal = Convert.ToInt32(sExpReal, 2);

            // 尾数
            sFractionReal = bReal[2].ToBinaryString().Substring(1, 7)
                            + bReal[1].ToBinaryString()
                            + bReal[0].ToBinaryString();

            if (iExpReal == 0 && sFractionReal == new string('0', 23))
            {
                sReturnReal = "0";
            }
            else
            {
                dFractionReal = 1;
                for (iReal = 0; iReal <= sFractionReal.Length - 1; iReal++)
                {
                    if (sFractionReal.Substring(iReal, 1) == "1")
                    {
                        dFractionReal = dFractionReal + Math.Pow(2, -1 * (iReal + 1));
                    }
                }

                dFractionReal = sSignReal * dFractionReal * Math.Pow(2, iExpReal - 127);
                sReturnReal = ((float)dFractionReal).ToString();
            }

            return sReturnReal;
        }

        private string TranslateData_Float(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sFloatValue, sExpFloat, sFractionFloat;
            byte[] bFloat;
            short sSignFloat;
            int iExpFloat, iFloat;
            double dFractionFloat;

            bFloat = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bFloat, 0, sLenth);

            sSignFloat = Convert.ToInt16(bFloat[7].ToBinaryString().Substring(0, 1) == "1" ? -1 : 1);

            // 指数
            sExpFloat = bFloat[sLenth - 1].ToBinaryString().Substring(1, 7)
                        + bFloat[sLenth - 2].ToBinaryString().Substring(0, 4);
            iExpFloat = Convert.ToInt32(sExpFloat, 2);

            // 尾数
            sFractionFloat = bFloat[6].ToBinaryString().Substring(4, 4)
                             + bFloat[5].ToBinaryString()
                             + bFloat[4].ToBinaryString()
                             + bFloat[3].ToBinaryString()
                             + bFloat[2].ToBinaryString()
                             + bFloat[1].ToBinaryString()
                             + bFloat[0].ToBinaryString();

            if (iExpFloat == 0 && sFractionFloat == new string('0', 52))
            {
                sFloatValue = "0";
            }
            else
            {
                dFractionFloat = 1;
                for (iFloat = 0; iFloat <= sFractionFloat.Length - 1; iFloat++)
                {
                    if (sFractionFloat.Substring(iFloat, 1) == "1")
                    {
                        dFractionFloat = dFractionFloat + Math.Pow(2, -1 * (iFloat + 1));
                    }
                }

                dFractionFloat = sSignFloat * dFractionFloat * Math.Pow(2, iExpFloat - 1023);
                sFloatValue = dFractionFloat.ToString();
            }

            return sFloatValue;
        }

        private string TranslateData_Binary(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnBinary;
            byte[] bBinary;
            short iBinary;

            sReturnBinary = "0x";
            bBinary = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bBinary, 0, sLenth);

            for (iBinary = 0; iBinary <= sLenth - 1; iBinary++)
            {
                sReturnBinary = sReturnBinary + bBinary[iBinary].ToString("X2");
            }

            return sReturnBinary;
        }

        private (string, string) TranslateData_VarBinary(byte[] data, FVarColumnInfo pvc)
        {
            string fvaluehex, fvalue, pointer, pagedata, tmpstr;
            byte[] bVarBinary;
            short iVarBinary, sActualLenth;
            int iCurrentIndex, i, pageqty, cutlen;
            FPageInfo firstpage, textmixpage;
            List<FPageInfo> tmps;

            if (pvc.InRow == true)
            {
                iCurrentIndex = pvc.FStartIndex;
                sActualLenth = (short)(pvc.FEndIndex - pvc.FStartIndex);
                bVarBinary = new byte[sActualLenth];
                Array.Copy(data, iCurrentIndex, bVarBinary, 0, sActualLenth);

                fvaluehex = "";
                for (iVarBinary = 0; iVarBinary <= sActualLenth - 1; iVarBinary++)
                {
                    fvaluehex = fvaluehex + bVarBinary[iVarBinary].ToString("X2");
                }
            }
            else
            {
                try
                {
                    pointer = pvc.FLogContents;
                    pointer = pointer.Stuff(0, 12 * 2, ""); // 跳过12个字节

                    i = 0;
                    tmpstr = pointer.Substring(i * 2, 12 * 2);
                    firstpage = new FPageInfo(tmpstr);
                    i = i + 12;

                    textmixpage = GetPageInfo(firstpage.FileNumPageNum_Hex);
                    firstpage.PageData = textmixpage.PageData;
                    firstpage.PageType = textmixpage.PageType;

                    tmps = new List<FPageInfo>();

                    if (firstpage.PageType == "4")  // TEXT_TREE_PAGE
                    {
                        pagedata = firstpage.PageData;
                        pagedata = pagedata.Stuff(0, (96 + 16) * 2, "");
                        tmpstr = pagedata.Substring(0, 4 * 2);
                        pageqty = Convert.ToInt32(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);

                        for (i = 0; i <= pageqty - 1; i++)
                        {
                            tmpstr = pagedata.Substring(8 + i * 16 * 2, 16 * 2);
                            textmixpage = new FPageInfo(tmpstr, "TEXT_TREE_PAGE");
                            tmps.Add(textmixpage);
                        }
                    }

                    if (firstpage.PageType == "3")  // TEXT_MIX_PAGE
                    {
                        tmps.Add(firstpage);

                        while (i + 12 <= (pointer.Length / 2))
                        {
                            tmpstr = pointer.Substring(i * 2, 12 * 2);
                            textmixpage = new FPageInfo(tmpstr);
                            tmps.Add(textmixpage);
                            i = i + 12;
                        }
                    }

                    fvaluehex = "";
                    i = 0;
                    foreach (FPageInfo tp in tmps)
                    {
                        cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));
                        textmixpage = GetPageInfo(tp.FileNumPageNum_Hex);

                        pagedata = textmixpage.PageData;
                        pagedata = pagedata.Stuff(0, (96 + 14) * 2, "");
                        pagedata = pagedata.Stuff(pagedata.Length - 42 * 2, 42 * 2, "");
                        pagedata = pagedata.Substring(0, cutlen * 2);

                        fvaluehex = fvaluehex + pagedata;
                        i = i + 1;
                    }
                }
                catch (Exception ex)
                {
                    fvaluehex = "";
                }
            }

            fvalue = "0x" + fvaluehex;

            return (fvaluehex, fvalue);
        }

        private string TranslateData_UniqueIdentifier(byte[] data, int iCurrentIndex, short sLenth)
        {
            string sReturnUniqueIdentifier;
            byte[] bUniqueIdentifier;
            short iUniqueIdentifier;

            sReturnUniqueIdentifier = "";
            bUniqueIdentifier = new byte[sLenth];
            Array.Copy(data, iCurrentIndex, bUniqueIdentifier, 0, sLenth);

            // 前4个字节反转
            for (iUniqueIdentifier = 0; iUniqueIdentifier <= 3; iUniqueIdentifier++)
            {
                sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[3 - iUniqueIdentifier].ToString("X2");
            }
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + "-";

            // 反转2个字节
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[5].ToString("X2") + bUniqueIdentifier[4].ToString("X2") + "-";

            // 反转2个字节
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[7].ToString("X2") + bUniqueIdentifier[6].ToString("X2") + "-";

            // 顺序读2个字节
            sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[8].ToString("X2") + bUniqueIdentifier[9].ToString("X2") + "-";

            // 顺序读6个字节
            for (iUniqueIdentifier = 10; iUniqueIdentifier <= sLenth - 1; iUniqueIdentifier++)
            {
                sReturnUniqueIdentifier = sReturnUniqueIdentifier + bUniqueIdentifier[iUniqueIdentifier].ToString("X2");
            }

            return sReturnUniqueIdentifier;
        }

        private (string, string) TranslateData_Text(byte[] data, FVarColumnInfo pv, bool isNText, int textinrow)
        {
            string fvaluehex, fvalue;

            if (pv.InRow == false)
            {
                if (textinrow == 0)
                {
                    fvaluehex = GetLOBDataHEX(pv.FLogContents);
                }
                else
                {
                    fvaluehex = GetLOBDataHEX_ForTextInRow(pv.FLogContents);
                }
            }
            else
            {
                fvaluehex = pv.FLogContents;
            }

            if (fvaluehex != null)
            {
                if (isNText == false)
                {
                    fvalue = System.Text.Encoding.Default.GetString(fvaluehex.ToByteArray());
                }
                else
                {
                    fvalue = System.Text.Encoding.Unicode.GetString(fvaluehex.ToByteArray());
                }
            }
            else
            {
                fvalue = "nullvalue";
            }

            return (fvaluehex, fvalue);
        }

        private (string, string) TranslateData_VarChar(byte[] data, FVarColumnInfo pvc, bool isunicode)
        {
            string fvaluehex, fvalue, pointer, pagedata, tmpstr;
            int i, cutlen;
            FPageInfo firstpage, temppage;
            List<FPageInfo> pagelist;

            if (pvc.InRow == true)
            {
                fvaluehex = pvc.FLogContents;
            }
            else
            {
                try
                {
                    pointer = pvc.FLogContents;
                    pointer = pointer.Stuff(0, 12 * 2, ""); // 跳过12个字节

                    i = 0;
                    tmpstr = pointer.Substring(i * 2, 12 * 2);
                    firstpage = new FPageInfo(tmpstr);
                    i = i + 12;

                    temppage = GetPageInfo(firstpage.FileNumPageNum_Hex);
                    firstpage.PageData = temppage.PageData;
                    firstpage.PageType = temppage.PageType;

                    pagelist = new List<FPageInfo>();

                    if (firstpage.PageType == "4")  // TEXT_TREE_PAGE
                    {
                        pagelist = GetTEXTTREEPAGESubPages(firstpage);
                    }

                    if (firstpage.PageType == "3")  // TEXT_MIX_PAGE
                    {
                        pagelist.Add(firstpage);

                        while (i + 12 <= (pointer.Length / 2))
                        {
                            tmpstr = pointer.Substring(i * 2, 12 * 2);
                            temppage = new FPageInfo(tmpstr);
                            pagelist.Add(temppage);
                            i = i + 12;
                        }
                    }

                    fvaluehex = "";
                    i = 0;
                    foreach (FPageInfo tp in pagelist)
                    {
                        cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : pagelist[i - 1].Offset));
                        temppage = GetPageInfo(tp.FileNumPageNum_Hex);

                        if (tp.SlotNum == 0)
                        {
                            pagedata = temppage.PageData;
                            pagedata = pagedata.Stuff(0, (96 + 14) * 2, "");
                            pagedata = pagedata.Stuff(pagedata.Length - 42 * 2, 42 * 2, "");
                            if (pagedata.Length / 2 >= cutlen)
                            {
                                pagedata = (cutlen > 0 ? pagedata.Substring(0, cutlen * 2) : pagedata);
                            }
                            else
                            {
                                pagedata = pagedata + new StringBuilder((cutlen - pagedata.Length / 2) * 2).Insert(0, "78", (cutlen - pagedata.Length / 2)).ToString();
                            }
                        }
                        else
                        {
                            pagedata = temppage.SlotData[tp.SlotNum];
                            pagedata = pagedata.Stuff(0, 14 * 2, "");
                            pagedata = pagedata.Substring(0, cutlen * 2);
                        }

                        fvaluehex = fvaluehex + pagedata;
                        i = i + 1;
                    }
                }
                catch (Exception ex)
                {
                    fvaluehex = "";
                }
            }

            if (isunicode)
            {
                fvalue = System.Text.Encoding.Unicode.GetString(fvaluehex.ToByteArray()).TrimEnd();
            }
            else
            {
                fvalue = System.Text.Encoding.Default.GetString(fvaluehex.ToByteArray()).TrimEnd();
            }

            return (fvaluehex, fvalue);
        }

        private List<FPageInfo> GetTEXTTREEPAGESubPages(FPageInfo fpage)
        {
            string pagedata, tmpstr, tmpstr2;
            int i, pageqty;
            FPageInfo textmixpage, temppage;
            List<FPageInfo> pagelist;

            pagelist = new List<FPageInfo>();
            pagedata = fpage.PageData;
            pagedata = pagedata.Stuff(0, (96 + 16) * 2, "");
            tmpstr = pagedata.Substring(0, 4 * 2);
            pageqty = Convert.ToInt32(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);

            for (i = 0; i <= pageqty - 1; i++)
            {
                tmpstr = pagedata.Substring(8 + i * 16 * 2, 16 * 2);
                temppage = new FPageInfo(tmpstr, "TEXT_TREE_PAGE");
                tmpstr2 = temppage.FileNumPageNum_Hex;
                temppage = GetPageInfo(tmpstr2);

                switch (temppage.PageType)
                {
                    case "3": // TEXT_MIX_PAGE
                        textmixpage = new FPageInfo(tmpstr, "TEXT_TREE_PAGE");
                        pagelist.Add(textmixpage);
                        break;
                    case "4": // TEXT_TREE_PAGE
                        pagelist.AddRange(GetTEXTTREEPAGESubPages(temppage));
                        break;
                    default:
                        break;
                }
            }

            return pagelist;
        }

        private (string, string) TranslateData_Image(byte[] data, FVarColumnInfo pvc)
        {
            string fvaluehex, fvalue;

            if (pvc.InRow == true)
            {
                fvaluehex = pvc.FLogContents;
            }
            else
            {
                fvaluehex = GetLOBDataHEX(pvc.FLogContents);
            }

            fvalue = "0x" + fvaluehex;

            return (fvaluehex, fvalue);
        }

        private (string, string) TranslateData_XML(FVarColumnInfo pvc)
        {
            int i, length;
            string fvaluehex, fvalue, logcont, nlen1, nlen2, ncont, nvalue, f0type, lastnode;
            List<string> stacks;

            fvaluehex = pvc.FLogContents;
            fvalue = "";

            try
            {
                logcont = pvc.FLogContents;
                logcont = logcont.Stuff(0, 10, "");
                stacks = new List<string>();
                f0type = "";
                lastnode = "";

                for (i = 0; i <= logcont.Length - 1;)
                {
                    switch (logcont.Substring(i, 2)) // node type
                    {
                        case "F0":
                            i = i + 2;
                            nlen1 = logcont.Substring(i, 2);
                            if (Convert.ToInt32(nlen1, 16) < 128)
                            {
                                length = Convert.ToInt32(nlen1, 16);
                            }
                            else
                            {
                                i = i + 2;
                                nlen2 = logcont.Substring(i, 2);
                                length = (Convert.ToInt32(nlen2, 16) * 128) + (Convert.ToInt32(nlen1, 16) - 128);
                            }
                            i = i + 2;
                            ncont = logcont.Substring(i, length * 4);
                            nvalue = System.Text.Encoding.Unicode.GetString(ncont.ToByteArray());
                            i = i + length * 4;
                            i = i + 12;
                            f0type = logcont.Substring(i - 4, 2);
                            if (f0type == "F8")
                            {
                                fvalue = fvalue + $"<{nvalue}>";
                                lastnode = nvalue;
                                stacks.Add(nvalue);
                            }
                            if (f0type == "F6")
                            {
                                if (fvalue.EndsWith(">"))
                                {
                                    fvalue = fvalue.Substring(0, fvalue.Length - 1);
                                }
                                fvalue = fvalue + $" {nvalue}=";
                            }
                            break;
                        case "11":
                            i = i + 2;
                            nlen1 = logcont.Substring(i, 2);
                            if (Convert.ToInt32(nlen1, 16) < 128)
                            {
                                length = Convert.ToInt32(nlen1, 16);
                            }
                            else
                            {
                                i = i + 2;
                                nlen2 = logcont.Substring(i, 2);
                                length = (Convert.ToInt32(nlen2, 16) * 128) + (Convert.ToInt32(nlen1, 16) - 128);
                            }
                            i = i + 2;
                            ncont = logcont.Substring(i, length * 4);
                            nvalue = System.Text.Encoding.Unicode.GetString(ncont.ToByteArray());
                            if (f0type == "F8")
                            {
                                fvalue = fvalue + $"{nvalue}";
                            }
                            if (f0type == "F6")
                            {
                                fvalue = fvalue + $"\"{nvalue}\"";
                            }
                            i = i + length * 4;
                            break;
                        case "F7":
                            nvalue = stacks.Last();
                            fvalue = fvalue + $"</{nvalue}>";
                            i = i + 2;
                            stacks.RemoveAt(stacks.Count - 1);
                            break;
                        case "F5":
                            fvalue = fvalue + ">";
                            f0type = "F8";
                            i = i + 2;
                            break;
                        case "F8":
                            fvalue = fvalue + $"<{lastnode}>";
                            stacks.Add(lastnode);
                            i = i + 4;
                            break;
                        default:
                            i = i + 2;
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                fvalue = "";
            }

            return (fvaluehex, fvalue);
        }

        private (string, string, SqlDbType?, short?, short?, string) TranslateData_Variant(byte[] data, FVarColumnInfo pvc)
        {
            string fvaluehex, fvalue, logcont, tmp, VariantCollation;
            short length;
            short? VariantLength, VariantScale;
            SqlDbType? VariantBaseType;

            fvaluehex = "";
            fvalue = "";
            VariantBaseType = null;
            VariantLength = null;
            VariantScale = null;
            VariantCollation = null;

            logcont = pvc.FLogContents;

            switch (logcont.Substring(0, 4))
            {
                case "2401": // uniqueidentifier
                    VariantBaseType = SqlDbType.UniqueIdentifier;
                    fvaluehex = logcont.Stuff(0, "2401".Length, "");
                    fvalue = TranslateData_UniqueIdentifier(data, pvc.FStartIndex + 2, 16);
                    break;
                case "2801": // date
                    VariantBaseType = SqlDbType.Date;
                    fvaluehex = logcont.Stuff(0, "2801".Length, "");
                    fvalue = TranslateData_Date(data, pvc.FStartIndex + 2);
                    break;
                case "2901": // time
                    VariantBaseType = SqlDbType.Time;
                    fvaluehex = logcont.Stuff(0, "2901".Length + 2, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 3);
                    VariantScale = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_Time(data, pvc.FStartIndex + 3, length, Convert.ToInt16(VariantScale));
                    break;
                case "2A01": // datetime2
                    VariantBaseType = SqlDbType.DateTime2;
                    fvaluehex = logcont.Stuff(0, "2A01".Length + 2, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 3);
                    VariantScale = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_DateTime2(data, pvc.FStartIndex + 3, length, Convert.ToInt16(VariantScale));
                    break;
                case "2B01": // datetimeoffset
                    VariantBaseType = SqlDbType.DateTimeOffset;
                    fvaluehex = logcont.Stuff(0, "2B01".Length + 2, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 3);
                    VariantScale = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_DateTimeOffset(data, pvc.FStartIndex + 3, length, Convert.ToInt16(VariantScale));
                    break;
                case "3001": // tinyint
                    VariantBaseType = SqlDbType.TinyInt;
                    fvaluehex = logcont.Stuff(0, "3001".Length, "");
                    fvalue = Convert.ToInt32(fvaluehex, 16).ToString();
                    break;
                case "3401": // smallint
                    VariantBaseType = SqlDbType.SmallInt;
                    fvaluehex = logcont.Stuff(0, "3401".Length, "");
                    tmp = fvaluehex.Substring(2, 2) + fvaluehex.Substring(0, 2);
                    fvalue = Convert.ToInt16(tmp, 16).ToString();
                    break;
                case "3801": // int
                    VariantBaseType = SqlDbType.Int;
                    fvaluehex = logcont.Stuff(0, "3801".Length, "");
                    tmp = fvaluehex.Substring(6, 2) + fvaluehex.Substring(4, 2) + fvaluehex.Substring(2, 2) + fvaluehex.Substring(0, 2);
                    fvalue = Convert.ToInt32(tmp, 16).ToString();
                    break;
                case "3A01": // smalldatetime
                    VariantBaseType = SqlDbType.SmallDateTime;
                    fvaluehex = logcont.Stuff(0, "3A01".Length, "");
                    fvalue = TranslateData_SmallDateTime(data, pvc.FStartIndex + 2);
                    break;
                case "3B01": // real
                    VariantBaseType = SqlDbType.Real;
                    fvaluehex = logcont.Stuff(0, "3B01".Length, "");
                    VariantLength = Convert.ToInt16(fvaluehex.Length / 2);
                    fvalue = TranslateData_Real(data, pvc.FStartIndex + 2, Convert.ToInt16(VariantLength));
                    break;
                case "3C01": // money
                    VariantBaseType = SqlDbType.Money;
                    fvaluehex = logcont.Stuff(0, "3C01".Length, "");
                    fvalue = TranslateData_Money(data, pvc.FStartIndex + 2);
                    break;
                case "3D01": // datetime
                    VariantBaseType = SqlDbType.DateTime;
                    fvaluehex = logcont.Stuff(0, "3D01".Length, "");
                    fvalue = TranslateData_DateTime(data, pvc.FStartIndex + 2);
                    break;
                case "3E01": // float
                    VariantBaseType = SqlDbType.Float;
                    fvaluehex = logcont.Stuff(0, "3E01".Length, "");
                    length = Convert.ToInt16(fvaluehex.Length / 2);
                    VariantLength = Convert.ToInt16(length == 8 ? 53 : 24);
                    fvalue = TranslateData_Float(data, pvc.FStartIndex + 2, length);
                    break;
                case "6801": // bit
                    VariantBaseType = SqlDbType.Bit;
                    fvaluehex = logcont.Stuff(0, "6801".Length, "");
                    fvalue = (fvaluehex == "01" ? "1" : "0");
                    break;
                case "6C01": // numeric decimal
                    VariantBaseType = SqlDbType.Decimal;
                    fvaluehex = logcont.Stuff(0, "6C01".Length + 4, "");
                    length = Convert.ToInt16(pvc.FEndIndex - pvc.FStartIndex - 4);
                    VariantLength = Int16.Parse(logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    VariantScale = Int16.Parse(logcont.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_Decimal(data, pvc.FStartIndex + 4, length, Convert.ToInt16(VariantScale));
                    break;
                case "A501": // varbinary
                    VariantBaseType = SqlDbType.VarBinary;
                    fvaluehex = logcont.Stuff(0, "A501".Length + 4, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    (_, fvalue) = TranslateData_VarBinary(data, new FVarColumnInfo() { InRow = true, FStartIndex = pvc.FStartIndex + 4, FEndIndex = pvc.FEndIndex });
                    break;
                case "AD01": // binary
                    VariantBaseType = SqlDbType.Binary;
                    fvaluehex = logcont.Stuff(0, "AD01".Length + 4, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    fvalue = TranslateData_Binary(data, pvc.FStartIndex + 4, Convert.ToInt16(VariantLength));
                    break;
                case "AF01": // char
                    VariantBaseType = SqlDbType.Char;
                    fvaluehex = logcont.Stuff(0, "AF01".Length + 12, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Default.GetString(data, pvc.FStartIndex + 8, Convert.ToInt16(VariantLength)).TrimEnd();
                    break;
                case "7A01": // smallmoney
                    VariantBaseType = SqlDbType.SmallMoney;
                    fvaluehex = logcont.Stuff(0, "7A01".Length, "");
                    fvalue = TranslateData_SmallMoney(data, pvc.FStartIndex + 2);
                    break;
                case "7F01": // bigint
                    VariantBaseType = SqlDbType.BigInt;
                    fvaluehex = logcont.Stuff(0, "7F01".Length, "");
                    fvalue = BitConverter.ToInt64(data, pvc.FStartIndex + 2).ToString();
                    break;
                case "A701": // varchar
                    VariantBaseType = SqlDbType.VarChar;
                    fvaluehex = logcont.Stuff(0, "A701".Length + 12, "");
                    VariantLength = Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Default.GetString(fvaluehex.ToByteArray());
                    break;
                case "E701": // nvarchar
                    VariantBaseType = SqlDbType.NVarChar;
                    fvaluehex = logcont.Stuff(0, "E701".Length + 12, "");
                    VariantLength = Convert.ToInt16(Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / Convert.ToInt16(2));
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Unicode.GetString(fvaluehex.ToByteArray());
                    break;
                case "EF01": // nchar
                    VariantBaseType = SqlDbType.NChar;
                    fvaluehex = logcont.Stuff(0, "EF01".Length + 12, "");
                    VariantLength = Convert.ToInt16(Int16.Parse(logcont.Substring(6, 2) + logcont.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 2);
                    VariantCollation = CollationHelper.GetCollationName(logcont.Substring(8, 8));
                    fvalue = System.Text.Encoding.Unicode.GetString(data, pvc.FStartIndex + 8, Convert.ToInt16(VariantLength * 2)).TrimEnd();
                    break;
                default:
                    break;
            }

            return (fvaluehex, fvalue, VariantBaseType, VariantLength, VariantScale, VariantCollation);
        }

        private string GetLOBDataHEX(string lobpointer)
        {
            int cutlen, pageqty, pageqty2, i;
            string fvaluehex, tmpstr, tmpstr2, storagetype, pagedata, subpage, fid, pagenum, filenum, slotnum;
            FPageInfo firstpage, tmppage;
            List<FPageInfo> tmps;

            try
            {
                fid = lobpointer.Substring(0, 8 * 2);
                pagenum = lobpointer.Substring(8 * 2, 4 * 2);
                filenum = lobpointer.Substring((8 + 4) * 2, 2 * 2);
                slotnum = lobpointer.Substring((8 + 4 + 2) * 2, 2 * 2);

                tmpstr = $"{new string('0', 4 * 2)}{pagenum}{filenum}{slotnum}";
                firstpage = new FPageInfo(tmpstr);
                tmppage = GetPageInfo(firstpage.FileNumPageNum_Hex);

                if (firstpage.SlotNum <= tmppage.SlotData.Count - 1)
                {
                    tmpstr = tmppage.SlotData[firstpage.SlotNum];
                    tmpstr = tmpstr.Stuff(0, tmpstr.IndexOf(fid) + fid.Length, "");
                    storagetype = tmpstr.Substring(0, 4);

                    switch (storagetype)
                    {
                        case "0000":
                            tmpstr = tmpstr.Stuff(0, 2 * 2, "");
                            cutlen = Convert.ToInt16(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);
                            tmpstr = tmpstr.Stuff(0, 6 * 2, "");
                            fvaluehex = tmpstr.Substring(0, cutlen * 2);
                            break;
                        case "0300":
                            tmpstr = tmpstr.Stuff(0, 4, "");
                            cutlen = tmpstr.IndexOf("00002121") / 2;
                            fvaluehex = tmpstr.Substring(0, cutlen * 2);
                            break;
                        case "0500":
                            tmpstr = tmpstr.Stuff(0, 4 * 2, "");
                            pageqty = Convert.ToInt32(tmpstr.Substring(2, 2) + tmpstr.Substring(0, 2), 16);
                            tmpstr = tmpstr.Stuff(0, 8 * 2, "");

                            tmps = new List<FPageInfo>();
                            for (i = 0; i <= pageqty - 1; i++)
                            {
                                subpage = tmpstr.Substring(i * 12 * 2, 12 * 2);
                                tmppage = new FPageInfo(subpage);
                                firstpage = GetPageInfo(tmppage.FileNumPageNum_Hex);

                                if (firstpage.PageType == "3")  // TEXT_MIX_PAGE
                                {
                                    tmps.Add(tmppage);
                                    continue;
                                }

                                if (firstpage.PageType == "4")  // TEXT_TREE_PAGE
                                {
                                    pagedata = firstpage.PageData;
                                    pagedata = pagedata.Stuff(0, (96 + 16) * 2, "");
                                    tmpstr2 = pagedata.Substring(0, 4 * 2);
                                    pageqty2 = Convert.ToInt32(tmpstr2.Substring(2, 2) + tmpstr2.Substring(0, 2), 16);

                                    for (i = 0; i <= pageqty2 - 1; i++)
                                    {
                                        tmpstr2 = pagedata.Substring(8 + i * 16 * 2, 16 * 2);
                                        tmppage = new FPageInfo(tmpstr2, "TEXT_TREE_PAGE");
                                        tmps.Add(tmppage);
                                    }
                                    continue;
                                }
                            }

                            fvaluehex = "";
                            i = 0;
                            foreach (FPageInfo tp in tmps)
                            {
                                cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));
                                tmppage = GetPageInfo(tp.FileNumPageNum_Hex);

                                subpage = tmppage.SlotData[tp.SlotNum];
                                subpage = subpage.Stuff(0, subpage.IndexOf(fid) + fid.Length, "");
                                subpage = subpage.Stuff(0, 2 * 2, "");
                                subpage = subpage.Substring(0, cutlen * 2);

                                fvaluehex = fvaluehex + subpage;
                                i = i + 1;
                            }
                            break;
                        case "0800":
                            fvaluehex = null;
                            break;
                        default:
                            fvaluehex = "";
                            break;
                    }
                }
                else
                {
                    fvaluehex = "";
                }

            }
            catch (Exception ex)
            {
                fvaluehex = "";
            }

            return fvaluehex;
        }

        private string GetLOBDataHEX_ForTextInRow(string logcontents)
        {
            int i, pageqty, cutlen;
            string tmpstr, subpage, fvaluehex;
            List<FPageInfo> tmps;
            FPageInfo tmppage;

            try
            {
                tmpstr = logcontents;
                tmpstr = tmpstr.Stuff(0, 12 * 2, "");
                pageqty = tmpstr.Length / 2 / 12;
                tmps = new List<FPageInfo>();
                for (i = 0; i <= pageqty - 1; i++)
                {
                    subpage = tmpstr.Substring(i * 12 * 2, 12 * 2);
                    tmppage = new FPageInfo(subpage);
                    tmps.Add(tmppage);
                }

                fvaluehex = "";
                i = 0;
                foreach (FPageInfo tp in tmps)
                {
                    cutlen = Convert.ToInt32(tp.Offset - (i == 0 ? 0 : tmps[i - 1].Offset));
                    tmppage = GetPageInfo(tp.FileNumPageNum_Hex);

                    subpage = tmppage.PageData;
                    subpage = subpage.Stuff(0, tmppage.SlotBeginIndex[tp.SlotNum] * 2, "");
                    subpage = subpage.Stuff(0, 14 * 2, "");
                    subpage = subpage.Substring(0, cutlen * 2);

                    fvaluehex = fvaluehex + subpage;
                    i = i + 1;
                }
            }
            catch (Exception ex)
            {
                fvaluehex = "";
            }

            return fvaluehex;
        }

    }

}
