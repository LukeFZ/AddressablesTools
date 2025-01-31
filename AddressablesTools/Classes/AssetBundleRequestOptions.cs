using AddressablesTools.JSON;
using AddressablesTools.Reader;
using System.Text.Json;
using System.Text.Json.Nodes;
using AddressablesTools.Binary;

namespace AddressablesTools.Classes
{
    public enum AssetLoadMode
    {
        RequestedAssetAndDependencies,
        AllPackedAssetsAndDependencies
    }

    public class AssetBundleRequestOptions : IBinaryReadable<AssetBundleRequestOptions>
    {
        public string Hash { get; set; }
        public uint Crc { get; set; }
        public int Timeout { get; set; }
        public bool ChunkedTransfer { get; set; }
        public int RedirectLimit { get; set; }
        public int RetryCount { get; set; }
        public string BundleName { get; set; }
        public AssetLoadMode AssetLoadMode { get; set; }
        public long BundleSize { get; set; }
        public bool UseCrcForCachedBundle { get; set; }
        public bool UseUnityWebRequestForLocalBundles { get; set; }
        public bool ClearOtherCachedVersionsWhenLoaded { get; set; }

        internal void Read(string jsonText)
        {
            var jsonObj = JsonSerializer.Deserialize<JsonObject>(jsonText);
            if (jsonObj == null)
            {
                return;
            }

            Hash = (string)jsonObj["m_Hash"];
            Crc = (uint)jsonObj["m_Crc"];
            Timeout = (int)jsonObj["m_Timeout"];
            ChunkedTransfer = (bool)jsonObj["m_ChunkedTransfer"];
            RedirectLimit = (int)jsonObj["m_RedirectLimit"];
            RetryCount = (int)jsonObj["m_RetryCount"];
            BundleName = (string)jsonObj["m_BundleName"];
            AssetLoadMode = (AssetLoadMode)(int)jsonObj["m_AssetLoadMode"];
            BundleSize = (long)jsonObj["m_BundleSize"];
            UseCrcForCachedBundle = (bool)jsonObj["m_UseCrcForCachedBundles"]; // not a typo
            UseUnityWebRequestForLocalBundles = (bool)jsonObj["m_UseUWRForLocalBundles"];
            ClearOtherCachedVersionsWhenLoaded = (bool)jsonObj["m_ClearOtherCachedVersionsWhenLoaded"];
        }

        internal string WriteJson()
        {
            var jsonObj = new JsonObject();

            // how many of these properties existed during v1?
            jsonObj["m_Hash"] = Hash;
            jsonObj["m_Crc"] = Crc;
            jsonObj["m_Timeout"] = Timeout;
            jsonObj["m_ChunkedTransfer"] = ChunkedTransfer;
            jsonObj["m_RedirectLimit"] = RedirectLimit;
            jsonObj["m_RetryCount"] = RetryCount;
            jsonObj["m_BundleName"] = BundleName;
            jsonObj["m_AssetLoadMode"] = (int)AssetLoadMode;
            jsonObj["m_BundleSize"] = BundleSize;
            jsonObj["m_UseCrcForCachedBundles"] = UseCrcForCachedBundle; // not a typo
            jsonObj["m_UseUWRForLocalBundles"] = UseUnityWebRequestForLocalBundles;
            jsonObj["m_ClearOtherCachedVersionsWhenLoaded"] = ClearOtherCachedVersionsWhenLoaded;

            return JsonSerializer.Serialize(jsonObj, CatalogJsonSerializerContext.CatalogJsonOptions);
        }

        internal void ReadInternal(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            var hashOffset = reader.ReadUInt32();
            var bundleNameOffset = reader.ReadUInt32();
            var crc = reader.ReadUInt32();
            var bundleSize = reader.ReadUInt32();
            var commonInfoOffset = reader.ReadUInt32();

            reader.BaseStream.Position = hashOffset;
            var hashV0 = reader.ReadUInt32();
            var hashV1 = reader.ReadUInt32();
            var hashV2 = reader.ReadUInt32();
            var hashV3 = reader.ReadUInt32();
            Hash = new Hash128(hashV0, hashV1, hashV2, hashV3).Value;

            BundleName = reader.ReadEncodedString(bundleNameOffset, '_');
            Crc = crc;
            BundleSize = bundleSize;

            // split in another class in case we need to do writing with duplicates later
            var commonInfo = new CommonInfo();
            commonInfo.Read(reader, commonInfoOffset);

            Timeout = commonInfo.Timeout;
            RedirectLimit = commonInfo.RedirectLimit;
            RetryCount = commonInfo.RetryCount;
            AssetLoadMode = commonInfo.AssetLoadMode;
            ChunkedTransfer = commonInfo.ChunkedTransfer;
            UseCrcForCachedBundle = commonInfo.UseCrcForCachedBundle;
            UseUnityWebRequestForLocalBundles = commonInfo.UseUnityWebRequestForLocalBundles;
            ClearOtherCachedVersionsWhenLoaded = commonInfo.ClearOtherCachedVersionsWhenLoaded;
        }

        static AssetBundleRequestOptions IBinaryReadable<AssetBundleRequestOptions>.Read(CatalogBinaryReader reader,
            uint offset)
        {
            var abro = new AssetBundleRequestOptions();
            abro.ReadInternal(reader, offset);
            return abro;
        }

        public class CommonInfo
        {
            public short Timeout { get; set; }
            public byte RedirectLimit { get; set; }
            public byte RetryCount { get; set; }
            public AssetLoadMode AssetLoadMode { get; set; }
            public bool ChunkedTransfer { get; set; }
            public bool UseCrcForCachedBundle { get; set; }
            public bool UseUnityWebRequestForLocalBundles { get; set; }
            public bool ClearOtherCachedVersionsWhenLoaded { get; set; }

            internal void Read(CatalogBinaryReader reader, uint offset)
            {
                reader.BaseStream.Position = offset;

                var timeout = reader.ReadInt16();
                var redirectLimit = reader.ReadByte();
                var retryCount = reader.ReadByte();
                var flags = reader.ReadInt32();

                Timeout = timeout;
                RedirectLimit = redirectLimit;
                RetryCount = retryCount;

                if ((flags & 1) != 0)
                {
                    AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;
                }
                else
                {
                    AssetLoadMode = AssetLoadMode.AllPackedAssetsAndDependencies;
                }

                ChunkedTransfer = (flags & 2) != 0;
                UseCrcForCachedBundle = (flags & 4) != 0;
                UseUnityWebRequestForLocalBundles = (flags & 8) != 0;
                ClearOtherCachedVersionsWhenLoaded = (flags & 16) != 0;
            }
        }
    }
}
