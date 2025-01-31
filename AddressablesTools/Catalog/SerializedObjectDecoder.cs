using AddressablesTools.Classes;
using AddressablesTools.Reader;
using System;
using System.IO;
using System.Text;

namespace AddressablesTools.Catalog
{
    internal static class SerializedObjectDecoder
    {
        private const string INT_TYPENAME = "mscorlib; System.Int32";
        private const string LONG_TYPENAME = "mscorlib; System.Int64";
        private const string BOOL_TYPENAME = "mscorlib; System.Boolean";
        private const string STRING_TYPENAME = "mscorlib; System.String";
        private const string HASH128_TYPENAME = "UnityEngine.CoreModule; UnityEngine.Hash128"; // ? what assembly
        private const string ABRO_TYPENAME = "Unity.ResourceManager; UnityEngine.ResourceManagement.ResourceProviders.AssetBundleRequestOptions";

        internal enum ObjectType
        {
            AsciiString,
            UnicodeString,
            UInt16,
            UInt32,
            Int32,
            Hash128,
            Type,
            JsonObject
        }

        internal static object DecodeV1(BinaryReader br)
        {
            var type = (ObjectType)br.ReadByte();

            switch (type)
            {
                case ObjectType.AsciiString:
                {
                    var str = ReadString4(br);
                    return str;
                }

                case ObjectType.UnicodeString:
                {
                    var str = ReadString4Unicode(br);
                    return str;
                }

                case ObjectType.UInt16:
                {
                    return br.ReadUInt16();
                }

                case ObjectType.UInt32:
                {
                    return br.ReadUInt32();
                }

                case ObjectType.Int32:
                {
                    return br.ReadInt32();
                }

                case ObjectType.Hash128:
                {
                    var str = ReadString1(br);
                    var hash = new Hash128(str);
                    return hash;
                }

                case ObjectType.Type:
                {
                    var str = ReadString1(br);
                    var typeReference = new TypeReference(str);
                    return typeReference;
                }

                case ObjectType.JsonObject:
                {
                    var assemblyName = ReadString1(br);
                    var className = ReadString1(br);
                    var jsonText = ReadString4Unicode(br);

                    var jsonObj = new ClassJsonObject(assemblyName, className, jsonText);
                    var matchName = jsonObj.Type.MatchName;
                    switch (matchName)
                    {
                        case ABRO_TYPENAME:
                        {
                            var obj = new AssetBundleRequestOptions();
                            obj.Read(jsonText);
                            return new WrappedSerializedObject(jsonObj.Type, obj);
                        }
                    }

                    // fallback to ClassJsonObject
                    return jsonObj;
                }

                default:
                {
                    return null;
                }
            }
        }

        internal static object DecodeV2(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            var typeNameOffset = reader.ReadUInt32();
            var objectOffset = reader.ReadUInt32();

            var isDefaultObject = objectOffset == uint.MaxValue;

            var type = reader.ReadObject<SerializedType>(typeNameOffset);
            var matchName = type.MatchName;

            switch (matchName)
            {
                case INT_TYPENAME:
                {
                    if (isDefaultObject)
                        return default(int);

                    reader.BaseStream.Position = objectOffset;
                    return reader.ReadInt32();
                }
                case LONG_TYPENAME:
                {
                    if (isDefaultObject)
                        return default(long);

                    reader.BaseStream.Position = objectOffset;
                    return reader.ReadInt64();
                }
                case BOOL_TYPENAME:
                {
                    if (isDefaultObject)
                        return default(bool);


                    reader.BaseStream.Position = objectOffset;
                    return reader.ReadBoolean();
                }
                case STRING_TYPENAME:
                {
                    if (isDefaultObject)
                        return default(string);

                    reader.BaseStream.Position = objectOffset;
                    var stringOffset = reader.ReadUInt32();
                    var separator = reader.ReadChar();
                    return reader.ReadEncodedString(stringOffset, separator);
                }
                case HASH128_TYPENAME:
                {
                    if (isDefaultObject)
                        return default(Hash128);

                    reader.BaseStream.Position = objectOffset;
                    var v0 = reader.ReadUInt32();
                    var v1 = reader.ReadUInt32();
                    var v2 = reader.ReadUInt32();
                    var v3 = reader.ReadUInt32();
                    return new Hash128(v0, v1, v2, v3);
                }
                case ABRO_TYPENAME:
                {
                    if (isDefaultObject)
                        return default(AssetBundleRequestOptions);

                    var abro = reader.ReadObject<AssetBundleRequestOptions>(objectOffset);
                    var wso = new WrappedSerializedObject(type, abro);

                    return wso;
                }
                default:
                {
                    throw new NotImplementedException("Unsupported type for deserialization " + matchName);
                }
            }
        }

