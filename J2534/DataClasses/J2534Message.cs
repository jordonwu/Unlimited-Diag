using System;
using System.Collections.Generic;

namespace J2534
{
    public class J2534Message
    {
        public int FlagsAsInt { get; set; }
        public uint Timestamp { get; set; }
        //public int ExtraData { get; set; }    //Not implemented.
        public IEnumerable<byte> Data { get; set; }

        public J2534Message()
        {
        }

        public J2534Message(IEnumerable<byte> Data, J2534TXFLAG TxFlags = J2534TXFLAG.NONE)
        {
            this.TxFlags = TxFlags;
            this.Data = Data;
        }

        public J2534RXFLAG RxStatus
        {
            get
            {
                return (J2534RXFLAG)FlagsAsInt;
            }
            set
            {
                FlagsAsInt = (int)value;
            }
        }

        public J2534TXFLAG TxFlags
        {
            get
            {
                return (J2534TXFLAG)FlagsAsInt;
            }
            set
            {
                FlagsAsInt = (int)value;
            }
        }
    }
}
