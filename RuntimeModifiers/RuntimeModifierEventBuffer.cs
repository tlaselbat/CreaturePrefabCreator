using System;
using System.Collections.Generic;

namespace CreaturePrefabCreator.RuntimeModifiers
{
    public sealed class RuntimeModifierEvent
    {
        public DateTime TimeUtc { get; set; }
        public string EventType { get; set; }
        public string PrefabName { get; set; }
        public ZDOID? ZdoId { get; set; }
        public string Message { get; set; }
    }

    public static class RuntimeModifierEventBuffer
    {
        public const int DefaultCapacity = 200;
        public const int MinCapacity = 50;
        public const int MaxCapacity = 1000;

        private static readonly object _lock = new object();
        private static RuntimeModifierEvent[] _buffer = new RuntimeModifierEvent[DefaultCapacity];
        private static int _head;
        private static int _count;
        private static int _capacity = DefaultCapacity;

        public static void SetCapacity(int capacity)
        {
            int clamped = Math.Max(MinCapacity, Math.Min(MaxCapacity, capacity));
            lock (_lock)
            {
                if (clamped == _capacity) return;
                _buffer = new RuntimeModifierEvent[clamped];
                _head = 0;
                _count = 0;
                _capacity = clamped;
            }
        }

        public static void Record(string eventType, string prefabName, ZDOID? zdoId, string message)
        {
            var ev = new RuntimeModifierEvent
            {
                TimeUtc = DateTime.UtcNow,
                EventType = eventType,
                PrefabName = prefabName,
                ZdoId = zdoId,
                Message = message
            };

            lock (_lock)
            {
                _buffer[_head] = ev;
                _head = (_head + 1) % _capacity;
                if (_count < _capacity) _count++;
            }
        }

        public static IReadOnlyList<RuntimeModifierEvent> GetRecent(int count = 50)
        {
            int clamped = Math.Max(1, Math.Min(count, MaxCapacity));
            lock (_lock)
            {
                int take = Math.Min(clamped, _count);
                var result = new List<RuntimeModifierEvent>(take);
                int start = (_head - _count + _capacity) % _capacity;
                for (int i = _count - take; i < _count; i++)
                {
                    int idx = (start + i) % _capacity;
                    result.Add(_buffer[idx]);
                }
                return result;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _capacity);
                _head = 0;
                _count = 0;
            }
        }

        public static int Count
        {
            get { lock (_lock) { return _count; } }
        }
    }
}
