﻿using System;
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
        public J2534Status ConnectionStatus { get; private set; }
        public bool IsOpen { get; private set; }
        public J2534PROTOCOL ProtocolID { get; private set; }
        public int Baud { get; set; }
        public J2534CONNECTFLAG ConnectFlags { get; internal set; }
        public List<PeriodicMsg> PeriodicMsgList = new List<PeriodicMsg>();
        public List<MessageFilter> FilterList = new List<MessageFilter>();
        public int DefaultTxTimeout { get; set; }
        public int DefaultRxTimeout { get; set; }
        public J2534TXFLAG DefaultTxFlag { get; set; }

        //Channel Constructor
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
                ConnectionStatus.Code = Device.Library.API.Connect(Device.DeviceID, (int)ProtocolID, (int)ConnectFlags, Baud, ChannelID);
                if (ConnectionStatus.IsClear)
                {
                    IsOpen = true;
                    this.ChannelID = ChannelID;
                }
                else
                    ConnectionStatus.Description = Device.Library.GetLastError();
            }
        }

        public void Disconnect()
        {
            if (IsOpen)
            {
                J2534Status Status = new J2534Status();
                lock (Device.Library.API_LOCK)
                {
                    IsOpen = false;
                    Status.Code = Device.Library.API.Disconnect(ChannelID);
                    if (Status.IsNOTClear)
                    {
                        Status.Description = Device.Library.GetLastError();
                        throw new J2534Exception(Status);
                    }
                }
            }
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

        public GetMessageResults MessageTransaction(List<byte> TxMessageData, int NumOfRxMsgs, Predicate<J2534Message> Comparer)
        {
            MessageSieve.AddScreen(10, Comparer);
            J2534Status Status = SendMessage(TxMessageData.ToArray());
            if (Status.IsClear) return GetMessages(NumOfRxMsgs, DefaultRxTimeout, Comparer, true);
            throw new J2534Exception(Status);
        }

        public GetMessageResults MessageTransaction(List<J2534Message> TxMessages, int NumOfRxMsgs, Predicate<J2534Message> Comparer)
        {
            MessageSieve.AddScreen(10, Comparer);
            J2534Status Status = SendMessages(TxMessages);
            if (Status.IsClear) return GetMessages(NumOfRxMsgs, DefaultRxTimeout, Comparer, true);
            throw new J2534Exception(Status);
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
        /// Reads 'NumMsgs' messages from the input buffer and then the device.  Will block
        /// until it gets 'NumMsgs' messages, or 'Timeout' expires.
        /// </summary>
        /// <param name="NumMsgs"></param>
        /// <param name="Timeout"></param>
        /// <returns>Returns 'false' if successful</returns>
        public GetMessageResults GetMessages(int NumMsgs, int Timeout)
        {
            GetMessageResults Results = new GetMessageResults();

            lock (Device.Library.API_LOCK)
            {
                HeapMessageArray.Length = NumMsgs;
                Results.Status.Code = Device.Library.API.ReadMsgs(ChannelID, HeapMessageArray.Ptr, HeapMessageArray.Length, Timeout);
                if (Results.Status.IsNOTClear) Results.Status.Description = Device.Library.GetLastError();
                Results.Messages = HeapMessageArray.ToList();
            }
            return Results;
        }

        //Thread safety in this method assumes that each thread will have unique comparers
        public GetMessageResults GetMessages(int NumMsgs, int Timeout, Predicate<J2534Message> ComparerHandle, bool Remove)
        {
            bool GetMoreMessages;
            Stopwatch FunctionTimer = new Stopwatch();
            FunctionTimer.Start();

            do
            {
                GetMessageResults RxMessages = GetMessages(CONST.HEAPMESSAGEBUFFERSIZE, 0);
                if (RxMessages.Status.IsClear ||
                    RxMessages.Status.Code == J2534ERR.BUFFER_EMPTY)
                {
                    MessageSieve.Sift(RxMessages.Messages);

                }
                else
                    throw new J2534Exception(RxMessages.Status);
                GetMoreMessages = (MessageSieve.ScreenMessageCount(ComparerHandle) < NumMsgs);

            } while (GetMoreMessages && (FunctionTimer.ElapsedMilliseconds < Timeout));

            if(GetMoreMessages)
                return new GetMessageResults(MessageSieve.EmptyScreen(ComparerHandle, Remove), new J2534Status(J2534ERR.TIMEOUT));
            else
                return new GetMessageResults(MessageSieve.EmptyScreen(ComparerHandle, Remove), new J2534Status(J2534ERR.STATUS_NOERROR));
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
                HeapMessageArray.Insert(ProtocolID, DefaultTxFlag, Message);
                Status.Code = Device.Library.API.WriteMsgs(ChannelID, HeapMessageArray.Ptr, HeapMessageArray.Length, DefaultTxTimeout);
                if (Status.IsNOTClear) Status.Description = Device.Library.GetLastError();
            }
            return Status;
        }

        /// <summary>
        /// Sends all messages contained in 'MsgList'
        /// </summary>
        /// <returns>Returns 'false' if successful</returns>
        public J2534Status SendMessages(List<J2534Message> Messages)
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                HeapMessageArray.Insert(Messages);
                Status.Code = Device.Library.API.WriteMsgs(ChannelID, HeapMessageArray.Ptr, HeapMessageArray.Length, DefaultTxTimeout);
                if (Status.IsNOTClear) Status.Description = Device.Library.GetLastError();
            }
            return Status;
        }

        public int StartPeriodicMessage(J2534Message Message, int Interval)
        {
            J2534Status Status;
            PeriodicMsg PeriodicMessage = new PeriodicMsg(Message, Interval);
            lock (Device.Library.API_LOCK)
            {
                PeriodicMsgList.Add(PeriodicMessage);
                Status = StartPeriodicMessage(PeriodicMsgList.IndexOf(PeriodicMessage));
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    PeriodicMsgList.Remove(PeriodicMessage);
                    throw new J2534Exception(Status);
                }
            }
            return PeriodicMsgList.IndexOf(PeriodicMessage);
        }

        private J2534Status StartPeriodicMessage(int Index)
        {
            J2534Status Status = new J2534Status();

            J2534HeapInt MessageID = new J2534HeapInt();

            J2534HeapMessage Message = new J2534HeapMessage(PeriodicMsgList[Index].Message);
            Status.Code = Device.Library.API.StartPeriodicMsg(ChannelID, Message, MessageID, PeriodicMsgList[Index].Interval);
            PeriodicMsgList[Index].MessageID = MessageID;
            return Status;
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
                if (Status.IsNOTClear)
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
            J2534Status Status;

            lock (Device.Library.API_LOCK)
            {
                FilterList.Add(Filter);
                Status = StartMsgFilter(FilterList.IndexOf(Filter));
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    FilterList.Remove(Filter);
                    throw new J2534Exception(Status);
                }
                return FilterList.IndexOf(Filter);
            }
        }

        private J2534Status StartMsgFilter(int Index)
        {
            J2534Status Status = new J2534Status();
            J2534HeapInt FilterID = new J2534HeapInt();

            J2534HeapMessage Mask = new J2534HeapMessage(new J2534Message(ProtocolID, FilterList[Index].TxFlags, FilterList[Index].Mask));
            J2534HeapMessage Pattern = new J2534HeapMessage(new J2534Message(ProtocolID, FilterList[Index].TxFlags, FilterList[Index].Pattern));
            J2534HeapMessage FlowControl = new J2534HeapMessage(new J2534Message(ProtocolID, FilterList[Index].TxFlags, FilterList[Index].FlowControl));
            //The lock is performed in the calling method to protect the 'FilterList' coherency.
            if (FilterList[Index].FilterType == J2534FILTER.FLOW_CONTROL_FILTER)
                Status.Code = Device.Library.API.StartMsgFilter(ChannelID, (int)FilterList[Index].FilterType, Mask, Pattern, FlowControl, FilterID);
            else
                Status.Code = Device.Library.API.StartMsgFilter(ChannelID, (int)FilterList[Index].FilterType, Mask, Pattern, IntPtr.Zero, FilterID);

            FilterList[Index].FilterId = FilterID;
            return Status;
        }

        public void StopMsgFilter(int Index)
        {
            J2534Status Status = new J2534Status();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.StopMsgFilter(ChannelID, FilterList[Index].FilterId);
                FilterList.RemoveAt(Index);
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public int GetConfig(J2534PARAMETER Parameter)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigArray = new HeapSConfigArray(new J2534.SConfig(Parameter, 0));
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.GET_CONFIG, SConfigArray, IntPtr.Zero);
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                return SConfigArray[0].Value;
            }
        }

        public List<SConfig> GetConfig(List<SConfig> SConfig)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigArray = new HeapSConfigArray(SConfig);

            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.GET_CONFIG, SConfigArray, IntPtr.Zero);
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                return SConfigArray.ToList();  //Implicit conversion to list ;)
            }
        }

        public void SetConfig(J2534PARAMETER Parameter, int Value)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigList = new HeapSConfigArray(new SConfig(Parameter, Value));
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.SET_CONFIG, SConfigList, IntPtr.Zero);
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void SetConfig(List<SConfig> SConfig)
        {
            J2534Status Status = new J2534Status();
            HeapSConfigArray SConfigList = new HeapSConfigArray(SConfig);
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.SET_CONFIG, SConfigList, IntPtr.Zero);
                if (Status.IsNOTClear)
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
                if (Status.IsNOTClear)
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
                if (Status.IsNOTClear)
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
                if (Status.IsNOTClear)
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
                if (Status.IsNOTClear)
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
                if (Status.IsNOTClear)
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
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.ADD_TO_FUNCT_MSG_LOOKUP_TABLE, SByteArray, IntPtr.Zero);
                if (Status.IsNOTClear)
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
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.ADD_TO_FUNCT_MSG_LOOKUP_TABLE, SByteArray, IntPtr.Zero);
                if (Status.IsNOTClear)
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
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE, SByteArray, IntPtr.Zero);
                if (Status.IsNOTClear)
                {
                    Status.Description = Device.Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void DeleteFromFunctMsgLookupTable(List<byte> AddressList)
        {
            J2534Status Status = new J2534Status();
            HeapSByteArray SByteArray = new HeapSByteArray(AddressList.ToArray());
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE, SByteArray, IntPtr.Zero);
                if (Status.IsNOTClear)
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
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.FIVE_BAUD_INIT, Input, Output);
                if (Status.IsNOTClear)
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
            J2534HeapMessage Input = new J2534HeapMessage(TxMessage);
            J2534HeapMessage Output = new J2534HeapMessage();
            lock (Device.Library.API_LOCK)
            {
                Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.FAST_INIT, Input, Output);
                if (Status.IsNOTClear)
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
                    Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.READ_PROG_VOLTAGE, IntPtr.Zero, Voltage);
                    if (Status.IsNOTClear)
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
                    Status.Code = Device.Library.API.IOCtl(ChannelID, (int)J2534IOCTL.READ_VBATT, IntPtr.Zero, Voltage);
                    if (Status.IsNOTClear)
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
