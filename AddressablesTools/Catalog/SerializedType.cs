using AddressablesTools.JSON;
using System;
using System.IO;
using AddressablesTools.Binary;
using AddressablesTools.Reader;

namespace AddressablesTools.Catalog
{
    public class SerializedType : IBinaryReadable<SerializedType>
    {
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }

        public string MatchName
        {
            get
            {
                _cachedMatchName ??= $"{ShortName}; {ClassName}";
                return _cachedMatchName;
            }
        }

        public string ShortName
        {
            get
            {
                if (_cachedShortName == null)
                {
                    if (!AssemblyName.Contains(','))
                    {
                        throw new InvalidDataException("Assembly name must have commas");
                    }

                    _cachedShortName = AssemblyName.Split(',')[0];
                }

                return _cachedShortName;
            }
        }

        private string? _cachedMatchName;
        private string? _cachedShortName;

        public override bool Equals(object obj)
        {
            return obj is SerializedType type &&
                   AssemblyName == type.AssemblyName &&
                   ClassName == type.ClassName;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AssemblyName, ClassName);
        }

        internal void Read(SerializedTypeJson type)
        {
            AssemblyName = type.m_AssemblyName;
            ClassName = type.m_ClassName;
        }

        internal void Write(SerializedTypeJson type)
        {
            type.m_AssemblyName = AssemblyName;
            type.m_ClassName = ClassName;
        }

        static SerializedType IBinaryReadable<SerializedType>.Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            var assemblyNameOffset = reader.ReadUInt32();
            var classNameOffset = reader.ReadUInt32();

            var assemblyName = reader.ReadEncodedString(assemblyNameOffset, '.');
            var className = reader.ReadEncodedString(classNameOffset, '.');

            return new SerializedType
            {
                AssemblyName = assemblyName,
                ClassName = className
            };
        }
    }
}
