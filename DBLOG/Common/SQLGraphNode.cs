using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    public class SQLGraphNode
    {
        public string type { get; set; }
        public string schema { get; set; }
        public string table { get; set; }
        public int id { get; set; }
    }
}
