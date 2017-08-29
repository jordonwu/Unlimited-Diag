using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace J2534
{
    public class J2534Status
    {
        public string Description { get; set; }
        public J2534ERR Code { get; set; }

        public J2534Status(J2534ERR Code = J2534ERR.STATUS_NOERROR, string Description = "")
        {
            this.Description = Description;
            this.Code = Code;
        }

        public bool IsClear
        {
            get
            {
                return Code == J2534ERR.STATUS_NOERROR;
            }
        }

        public bool IsNOTClear
        {
            get
            {
                return Code != J2534ERR.STATUS_NOERROR;
            }
        }
    }
}
