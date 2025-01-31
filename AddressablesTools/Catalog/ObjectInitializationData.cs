using AddressablesTools.Binary;
using AddressablesTools.JSON;
using AddressablesTools.Reader;

namespace AddressablesTools.Catalog
{
    public class ObjectInitializationData : IBinaryReadable<ObjectInitializationData>
    {
        public string Id { get; set; }
        public SerializedType ObjectType { get; set; }
        public string Data { get; set; }

        internal void Read(ObjectInitializationDataJson obj)
        {
            Id = obj.m_Id;
            ObjectType = new SerializedType();
            ObjectType.Read(obj.m_ObjectType);
            Data = obj.m_Data;
        }

        internal void Write(ObjectInitializationDataJson obj)
        {
            obj.m_Id = Id;
            obj.m_ObjectType = new SerializedTypeJson();
            ObjectType.Write(obj.m_ObjectType);
            obj.m_Data = Data;
        }

        static ObjectInitializationData IBinaryReadable<ObjectInitializationData>.Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            var idOffset = reader.ReadUInt32();
            var objectTypeOffset = reader.ReadUInt32();
            var dataOffset = reader.ReadUInt32();

            var id = reader.ReadEncodedString(idOffset);
            var objectType = reader.ReadObject<SerializedType>(objectTypeOffset);
            var data = reader.ReadEncodedString(dataOffset);

            return new ObjectInitializationData
            {
                Id = id,
                ObjectType = objectType,
                Data = data
            };
        }
    }
}
