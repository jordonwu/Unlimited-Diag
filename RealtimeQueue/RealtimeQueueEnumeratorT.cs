using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace RealtimeQueue
{
    public class RealtimeQueueEnumerator<T> : IEnumerator<T>
    {
        private RealtimeQueue<T> rootList;
        private RealtimeQueueNode<T> CurrentNode;

        private int Timeout;
        private Stopwatch TimeoutClock = new Stopwatch();

        public RealtimeQueueEnumerator(RealtimeQueue<T> Root, int Timeout)
        {
            rootList = Root;
            this.Timeout = Timeout;
            Reset();
        }
        
        public T Current
        {
            get { return CurrentNode.Item; }
        }

        object IEnumerator.Current
        {
            get { return CurrentNode.Item; }
        }

        public bool MoveNext()
        {
            return TryMove();
        }

        public bool MoveWhere(Predicate<T> Predicate, bool Remove = false)
        {
            if (TryMove(Predicate, Remove)) return true;

            var Timeremaining = Timeout - (int)TimeoutClock.ElapsedMilliseconds;

            if(Timeout == 0 || Timeremaining < 1)
            {
                return false;
            }

            do
            {
                rootList.AddActionMultiplex.Invoke();
                if (TryMove(Predicate, Remove)) return true;
            } while (TimeoutClock.ElapsedMilliseconds < Timeout);

            return false;
        }

        public void Reset()
        {
            if(CurrentNode != null)
            {
                CurrentNode.EnumeratorRelease();
            }
            CurrentNode = rootList.LinkNode;
            CurrentNode.EnumeratorHold();
            TimeoutClock.Reset();
            TimeoutClock.Start();
        }

        private bool TryMove(Predicate<T> Predicate = null, bool Remove = false)
        {
            lock (rootList.mutex)
            {
                var StartNode = CurrentNode;

                //CurrentNode.Next should never be null
                while (!CurrentNode.Next.Equals(rootList.LinkNode))
                {                    
                    CurrentNode = CurrentNode.Next;
                    if (Predicate?.Invoke(CurrentNode.Item) ?? true)
                    {
                        StartNode.EnumeratorRelease();
                        CurrentNode.EnumeratorHold();
                        if (Remove)
                        {
                            CurrentNode.Remove();
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CurrentNode.EnumeratorRelease();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
