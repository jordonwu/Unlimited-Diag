using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeQueue
{
    public partial class RealtimeQueue<T> : RealtimeQueueEnumerable<T>
    {
        internal RealtimeQueueNode<T> LinkNode { get; }
        internal Stack<RealtimeQueueNode<T>> NodeRecycleBin = new Stack<RealtimeQueueNode<T>>();
        internal object mutex = new object();
        internal ActionMultiplex AddActionMultiplex;

        public RealtimeQueue(Action AddAction) : base(null)
        {
            base.rootList = this;
            LinkNode = new RealtimeQueueNode<T>(this);
            LinkNode.Next = LinkNode;
            AddActionMultiplex = new ActionMultiplex(AddAction);
        }

        public void AddRange(IEnumerable<T> Range)
        {
            var Enumerator = Range?.GetEnumerator();

            if(Enumerator?.MoveNext() ?? false)
            {
                var FirstNode = GenerateNode(Enumerator.Current);
                var LastNode = FirstNode;
                while (Enumerator.MoveNext())
                {
                    LastNode.Next = GenerateNode(Enumerator.Current);
                    LastNode = LastNode.Next;
                }
                LastNode.Next = rootList.LinkNode;
                LinkNode.NotifySequenceChanged(NewReference: FirstNode, ExcludedNode: LastNode);
            }
        }

        public void Add(T Item)
        {
            var NewNode = GenerateNode(Item);
            NewNode.Next = LinkNode;
            LinkNode.NotifySequenceChanged(NewReference: NewNode, ExcludedNode: NewNode);
        }

        private RealtimeQueueNode<T> GenerateNode(T Item = default(T))
        {
            RealtimeQueueNode<T> NewNode;

            if (NodeRecycleBin.Count > 0)
            {
                NewNode = NodeRecycleBin.Pop();
            }
            else
            {
                NewNode = new RealtimeQueueNode<T>(this);
            }

            NewNode.Item = Item;
            
            return NewNode;
        }
    }
}
