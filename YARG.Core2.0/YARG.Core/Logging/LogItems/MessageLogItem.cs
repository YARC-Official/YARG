using System.Collections.Concurrent;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public class MessageLogItem : LogItem
    {
        private const int STARTING_INSTANCES = 5;

        private static ConcurrentBag<MessageLogItem> _instancePool = new();

        public string Message = "";

        static MessageLogItem()
        {
            for (int i = 0; i < STARTING_INSTANCES; i++)
            {
                _instancePool.Add(new());
            }
        }

        private MessageLogItem() {}

        ~MessageLogItem()
        {
            YargLogger.LogError("Log item instance was not returned to the pool!");
        }

        public static MessageLogItem MakeItem(string message)
        {
            if (!_instancePool.TryTake(out var item))
                item = new();

            item.Message = message;
            return item;
        }

        public override void FormatMessage(ref Utf16ValueStringBuilder output)
        {
            output.Append(Message);
        }

        protected override void ReturnToPool()
        {
            _instancePool.Add(this);
        }
    }
}