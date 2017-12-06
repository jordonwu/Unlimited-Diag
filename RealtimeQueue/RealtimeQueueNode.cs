using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeQueue
{
    internal class RealtimeQueueNode<T>
    {
        internal T Item { get; set; }
        internal event EventHandler<SequenceChangedEventArgs<T>> SequenceChanged;

        private RealtimeQueueNode<T> next;
        private RealtimeQueue<T> rootList;
        private int EnumeratorsHoldingAReference;

        internal RealtimeQueueNode(RealtimeQueue<T> Root)
        {
            rootList = Root;
        }

        internal void EnumeratorHold()
        {
            EnumeratorsHoldingAReference++;
        }

        internal void EnumeratorRelease()
        {
            EnumeratorsHoldingAReference--;
            //If nothing is holding a reference to this node, clear and recycle it.
            if (EnumeratorsHoldingAReference == 0 && SequenceChanged == null)
            {
                Next = null;    //This unregisters the ForwardReferenceChanged Event in the property setter
                Item = default(T);  //Release reference to item, to assist GC
                var realtimeQueueNode = this;
                rootList.NodeRecycleBin.Push(this);
            }
        }

        internal RealtimeQueueNode<T> Next
        {
            get { return next; }
            set
            {
                if (next != null) next.SequenceChanged -= OnSequenceChanged;
                next = value;
                if (next != null) next.SequenceChanged += OnSequenceChanged;
            }
        }

        internal void NotifySequenceChanged(RealtimeQueueNode<T> NewReference, RealtimeQueueNode<T> ExcludedNode = null)
        {
            lock (rootList.mutex)
                SequenceChanged?.Invoke(this, new SequenceChangedEventArgs<T>(NewReference, ExcludedNode));
        }

        internal void Remove()
        {
            NotifySequenceChanged(NewReference: Next);  //Note: this is the field next, not the property
        }

        private void OnSequenceChanged(object sender, SequenceChangedEventArgs<T> args)
        {
            if (!this.Equals(args.ExcludedNode))
            {
                Next = args.NewNodeReference;
            }
        }
    }
}
