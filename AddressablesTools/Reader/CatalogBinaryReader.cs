using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AddressablesTools.Catalog;

namespace AddressablesTools.Reader
{
    internal class CatalogBinaryReader : BinaryReader
    {
        public int Version { get; set; } = 1;

        public CatalogBinaryReader(Stream input) : base(input) { }

        private readonly Dictionary<uint, string> _stringCache = [];
        private readonly Dictionary<uint, SerializedType> _typeCache = [];
        private readonly Dictionary<uint, uint[]> _arrayCache = [];
        private readonly Dictionary<uint, ResourceLocation> _locationCache = [];
        private readonly Dictionary<uint, object> _objectCache = [];

        private string ReadBasicString(long offset, bool unicode)
        {
            BaseStream.Position = offset - 4;

            var length = ReadInt32();
            var data = ReadBytes(length);

            var stringValue = unicode
                ? Encoding.Unicode.GetString(data)
                : Encoding.ASCII.GetString(data);

            stringValue = string.Intern(stringValue);

            return stringValue;
        }

        private string ReadDynamicString(long offset, bool unicode, char sep)
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

            if (stack.Count == 1)
                return stack.Pop();

            // todo: investigate if v2 needs this reversed again

            return string.Join(sep, stack.Reverse());
        }

        public string ReadEncodedString(uint encodedOffset, char dynstrSep = '\0')
        {
            if (encodedOffset == uint.MaxValue)
                return null;

            if (!_stringCache.TryGetValue(encodedOffset, out var value))
            {
                var unicode = (encodedOffset & 0x80000000) != 0;
                var dynamicString = (encodedOffset & 0x40000000) != 0 && dynstrSep != '\0';
                var offset = (int)(encodedOffset & 0x3fffffff);

                _stringCache[encodedOffset] = value = dynamicString
                    ? ReadDynamicString(offset, unicode, dynstrSep)
                    : ReadBasicString(offset, unicode);
            }

            return value;
        }

        public uint[] ReadOffsetArray(uint encodedOffset)
        {
            if (encodedOffset == uint.MaxValue)
                return [];

            if (!_arrayCache.TryGetValue(encodedOffset, out var value))
            {
                BaseStream.Position = encodedOffset - 4;

                var byteSize = ReadInt32();
                if (byteSize % sizeof(uint) != 0)
                {
                    throw new InvalidDataException("Array size must be a multiple of 4");
                }

                var elemCount = byteSize / sizeof(uint);
                _arrayCache[encodedOffset] = value = new uint[elemCount];

                for (int i = 0; i < elemCount; i++)
                    value[i] = ReadUInt32();
            }

            return value;
        }

        public SerializedType ReadSerializedType(uint offset)
        {
            if (!_typeCache.TryGetValue(offset, out var value))
            {
                BaseStream.Position = offset;

                var assemblyNameOffset = ReadUInt32();
                var classNameOffset = ReadUInt32();

                var assemblyName = ReadEncodedString(assemblyNameOffset, '.');
                var className = ReadEncodedString(classNameOffset, '.');

                _typeCache[offset] = value = new SerializedType
                {
                    AssemblyName = assemblyName,
                    ClassName = className
                };
            }

            return value;
        }

        public ResourceLocation ReadResourceLocation(uint offset)
        {
            if (!_locationCache.TryGetValue(offset, out var value))
            {
                _locationCache[offset] = value = new ResourceLocation();
                value.Read(this, offset);
            }

            return value;
        }

        public object ReadSerializedObject(uint offset)
        {
            if (!_objectCache.TryGetValue(offset, out var value))
            {
                _objectCache[offset] = value = SerializedObjectDecoder.DecodeV2(this, offset);
            }

            return value;
        }
    }
}
