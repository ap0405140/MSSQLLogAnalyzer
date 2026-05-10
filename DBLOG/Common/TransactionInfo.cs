using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBLOG.Common
{
    public class TransactionInfo
    {
        public string TransactionID { get; set; }
        public string TransactionType { get; set; }
        public string TransactionName { get; set; }
        public DateTime FTime { get; set; }

        public List<string> PartitionID;
        public List<string> AllocUnitId { get; set; }
        public string AllocUnitName { get; set; }

        public List<string> LSNList { get; set; }

        public TransactionInfo()
        {
            AllocUnitId = new List<string>();
            LSNList = new List<string>();

        }

    }

}
