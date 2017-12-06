using System;
using System.Runtime.InteropServices;
using System.Text;

namespace J2534
{
    public class J2534Library
    {
        public string FileName { get; private set; }
        internal API_SIGNATURE API_Signature { get; private set; }
        public bool IsLoaded { get; private set; }
        internal J2534APIWrapper API { get; private set; }
        internal object API_LOCK = new object();

        public string API_Support
        {
            get
            {
                StringBuilder API_String = new StringBuilder();
                if (API_Signature.DREWTECH_API != DREWTECH_API.NONE)
                    API_String.Append("DREWTECH ");
                else if (API_Signature.SAE_API != SAE_API.NONE)
                    API_String.Append("SAE ");
                else
                    API_String.Append("NO J2534 API DETECTED");

                switch (API_Signature.SAE_API)
                {
                    case SAE_API.V202_SIGNATURE:
                        API_String.Append("J2534 v2.02");
                        break;
                    case SAE_API.V404_SIGNATURE:
                        API_String.Append("J2534 v4.04");
                        break;
                    case SAE_API.V500_SIGNATURE:
                        API_String.Append("J2534 v5.00");
                        break;
                    default:
                        API_String.Append("UNKNOWN API");
                        break;
                }
                return API_String.ToString();
            }
        }

        internal J2534Library(string FileName)
        {
            this.FileName = FileName;
            API = new J2534.J2534APIWrapper();
            Load();
        }

        internal bool Load()
        {
            API_Signature = API.LoadJ2534Library(FileName);

            if (API_Signature.SAE_API == SAE_API.V202_SIGNATURE ||
                API_Signature.SAE_API == SAE_API.V404_SIGNATURE ||
                API_Signature.SAE_API == SAE_API.V500_SIGNATURE)
                IsLoaded = true;
            else
            {
                IsLoaded = false;
                API.FreeLibrary();
            }
            return IsLoaded;
        }

        internal bool Free()
        {
            lock (API_LOCK)
                IsLoaded = API.FreeLibrary();   //Does this return a true or false????
            return IsLoaded;
        }

        internal J2534Device ConstructDevice()
        {
            return new J2534.J2534Device(this);
        }

        internal J2534Device ConstructDevice(string DeviceName)
        {
            return new J2534.J2534Device(this, DeviceName);
        }

        internal J2534Device ConstructDevice(GetNextCarDAQResults CarDAQ)
        {
            return new J2534.J2534Device(this, CarDAQ);
        }

        internal void GetNextDevice()
        {
            //API.GetNextDevice(pSDevice);
        }

        internal J2534Status GetNextCarDAQ_RESET()
        {
            J2534Status Status = new J2534Status();
            lock (API_LOCK)
            {
                Status.Code = API.GetNextCarDAQ(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (Status.IsNotOK)
                {
                    Status.Description = GetLastError();
                }
            }
            return Status;
        }

        internal GetNextCarDAQResults GetNextCarDAQ()
        {
            J2534Status Status = new J2534Status();
            IntPtr pName = Marshal.AllocHGlobal(4);
            IntPtr pAddr = Marshal.AllocHGlobal(4);
            IntPtr pVer = Marshal.AllocHGlobal(4);

            lock (API_LOCK)
            {
                Status.Code = API.GetNextCarDAQ(pName, pVer, pAddr);


                if (Status.Code == J2534ERR.FUNCTION_NOT_ASSIGNED || Marshal.ReadIntPtr(pName) == IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pName);
                    Marshal.FreeHGlobal(pVer);
                    Marshal.FreeHGlobal(pAddr);
                    return new GetNextCarDAQResults() { Exists = false };
                }
                else if (Status.IsNotOK)
                {
                    Status.Description = GetLastError();
                    Marshal.FreeHGlobal(pName);
                    Marshal.FreeHGlobal(pVer);
                    Marshal.FreeHGlobal(pAddr);
                    throw new J2534Exception(Status);
                }

                byte[] b = new byte[3];
                Marshal.Copy(pVer, b, 0, 3);

                GetNextCarDAQResults Result = new GetNextCarDAQResults()
                {
                    Exists = true,
                    Name = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(pName)),
                    Version = string.Format("{2}.{1}.{0}", b[0], b[1], b[2]),
                    Address = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(pAddr))
                };

                Marshal.FreeHGlobal(pName);
                Marshal.FreeHGlobal(pAddr);
                Marshal.FreeHGlobal(pVer);

                return Result;
            }
        }

        internal string GetLastError()  //Should never be called outside of API_LOCK
        {
            J2534ERR Status;
            string error_description;
            IntPtr pErrorDescription = Marshal.AllocHGlobal(80);
            Status = (J2534ERR)API.GetLastError(pErrorDescription);
            if (Status == J2534ERR.STATUS_NOERROR)
                error_description = Marshal.PtrToStringAnsi(pErrorDescription);
            else
                error_description = String.Format("GetLastError failed with status: {0}", Status.ToString());
            Marshal.FreeHGlobal(pErrorDescription);
            return error_description;
        }
    }
}
