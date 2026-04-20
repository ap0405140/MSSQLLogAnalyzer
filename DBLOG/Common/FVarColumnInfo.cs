using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    public class FVarColumnInfo
    {
        public short FIndex { get; set; }
        public string FLogContents { get; set; }
        public int FStartIndex { get; set; }
        public int FEndIndex { get; set; }
        public string FEndIndexHex { get; set; }
        public bool InRow { get; set; }
    }

}
