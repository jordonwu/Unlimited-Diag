using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace J2534.DataClasses
{
    unsafe struct NativePassThruMessage
    {
        public int ProtocolID;
        public int RxStatus;
        public int TxFlags;
        public uint TimeStamp;
        public int DataLength;
        public int ExtraDataIndex;
        public fixed byte Data[4128];
    }
    struct NativeSConfig
    {
        int Parameter;
        int Value;
    }
    struct NativeSConfigList
    {
        int NumOfParams;
        int PtrToSConfig;
    }
    struct NativeSByteArray
    {
        int NumOfBytes;
        int PtrToArray;
    }
}
