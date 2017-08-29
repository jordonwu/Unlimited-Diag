using System;
using System.Collections.Generic;

namespace SAE
{
    public class SAEMessage
    {
        private SAEModes mode;
        private List<byte> data;
        public int Address { get; set; }
        public SAEModes Mode
        {
            get
            {
                return mode;
            }
            set
            {
                DataList.Clear();
                data.Clear();
                mode = value;
            }
        }
        public List<byte[]> DataList { get; set; }
        public SAE_responses Response { get; set; }
        public bool IsValid { get; set; }
//        public J2534.J2534ERR Status { get; set; }

        public SAEMessage(int Address) : this()
        {
            this.Address = Address;
        }

        public SAEMessage()
        {
            DataList = new List<byte[]>();
            mode = new SAEModes();
            data = new List<byte>();
            IsValid = false;
        }

        public byte[] Data
        {
            get
            {
                return data.ToArray();
            }
            set
            {
                data.Clear();
                data.AddRange(value);
            }
        }

        public void AddInt8(int Value)
        {
            data.Add((byte)Value);
        }

        public void AddInt16(int Value)
        {
            data.Add((byte)(Value >> 8));
            data.Add((byte)Value);
        }

        public void AddInt24(int Value)
        {
            data.Add((byte)(Value >> 16));
            data.Add((byte)(Value >> 8));
            data.Add((byte)Value);
        }

        public void AddInt32(int Value)
        {
            data.Add((byte)(Value >> 24));
            data.Add((byte)(Value >> 16));
            data.Add((byte)(Value >> 8));
            data.Add((byte)Value);
        }

        public void AddBytes(byte[] Data)
        {
            data.AddRange(Data);
        }
    }
}
