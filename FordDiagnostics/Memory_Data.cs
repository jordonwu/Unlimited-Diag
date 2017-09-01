using System;

namespace FordDiag
{
    public class Memory_Blob
    {
        public int Address { get; set; }
        public byte[] Data { get; set; }

        public Memory_Blob()
        {

        }

        public Memory_Blob(int address, byte [] data)
        {
            Address = address;
            Data = data;
        }
    }
}
