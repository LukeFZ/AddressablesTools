using AddressablesTools.Reader;

namespace AddressablesTools.Binary;

internal interface IBinaryReadable<out T>
{
    internal static abstract T Read(CatalogBinaryReader reader, uint offset);
}