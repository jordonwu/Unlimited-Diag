using SAE.Session.Ford;
using J2534;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
namespace FordDiag.Controllers
{
    public class Ford_SCP_Module
    {
        private SCP_FLASH_IMAGE firmware_image;
        private FordPWMSession session;
        private J2534Device device;
        private byte salt;

        public Ford_SCP_Module(J2534Device Device)
        {
            device = Device;
            session = new FordPWMSession(device);
            System.Random random = new System.Random();
            salt = (byte)random.Next(255);
        }

        public void EraseFlash()
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            Command.Mode = SAE.SAEModes.START_DIAG_ROUTINE_BY_NUMBER;
            Command.AddInt8((int)DiagRoutine.EraseFlash);

            Response = session.SAETxRx(Command, 0);

            if (Response.Response != SAE.SAE_responses.ROUTINE_NOT_COMPLETE)
            {
                throw new Exception("Failure executing flash erase routine");
            }

            System.Threading.Thread.Sleep(4000);

            Command.Mode = SAE.SAEModes.STOP_DIAG_ROUTINE_BY_NUMBER;
            Command.AddInt8((int)DiagRoutine.EraseFlash);

            Response = session.SAETxRx(Command, 0);
            if (Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
            {
                throw new Exception("Failure stopping flash erase routine!");
            }
        }

        public bool Checksum()
        {

            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            Command.Mode = SAE.SAEModes.START_DIAG_ROUTINE_BY_NUMBER;
            Command.AddInt8((int)DiagRoutine.Checksum);
            Command.AddInt16(firmware_image.Checksum);

            Response = session.SAETxRx(Command, 0);

            if (Response.Response != SAE.SAE_responses.ROUTINE_NOT_COMPLETE)
            {
                throw new Exception("Failure attempting to execute checksum routine on PCM");
            }

            System.Threading.Thread.Sleep(2000);

            Command.Mode = SAE.SAEModes.STOP_DIAG_ROUTINE_BY_NUMBER;
            Command.AddInt8((int)DiagRoutine.Checksum);

            Response = session.SAETxRx(Command, 0);

            if (Response.Response == SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
            {
                return true;
            }
            return false;
        }

        public void ReadFirmware()
        {
            if (IVFERHandshake())
            {
                firmware_image = new SCP_FLASH_IMAGE(ProbeMemLayout());

                switch (firmware_image.FlashSize)
                {
                    case RomSize._216k:
                        firmware_image.Write(0x02000, BlockRead(0x002000, 0x00FFFF));
                        firmware_image.Write(0x12000, BlockRead(0x012000, 0x01DFFF));
                        firmware_image.Write(0x82000, BlockRead(0x082000, 0x08FFFF));
                        firmware_image.Write(0x92000, BlockRead(0x092000, 0x09FFFF));
                        break;
                    case RomSize._112k:
                        firmware_image.Write(0x12000, BlockRead(0x012000, 0x01FFFF));
                        firmware_image.Write(0x82000, BlockRead(0x082000, 0x08FFFF));
                        break;
                    case RomSize._88k:
                        firmware_image.Write(0x12000, BlockRead(0x012000, 0x019FFF));
                        firmware_image.Write(0x82000, BlockRead(0x082000, 0x08FFFF));
                        break;
                    default:
                        break;
                }
                if (!Checksum())
                {
                    throw new Exception("Checksum of firmware read failed!");
                }
                //System.IO.File.WriteAllBytes("read.bin", firmware_image.ToSCTBIN());
            }
        }

        public void WriteFirmware(bool TransferVIN = true)
        {
            firmware_image = new SCP_FLASH_IMAGE(RomSize._216k);
            firmware_image.SCTBINImage = System.IO.File.ReadAllBytes("read.bin");

            if (IVFERHandshake())
            {
                if (ProbeMemLayout() != firmware_image.FlashSize)
                    throw new Exception("Loaded firmware image does not match target hardware!");

                SCP_VIN_Parser PCM_VIN = new SCP_VIN_Parser(ReadVINBlock());
                //Write Vin to disk

                if (TransferVIN)
                {
                    firmware_image.VIN.PATS_Key = PCM_VIN.PATS_Key;
                    firmware_image.VIN.VIN = PCM_VIN.VIN;
                }

                EraseFlash();

                switch (firmware_image.FlashSize)
                {
                    case RomSize._88k:
                        ProgramBank(0x12000, firmware_image.GetBank(1));
                        ProgramBank(0x82000, firmware_image.GetBank(8));
                        break;
                    case RomSize._112k:
                        ProgramBank(0x12000, firmware_image.GetBank(1));
                        ProgramBank(0x82000, firmware_image.GetBank(8));
                        break;
                    case RomSize._216k:
                        ProgramBank(0x02000, firmware_image.GetBank(0));
                        ProgramBank(0x12000, firmware_image.GetBank(1));
                        ProgramBank(0x92000, firmware_image.GetBank(9));
                        ProgramBank(0x82000, firmware_image.GetBank(8));
                        break;
                }
                if (!Checksum())
                {
                    throw new Exception("Checksum of firmware write failed!");
                }
            }
        }

        private byte [] ReadVINBlock()
        {
            List<byte> vin = new List<byte>();
            int VinAddress = firmware_image.VinAddress;

            for(int offset = 0;offset < 256;offset += 4)
            {
                vin.AddRange(ReadLocation(VinAddress + offset));
            }
            return vin.ToArray();
        }

        private byte [] ReadLocation(int Address)
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            Command.Mode = SAE.SAEModes.DATA_BY_ADDRESS;
            Command.AddInt24(Address);

            Response = session.SAETxRx(Command, 2);

            if (Response.Mode != SAE.SAEModes.DATA_BY_ADDRESS_RESPONSE)
            {
                throw new Exception("Failure reading memory address");
            }

            return Response.Data;
        }