        internal static void EncodeV1(BinaryWriter bw, object ob)
        {
            switch (ob)
            {
                case string str:
                {
                    var asciiEncoding = Encoding.ASCII.GetBytes(str);
                    var asciiText = Encoding.ASCII.GetString(asciiEncoding);
                    if (str != asciiText)
                    {
                        bw.Write((byte)ObjectType.UnicodeString);
                        WriteString4Unicode(bw, str);
                    }
                    else
                    {
                        bw.Write((byte)ObjectType.AsciiString);
                        WriteString4(bw, str);
                    }
                    break;
                }

                case ushort ush:
                {
                    bw.Write((byte)ObjectType.UInt16);
                    bw.Write(ush);
                    break;
                }

                case uint uin:
                {
                    bw.Write((byte)ObjectType.UInt32);
                    bw.Write(uin);
                    break;
                }

                case int i:
                {
                    bw.Write((byte)ObjectType.Int32);
                    bw.Write(i);
                    break;
                }

                case Hash128 hash:
                {
                    bw.Write((byte)ObjectType.Hash128);
                    bw.Write(hash.Value);
                    break;
                }

                case TypeReference type:
                {
                    bw.Write((byte)ObjectType.Type);
                    WriteString1(bw, type.Clsid);
                    break;
                }

                case ClassJsonObject jsonObject:
                {
                    // fallback class, shouldn't be used but here just in case
                    // use WrappedSerializedObject if possible
                    bw.Write((byte)ObjectType.JsonObject);
                    WriteString1(bw, jsonObject.Type.AssemblyName);
                    WriteString1(bw, jsonObject.Type.ClassName);
                    WriteString4Unicode(bw, jsonObject.JsonText);
                    break;
                }

                case WrappedSerializedObject wso:
                {
                    var matchName = wso.Type.MatchName;
                    string jsonText;

                    switch (matchName)
                    {
                        case ABRO_TYPENAME:
                        {
                            var abro = (AssetBundleRequestOptions)wso.Object;
                            jsonText = abro.WriteJson();
                            break;
                        }
                        default:
                        {
                            throw new Exception($"Serialized type {wso.Type.AssemblyName}; {wso.Type.ClassName} not supported");
                        }
                    }

                    bw.Write((byte)ObjectType.JsonObject);
                    WriteString1(bw, wso.Type.AssemblyName);
                    WriteString1(bw, wso.Type.ClassName);
                    WriteString4Unicode(bw, jsonText);
                    break;
                }

                default:
                {
                    throw new Exception($"Type {ob.GetType().FullName} not supported");
                }
            }
        }

        private static string ReadString1(BinaryReader br)
        {
            int length = br.ReadByte();
            var str = Encoding.ASCII.GetString(br.ReadBytes(length));
            return str;
        }

        private static string ReadString4(BinaryReader br)
        {
            var length = br.ReadInt32();
            var str = Encoding.ASCII.GetString(br.ReadBytes(length));
            return str;
        }

        private static string ReadString4Unicode(BinaryReader br)
        {
            var length = br.ReadInt32();
            var str = Encoding.Unicode.GetString(br.ReadBytes(length));
            return str;
        }

        private static void WriteString1(BinaryWriter bw, string str)
        {
            if (str.Length > 255)
                throw new ArgumentException("String length cannot be greater than 255");

            var bytes = Encoding.ASCII.GetBytes(str);
            bw.Write((byte)bytes.Length);
            bw.Write(bytes);
        }

        private static void WriteString4(BinaryWriter bw, string str)
        {
            var bytes = Encoding.ASCII.GetBytes(str);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        private static void WriteString4Unicode(BinaryWriter bw, string str)
        {
            var bytes = Encoding.Unicode.GetBytes(str);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }
    }
}
