using System;
using System.Linq;
using System.Collections.Generic;
using J2534;

namespace SAE.Session
{
    //Base class that all J1850 sessions are built from
    public abstract class J1850Session
    {
        private bool disposed;
        protected J2534Device device;
        protected Channel channel;
        protected J2534PROTOCOL SessionProtocol;
        protected J2534TXFLAG SessionTxFlags;

        protected byte[] default_message_prototype;


        public SAEMessage SAETxRx(SAEMessage Message, int RxDataIndex)
        {
            J1850Message MessageParser = new J1850Message(default_message_prototype);
            GetMessageResults Results;
            SAEMessage ReturnMessage = new SAEMessage();

            MessageParser.TargetAddress = Message.Address;
            MessageParser.SAEMode = Message.Mode;
            MessageParser.Data = Message.Data;
            MessageParser.RxDataIndex = RxDataIndex;

            Results = channel.MessageTransaction(MessageParser.RawMessage, 1, MessageParser.DefaultRxComparer);
            if (Results.Status.IsOK)
            {
                MessageParser.RawMessage = (byte [])Results.Messages[0].Data;
                ReturnMessage.Mode = MessageParser.SAEMode;
                ReturnMessage.Data = MessageParser.Data;
                ReturnMessage.Response = MessageParser.ResponseByte;
                ReturnMessage.IsValid = true;
            }
            return ReturnMessage;
        }

        public void SAETx(SAEMessage Message)
        {
            J1850Message MessageParser = new J1850Message(default_message_prototype);
            MessageParser.TargetAddress = Message.Address;
            MessageParser.SAEMode = Message.Mode;

            List<J2534Message> J2534Messages = new List<J2534Message>();

            Message.DataList.ForEach(data =>
            {
                MessageParser.Data = data;
                J2534Messages.Add(new J2534Message(SessionProtocol, SessionTxFlags, MessageParser.RawMessage));
            });

            J2534Status Status = channel.SendMessages(J2534Messages);
            if(Status.IsNotOK)
            {
                throw new J2534Exception(Status);
            }
        }

        public object CreateRxHandle(int Addr, SAEModes Mode)
        {
            J1850Message MessageParser = new J1850Message(default_message_prototype);
            MessageParser.TargetAddress = Addr;
            MessageParser.SAEMode = Mode;

            Predicate<J2534Message> Comparer = (TestMessage =>
            {
                if(TestMessage.Data.Skip(1).Take(3).SequenceEqual(MessageParser.RawMessage.Skip(1).Take(3))
                   && TestMessage.RxStatus == J2534.J2534RXFLAG.NONE)
                {
                    return true;
                }
                return false;
            });
            channel.AddMessageScreen(Comparer);
            return (object)Comparer;
        }

        public void DestroyRxHandle(object Handle)
        {
            channel.RemoveMessageScreen((Predicate<J2534Message>)Handle);
        }

        public List<byte[]> SAERx(object RxHandle, int NumOfMsgs, int Timeout, bool DestroyHandle = false)
        {
            List<byte[]> Messages = new List<byte[]>();
            J1850Message MessageParser = new J1850Message(default_message_prototype);
            GetMessageResults Results = channel.GetMessages(NumOfMsgs, Timeout, (Predicate<J2534Message>)RxHandle, DestroyHandle);
            if(Results.Messages.Count > NumOfMsgs)
            {
                int a = 1;
            }
            Results.Messages.ForEach(j2534message =>
            {
                MessageParser.RawMessage = (byte [])j2534message.Data;  //This should always be a byte [] so, no type checking is done.
                Messages.Add(MessageParser.Data);
            });
            return Messages;
        }
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
                channel.Disconnect();
                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
        }

    }
}