        private bool IVFERHandshake(bool HighSpeed = true)
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address:0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            session.InitializeIVEFERConfig();
            session.FEPSOn();
            do
            {
                //break on key press or timeout?
            } while (session.CatchBroadcastMessage() == false);

            Command.Mode = SAE.SAEModes.DIAG_HEARTBEAT;

            session.StartPeriodicMessage(Command);

            Command.Mode = SAE.SAEModes.START_DIAG_ROUTINE_BY_NUMBER;
            Command.AddInt8((int)DiagRoutine.IVFEREntry);
            Command.AddInt16(0x00D8);   //Number of blocks to write (not used for reading)
            Command.AddInt8(salt);
            Command.AddInt8(0x00);

            Response = session.SAETxRx(Command, 0);

            if (Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
            {
                System.Windows.Forms.MessageBox.Show("IVFER entry failed!");
                return false;
            }

            Command.Mode = SAE.SAEModes.REQ_SECURITY_ACCESS;
            Command.AddInt8(0x01);

            Response = session.SAETxRx(Command, 1);

            if (Response.Mode != SAE.SAEModes.REQ_SECURITY_ACCESS_RESPONSE
               && Response.Data.Length == 3)
            {
                System.Windows.Forms.MessageBox.Show("Security Access Level 1 entry failed!");
                return false;
            }

            Command.Mode = SAE.SAEModes.REQ_SECURITY_ACCESS;
            Command.AddInt8(0x02);
            Command.AddBytes(ComputeKey1(Response.Data));

            Response = session.SAETxRx(Command, 1);

            if (Response.Response != SAE.SAE_responses.SECURITY_ACCESS_ALLOWED)
            {
                System.Windows.Forms.MessageBox.Show("Security Access Level 1 Challenge failed!");
                return false;
            }

            //Request level two security for high data rate
            Command.Mode = SAE.SAEModes.REQ_SECURITY_ACCESS;
            Command.AddInt8(0x01);

            Response = session.SAETxRx(Command, 1);

            if (Response.Mode == SAE.SAEModes.REQ_SECURITY_ACCESS_RESPONSE
                && Response.Data.Length == 3)
            {
                Command.Mode = SAE.SAEModes.REQ_SECURITY_ACCESS;
                Command.AddInt8(0x02);
                Command.AddBytes(ComputeKey2(Response.Data));

                Response = session.SAETxRx(Command, 1);

                if (Response.Response != SAE.SAE_responses.SECURITY_ACCESS_ALLOWED)
                {
                    System.Windows.Forms.MessageBox.Show("Security Access Level 2 Challenge failed!");
                    return false;
                }

                if(!HighSpeed)
                    return true;

                Command.Mode = SAE.SAEModes.START_DIAG_ROUTINE_BY_NUMBER;
                Command.AddInt8((int)DiagRoutine.BaudRate);
                Command.AddInt8(0x03);    //83.2k baud rate

                Response = session.SAETxRx(Command, 0);

                if (Response.Response == SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                {
                    session.HighSpeedMode();
                }
            }
            return true;
        }

        private RomSize ProbeMemLayout()
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            Command.Mode = SAE.SAEModes.DATA_BY_ADDRESS;
            Command.AddInt24(0x09FF00);

            Response = session.SAETxRx(Command, 2);

            if(Response.Mode == SAE.SAEModes.DATA_BY_ADDRESS_RESPONSE)
            {
                return RomSize._216k;
            }

            Command.AddInt24(0x01FFFC);

            Response = session.SAETxRx(Command, 2);

            if (Response.Mode == SAE.SAEModes.DATA_BY_ADDRESS_RESPONSE)
            {
                return RomSize._112k;                
            }

            Command.AddInt24(0x019FFC);

            Response = session.SAETxRx(Command, 2);

            if (Response.Mode == SAE.SAEModes.DATA_BY_ADDRESS_RESPONSE)
            {
                return RomSize._88k;
            }

            throw new Exception("Memory layout is unknown!");   //No known matching memory models
        }

