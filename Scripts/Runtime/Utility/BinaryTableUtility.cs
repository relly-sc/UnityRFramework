using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UnityRFramework 单表二进制协议读取器。
    /// 所有整数均为 little-endian，字符串为 Int32 字节长度 + UTF-8 数据。
    /// Config 支持 URFC v1 反射映射和 URFC v2 生成 Codec 两种格式。
    /// Localization 使用 URFL v2 Key/Value + CRC32 格式，并兼容读取 v1。
    /// </summary>
    internal static class BinaryTableUtility
    {
        private const int MaxRows = 1_000_000;
        private const int MaxColumns = 1024;
        private const int MaxStringBytes = 16 * 1024 * 1024;

        private static readonly byte[] ConfigMagic = Encoding.ASCII.GetBytes("URFC");
        private static readonly byte[] ConfigBundleMagic = Encoding.ASCII.GetBytes("URFM");
        private static readonly byte[] LocalizationMagic = Encoding.ASCII.GetBytes("URFL");

        /// <summary>
        /// 读取并验证 URFC 文件头，返回协议版本。
        /// </summary>
        /// <param name="bytes">URFC 文件字节。</param>
        /// <returns>协议版本。</returns>
        public static ushort GetConfigVersion(byte[] bytes)
        {
            if (bytes == null || bytes.Length < ConfigMagic.Length + sizeof(ushort))
            {
                throw new RFrameworkException("Binary config data is empty or truncated.");
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
            {
                ReadAndValidateMagic(reader, ConfigMagic, "config");
                return reader.ReadUInt16();
            }
        }

        /// <summary>
        /// 读取 URFM v1 多表容器，并按配置行类型合并所有分片。
        /// </summary>
        public static IReadOnlyDictionary<Type, object> ReadGeneratedConfigBundle(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException("Binary config bundle data is empty.");
            }

            Dictionary<Type, object> result = new Dictionary<Type, object>();
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    ReadAndValidateHeader(
                        reader, ConfigBundleMagic, BinaryFormatUtility.ConfigBundleVersion,
                        "config bundle");
                    int segmentCount = ReadBoundedCount(reader, MaxRows, "config segment");
                    if (segmentCount == 0)
                    {
                        throw new RFrameworkException(
                            "Binary config bundle must contain at least one segment.");
                    }

                    HashSet<string> segmentNames =
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < segmentCount; i++)
                    {
                        string segmentName = ReadUtf8String(reader, false);
                        if (string.IsNullOrWhiteSpace(segmentName)
                            || !segmentNames.Add(segmentName))
                        {
                            throw new RFrameworkException(
                                $"Binary config bundle segment {i} has an empty or duplicate name "
                                + $"'{segmentName}'.");
                        }

                        uint tableId = reader.ReadUInt32();
                        ulong schemaHash = reader.ReadUInt64();
                        int rowCount = ReadBoundedCount(reader, MaxRows, "config row");
                        int bodyLength = reader.ReadInt32();
                        uint checksum = reader.ReadUInt32();
                        if (bodyLength < 0 || bodyLength > stream.Length - stream.Position)
                        {
                            throw new RFrameworkException(
                                $"Binary config bundle segment '{segmentName}' body length is invalid.");
                        }

                        byte[] body = reader.ReadBytes(bodyLength);
                        if (body.Length != bodyLength)
                        {
                            throw new EndOfStreamException();
                        }

                        if (BinaryFormatUtility.ComputeCrc32(body) != checksum)
                        {
                            throw new RFrameworkException(
                                $"Binary config bundle segment '{segmentName}' checksum mismatch.");
                        }

                        if (!ConfigSchemaRegistry.TryGet(tableId, out ConfigSchemaInfo schema))
                        {
                            throw new RFrameworkException(
                                $"No Config Schema is registered for TableId '{tableId:X8}'.");
                        }

                        object segmentTable = ReadGeneratedConfigTable(
                            schema.RowType,
                            BuildGeneratedConfigBytes(
                                tableId, schemaHash, rowCount, checksum, body));
                        MergeConfigSegment(result, schema.RowType, segmentName, segmentTable);
                    }

                    EnsureFullyConsumed(stream, "config bundle");
                }

                return result;
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (Exception ex) when (ex is EndOfStreamException
                || ex is IOException
                || ex is ArgumentException
                || ex is OverflowException)
            {
                throw new RFrameworkException(
                    "Binary config bundle is truncated or malformed.", ex);
            }
        }

        private static byte[] BuildGeneratedConfigBytes(
            uint tableId, ulong schemaHash, int rowCount, uint checksum, byte[] body)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(ConfigMagic);
                writer.Write(BinaryFormatUtility.ConfigGeneratedVersion);
                writer.Write(tableId);
                writer.Write(schemaHash);
                writer.Write(rowCount);
                writer.Write(body.Length);
                writer.Write(checksum);
                writer.Write(body);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void MergeConfigSegment(
            IDictionary<Type, object> result,
            Type rowType,
            string segmentName,
            object segmentTable)
        {
            if (!(segmentTable is IDictionary source))
            {
                throw new RFrameworkException(
                    $"Config segment '{segmentName}' did not produce a dictionary table.");
            }

            if (!result.TryGetValue(rowType, out object destinationObject))
            {
                result.Add(rowType, segmentTable);
                return;
            }

            if (!(destinationObject is IDictionary destination))
            {
                throw new RFrameworkException(
                    $"Merged config table '{rowType.Name}' is not a dictionary.");
            }

            foreach (DictionaryEntry entry in source)
            {
                if (destination.Contains(entry.Key))
                {
                    throw new RFrameworkException(
                        $"Config row type '{rowType.Name}' contains duplicate Id "
                        + $"'{entry.Key}' across segments, including '{segmentName}'.");
                }

                destination.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// 使用已注册 Codec 读取 URFC v2 配置表。
        /// </summary>
        /// <param name="rowType">配置行类型。</param>
        /// <param name="bytes">URFC v2 文件字节。</param>
        /// <returns>Codec 创建的强类型表对象。</returns>
        public static object ReadGeneratedConfigTable(Type rowType, byte[] bytes)
        {
            if (rowType == null)
            {
                throw new RFrameworkException("Binary config row type is invalid.");
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException("Binary config data is empty.");
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    ReadAndValidateHeader(
                        reader, ConfigMagic, BinaryFormatUtility.ConfigGeneratedVersion, "config");

                    uint tableId = reader.ReadUInt32();
                    ulong schemaHash = reader.ReadUInt64();
                    int rowCount = ReadBoundedCount(reader, MaxRows, "config row");
                    int bodyLength = reader.ReadInt32();
                    uint expectedChecksum = reader.ReadUInt32();
                    long remaining = stream.Length - stream.Position;
                    if (bodyLength < 0 || bodyLength != remaining)
                    {
                        throw new RFrameworkException(
                            $"Binary config body length '{bodyLength}' does not match remaining bytes '{remaining}'.");
                    }

                    byte[] body = reader.ReadBytes(bodyLength);
                    if (body.Length != bodyLength)
                    {
                        throw new EndOfStreamException();
                    }

                    uint actualChecksum = BinaryFormatUtility.ComputeCrc32(body);
                    if (actualChecksum != expectedChecksum)
                    {
                        throw new RFrameworkException(
                            "Binary config checksum mismatch. "
                            + $"Expected '{expectedChecksum:X8}', actual '{actualChecksum:X8}'.");
                    }

                    if (!BinaryConfigCodecRegistry.TryGet(rowType, out IBinaryConfigCodec codec))
                    {
                        throw new RFrameworkException(
                            $"No URFC v2 codec is registered for '{rowType.FullName}'. "
                            + "Generate the config codec before loading this table.");
                    }

                    if (codec.TableId != tableId)
                    {
                        throw new RFrameworkException(
                            $"Binary config table id mismatch for '{rowType.Name}'. "
                            + $"File '{tableId:X8}', codec '{codec.TableId:X8}'.");
                    }

                    using (MemoryStream bodyStream = new MemoryStream(body, false))
                    using (BinaryReader bodyReader = new BinaryReader(bodyStream, Encoding.UTF8, false))
                    {
                        object table;
                        if (codec.SchemaHash == schemaHash)
                        {
                            table = codec.ReadTable(bodyReader, rowCount);
                        }
                        else
                        {
                            table = ReadAndMigrateTable(
                                rowType, schemaHash, codec, bodyReader, rowCount);
                        }

                        if (table == null)
                        {
                            throw new RFrameworkException(
                                $"Binary config codec for '{rowType.Name}' returned null.");
                        }

                        EnsureFullyConsumed(bodyStream, "config body");
                        return table;
                    }
                }
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (Exception ex) when (ex is EndOfStreamException
                || ex is IOException
                || ex is ArgumentException
                || ex is OverflowException)
            {
                throw new RFrameworkException(
                    $"Generated binary config '{rowType.Name}' is truncated or malformed.", ex);
            }
        }

        private static object ReadAndMigrateTable(
            Type rowType,
            ulong sourceSchemaHash,
            IBinaryConfigCodec currentCodec,
            BinaryReader reader,
            int rowCount)
        {
            if (!BinaryConfigMigrationRegistry.TryGet(
                rowType, sourceSchemaHash, out IBinaryConfigMigration migration))
            {
                throw new RFrameworkException(
                    $"Binary config schema mismatch for '{rowType.Name}'. "
                    + $"File '{sourceSchemaHash:X16}', codec "
                    + $"'{currentCodec.SchemaHash:X16}', and no migration is registered.");
            }

            if (migration.TargetSchemaHash != currentCodec.SchemaHash)
            {
                throw new RFrameworkException(
                    $"Binary config migration target mismatch for '{rowType.Name}'. "
                    + $"Migration '{migration.TargetSchemaHash:X16}', codec "
                    + $"'{currentCodec.SchemaHash:X16}'.");
            }

            return migration.ReadAndMigrate(reader, rowCount);
        }

        /// <summary>
        /// 使用反射字段映射读取 URFC v1 配置行。
        /// </summary>
        /// <param name="rowType">配置行类型。</param>
        /// <param name="bytes">URFC v1 文件字节。</param>
        /// <returns>配置行对象集合。</returns>
        public static List<object> ReadConfigRows(Type rowType, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException("Binary config data is empty.");
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    ReadAndValidateHeader(
                        reader, ConfigMagic, BinaryFormatUtility.ConfigReflectionVersion, "config");
                    int rowCount = ReadBoundedCount(reader, MaxRows, "config row");
                    int columnCount = ReadBoundedCount(reader, MaxColumns, "config column");
                    if (columnCount == 0)
                    {
                        throw new RFrameworkException("Binary config must contain at least one column.");
                    }

                    Dictionary<string, MemberAccessor> members = GetWritableMembers(rowType);
                    MemberAccessor[] columns = new MemberAccessor[columnCount];
                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < columnCount; i++)
                    {
                        string name = ReadUtf8String(reader, false);
                        if (string.IsNullOrEmpty(name) || !names.Add(name))
                        {
                            throw new RFrameworkException(
                                $"Binary config '{rowType.Name}' contains an empty or duplicate column '{name}'.");
                        }

                        if (!members.TryGetValue(name, out MemberAccessor accessor))
                        {
                            throw new RFrameworkException(
                                $"Binary config column '{name}' has no matching public writable member "
                                + $"on '{rowType.Name}'.");
                        }

                        EnsureSupportedType(accessor.ValueType, rowType, accessor.Name);
                        columns[i] = accessor;
                    }

                    List<object> rows = new List<object>(rowCount);
                    for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                    {
                        object row;
                        try
                        {
                            row = Activator.CreateInstance(rowType);
                        }
                        catch (Exception ex)
                        {
                            throw new RFrameworkException(
                                $"Binary config row type '{rowType.Name}' must have a public "
                                + "parameterless constructor.", ex);
                        }

                        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                        {
                            MemberAccessor accessor = columns[columnIndex];
                            object value = ReadValue(reader, accessor.ValueType);
                            try
                            {
                                accessor.SetValue(row, value);
                            }
                            catch (Exception ex)
                            {
                                throw new RFrameworkException(
                                    $"Binary config failed to assign row {rowIndex}, member '{accessor.Name}'.", ex);
                            }
                        }

                        rows.Add(row);
                    }

                    EnsureFullyConsumed(stream, "config");
                    return rows;
                }
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (Exception ex) when (ex is EndOfStreamException
                || ex is IOException
                || ex is ArgumentException
                || ex is OverflowException)
            {
                throw new RFrameworkException(
                    $"Binary config '{rowType.Name}' is truncated or malformed.", ex);
            }
        }

        /// <summary>
        /// 读取 URFL v1/v2 本地化键值表。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <param name="bytes">URFL 文件字节。</param>
        /// <returns>本地化键值字典。</returns>
        public static Dictionary<string, string> ReadLocalization(string language, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException("Binary localization language code is invalid.");
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException(
                    $"Binary localization data for '{language}' is empty.");
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    ReadAndValidateMagic(reader, LocalizationMagic, "localization");
                    ushort version = reader.ReadUInt16();
                    int entryCount = ReadBoundedCount(reader, MaxRows, "localization entry");
                    if (version == BinaryFormatUtility.LocalizationLegacyVersion)
                    {
                        Dictionary<string, string> legacy = ReadLocalizationEntries(
                            language, reader, entryCount);
                        EnsureFullyConsumed(stream, "localization");
                        return legacy;
                    }

                    if (version != BinaryFormatUtility.LocalizationVersion)
                    {
                        throw new RFrameworkException(
                            $"Binary localization version '{version}' is not supported. "
                            + $"Expected '{BinaryFormatUtility.LocalizationLegacyVersion}' or "
                            + $"'{BinaryFormatUtility.LocalizationVersion}'.");
                    }

                    int bodyLength = reader.ReadInt32();
                    uint expectedChecksum = reader.ReadUInt32();
                    long remaining = stream.Length - stream.Position;
                    if (bodyLength < 0 || bodyLength != remaining)
                    {
                        throw new RFrameworkException(
                            $"Binary localization body length '{bodyLength}' does not match "
                            + $"remaining bytes '{remaining}'.");
                    }

                    byte[] body = reader.ReadBytes(bodyLength);
                    if (body.Length != bodyLength)
                    {
                        throw new EndOfStreamException();
                    }

                    uint actualChecksum = BinaryFormatUtility.ComputeCrc32(body);
                    if (actualChecksum != expectedChecksum)
                    {
                        throw new RFrameworkException(
                            "Binary localization checksum mismatch. "
                            + $"Expected '{expectedChecksum:X8}', actual '{actualChecksum:X8}'.");
                    }

                    using (MemoryStream bodyStream = new MemoryStream(body, false))
                    using (BinaryReader bodyReader = new BinaryReader(
                        bodyStream, Encoding.UTF8, false))
                    {
                        Dictionary<string, string> result = ReadLocalizationEntries(
                            language, bodyReader, entryCount);
                        EnsureFullyConsumed(bodyStream, "localization body");
                        return result;
                    }
                }
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (Exception ex) when (ex is EndOfStreamException
                || ex is IOException
                || ex is ArgumentException
                || ex is OverflowException)
            {
                throw new RFrameworkException(
                    $"Binary localization '{language}' is truncated or malformed.", ex);
            }
        }

        private static Dictionary<string, string> ReadLocalizationEntries(
            string language, BinaryReader reader, int entryCount)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                string key = ReadUtf8String(reader, false);
                string value = ReadUtf8String(reader, false);
                if (string.IsNullOrEmpty(key))
                {
                    throw new RFrameworkException(
                        $"Binary localization '{language}' contains an empty key at index {i}.");
                }

                if (result.ContainsKey(key))
                {
                    throw new RFrameworkException(
                        $"Binary localization '{language}' contains duplicate key '{key}'.");
                }

                result.Add(key, value);
            }

            return result;
        }

        private static void ReadAndValidateHeader(
            BinaryReader reader, byte[] expectedMagic, ushort expectedVersion, string dataName)
        {
            ReadAndValidateMagic(reader, expectedMagic, dataName);

            ushort version = reader.ReadUInt16();
            if (version != expectedVersion)
            {
                throw new RFrameworkException(
                    $"Binary {dataName} version '{version}' is not supported. Expected '{expectedVersion}'.");
            }
        }

        private static void ReadAndValidateMagic(
            BinaryReader reader, byte[] expectedMagic, string dataName)
        {
            byte[] actualMagic = reader.ReadBytes(expectedMagic.Length);
            if (actualMagic.Length != expectedMagic.Length)
            {
                throw new EndOfStreamException();
            }

            for (int i = 0; i < expectedMagic.Length; i++)
            {
                if (actualMagic[i] != expectedMagic[i])
                {
                    throw new RFrameworkException(
                        $"Binary {dataName} magic is invalid.");
                }
            }

        }

        private static int ReadBoundedCount(BinaryReader reader, int maximum, string name)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new RFrameworkException(
                    $"Binary {name} count '{count}' is outside the supported range 0..{maximum}.");
            }

            return count;
        }

        private static string ReadUtf8String(BinaryReader reader, bool allowNull)
        {
            return BinaryFormatUtility.ReadUtf8String(reader, allowNull);
        }

        private static object ReadValue(BinaryReader reader, Type valueType)
        {
            Type nullableType = Nullable.GetUnderlyingType(valueType);
            bool canBeNull = nullableType != null || !valueType.IsValueType;
            if (canBeNull)
            {
                byte marker = reader.ReadByte();
                if (marker == 0)
                {
                    return null;
                }

                if (marker != 1)
                {
                    throw new RFrameworkException(
                        $"Binary nullable marker '{marker}' is invalid. Expected 0 or 1.");
                }
            }

            Type type = nullableType ?? valueType;
            if (type.IsEnum)
            {
                return Enum.ToObject(type, reader.ReadInt64());
            }

            if (type == typeof(bool))
            {
                byte value = reader.ReadByte();
                if (value > 1)
                {
                    throw new RFrameworkException($"Binary Boolean value '{value}' is invalid.");
                }

                return value == 1;
            }

            if (type == typeof(byte))
            {
                return reader.ReadByte();
            }

            if (type == typeof(sbyte))
            {
                return reader.ReadSByte();
            }

            if (type == typeof(short))
            {
                return reader.ReadInt16();
            }

            if (type == typeof(ushort))
            {
                return reader.ReadUInt16();
            }

            if (type == typeof(int))
            {
                return reader.ReadInt32();
            }

            if (type == typeof(uint))
            {
                return reader.ReadUInt32();
            }

            if (type == typeof(long))
            {
                return reader.ReadInt64();
            }

            if (type == typeof(ulong))
            {
                return reader.ReadUInt64();
            }

            if (type == typeof(float))
            {
                return reader.ReadSingle();
            }

            if (type == typeof(double))
            {
                return reader.ReadDouble();
            }

            if (type == typeof(decimal))
            {
                return reader.ReadDecimal();
            }

            if (type == typeof(char))
            {
                return (char)reader.ReadUInt16();
            }

            if (type == typeof(string))
            {
                return ReadUtf8String(reader, false);
            }

            if (type == typeof(DateTime))
            {
                long ticks = reader.ReadInt64();
                byte kind = reader.ReadByte();
                if (kind > (byte)DateTimeKind.Local)
                {
                    throw new RFrameworkException($"Binary DateTimeKind '{kind}' is invalid.");
                }

                return new DateTime(ticks, (DateTimeKind)kind);
            }

            if (type == typeof(TimeSpan))
            {
                return new TimeSpan(reader.ReadInt64());
            }

            if (type == typeof(Guid))
            {
                byte[] guid = reader.ReadBytes(16);
                if (guid.Length != 16)
                {
                    throw new EndOfStreamException();
                }

                return new Guid(guid);
            }

            if (type == typeof(byte[]))
            {
                int length = ReadBoundedCount(reader, MaxStringBytes, "byte array");
                byte[] data = reader.ReadBytes(length);
                if (data.Length != length)
                {
                    throw new EndOfStreamException();
                }

                return data;
            }

            throw new RFrameworkException($"Binary value type '{type.FullName}' is not supported.");
        }

        private static void EnsureSupportedType(Type valueType, Type rowType, string memberName)
        {
            Type type = Nullable.GetUnderlyingType(valueType) ?? valueType;
            bool supported = type.IsEnum
                || type == typeof(bool)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(char)
                || type == typeof(string)
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(Guid)
                || type == typeof(byte[]);

            if (!supported)
            {
                throw new RFrameworkException(
                    $"Binary config member '{rowType.Name}.{memberName}' uses unsupported type '{valueType.FullName}'. "
                    + "Use a custom ConfigHelperBase implementation for complex values.");
            }
        }

        private static Dictionary<string, MemberAccessor> GetWritableMembers(Type rowType)
        {
            Dictionary<string, MemberAccessor> result =
                new Dictionary<string, MemberAccessor>(StringComparer.OrdinalIgnoreCase);

            FieldInfo[] fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    result[field.Name] = new MemberAccessor(field);
                }
            }

            PropertyInfo[] properties = rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetSetMethod() != null && property.GetIndexParameters().Length == 0)
                {
                    result[property.Name] = new MemberAccessor(property);
                }
            }

            return result;
        }

        private static void EnsureFullyConsumed(Stream stream, string dataName)
        {
            if (stream.Position != stream.Length)
            {
                throw new RFrameworkException(
                    $"Binary {dataName} contains {stream.Length - stream.Position} unexpected trailing bytes. "
                    + "The current Runtime protocol supports exactly one table per file.");
            }
        }

        private sealed class MemberAccessor
        {
            private readonly FieldInfo field;
            private readonly PropertyInfo property;

            /// <summary>
            /// 初始化字段访问器。
            /// </summary>
            /// <param name="field">公开可写字段。</param>
            public MemberAccessor(FieldInfo field)
            {
                this.field = field;
                Name = field.Name;
                ValueType = field.FieldType;
            }

            /// <summary>
            /// 初始化属性访问器。
            /// </summary>
            /// <param name="property">公开可写属性。</param>
            public MemberAccessor(PropertyInfo property)
            {
                this.property = property;
                Name = property.Name;
                ValueType = property.PropertyType;
            }

            /// <summary>获取成员名称。</summary>
            public string Name { get; }

            /// <summary>获取成员值类型。</summary>
            public Type ValueType { get; }

            /// <summary>
            /// 为目标实例设置成员值。
            /// </summary>
            /// <param name="target">目标配置行。</param>
            /// <param name="value">待设置的值。</param>
            public void SetValue(object target, object value)
            {
                if (field != null)
                {
                    field.SetValue(target, value);
                }
                else
                {
                    property.SetValue(target, value);
                }
            }
        }
    }
}
