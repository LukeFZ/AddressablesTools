using AddressablesTools.Reader;
using System.Collections.Generic;
using System.Linq;

namespace AddressablesTools.Catalog
{
    public class ResourceLocation
    {
        public string InternalId { get; set; }
        public string ProviderId { get; set; }
        public object DependencyKey { get; set; }
        public List<ResourceLocation> Dependencies { get; set; }
        public object Data { get; set; }
        public int HashCode { get; set; }
        public int DependencyHashCode { get; set; }
        public string PrimaryKey { get; set; }
        public SerializedType Type { get; set; }

        internal void Read(
            string internalId, string providerId, object dependencyKey, object data,
            int depHashCode, object primaryKey, SerializedType resourceType
        )
        {
            InternalId = internalId;
            ProviderId = providerId;
            DependencyKey = dependencyKey;
            Dependencies = null;
            Data = data;
            HashCode = internalId.GetHashCode() * 31 + providerId.GetHashCode();
            DependencyHashCode = depHashCode;
            PrimaryKey = primaryKey.ToString();
            Type = resourceType;
        }

        internal void Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;
            var primaryKeyOffset = reader.ReadUInt32();
            var internalIdOffset = reader.ReadUInt32();
            var providerIdOffset = reader.ReadUInt32();
            var dependenciesOffset = reader.ReadUInt32();
            var dependencyHashCode = reader.ReadInt32();
            var dataOffset = reader.ReadUInt32();
            var typeOffset = reader.ReadUInt32();

            PrimaryKey = reader.ReadEncodedString(primaryKeyOffset, '/');
            InternalId = reader.ReadEncodedString(internalIdOffset, '/');
            ProviderId = reader.ReadEncodedString(providerIdOffset, '.');

            var dependencyOffsets = reader.ReadOffsetArray(dependenciesOffset);

            var dependencies = new List<ResourceLocation>(dependencyOffsets.Length);
            dependencies.AddRange(dependencyOffsets.Select(reader.ReadResourceLocation));

            DependencyKey = null;
            Dependencies = dependencies;

            // officially, dependenciesOffset is used here. lol. we can't do
            // that since writing the file would permenantly lose that value.
            DependencyHashCode = dependencyHashCode;
            Data = reader.ReadSerializedObject(dataOffset);
            Type = reader.ReadSerializedType(typeOffset);
        }
    }
}
