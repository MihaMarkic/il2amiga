using System.Reflection;

namespace IL2Amiga.Engine
{
    public class ScannerQueueItem
    {
        public MemberInfo Item { get; }
        public string QueueReason { get; }
        public string SourceItem { get; }

        public ScannerQueueItem(MemberInfo memberInfo, string queueReason, string sourceItem)
        {
            Item = memberInfo;
            QueueReason = queueReason;
            SourceItem = sourceItem;
        }

        public override string ToString()
        {
            return $"{Item.MemberType} {Item}";
        }
    }
}
