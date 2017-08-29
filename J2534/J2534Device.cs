using System;
using System.Runtime.InteropServices;

namespace J2534
{
    public class J2534Device
    {
        internal int DeviceID { get; private set; }
        public J2534Library Library { get; private set; }
        public string FirmwareVersion { get; private set; }
        public string LibraryVersion { get; private set; }
        public string APIVersion { get; private set; }
        public string DeviceName { get; private set; }
        public string DrewtechVersion { get; private set; }
        public string DrewtechAddress { get; private set; }
        private bool ValidDevice;   //Flag used to determine if this device failed initial connection

        internal J2534Device(J2534Library Library)
        {
            this.Library = Library;
            ConnectToDevice("");
        }

        internal J2534Device(J2534Library Library, string DeviceName)
        {
            this.Library = Library;
            this.DeviceName = DeviceName;
            ConnectToDevice(this.DeviceName);
        }

        internal J2534Device(J2534Library Library, GetNextCarDAQResults CarDAQ)
        {
            this.Library = Library;
            this.DeviceName = CarDAQ.Name;
            this.DrewtechVersion = CarDAQ.Version;
            this.DrewtechAddress = CarDAQ.Address;

            ConnectToDevice(DeviceName);
        }

        public bool IsConnected
        {
            get
            {   
                if (!ValidDevice) return false;
                //GetVersion is used as a 'ping'
                return (GetVersion().IsClear);
            }
        }

        public J2534Status ConnectToDevice(string Device)
        {
            J2534Status Status = new J2534Status();

            IntPtr pDeviceName = IntPtr.Zero;
            if (!string.IsNullOrEmpty(Device))
                pDeviceName = Marshal.StringToHGlobalAnsi(Device);
            else
                DeviceName = string.Format("Device {0}", J2534Discovery.PhysicalDevices.FindAll(Listed => Listed.Library == this.Library).Count + 1);

            J2534HeapInt DeviceID = new J2534HeapInt();

            lock (Library.API_LOCK)
            {
                Status.Code = Library.API.Open(pDeviceName, DeviceID);

                Marshal.FreeHGlobal(pDeviceName);

                if (Status.IsClear || (Library.API_Signature.SAE_API == SAE_API.V202_SIGNATURE &&
                                       J2534Discovery.PhysicalDevices.FindAll(Listed => Listed.Library == this.Library).Count == 0 &&
                                       IsConnected))
                {
                    this.DeviceID = DeviceID;
                    ValidDevice = true;
                    Status.Code = J2534ERR.STATUS_NOERROR;
                    GetVersion();
                }
                else
                {
                    Status.Description = Library.GetLastError();
                }
                return Status;
            }
        }

        public void DisconnectDevice()
        {
            J2534Status Status = new J2534Status();
            lock (Library.API_LOCK)
            {
                Status.Code = Library.API.Close(DeviceID);
                if (Status.IsNOTClear)
                {
                    Status.Description = Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        public void SetProgrammingVoltage(J2534PIN PinNumber, int Voltage)
        {
            J2534Status Status = new J2534Status();
            lock (Library.API_LOCK)
            {
                Status.Code = Library.API.SetProgrammingVoltage(DeviceID, (int)PinNumber, Voltage);
                if (Status.IsNOTClear)
                {
                    Status.Description = Library.GetLastError();
                    throw new J2534Exception(Status);
                }
            }
        }

        private J2534Status GetVersion()
        {
            J2534Status Status = new J2534Status();
            IntPtr pFirmwareVersion = Marshal.AllocHGlobal(80);
            IntPtr pDllVersion = Marshal.AllocHGlobal(80);
            IntPtr pApiVersion = Marshal.AllocHGlobal(80);

            lock (Library.API_LOCK)
            {
                Status.Code = Library.API.ReadVersion(DeviceID, pFirmwareVersion, pDllVersion, pApiVersion);

                if (Status.IsClear)
                {
                    FirmwareVersion = Marshal.PtrToStringAnsi(pFirmwareVersion);
                    LibraryVersion = Marshal.PtrToStringAnsi(pDllVersion);
                    APIVersion = Marshal.PtrToStringAnsi(pApiVersion);
                }
                else
                {
                    Status.Description = Library.GetLastError();
                    //No exception is thrown because this method is used as a 'Ping' and I don't
                    //want exceptions occuring just because a ping failed for any reason.
                }
                Marshal.FreeHGlobal(pFirmwareVersion);
                Marshal.FreeHGlobal(pDllVersion);
                Marshal.FreeHGlobal(pApiVersion);
            }
            return Status;
        }

        public int MeasureBatteryVoltage()
        {
            J2534Status Status = new J2534Status();

            J2534HeapInt Voltage = new J2534HeapInt();
            lock (Library.API_LOCK)
            {
                Status.Code = Library.API.IOCtl(DeviceID, (int)J2534IOCTL.READ_VBATT, IntPtr.Zero, Voltage);
                if (Status.IsNOTClear)
                {
                    Status.Description = Library.GetLastError();
                    throw new J2534Exception(Status);
                }

                //The return was kept inside the lock here to ensure the conversion to INT is done before the
                //lock is released.  This is in case the API reuses the Ptr location for this data on subsequent
                //calls.  In that case, two back to back calls could interfere with each other of the second
                //call is allowed to execute before the first call marshals the Int from the heap.
                return Voltage;
            }
        }

        public int MeasureProgrammingVoltage()
        {
            J2534Status Status = new J2534Status();
            J2534HeapInt Voltage = new J2534HeapInt();

            lock (Library.API_LOCK)
            {
                Status.Code = Library.API.IOCtl(DeviceID, (int)J2534IOCTL.READ_PROG_VOLTAGE, IntPtr.Zero, Voltage);
                if (Status.IsNOTClear)
                {
                    Status.Description = Library.GetLastError();
                    throw new J2534Exception(Status);
                }
                return Voltage;
            }
        }

        public Channel ConstructChannel(J2534PROTOCOL ProtocolID, J2534BAUD Baud, J2534CONNECTFLAG ConnectFlags)
        {
            return new Channel(this, ProtocolID, Baud, ConnectFlags);
        }
    }
}
