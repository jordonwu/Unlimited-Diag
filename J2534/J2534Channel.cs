using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace J2534
{
    public class Channel
    {
        public J2534Device Device { get; private set; }
        private int ChannelID;
        private J2534HeapMessageArray HeapMessageArray;
        private Sieve MessageSieve = new Sieve();
        private List<PeriodicMsg> periodicmsglist = new List<PeriodicMsg>();
        private List<MessageFilter> filterlist = new List<MessageFilter>();

        public J2534Status ConnectionStatus { get; private set; }
        public bool IsOpen { get; private set; }
        public J2534PROTOCOL ProtocolID { get; private set; }
        public int Baud { get; set; }
        public J2534CONNECTFLAG ConnectFlags { get; internal set; }
        public IList<PeriodicMsg> PeriodicMsgList { get { return periodicmsglist.AsReadOnly(); } }
        public IList<MessageFilter> FilterList { get { return filterlist.AsReadOnly(); } }
        public int DefaultTxTimeout { get; set; }
        public int DefaultRxTimeout { get; set; }
        public J2534TXFLAG DefaultTxFlag { get; set; }



        /// <summary>
        /// Establish a logical communication channel with the vehicle network (via the PassThru device) using the specified network layer protocol and selected protocol options.
        /// </summary>
        /// <param name="Device">Vehicle interface identifier</param>
        /// <param name="ProtocolID">The protocol identifier selects the network layer protocol that will be used for the communications channel</param>
        /// <param name="Baud">Initial baud rate for the channel</param>
        /// <param name="ConnectFlags">Protocol specific options that are defined by bit fields. This parameter is usually set to zero</param>
        internal Channel(J2534Device Device, J2534PROTOCOL ProtocolID, J2534BAUD Baud, J2534CONNECTFLAG ConnectFlags)
        {
            HeapMessageArray = new J2534HeapMessageArray(CONST.HEAPMESSAGEBUFFERSIZE);
            this.Device = Device;
            this.ProtocolID = ProtocolID;
            this.Baud = (int)Baud;
            this.ConnectFlags = ConnectFlags;
            DefaultTxTimeout = 450;
            DefaultRxTimeout = 450;
            DefaultTxFlag = J2534TXFLAG.NONE;
            Connect();
        }

        private void Connect()
        {
            J2534HeapInt ChannelID = new J2534HeapInt();

            lock (Device.Library.API_LOCK)
            {
                ConnectionStatus.Code = Device.Library.API.Connect(Device.DeviceID, (int)ProtocolID, (int)ConnectFlags, Baud, ChannelID.Ptr);
                if (ConnectionStatus.IsOK)
                {
                    IsOpen = true;
                    this.ChannelID = ChannelID;
                }
                else
                    ConnectionStatus.Description = Device.Library.GetLastError();
            }
        }

        /// <summary>
        /// Terminate an existing logical communication channel between the User Application and the vehicle network (via the PassThru device). Once disconnected the channel identifier or handle is invalid. For the associated network protocol this function will terminate the transmitting of periodic messages and the filtering of receive messages. The PassThru device periodic and filter message tables will be cleared
        /// </summary>
        public void Disconnect()
        {
            if (IsOpen)
            {
                lock (Device.Library.API_LOCK)
                {
                    IsOpen = false;
                    ConnectionStatus.Code = Device.Library.API.Disconnect(ChannelID);
                    if (ConnectionStatus.IsNotOK)
                    {
                        ConnectionStatus.Description = Device.Library.GetLastError();
                        throw new J2534Exception(ConnectionStatus);
                    }
                }
            }
        }

        public GetMessageResults GetMessage()
        {
            return GetMessages(1, DefaultRxTimeout);
        }

        /// <summary>
        /// Reads 'NumMsgs' messages from the input buffer and then the device.  Will block
        /// until it gets 'NumMsgs' messages, or 'DefaultRxTimeout' expires.
        /// </summary>
        /// <param name="NumMsgs"></param>
        /// <returns>Returns 'false' if successful</returns>
        public GetMessageResults GetMessages(int NumMsgs)
        {
            return GetMessages(NumMsgs, DefaultRxTimeout);
        }

        /// <summary>
        /// Attempts to read 'NumMsgs' messages from the J2534 Device within 'Timeout' time.
        /// </summary>
        /// <param name="NumMsgs">The desired number of J2534 messages. Due to timeouts, the number of messages returned may be less than the number requested.  Number must be less than or equal to J2534.CONST.HEAPMESSAGEBUFFERSIZE (default is 200)</param>
        /// <param name="Timeout">Timeout (in milliseconds) for read completion. A value of zero reads buffered messages and returns immediately. A non-zero value blocks (does not return) until the specified number of messages have been read, or until the timeout expires.</param>
        /// <returns>Returns get message results</returns>
        public GetMessageResults GetMessages(int NumMsgs, int Timeout)
        {
            GetMessageResults Results = new GetMessageResults();

            lock (Device.Library.API_LOCK)
            {
                HeapMessageArray.Length = NumMsgs;
                Results.Status.Code = Device.Library.API.ReadMsgs(ChannelID, HeapMessageArray.Ptr, HeapMessageArray.Length.Ptr, Timeout);
                if (Results.Status.IsNotOK) Results.Status.Description = Device.Library.GetLastError();
                Results.Messages = HeapMessageArray.ToJ2534MessageList();
            }
            return Results;
        }

        public void AddMessageScreen(Predicate<J2534Message> Comparer, int Priority = 10)
        {
            MessageSieve.AddScreen(Priority, Comparer);
        }

        public void RemoveMessageScreen(Predicate<J2534Message> Comparer)
        {
            MessageSieve.RemoveScreen(Comparer);
        }

        public void RemoveAllScreens()
        {
            MessageSieve.RemoveAllScreens();
        }

        //Thread safety in this method assumes that each call will have unique comparers
        //An option is to lock on the ComparerHandle, but that seems unnecessary
        //There is no good reason I know of that multiple call would be made to this method
        //using the same ComparerHandle
        public GetMessageResults GetMessages(int NumMsgs, int Timeout, Predicate<J2534Message> ComparerHandle, bool Remove)
        {
            bool WantMoreMessages;
            Stopwatch FunctionTimer = new Stopwatch();
            FunctionTimer.Start();
            long actual_execution_time;
            do
            {
                //execution time is measured here to guarentee the API will actually get
                //TIMEOUT to export messages.  This covers the case of this thread being preempted
                //and blocked for a significant time.
                actual_execution_time = FunctionTimer.ElapsedMilliseconds;
                GetMessageResults RxMessages = GetMessages(CONST.HEAPMESSAGEBUFFERSIZE, 0);
                if (RxMessages.Status.IsOK ||
                    RxMessages.Status.Code == J2534ERR.BUFFER_EMPTY)
                {
                    MessageSieve.Sift(RxMessages.Messages);
                }
                else
                    throw new J2534Exception(RxMessages.Status);
                WantMoreMessages = (MessageSieve.ScreenMessageCount(ComparerHandle) < NumMsgs);

            } while (WantMoreMessages && (actual_execution_time < Timeout));

            if(WantMoreMessages)
                return new GetMessageResults(MessageSieve.EmptyScreen(ComparerHandle, Remove), new J2534Status(J2534ERR.TIMEOUT, "Timeout expired before all messages could be received in GetMessages"));
            else
                return new GetMessageResults(MessageSieve.EmptyScreen(ComparerHandle, Remove), new J2534Status(J2534ERR.STATUS_NOERROR));
        }

        public GetMessageResults MessageTransaction(IEnumerable<byte> TxMessageData, int NumOfRxMsgs, Predicate<J2534Message> Comparer)
        {
            MessageSieve.AddScreen(10, Comparer);
            J2534Status Status = SendMessage(TxMessageData.ToArray());
            if (Status.IsOK) return GetMessages(NumOfRxMsgs, DefaultRxTimeout, Comparer, true);
            throw new J2534Exception(Status);
        }

        public GetMessageResults MessageTransaction(List<J2534Message> TxMessages, int NumOfRxMsgs, Predicate<J2534Message> Comparer)
        {
            lock (MessageSieve) MessageSieve.AddScreen(10, Comparer);
            J2534Status Status = SendMessages(TxMessages);
            if (Status.IsOK) return GetMessages(NumOfRxMsgs, DefaultRxTimeout, Comparer, true);
            throw new J2534Exception(Status);
        }

        /// <summary>
        /// Sends a single message 'Message'
        /// </summary>
        /// <param name="Message"></param>
        /// <returns>Returns 'false' if successful</returns>
        public J2534Status SendMessage(byte[] Message)
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                HeapMessageArray.InsertSingle(ProtocolID, DefaultTxFlag, Message);
                Status.Code = Device.Library.API.WriteMsgs(ChannelID, HeapMessageArray.Ptr, HeapMessageArray.Length.Ptr, DefaultTxTimeout);
                if (Status.IsNotOK) Status.Description = Device.Library.GetLastError();
            }
            return Status;
        }

        /// <summary>
        /// Sends all messages contained in 'MsgList'
        /// </summary>
        /// <returns>Returns 'false' if successful</returns>
        public J2534Status SendMessages(J2534MessageList Messages)
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                HeapMessageArray.DeepCopy(Messages);
                Status.Code = Device.Library.API.WriteMsgs(ChannelID, HeapMessageArray.Ptr, HeapMessageArray.Length.Ptr, DefaultTxTimeout);
                if (Status.IsNotOK) Status.Description = Device.Library.GetLastError();
            }
            return Status;
        }

        public int StartPeriodicMessage(PeriodicMsg PeriodicMessage)
        {
            J2534Status Status = new J2534Status();
            J2534HeapInt MessageID = new J2534HeapInt();

            J2534HeapMessage PeriodicMessageHeap = new J2534HeapMessage(ProtocolID,
                                                            PeriodicMessage.Message.TxFlags,
                                                            PeriodicMessage.Message.Data);
            lock (Device.Library.API_LOCK)
            {


                Status.Code = Device.Library.API.StartPeriodicMsg(ChannelID,
                                                                  PeriodicMessageHeap.Ptr,
                                                                  MessageID.Ptr,
                                                                  PeriodicMessage.Interval);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                PeriodicMessage.MessageID = MessageID;
                PeriodicMsgList.Add(PeriodicMessage);
            }
            return PeriodicMsgList.IndexOf(PeriodicMessage);
        }

        /// <summary>
        /// Stops the periodic message in 'PeriodicMsgList' referenced by 'Index'.
        /// </summary>
        /// <param name="Index"></param>
        /// <returns>Returns 'false' if successful</returns>
        public void StopPeriodicMsg(int Index)
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.StopPeriodicMsg(ChannelID, PeriodicMsgList[Index].MessageID);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        /// <summary>
        /// Starts a single message filter and if successful, adds it to the FilterList.
        /// </summary>
        /// <param name="Filter"></param>
        /// <returns>Returns false if successful</returns>
        public int StartMsgFilter(MessageFilter Filter)
        {
            J2534Status Status = new J2534Status();
            J2534HeapInt FilterID = new J2534HeapInt();

            J2534HeapMessage Mask = new J2534HeapMessage(ProtocolID, Filter.TxFlags, Filter.Mask);
            J2534HeapMessage Pattern = new J2534HeapMessage(ProtocolID, Filter.TxFlags, Filter.Pattern);
            J2534HeapMessage FlowControl = new J2534HeapMessage(ProtocolID, Filter.TxFlags, Filter.FlowControl);

            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.StartMsgFilter(ChannelID,
                                                                (int)Filter.FilterType,
                                                                Mask.Ptr,
                                                                Pattern.Ptr,
                                                                Filter.FilterType == J2534FILTER.FLOW_CONTROL_FILTER ? FlowControl.Ptr : IntPtr.Zero,
                                                                FilterID.Ptr);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                filterlist.Add(Filter);
            }
            return filterlist.IndexOf(Filter);
        }

        public void StopMsgFilter(int Index)
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.StopMsgFilter(ChannelID, filterlist[Index].FilterId);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                filterlist.RemoveAt(Index);
            }
        }

        public int GetConfig(J2534PARAMETER Parameter)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigArray = new HeapSConfigArray(new J2534.SConfig(Parameter, 0));
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.GET_CONFIG, SConfigArray.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                return SConfigArray[0].Value;
            }
        }

        public void SetConfig(J2534PARAMETER Parameter, int Value)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigList = new HeapSConfigArray(new SConfig(Parameter, Value));
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.SET_CONFIG, SConfigList.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public List<SConfig> GetConfig(List<SConfig> SConfig)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigArray = new HeapSConfigArray(SConfig);

            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.GET_CONFIG, SConfigArray.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
            return SConfigArray.ToList();
        }

        public void SetConfig(List<SConfig> SConfig)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigList = new HeapSConfigArray(SConfig);
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.SET_CONFIG, SConfigList.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void ClearTxBuffer()
        {
            J2534Status Status = new J2534Status();

            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.CLEAR_TX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void ClearRxBuffer()
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.CLEAR_RX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void ClearPeriodicMsgs()
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.CLEAR_PERIODIC_MSGS, IntPtr.Zero, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void ClearMsgFilters()
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.CLEAR_MSG_FILTERS, IntPtr.Zero, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void ClearFunctMsgLookupTable()
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.CLEAR_FUNCT_MSG_LOOKUP_TABLE, IntPtr.Zero, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void AddToFunctMsgLookupTable(byte Addr)
        {
            J2534Status Status = new J2534Status();
            HeapSByteArray SByteArray = new HeapSByteArray(Addr);
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.ADD_TO_FUNCT_MSG_LOOKUP_TABLE, SByteArray.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void AddToFunctMsgLookupTable(List<byte> AddressList)
        {
            J2534Status Status = new J2534Status();
            HeapSByteArray SByteArray = new HeapSByteArray(AddressList.ToArray());
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.ADD_TO_FUNCT_MSG_LOOKUP_TABLE, SByteArray.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void DeleteFromFunctMsgLookupTable(byte Addr)
        {
            J2534Status Status = new J2534Status();
            HeapSByteArray SByteArray = new HeapSByteArray(Addr);
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE, SByteArray.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void DeleteFromFunctMsgLookupTable(IEnumerable<byte> AddressList)
        {
            J2534Status Status = new J2534Status();
            HeapSByteArray SByteArray = new HeapSByteArray(AddressList.ToArray());
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE, SByteArray.Ptr, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public byte[] FiveBaudInit(byte TargetAddress)
        {
            J2534Status Status = new J2534Status();
            HeapSByteArray Input = new HeapSByteArray(new byte[] { TargetAddress });
            HeapSByteArray Output = new HeapSByteArray(new byte[2]);
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.FIVE_BAUD_INIT, Input.Ptr, Output.Ptr);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                return Output;
            }
        }

        public J2534Message FastInit(J2534Message TxMessage)
        {
            J2534Status Status = new J2534Status();
            J2534HeapMessage Input = new J2534HeapMessage(ProtocolID, TxMessage.TxFlags, TxMessage.Data);
            J2534HeapMessage Output = new J2534HeapMessage();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.FAST_INIT, Input.Ptr, Output.Ptr);
                if (Status.IsNotOK)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                return Output;
            }
        }

        public void SetProgrammingVoltage(J2534PIN PinNumber, int Voltage)
        {
            Device.SetProgrammingVoltage(PinNumber, Voltage);
        }

        public int MeasureProgrammingVoltage()
        {
            if (Device.Library.API_Signature.SAE_API == SAE_API.V202_SIGNATURE)
            {
                J2534Status Status = new J2534Status();
                J2534HeapInt Voltage = new J2534HeapInt();
                lock(Device.Library.API_LOCK)
                {
                    Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.READ_PROG_VOLTAGE, IntPtr.Zero, Voltage.Ptr);
                    if (Status.IsNotOK)
                    {
                        Status.Description = Device.Library.GetLastError();
                        throw new J2534Exception(Status);
                    }
                    return Voltage;
                }
            }
            return Device.MeasureProgrammingVoltage();
        }

        public int MeasureBatteryVoltage()
        {
            if(Device.Library.API_Signature.SAE_API == SAE_API.V202_SIGNATURE)
            {
                J2534Status Status = new J2534Status();
                J2534HeapInt Voltage = new J2534HeapInt();
                lock (Device.Library.API_LOCK)
                {
                    Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.READ_VBATT, IntPtr.Zero, Voltage.Ptr);
                    if (Status.IsNotOK)
                    {
                        Status.Description = Device.Library.GetLastError();
                        throw new J2534Exception(Status);
                    }
                    return Voltage;
                }
            }
            return Device.MeasureBatteryVoltage();
        }
    }
}
