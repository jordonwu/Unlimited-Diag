using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FordDiag
{
    public class SCP_VIN_Parser
    {
        private byte[] vin_raw;

        public SCP_VIN_Parser()
        {
            vin_raw = new byte[0x100];
        }

        public SCP_VIN_Parser(byte [] Vin_block) : this()
        {
            RAWVIN = Vin_block;
        }

        public byte [] RAWVIN
        {
            get
            {
                return vin_raw;
            }
            set
            {
                value.Take(0x100).ToArray().CopyTo(vin_raw, 0);
                FixCheckSum();
            }
        }

        public byte [] PATS_Key
        {
            get
            {
                return vin_raw.Skip(0x1C).Take(0x10).ToArray();
            }
            set
            {
                value.Take(0x10).ToArray().CopyTo(vin_raw, 0x1C);
                FixCheckSum();
            }
        }

        public byte [] VIN
        {
            get
            {
                return vin_raw.Skip(0x80).Take(0x15).ToArray();
            }
            set
            {
                value.Take(0x15).ToArray().CopyTo(vin_raw, 0x80);
                FixCheckSum();
            }
        }

        public string FileName
        {
            get
            {
                return Encoding.UTF8.GetString(vin_raw.Skip(0x06).Take(0x0B).ToArray());
            }
            set
            {
                Encoding.UTF8.GetBytes(value).Take(0x0B).ToArray().CopyTo(vin_raw, 0x06);
                FixCheckSum();
            }
        }

        public string Copyright
        {
            get
            {
                return Encoding.UTF8.GetString(vin_raw.Skip(0x63).Take(0x1D).ToArray());
            }
            set
            {
                Encoding.UTF8.GetBytes(value).Take(0x1D).ToArray().CopyTo(vin_raw, 0x63);
                FixCheckSum();
            }
        }
        public string StrategyName
        {
            get
            {
                return Encoding.UTF8.GetString(vin_raw.Skip(0x14).Take(0x07).ToArray());
            }
            set
            {
                Encoding.UTF8.GetBytes(value).Take(0x07).ToArray().CopyTo(vin_raw, 0x14);
                FixCheckSum();
            }
        }

        public float AxleRatio
        {
            get
            {
                return (float)(BitConverter.ToInt16(vin_raw, 0x96) * (Math.Pow(2, -10)));
            }
            set
            {
                BitConverter.GetBytes((short)(value * (Math.Pow(2, 10)))).CopyTo(vin_raw, 96);
                FixCheckSum();
            }
        }

        public bool IsCheckSumValid
        {
            get
            {
                if (calculated_checksum == BitConverter.ToInt16(vin_raw, 0xFE))
                {
                    return true;
                }
                return false;
            }
        }

        public void FixCheckSum()
        {
            int checksum = calculated_checksum;
            vin_raw[0xFE] = (byte)checksum;
            vin_raw[0xFF] = (byte)(checksum >> 8);
        }

        private int calculated_checksum
        {
            get
            {
                int checksum = 0;
                for (int i = 0; i < 0xFE; i += 2)
                {
                    checksum += BitConverter.ToInt16(vin_raw, i);
                }
                return ~checksum;
            }
        }
    }
}
