using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace RealtimeQueue
{
    public class ActionMultiplex
    {
        private bool PropagateExceptions;
        private BoolInterlock Interlock = new BoolInterlock();
        private Exception MultiplexException;
        private EventWaitHandle MultiplexWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        public Action EventAction;

        public ActionMultiplex(Action EventAction, bool PropagateExceptions = false)
        {
            this.EventAction = EventAction;
            this.PropagateExceptions = PropagateExceptions;
        }

        public void Invoke()
        {
            if (Interlock.Enter())
            {
                MultiplexException = null;
                try
                {
                    EventAction.Invoke();
                }
                catch (Exception ActionExcpetion)
                {
                    MultiplexException = ActionExcpetion;
                    throw;
                }
                finally
                {
                    MultiplexWaitHandle.Set();
                    Interlock.Exit();
                }
            }
            else
            {
                MultiplexWaitHandle.WaitOne();
                if (MultiplexException != null && PropagateExceptions) throw MultiplexException;
            }
        }
    }
}
