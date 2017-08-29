using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FordDiag.Controllers
{
    public class SCP_FLASH_IMAGE
    {
        private byte[] bank0 = new byte[0xE000];
        private byte[] bank1;
        private byte[] bank8 = new byte[0xE000];
        private byte[] bank9 = new byte[0xE000];
        private byte[] bankFE = new byte[0xE000];

        private SCP_VIN_Parser vin;
        private RomSize flash_size;

        public SCP_FLASH_IMAGE(RomSize FlashSize)
        {
            switch (FlashSize)
            {
                case RomSize._88k:
                    bank1 = new byte[0x8000];
                    break;
                case RomSize._112k:
                    bank1 = new byte[0xE000];
                    break;
                case RomSize._216k:
                    bank1 = new byte[0xC000];
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Invalid romsize when constructing SCP_FLASH_IMAGE");
            }
            flash_size = FlashSize;
            vin = new SCP_VIN_Parser();
        }

        public byte[] Read(int Address, int Length)
        {
            int data_end = Address + Length;

            if (Address >= 0x82000 && data_end <= 0x90000)
            {
                return bank8.Skip(Address - 0x82000).Take(Length).ToArray();
            }
            if (Address >= 0xFE2000 && data_end < 0xFF0000)
            {
                return bankFE.Skip(Address - 0xFE2000).Take(Length).ToArray();
            }
            switch (flash_size)
            {
                case RomSize._88k:
                    if (Address >= 0x12000 && data_end <= 0x1A000)
                    {
                        return bank1.Skip(Address - 0x12000).Take(Length).ToArray();
                    }
                    break;
                case RomSize._112k:
                    if (Address >= 0x12000 && data_end <= 0x20000)
                    {
                        return bank1.Skip(Address - 0x12000).Take(Length).ToArray();
                    }
                    break;
                case RomSize._216k:
                    if (Address >= 0x12000 && data_end <= 0x1E000)
                    {
                        return bank1.Skip(Address - 0x12000).Take(Length).ToArray();
                    }
                    if (Address >= 0x02000 && data_end <= 0x10000)
                    {
                        return bank0.Skip(Address - 0x02000).Take(Length).ToArray();
                    }
                    if (Address >= 0x92000 && data_end <= 0xA0000)
                    {
                        return bank9.Skip(Address - 0x92000).Take(Length).ToArray();
                    }
                    break;
                default:
                    break;
            }
            throw new ArgumentOutOfRangeException("Address and/or Data.length out of range!");
        }

        public void Write(int Address, byte[] Data)
        {
            int data_end = Address + Data.Length;

            if (Address >= 0x82000 && data_end <= 0x90000)
            {
                Buffer.BlockCopy(Data, 0, bank8, Address - 0x82000, Data.Length);
                return;
            }
            if (Address >= 0xFE2000 && data_end < 0xFF0000)
            {
                Buffer.BlockCopy(Data, 0, bankFE, Address - 0xFE2000, Data.Length);
                return;
            }
            switch (flash_size)
            {
                case RomSize._88k:
                    if(Address >= 0x12000 && data_end <= 0x1A000)
                    {
                        Buffer.BlockCopy(Data, 0, bank1, Address - 0x12000, Data.Length);
                        if (data_end > 0x19F00)
                        {
                            vin.RAWVIN = Read(0x19F00, 0x100);
                        }
                        return;
                    }
                    break;
                case RomSize._112k:
                    if (Address >= 0x12000 && data_end <= 0x20000)
                    {
                        Buffer.BlockCopy(Data, 0, bank1, Address - 0x12000, Data.Length);
                        if (data_end > 0x1FF00)
                        {
                            vin.RAWVIN = Read(0x1FF00, 0x100);
                        }
                        return;
                    }
                    break;
                case RomSize._216k:
                    if (Address >= 0x12000 && data_end <= 0x1E000)
                    {
                        Buffer.BlockCopy(Data, 0, bank1, Address - 0x12000, Data.Length);
                        return;
                    }
                    if (Address >= 0x02000 && data_end <= 0x10000)
                    {
                        Buffer.BlockCopy(Data, 0, bank0, Address - 0x02000, Data.Length);
                        return;
                    }
                    if (Address >= 0x92000 && data_end <= 0xA0000)
                    {
                        Buffer.BlockCopy(Data, 0, bank9, Address - 0x92000, Data.Length);
                        if (data_end > 0x9FF00)
                        {
                            vin.RAWVIN = Read(0x9FF00, 0x100);
                        }
                        return;
                    }
                    break;
                default:
                    break;            }
            throw new ArgumentOutOfRangeException("Address and/or Data.length out of range!");
        }

        public SCP_VIN_Parser VIN
        {
            get
            {
                return vin;
            }
            set
            {
                vin = value;
                switch (flash_size)
                {
                    case RomSize._88k:
                        Write(0x19F00, vin.RAWVIN);
                        break;
                    case RomSize._112k:
                        Write(0x1FF00, vin.RAWVIN);
                        break;
                    case RomSize._216k:
                        Write(0x9FF00, vin.RAWVIN);
                        break;
                }
            }
        }

        public RomSize FlashSize
        {
            get
            {
                return flash_size;
            }
        }

        public int VinAddress
        {
            get
            {
                switch (flash_size)
                {
                    case RomSize._216k:
                        return 0x09FF00;
                    case RomSize._112k:
                        return 0x01FF00;
                    case RomSize._88k:
                        return 0x019F00;
                }
                throw new ArgumentOutOfRangeException("Invalid flash_size in VinAddress");
            }
        }

        public byte[] GetBank(int Bank)
        {
            switch (Bank)
            {
                case 8:
                    return bank8.ToArray();
                case 1:
                    return bank1.ToArray();
                case 0:
                    return bank0.ToArray();
                case 9:
                    return bank9.ToArray();
                case 0xFE:
                    return bankFE.ToArray();
                default:
                    return Array.Empty<byte>();
            }
        }

        public int Checksum
        {
            get
            {
                int checksum = 0;

                switch (flash_size)
                {
                    case RomSize._216k:
                        checksum += bank0.Sum(b => (int)b);
                        checksum += bank1.Sum(b => (int)b);
                        checksum += bank8.Sum(b => (int)b);
                        checksum += bank9.Take(0xDF80).Sum(b => (int)b);
                        break;
                    case RomSize._112k:
                        checksum += bank1.Take(0xDF80).Sum(b => (int)b);
                        checksum += bank8.Sum(b => (int)b);
                        break;
                    case RomSize._88k:
                        checksum += bank1.Take(0x9F80).Sum(b => (int)b);
                        checksum += bank8.Sum(b => (int)b);
                        break;
                    default:
                        throw new Exception("Undefined flash size when attempting to calculate checksum!");
                }
                return checksum;
            }
        }

        public string ToHex()
        {
            switch (flash_size)
            {
                case RomSize._216k:
                case RomSize._112k:
                case RomSize._88k:
                    return null;
            }
            throw new Exception();
        }

        public byte[] SCTBINImage
        {
            get
            {
                List<byte> result = new List<byte>();
                switch (flash_size)
                {
                    case RomSize._216k:
                        result.AddRange(GetBank(0));
                        result.AddRange(GetBank(1));
                        result.AddRange(GetBank(9));
                        result.AddRange(GetBank(8));
                        return result.ToArray();
                    case RomSize._112k:
                    case RomSize._88k:
                        result.AddRange(GetBank(1));
                        result.AddRange(GetBank(8));
                        return result.ToArray();

                }
                throw new Exception();
            }
            set
            {
                switch (value.Length)
                {
                    case 0x36000:
                        flash_size = RomSize._216k;
                        Write(0x02000, value.Take(0xE000).ToArray());
                        Write(0x12000, value.Skip(0xE000).Take(0xC000).ToArray());
                        Write(0x92000, value.Skip(0x1A000).Take(0xE000).ToArray());
                        Write(0x82000, value.Skip(0x28000).Take(0xE000).ToArray());
                        break;
                    case 0x1C000:
                        flash_size = RomSize._112k;
                        Write(0x12000, value.Take(0xE000).ToArray());
                        Write(0x82000, value.Skip(0xE000).Take(0xE000).ToArray());
                        break;
                    case 0x16000:
                        flash_size = RomSize._88k;
                        Write(0x12000, value.Take(0x8000).ToArray());
                        Write(0x82000, value.Skip(0x8000).Take(0xE000).ToArray());
                        break;
                    default:
                        throw new Exception("Unknown flash size");
                }
            }
        }

        public byte[] DiabloBINImage
        {
            get
            {
                List<byte> result = new List<byte>();
                switch (flash_size)
                {
                    case RomSize._216k:
                        result.AddRange(GetBank(8));
                        result.AddRange(GetBank(1));
                        result.AddRange(new byte[0x2000]);  //Pad file
                        result.AddRange(GetBank(0));
                        result.AddRange(GetBank(9));
                        return result.ToArray();
                    case RomSize._112k:
                        result.AddRange(GetBank(8));
                        result.AddRange(GetBank(1));
                        result.AddRange(new byte[0x1C000]);  //Padding
                        return result.ToArray();
                    case RomSize._88k:
                        result.AddRange(GetBank(8));
                        result.AddRange(GetBank(1));
                        result.AddRange(new byte[0x22000]);  //Padding
                        return result.ToArray();
                }
                throw new Exception();
            }
        }
    }

    public enum RomSize
    {
        _216k = 0xD8,
        _112k = 0x70,
        _88k = 0x58,
        UNDEFINED = 0x00
    }
}
