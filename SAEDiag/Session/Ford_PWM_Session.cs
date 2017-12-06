using System;
using System.Linq;
using J2534;

namespace SAE.Session.Ford
{
    public class FordPWMSession : J1850PWM_Session
    {
        private byte[] broadcastmessage;
        private Predicate<J2534Message> broadcast_rx_handle;

        public FordPWMSession(J2534Device Device) : base(Device)
        {
        }

        public void InitializeIVEFERConfig()
        {
            channel.SetConfig(J2534PARAMETER.DATA_RATE, (int)J2534BAUD.J1850PWM_41600);
            channel.ClearMsgFilters();
            channel.ClearFunctMsgLookupTable();
            channel.AddToFunctMsgLookupTable(0x05);
            channel.StartMsgFilter(new MessageFilter()
            {
                Mask = new byte[] { 0x00, 0x00, 0x00, 0xFF, 0xFF },
                Pattern = new byte[] { 0x00, 0x00, 0x00, 0x7F, 0x3F },
                FilterType = J2534FILTER.BLOCK_FILTER
            });
            channel.StartMsgFilter(new MessageFilter()
            {
                Mask = new byte[] { 0x00, 0xFF, 0x00 },
                Pattern = new byte[] { 0x00, 0x05, 0x00 },
                FilterType = J2534FILTER.PASS_FILTER
            });
            channel.StartMsgFilter(new MessageFilter()
            {
                Mask = new byte[] { 0x00, 0xFF, 0x00 },
                Pattern = new byte[] { 0x00, 0xF1, 0x00 },
                FilterType = J2534FILTER.PASS_FILTER
            });
            ToolAddress = 0xF1;
            channel.ClearRxBuffer();
            broadcast_rx_handle = (TestMessage =>
            {
                byte[] broadcast_signature = new byte[] { 0x05, 0x10, 0x04 };
                if (TestMessage.Data.Skip(1).Take(3).SequenceEqual(broadcast_signature))
                    return true;
                return false;
            });
            channel.AddMessageScreen(broadcast_rx_handle);
        }

        public bool CatchBroadcastMessage(int Timeout = 200)
        {
            GetMessageResults Results = channel.GetMessages(1, Timeout, broadcast_rx_handle, false);
            if (Results.Status.IsNotOK)
                return false;
            broadcastmessage = (byte [])Results.Messages[0].Data;
            channel.RemoveMessageScreen(broadcast_rx_handle);
            return true;
        }

        public void HighSpeedMode()
        {
            channel.SetConfig(J2534PARAMETER.DATA_RATE, (int)J2534BAUD.J1850PWM_83200);
        }

        public void FEPSOn()
        {
            channel.SetProgrammingVoltage(J2534PIN.PIN_13, 18000);
        }

        public void FEPSOff()
        {
            channel.SetProgrammingVoltage(J2534PIN.PIN_13, -1);
        }

        public void StartPeriodicMessage(SAEMessage Message)
        {
            J1850Message MessageParser = new J1850Message(default_message_prototype);

            MessageParser.TargetAddress = Message.Address;
            MessageParser.SAEMode = Message.Mode;
            MessageParser.Data = Message.Data;

            J2534Message TesterPresent = new J2534Message(J2534PROTOCOL.J1850PWM, J2534TXFLAG.NONE, MessageParser.RawMessage);

            channel.StartPeriodicMessage(TesterPresent, 3000);
        }

    }
    public enum DiagRoutine
    {
        IVFEREntry = 0xA0,
        EraseFlash = 0xA1,
        WriteFlash = 0xA2,
        Checksum = 0xA3,
        BaudRate = 0xA4
    }
}
