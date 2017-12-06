using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeQueue
{
    internal class SequenceChangedEventArgs<T>
    {
        public RealtimeQueueNode<T> NewNodeReference { get; set; }
        public RealtimeQueueNode<T> ExcludedNode { get; set; }
        public SequenceChangedEventArgs(RealtimeQueueNode<T> NewReference, RealtimeQueueNode<T> ExcludedNode)
        {
            NewNodeReference = NewReference;
            this.ExcludedNode = ExcludedNode;
        }
    }
}
