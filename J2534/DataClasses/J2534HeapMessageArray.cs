using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace J2534
{
    public class J2534HeapMessageArray : IDisposable
    {
        private int global_protocol_id;
        private int array_max_length;
        private IntPtr pMessages;
        private J2534HeapInt length;
        private bool disposed;

        public IntPtr Ptr { get { return pMessages; } }

        public J2534HeapMessageArray(int Length)
        {
            if (Length < 1)
                throw new ArgumentException("Length must be at least 1 (HEAPMessageArray");
            array_max_length = Length;
            length = new J2534HeapInt();
            pMessages = Marshal.AllocHGlobal(CONST.J2534MESSAGESIZE * Length);
        }

        public J2534HeapInt Length
        {
            get
            {
                return length;
            }
            set
            {
                if (value > array_max_length)
                {
                    throw new IndexOutOfRangeException("Length is greater than array bound (HEAPMessageArray)");
                }
                else
                    length = value;
            }
        }

        private J2534PROTOCOL ProtocolID
        {
            get
            {
                global_protocol_id = Marshal.ReadInt32(pMessages); //return the ProtocolID of the first message in the array
                return (J2534PROTOCOL)global_protocol_id;
            }
            set
            {
                global_protocol_id = (int)value;
            }
        }

        public J2534Message this[int index]
        {
            get
            {
                if (index < length &&
                    index >= 0)
                    return ElementAt(index);
                throw new IndexOutOfRangeException("Index out of range in J2534HeapMessageArray get[]");
            }
            set
            {
                if (index < length &&
                    index >= 0)
                    InsertAt(index, value);
                throw new IndexOutOfRangeException("Index out of range in J2534HeapMessageArray set[]");
            }
        }

        //This method is kept private because it bypasses bounds checks
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private J2534Message ElementAt(int index)
        {
            IntPtr pMessage = IntPtr.Add(pMessages, index * CONST.J2534MESSAGESIZE);
            return new J2534Message()
            {
                FlagsAsInt = Marshal.ReadInt32(pMessage, 4),
                Timestamp = (uint)Marshal.ReadInt32(pMessage, 12),
                //ExtraDataIndex = (uint)Marshal.ReadInt32(pMessage, 20),
                Data = MarshalHeapDataToArray(pMessage),
            };
        }

        //This method is kept private because it bypasses bounds checks
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertAt(int index, J2534Message value)
        {
            IntPtr pMessage = IntPtr.Add(pMessages, index * CONST.J2534MESSAGESIZE);
            Marshal.WriteInt32(pMessage, global_protocol_id);
            Marshal.WriteInt32(pMessage, 8, value.FlagsAsInt);
            MarshalIEnumerableToHeapData(pMessage, value.Data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] MarshalHeapDataToArray(IntPtr pData)
        {
            int Length = Marshal.ReadInt32(pData, 16);
            byte[] data = new byte[Length];
            Marshal.Copy(IntPtr.Add(pData, 24), data, 0, Length);
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MarshalIEnumerableToHeapData(IntPtr pMessage, IEnumerable<byte> Data)
        {
            if (Data is byte[])  //Byte[] is fastest
            {
                var DataAsArray = (byte[])Data;
                Marshal.WriteInt32(pMessage, 16, DataAsArray.Length);
                Marshal.Copy(DataAsArray, 0, IntPtr.Add(pMessage, 24), DataAsArray.Length);
            }
            else if (Data is IList<byte>)   //Collection with indexer is second best
            {
                var DataAsList = (IList<byte>)Data;
                int length = DataAsList.Count;
                IntPtr Ptr = IntPtr.Add(pMessage, 24);  //Offset to data array
                Marshal.WriteInt32(pMessage, 16, length);
                for (int indexer = 0; indexer < length; indexer++)
                {
                    Marshal.WriteByte(Ptr, indexer, DataAsList[indexer]);
                }
            }
            else//Enumerator is third
            {
                IntPtr Ptr = IntPtr.Add(pMessage, 24);  //Offset to data array
                int index_count = 0;
                foreach (byte b in Data)
                {
                    Marshal.WriteByte(Ptr, index_count, b);
                    index_count++;
                }
                Marshal.WriteInt32(pMessage, 16, index_count);  //Set length
            }
        }

        public J2534MessageList ToJ2534MessageList()
        {
            J2534MessageList return_list = new J2534MessageList();
            return_list.ProtocolID = ProtocolID;
            for (int i = 0; i < Length; i++)
                return_list.Add(ElementAt(i));
            return return_list;
        }

        public void DeepCopy(J2534MessageList Messages)
        {
            if (Messages.Count > array_max_length)
                throw new ArgumentException("J2534MessageList Count exceeds J2534HeapMessageArray buffer length!");
            ProtocolID = Messages.ProtocolID;
            int index = 0;
            for (; index < Messages.Count;index++)
            {
                InsertAt(index, Messages[index]);
                index++;
            }
            Length = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InsertSingle(J2534PROTOCOL ProtocolID, J2534TXFLAG TxFlags, IEnumerable<byte> Data)
        {
            Length = 1;
            Marshal.WriteInt32(pMessages, (int)ProtocolID);
            Marshal.WriteInt32(pMessages, 8, (int)TxFlags);
            MarshalIEnumerableToHeapData(pMessages, Data);
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
            Marshal.FreeHGlobal(pMessages);
            length.Dispose();
            disposed = true;
        }
        ~J2534HeapMessageArray()
        {
            Dispose(false);
        }
    }
}
