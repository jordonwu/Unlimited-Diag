using System;
using System.Collections.Generic;

namespace J2534
{
    public class J2534Message
    {
        public J2534PROTOCOL ProtocolID { get; set; }
        public J2534RXFLAG RxStatus { get; set; }
        public J2534TXFLAG TxFlags { get; set; }
        public uint Timestamp { get; set; }
        public uint ExtraDataIndex { get; set; }
        public IEnumerable<byte> Data { get; set; }

        public J2534Message()
        {
        }

        public J2534Message(J2534PROTOCOL ProtocolID, J2534TXFLAG TxFlags, IEnumerable<byte> Data)
        {
            this.ProtocolID = ProtocolID;
            this.TxFlags = TxFlags;
            this.Data = Data;
        }
    }
}
