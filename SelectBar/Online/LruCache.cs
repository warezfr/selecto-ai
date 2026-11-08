using System;
using System.Collections.Generic;

namespace SelectBar.Online;

/// <summary>Cache mémoire LRU, sûr entre threads.</summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map = new();
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();
    private readonly object _gate = new();

    public LruCache(int capacity = 200) { _capacity = Math.Max(1, capacity); }

    public int Count { get { lock (_gate) return _map.Count; } }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existing)) { _order.Remove(existing); _map.Remove(key); }
            var node = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
            _order.AddFirst(node);
            _map[key] = node;
            while (_map.Count > _capacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
            }
        }
    }
}
