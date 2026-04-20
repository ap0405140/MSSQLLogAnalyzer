using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    public class FPageInfo
    {
        private string offset_str, pagenum_str, filenum_str, slotnum_str;

        public FPageInfo()
        {

        }

        public FPageInfo(string ppageinfo, string pfrom = "INROW")
        {
            if (pfrom == "INROW")
            {
                offset_str = ppageinfo.Substring(0, 4 * 2);
                pagenum_str = ppageinfo.Substring(4 * 2, 4 * 2);
                filenum_str = ppageinfo.Substring(8 * 2, 2 * 2);
                slotnum_str = ppageinfo.Substring(10 * 2, 2 * 2);
            }
            else
            {   // TEXT_TREE_PAGE
                offset_str = ppageinfo.Substring(0, 4 * 2);
                pagenum_str = ppageinfo.Substring(8 * 2, 4 * 2);
                filenum_str = ppageinfo.Substring(12 * 2, 2 * 2);
                slotnum_str = ppageinfo.Substring(14 * 2, 2 * 2);
            }

            Offset = Convert.ToInt64(offset_str.Substring(6, 2) + offset_str.Substring(4, 2) + offset_str.Substring(2, 2) + offset_str.Substring(0, 2), 16);
            PageNum = Convert.ToInt64(pagenum_str.Substring(6, 2) + pagenum_str.Substring(4, 2) + pagenum_str.Substring(2, 2) + pagenum_str.Substring(0, 2), 16);
            FileNum = Convert.ToInt32(filenum_str.Substring(2, 2) + filenum_str.Substring(0, 2), 16);
            SlotNum = Convert.ToInt32(slotnum_str.Substring(2, 2) + slotnum_str.Substring(0, 2), 16);
            FileNumPageNum_Hex = $"{filenum_str.Substring(2, 2)}{filenum_str.Substring(0, 2)}:{pagenum_str.Substring(6, 2)}{pagenum_str.Substring(4, 2)}{pagenum_str.Substring(2, 2)}{pagenum_str.Substring(0, 2)}";
        }

        public long Offset { get; set; }
        public long PageNum { get; set; }
        public int FileNum { get; set; }
        public int SlotNum { get; set; }
        public string FileNumPageNum_Hex { get; set; }
        public string PageData { get; set; }
        public string PageType { get; set; }

        public int SlotCnt { get; set; }
        public List<int> SlotBeginIndex { get; set; }
        public Dictionary<int, string> SlotData { get; set; }
    }

}
