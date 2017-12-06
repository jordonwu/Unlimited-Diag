using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace J2534
{
    //Class for creating a single message on the heap.  Used for Periodic messages, filters, etc.
    public class J2534HeapMessage : IDisposable
    {
        private IntPtr pMessage;
        private bool disposed;

        public IntPtr Ptr { get { return pMessage; } }

        public J2534HeapMessage()
        {
            pMessage = Marshal.AllocHGlobal(CONST.J2534MESSAGESIZE);
        }

        public J2534HeapMessage(J2534PROTOCOL ProtocolID, J2534TXFLAG TxFlags, IEnumerable<byte> Data) : this()
        {
            this.ProtocolID = ProtocolID;
            this.TxFlags = TxFlags;
            this.Data = Data;
        }

        public J2534Message Message
        {
            get
            {
                return new J2534Message()
                {
                    RxStatus = this.RxStatus,
                    Timestamp = this.Timestamp,
                    //ExtraDataIndex = this.ExtraDataIndex,
                    Data = this.Data
                };
            }
            set
            {
                this.TxFlags = value.TxFlags;
                this.Timestamp = value.Timestamp;
                //this.ExtraDataIndex = value.ExtraDataIndex;
                this.Data = value.Data;
            }
        }

        public J2534PROTOCOL ProtocolID
        {
            get
            {
                return (J2534PROTOCOL)Marshal.ReadInt32(pMessage); 
            }
            set
            {
                Marshal.WriteInt32(pMessage, (int)value);
            }
        }

        public J2534RXFLAG RxStatus
        {
            get
            {
                return (J2534RXFLAG)Marshal.ReadInt32(pMessage, 4);
            }
            set
            {
                Marshal.WriteInt32(pMessage, 4, (int)value);
            }
        }

        public J2534TXFLAG TxFlags
        {
            get
            {
                return (J2534TXFLAG)Marshal.ReadInt32(pMessage, 8);
            }
            set
            {
                Marshal.WriteInt32(pMessage, 8, (int)value);
            }
        }

        public uint Timestamp
        {
            get
            {
                return (uint)Marshal.ReadInt32(pMessage, 12);
            }
            set
            {
                Marshal.WriteInt32(pMessage, 12, (int)value);
            }
        }

        public uint ExtraDataIndex
        {
            get
            {
                return (uint)Marshal.ReadInt32(pMessage, 20);
            }
            set
            {
                Marshal.WriteInt32(pMessage, 20, (int)value);
            }
        }

        public int Length
        {
            get
            {
                return Marshal.ReadInt32(pMessage, 16);
            }
            private set
            {
                if (value > CONST.J2534MESSAGESIZE)
                {
                    throw new ArgumentException("Message Data.Length is greator than fixed maximum");
                }
                Marshal.WriteInt32(pMessage, 16, value);
            }
        }

        public IEnumerable<byte> Data
        {
            get
            {
                byte[] data = new byte[Marshal.ReadInt32(pMessage, 16)];
                Marshal.Copy(IntPtr.Add(pMessage, 24), data, 0, data.Length);
                return data;
            }
            set
            {
                if (value is byte[])  //Byte[] is fastest
                {
                    var ValueAsArray = (byte[])value;
                    Length = ValueAsArray.Length;
                    Marshal.Copy(ValueAsArray, 0, IntPtr.Add(pMessage, 24), ValueAsArray.Length);
                }
                else if (value is IList<byte>)   //Collection with indexer is second best
                {
                    var ValueAsList = (IList<byte>)value;
                    int length = ValueAsList.Count;
                    IntPtr Ptr = IntPtr.Add(pMessage, 24);  //Offset to data array
                    Length = length;
                    for (int indexer = 0; indexer < length; indexer++)
                    {
                        Marshal.WriteByte(Ptr, indexer, ValueAsList[indexer]);
                    }
                }
                else//Enumerator is third
                {
                    IntPtr Ptr = IntPtr.Add(pMessage, 24);  //Offset to data array
                    int index_count = 0;
                    foreach (byte b in value)
                    {
                        Marshal.WriteByte(Ptr, index_count, b);
                        index_count++;
                    }
                    Length = index_count;  //Set length
                }
            }
        }

        public static implicit operator J2534Message(J2534HeapMessage HeapMessage)
        {
            return HeapMessage.Message;
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //
            Marshal.FreeHGlobal(pMessage);
            disposed = true;
        }
        ~J2534HeapMessage()
        {
            Dispose(false);
        }
    }
}
