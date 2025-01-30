using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AddressablesTools.Classes;

namespace AddressablesTools.JSON;

[JsonSerializable(typeof(Dictionary<string, BundleInfo>))]
[JsonSerializable(typeof(BundleInfo))]
[JsonSerializable(typeof(AssetBundleRequestOptions))]
[JsonSerializable(typeof(ContentCatalogDataJson))]
[JsonSerializable(typeof(ObjectInitializationDataJson))]
[JsonSerializable(typeof(SerializedTypeJson))]
[JsonSerializable(typeof(JsonObject))]
internal partial class CatalogJsonSerializerContext : JsonSerializerContext
{
    public static readonly JsonSerializerOptions CatalogJsonOptions;

    static CatalogJsonSerializerContext()
    {
        CatalogJsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            TypeInfoResolver = Default
        };
    }
}