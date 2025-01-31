using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AddressablesTools.Binary;
using AddressablesTools.Catalog;

namespace AddressablesTools.Reader
{
    internal class CatalogBinaryReader(Stream input) : BinaryReader(input)
    {
        public int Version { get; set; } = 1;

        private readonly Dictionary<uint, object> _cache = [];

        private bool TryGetCachedValue<T>(uint offset, out T value)
        {
            value = default;

            if (_cache.TryGetValue(offset, out var entry) && entry is T entryValue)
            {
                value = entryValue;
                return true;
            }

            return false;
        }

        private void CacheValue<T>(uint offset, T value)
        {
            Debug.Assert(!_cache.ContainsKey(offset));
            _cache[offset] = value;
        }

        private string ReadBasicString(uint offset, bool unicode)
        {
            BaseStream.Position = offset - 4;

            var length = ReadInt32();
            var data = ReadBytes(length);

            var stringValue = unicode
                ? Encoding.Unicode.GetString(data)
                : Encoding.ASCII.GetString(data);

            return stringValue;
        }

        private string ReadDynamicString(uint offset, char sep)
        {
            var stack = new Stack<string>();

            BaseStream.Position = offset;
            while (true)
            {
                var partStringOffset = ReadUInt32();
                var nextPartOffset = ReadUInt32();

                stack.Push(ReadEncodedString(partStringOffset));
                if (nextPartOffset == uint.MaxValue)
                    break;

                BaseStream.Position = nextPartOffset;
            }

            return stack.Count == 1
                // todo: investigate if v2 needs this reversed again
                ? stack.Pop() 
                : string.Join(sep, stack.Reverse());
        }

        public string ReadEncodedString(uint encodedOffset, char dynstrSep = '\0', bool cache = true)
        {
            if (encodedOffset == uint.MaxValue)
                return null;

            if (TryGetCachedValue(encodedOffset, out string cachedString))
                return cachedString;

            var unicode = (encodedOffset & 0x80000000) != 0;
            var dynamicString = (encodedOffset & 0x40000000) != 0 && dynstrSep != '\0';
            var offset = encodedOffset & 0x3fffffff;

            var value = dynamicString
                ? ReadDynamicString(offset, dynstrSep)
                : ReadBasicString(offset, unicode);

            if (cache)
                CacheValue(encodedOffset, value);

            return value;
        }

        public T ReadObject<T>(uint offset, bool cache = true) where T : IBinaryReadable<T>
        {
            if (offset == uint.MaxValue)
                return default;

            if (TryGetCachedValue(offset, out T value))
                return value;

            value = T.Read(this, offset);

            if (cache)
                CacheValue(offset, value);

            return value;
        }

        public T[] ReadObjectArray<T>(uint offset, bool cache = true) where T : IBinaryReadable<T>
        {
            if (offset == uint.MaxValue)
                return [];

            if (TryGetCachedValue(offset, out T[] value))
                return value;

            BaseStream.Position = offset - 4;

            var sizeInBytes = ReadInt32();
            if (sizeInBytes % sizeof(uint) != 0)
                throw new InvalidDataException("Array size must be a multiple of 4");

            var entryCount = sizeInBytes / sizeof(uint);

            var offsets = ArrayPool<uint>.Shared.Rent(entryCount);
            value = new T[entryCount];

            for (int i = 0; i < entryCount; i++)
                offsets[i] = ReadUInt32();

            for (int i = 0; i < entryCount; i++)
                value[i] = ReadObject<T>(offsets[i], cache);

            ArrayPool<uint>.Shared.Return(offsets);

            if (cache)
                CacheValue(offset, value);

            return value;
        }

        public uint[] ReadOffsetArray(uint offset)
        {
            if (offset == uint.MaxValue)
                return [];

            BaseStream.Position = offset - 4;

            var sizeInBytes = ReadInt32();
            if (sizeInBytes % sizeof(uint) != 0)
                throw new InvalidDataException("Array size must be a multiple of 4");

            var entryCount = sizeInBytes / sizeof(uint);

            var value = new uint[entryCount];
            for (int i = 0; i < entryCount; i++)
                value[i] = ReadUInt32();

            return value;
        }

        public object ReadSerializedObject(uint offset, bool cache = true)
        {
            if (offset == uint.MaxValue)
                return null;

            if (TryGetCachedValue(offset, out object value))
                return value;

            value = SerializedObjectDecoder.DecodeV2(this, offset);

            if (cache)
                CacheValue(offset, value);

            return value;
        }
    }
}
