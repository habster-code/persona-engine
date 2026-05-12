using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PersonaEngine
{
    public static class ModelParser
    {
        public static bool TryGetContextLength(string filePath, out int contextLength)
        {
            contextLength = 0;
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
                using BinaryReader br = new(fs, Encoding.UTF8);
                if (br.ReadUInt32() != 0x46554747)
                {
                    return false;
                }

                br.ReadUInt32();
                br.ReadUInt64();
                ulong kvCount = br.ReadUInt64();

                for (ulong i = 0; i < kvCount; i++)
                {
                    string key = ReadString(br);
                    GGUFValueType valueType = (GGUFValueType)br.ReadUInt32();

                    if (key == "llama.context_length" ||
                        key == "context_length" ||
                        key.EndsWith(".context_length"))
                    {
                        if (valueType == GGUFValueType.UINT32)
                        {
                            contextLength = (int)br.ReadUInt32();
                            return true;
                        }
                        if (valueType == GGUFValueType.INT32)
                        {
                            contextLength = br.ReadInt32();
                            return true;
                        }
                        SkipValue(br, valueType);
                    }
                    else
                    {
                        SkipValue(br, valueType);
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryGetModelInfo(string filePath, out ModelInfo info)
        {
            info = new ModelInfo { hasMetadata = false };
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
                using BinaryReader reader = new(fs, Encoding.UTF8);
                if (reader.ReadUInt32() != 0x46554747)
                {
                    return false;
                }

                reader.ReadUInt32();
                reader.ReadUInt64();
                ulong kvCount = reader.ReadUInt64();

                var kvDict = new Dictionary<string, object>();

                for (ulong i = 0; i < kvCount; i++)
                {
                    string key = ReadString(reader);
                    GGUFValueType valType = (GGUFValueType)reader.ReadUInt32();
                    object value = ReadValue(reader, valType);
                    kvDict[key] = value;
                }

                info.architecture = GetString(kvDict, "general.architecture");
                string arch = info.architecture;
                info.contextLength = GetInt(kvDict, $"{arch}.context_length") ?? GetInt(kvDict, "context_length") ?? 0;
                info.embeddingLength = GetInt(kvDict, $"{arch}.embedding_length") ?? GetInt(kvDict, "embedding_length") ?? 0;
                info.blockCount = GetInt(kvDict, $"{arch}.block_count") ?? GetInt(kvDict, "block_count") ?? 0;
                info.headCount = GetInt(kvDict, $"{arch}.attention.head_count") ?? GetInt(kvDict, "head_count") ?? 0;
                info.headCountKV = GetInt(kvDict, $"{arch}.attention.head_count_kv") ?? GetInt(kvDict, "head_count_kv") ?? info.headCount;
                info.ropeDimension = GetInt(kvDict, $"{arch}.rope.dimension_count") ?? GetInt(kvDict, "rope.dimension_count") ?? 0;
                info.ropeScale = GetFloat(kvDict, $"{arch}.rope.scaling.factor") ?? 1.0f;
                info.tokenizerModel = GetString(kvDict, "tokenizer.ggml.model") ?? "unknown";
                info.hasMetadata = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            return dict.TryGetValue(key, out object val) ? val as string : null;
        }

        private static int? GetInt(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out object val))
            {
                if (val is int i)
                {
                    return i;
                }
                if (val is uint ui)
                {
                    return (int)ui;
                }
                if (val is long l)
                {
                    return (int)l;
                }
                if (val is ulong ul)
                {
                    return (int)ul;
                }
            }
            return null;
        }

        private static float? GetFloat(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out object val))
            {
                if (val is float f)
                {
                    return f;
                }
                if (val is double d)
                {
                    return (float)d;
                }
                if (val is int i)
                {
                    return i;
                }
            }
            return null;
        }

        private static object ReadValue(BinaryReader reader, GGUFValueType type)
        {
            switch (type)
            {
                case GGUFValueType.UINT8: return reader.ReadByte();
                case GGUFValueType.INT8: return reader.ReadSByte();
                case GGUFValueType.UINT16: return reader.ReadUInt16();
                case GGUFValueType.INT16: return reader.ReadInt16();
                case GGUFValueType.UINT32: return reader.ReadUInt32();
                case GGUFValueType.INT32: return reader.ReadInt32();
                case GGUFValueType.FLOAT32: return reader.ReadSingle();
                case GGUFValueType.BOOL: return reader.ReadBoolean();
                case GGUFValueType.STRING: return ReadString(reader);
                case GGUFValueType.UINT64: return reader.ReadUInt64();
                case GGUFValueType.INT64: return reader.ReadInt64();
                case GGUFValueType.FLOAT64: return reader.ReadDouble();
                case GGUFValueType.ARRAY:
                    {
                        var elemType = (GGUFValueType)reader.ReadUInt32();
                        ulong count = reader.ReadUInt64();
                        var arr = new object[count];
                        for (ulong i = 0; i < count; i++)
                        {
                            arr[i] = ReadValue(reader, elemType);
                        }
                        return arr;
                    }
                default:
                    reader.ReadBytes(8);
                    return null;
            }
        }

        private static void SkipValue(BinaryReader reader, GGUFValueType type)
        {
            switch (type)
            {
                case GGUFValueType.UINT8:
                case GGUFValueType.INT8:
                    reader.ReadByte();
                    break;
                case GGUFValueType.UINT16:
                case GGUFValueType.INT16:
                    reader.ReadUInt16();
                    break;
                case GGUFValueType.UINT32:
                case GGUFValueType.INT32:
                case GGUFValueType.FLOAT32:
                    reader.ReadBytes(4);
                    break;
                case GGUFValueType.UINT64:
                case GGUFValueType.INT64:
                case GGUFValueType.FLOAT64:
                    reader.ReadBytes(8);
                    break;
                case GGUFValueType.BOOL:
                    reader.ReadBoolean();
                    break;
                case GGUFValueType.STRING:
                    _ = ReadString(reader);
                    break;
                case GGUFValueType.ARRAY:
                    {
                        GGUFValueType elemType = (GGUFValueType)reader.ReadUInt32();
                        ulong count = reader.ReadUInt64();
                        for (ulong i = 0; i < count; i++)
                        {
                            SkipValue(reader, elemType);
                        }
                    }
                    break;
                default:
                    reader.ReadBytes(8);
                    break;
            }
        }

        private static string ReadString(BinaryReader reader)
        {
            ulong length = reader.ReadUInt64();
            if (length > int.MaxValue)
            {
                throw new IOException("Invalid string length in GGUF file.");
            }
            byte[] bytes = reader.ReadBytes((int)length);
            return Encoding.UTF8.GetString(bytes);
        }


        public struct ModelInfo
        {
            public string architecture;
            public int contextLength;
            public int embeddingLength;
            public int blockCount;
            public int headCount;
            public int headCountKV;
            public int ropeDimension;
            public float ropeScale;
            public string tokenizerModel;
            public bool hasMetadata;
        }

        private enum GGUFValueType : uint
        {
            UINT8 = 0,
            INT8 = 1,
            UINT16 = 2,
            INT16 = 3,
            UINT32 = 4,
            INT32 = 5,
            FLOAT32 = 6,
            BOOL = 7,
            STRING = 8,
            ARRAY = 9,
            UINT64 = 10,
            INT64 = 11,
            FLOAT64 = 12
        }
    }
}