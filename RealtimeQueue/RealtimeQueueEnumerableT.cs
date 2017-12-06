using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeQueue
{
    public class RealtimeQueueEnumerable<T> : IEnumerable<T>
    {
        protected RealtimeQueue<T> rootList;
        private int timeout = 0;

        public RealtimeQueueEnumerable(RealtimeQueue<T> Root, int Timeout = 0)
        {
            rootList = Root;
            timeout = Timeout;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new RealtimeQueueEnumerator<T>(rootList, timeout);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            //TODO: Add legacy code
            throw new NotImplementedException();
        }

        public T DecueueOne()
        {
            var item = default(T);
            lock (rootList.mutex)
            {
                if (!rootList.LinkNode.Next.Equals(rootList.LinkNode))
                {
                    var FirstNode = rootList.LinkNode.Next; //
                    FirstNode.EnumeratorHold();
                    FirstNode.Remove();
                    item = FirstNode.Item;
                    FirstNode.EnumeratorRelease();
                }
            }
            return item;
        }

        public IEnumerable<T> Decueue()
        {
            var Enumerator = new RealtimeQueueEnumerator<T>(rootList, timeout);

            if (Enumerator.MoveWhere(null, true))
            {
                yield return Enumerator.Current;
            }
        }

        public IEnumerable<T> DecueueWhere(Predicate<T> Predicate)
        {
            var Enumerator = new RealtimeQueueEnumerator<T>(rootList, timeout);

            if (Enumerator.MoveWhere(Predicate, true))
            {
                yield return Enumerator.Current;
            }
        }

        //Optimized version of WHERE.
        public IEnumerable<T> Where(Predicate<T> Predicate)
        {
            var Enumerator = new RealtimeQueueEnumerator<T>(rootList, timeout);

            if (Enumerator.MoveWhere(Predicate))
            {
                yield return Enumerator.Current;
            }
        }
    }
}
