using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace YARG.Helpers
{
    /// <summary>
    /// Generic Unity serializable dictionary that otherwise acts like a regular C# Dictionary<br />
    /// Create a concrete subclass of type SerializedDictionary&lt;TEnum, TValue&gt;<br />
    /// NOTE: The associated property drawer only works for enum keys with a value type unity can display in the inspector.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public abstract class SerializedDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        [SerializeField]
        protected List<TKey> _keys = new();

        [SerializeField]
        protected List<TValue> _values = new();

        private readonly Dictionary<TKey, TValue> _dictionary = new();

        public Dictionary<TKey, TValue> Dictionary => _dictionary;
        public IReadOnlyList<TKey> Keys => _keys;
        public IReadOnlyList<TValue> Values => _values;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public void OnBeforeSerialize()
        {
            _keys.Clear();
            _values.Clear();

            foreach (var (key, value) in _dictionary)
            {
                _keys.Add(key);
                _values.Add(value);
            }
        }

        public virtual void OnAfterDeserialize()
        {
            _dictionary.Clear();

            int count = Math.Min(_keys.Count, _values.Count);

            for (int i = 0; i < count; i++)
            {
                _dictionary.Add(_keys[i], _values[i]);
            }
        }

        public bool TryAdd(TKey key, TValue value)
        {
            return _dictionary.TryAdd(key, value);
        }

        public void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
        }

        public bool Remove(TKey key)
        {
            return _dictionary.Remove(key);
        }

        public bool Remove(TKey key, out TValue value)
        {
            return _dictionary.Remove(key, out value);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public void Clear()
        {
            _keys.Clear();
            _values.Clear();
            _dictionary.Clear();
        }

        public Dictionary<TKey, TValue> ToDictionary()
        {
            return new Dictionary<TKey, TValue>(_dictionary);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public bool ContainsValue(TValue value)
        {
            return _dictionary.ContainsValue(value);
        }
    }
}