        private int VinAddress()
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            Command.Mode = SAE.SAEModes.DATA_BY_PID;
            Command.AddInt16(0x1100);

            Response = session.SAETxRx(Command, 1);

            if (Response.IsValid)
                return BitConverter.ToInt32(Response.Data, 0);
            throw new J2534Exception("Failure in 'VinAddress'");
        }

        private byte[] BlockRead(int RangeStart, int RangeEnd)
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            int block_length = 0x0400;
            int packets_per_block = block_length / 6 + 1;
            if(block_length % packets_per_block == 0)
            {

            }

            List<byte> data = new List<byte>();

            object block_transfer_handle = session.CreateRxHandle(Command.Address, SAE.SAEModes.DATA_TRANSFER);

            for (int block_address = RangeStart;block_address < RangeEnd;block_address += block_length)
            {
                Command.Mode = SAE.SAEModes.REQ_UPLOAD;
                byte[] Data = GetBytes.Int8(0x80).Concat(
                              GetBytes.Int16(block_length)).Concat(
                              GetBytes.Int24(block_address)).ToArray();
                //Data.C
                Command.AddInt8(0x80);    //bank selection?  (note from old code)
                Command.AddInt16(block_length);
                Command.AddInt24(block_address);

                Response = session.SAETxRx(Command, 2);

                if(Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                {
                    throw new Exception();
                }

                List<byte[]> rx_data = session.SAERx(block_transfer_handle, packets_per_block, 600);

                if(rx_data.Count != packets_per_block)
                {
                    throw new Exception();
                    //fail
                }
                else
                {
                    int a = 1;
                }

                data.AddRange(rx_data.Aggregate((acumulator, next_packet) => acumulator.Concat(next_packet).ToArray()).Take(block_length));

                Command.Mode = SAE.SAEModes.TRANSFER_ROUTINE_EXIT;
                Command.AddInt8(0x80);

                Response = session.SAETxRx(Command, 0);

                if(Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                {
                    throw new Exception();//fail
                }
            }
            session.DestroyRxHandle(block_transfer_handle);
            return data.ToArray();
        }

        public void ProgramBank(int Address, byte [] Data)
        {
            SAE.SAEMessage Command = new SAE.SAEMessage(Address: 0x10);
            SAE.SAEMessage Response = new SAE.SAEMessage();

            int block_length = 0x0400;
            int packets_per_block = block_length / 6 + 1;

            for(int offset = Data.Length - block_length;offset >= 0;offset -= block_length)
            {
                int retry_count = 3;
                do
                {
                    Command.Mode = SAE.SAEModes.REQ_DOWNLOAD;
                    Command.AddInt8(0x80);
                    Command.AddInt16(block_length);
                    Command.AddInt24(Address + offset);

                    Response = session.SAETxRx(Command, 2);

                    if (Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                    {
                        throw new Exception("failure initiatiing block transfer");  //fail
                    }

                    Command.Mode = SAE.SAEModes.DATA_TRANSFER;
                    int BlockCheckSum = Data.Skip(offset).Take(block_length).Sum(b => (int)b);
                    int p = 0;
                    for (; p < (block_length - 4); p += 6)  //Create 170 packets with 6 bytes each
                    {
                        Command.DataList.Add(Data.Skip(offset + p).Take(6).ToArray());
                    }
                    //Create the last packet with the remaining 4 bytes of data and 2 byte checksum
                    Command.DataList.Add(Data.Skip(offset + p).Take(4).Concat(BitConverter.GetBytes((short)BlockCheckSum).Reverse()).ToArray());

                    //send all the packets at one time
                    session.SAETx(Command);

                    Command.Mode = SAE.SAEModes.TRANSFER_ROUTINE_EXIT;
                    Command.AddInt8(0x80);

                    Response = session.SAETxRx(Command, 0);

                    retry_count--;
                    if (Response.Response == SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                    {
                        retry_count = 0;
                    }
                    else if (Response.Response != SAE.SAE_responses.FAIL_WITHOUT_RESULTS
                             || retry_count < 1)
                    {
                        throw new Exception("Too many retries or general failure"); //fail
                    }
                } while (retry_count > 0);

                Command.Mode = SAE.SAEModes.START_DIAG_ROUTINE_BY_NUMBER;
                Command.AddInt8((int)DiagRoutine.WriteFlash);

                Response = session.SAETxRx(Command, 0);

                if(Response.Response != SAE.SAE_responses.ROUTINE_NOT_COMPLETE &&
                   Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                {
                    throw new Exception("Something failed attempting to write block to flash"); //fail
                }

                Command.Mode = SAE.SAEModes.STOP_DIAG_ROUTINE_BY_NUMBER;
                Command.AddInt8((int)DiagRoutine.WriteFlash);

                Response = session.SAETxRx(Command, 0);

                if (Response.Response != SAE.SAE_responses.AFFIRMITIVE_RESPONSE)
                {
                    throw new Exception("Something failed attempting to write block to flash");//fail
                }
            }
        }

        public byte[] ComputeKey0(byte S1)
        {
            throw new Exception();
        }

        private byte[] ComputeKey1(byte[] Seeds)
        {
            int Key0Poly, Key1Poly;
            byte[] KeyBytes = new byte[2];

            if ((salt & 0x01) == 0x01)
            {
                Key0Poly = (Seeds[2] >> 6) & 0x03;
                Key1Poly = (Seeds[2] >> 4) & 0x03;
            }
            else
            {
                Key0Poly = (Seeds[2] >> 2) & 0x03;
                Key1Poly = (Seeds[2] >> 0) & 0x03;
            }

            switch (Key0Poly)
            {
                case 0:
                    KeyBytes[0] = (byte)(POW_INT((Seeds[0] + 100), 2) + (salt * (POW_INT(Seeds[0], salt) - 1)));
                    break;
                case 1:
                    KeyBytes[0] = (byte)((salt * 5) - POW_INT((Seeds[0] + 5), 3));
                    break;
                case 2:
                    KeyBytes[0] = (byte)(POW_INT((POW_INT(Seeds[0], 3) - POW_INT(salt, 2) + 10), 2));
                    break;
                case 3:
                    KeyBytes[0] = (byte)(salt + POW_INT(Seeds[0], 2) - 40 + POW_INT((Seeds[0] + 34), 3));
                    break;
            }
            switch (Key1Poly)
            {
                case 0:
                    KeyBytes[1] = (byte)(POW_INT((Seeds[1] + 100), 2) + salt * (POW_INT(Seeds[1], salt) - 1));
                    break;
                case 1:
                    KeyBytes[1] = (byte)(salt * 5 - POW_INT((Seeds[1] + 5), 3));
                    break;
                case 2:
                    KeyBytes[1] = (byte)(POW_INT((POW_INT(Seeds[1], 3) - POW_INT(salt, 2) + 10), 2));
                    break;
                case 3:
                    KeyBytes[1] = (byte)(salt + POW_INT(Seeds[1], 2) - 40 + POW_INT((Seeds[1] + 34), 3));
                    break;
            }
            return KeyBytes;
        }

        private byte[] ComputeKey2(byte[] Seeds)
        {
            int Key0Poly, Key1Poly;
            byte[] KeyBytes = new byte[2];

            if ((salt & 0x01) == 0x01)
            {
                Key0Poly = (Seeds[2] >> 6) & 0x03;
                Key1Poly = (Seeds[2] >> 4) & 0x03;
            }
            else
            {
                Key0Poly = (Seeds[2] >> 2) & 0x03;
                Key1Poly = (Seeds[2] >> 0) & 0x03;
            }

            switch (Key0Poly)
            {
                case 0:
                    KeyBytes[0] = (byte)((POW_INT((Seeds[0] + 99), 2)) + (salt * (POW_INT(Seeds[0], salt) - 1)));
                    break;
                case 1:
                    KeyBytes[0] = (byte)((salt * 5) + POW_INT((Seeds[0] + 5), 3));
                    break;
                case 2:
                    KeyBytes[0] = (byte)(POW_INT((POW_INT(Seeds[0], 3) - POW_INT(salt, 2) + 11), 2));
                    break;
                case 3:
                    KeyBytes[0] = (byte)(salt + POW_INT(Seeds[0], 2) - 40 + POW_INT((Seeds[0] + 33), 3));
                    break;
            }
            switch (Key1Poly)
            {
                case 0:
                    KeyBytes[1] = (byte)(POW_INT((Seeds[1] + 99), 2) + salt * (POW_INT(Seeds[1], salt) - 1));
                    break;
                case 1:
                    KeyBytes[1] = (byte)((salt * 5) + POW_INT((Seeds[1] + 5), 3));
                    break;
                case 2:
                    KeyBytes[1] = (byte)(POW_INT((POW_INT(Seeds[1], 3) - POW_INT(salt, 2) + 11), 2));
                    break;
                case 3:
                    KeyBytes[1] = (byte)(salt + POW_INT(Seeds[1], 2) - 40 + POW_INT((Seeds[1] + 33), 3));
                    break;
            }
            return KeyBytes;
        }
        private int POW_INT(int n, int e)
        {
            int ret = 1;
            while (e != 0)
            {
                if ((e & 1) == 1)
                    ret *= n;
                n *= n;
                e >>= 1;
            }
            return ret;
        }
    }
}
