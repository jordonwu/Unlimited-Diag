using J2534;
using System.Collections.Generic;

namespace SAE
{
    public interface ISAESession
    {
        SAEMessage SAETxRx(SAEMessage Message, int RxDataIndex);
        void SAETx(SAEMessage Message);
        object CreateRxHandle(int Addr, SAEModes Mode);
        void DestroyRxHandle(object Handle);
        List<byte[]> SAERx(object RxHandle, int NumOfMsgs, int Timeout, bool DestroyHandle = false);
    }
}
