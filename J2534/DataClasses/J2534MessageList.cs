using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace J2534
{
    public class J2534MessageList : List<J2534Message>
    {
        public J2534PROTOCOL ProtocolID { get; set; }
    }
}
