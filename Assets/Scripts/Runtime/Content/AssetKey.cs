using System;

namespace TapTheObject.Content
{
    /// <summary>
    /// Identifies an addressable asset. Wrapping the runtime key keeps Addressables types out of the game
    /// logic, so the round loop can run against a fake provider.
    /// </summary>
    public readonly struct AssetKey : IEquatable<AssetKey>
    {
        private readonly object _value;

        private AssetKey(object value)
        {
            _value = value;
        }

        public bool IsValid => _value != null;

        internal object Value => _value;

        public static AssetKey From(object runtimeKey)
        {
            if (runtimeKey == null)
            {
                throw new ArgumentNullException(nameof(runtimeKey));
            }

            return new AssetKey(runtimeKey);
        }

        public bool Equals(AssetKey other) => Equals(_value, other._value);

        public override bool Equals(object obj) => obj is AssetKey other && Equals(other);

        public override int GetHashCode() => _value == null ? 0 : _value.GetHashCode();

        public override string ToString() => _value == null ? "<invalid key>" : _value.ToString();
    }
}
