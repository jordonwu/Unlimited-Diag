using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace RealtimeQueue
{
    public class BoolInterlock
    {
        private int state = States.Unlocked;

        public bool Enter()
        {
            //Set state to Locked, and return the original state
            return Interlocked.Exchange(ref state, States.Locked) == States.Unlocked;
        }
        public void Exit()
        {
            state = States.Unlocked;
        }
        private static class States
        {
            public const int Unlocked = 0;
            public const int Locked = 1;
        }
    }
}
