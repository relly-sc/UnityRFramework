using System;
using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// URFC v2 历史 Schema 迁移器注册表。
    /// </summary>
    public static class BinaryConfigMigrationRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, Dictionary<ulong, IBinaryConfigMigration>>
            Migrations = new Dictionary<Type, Dictionary<ulong, IBinaryConfigMigration>>();

        /// <summary>
        /// 注册或替换一个配置行类型的历史 Schema 迁移器。
        /// </summary>
        /// <param name="migration">历史 Schema 迁移器。</param>
        public static void Register(IBinaryConfigMigration migration)
        {
            if (migration == null || migration.RowType == null)
            {
                throw new RFrameworkException(
                    "Binary config migration or row type is invalid.");
            }

            if (migration.SourceSchemaHash == 0 || migration.TargetSchemaHash == 0)
            {
                throw new RFrameworkException(
                    "Binary config migration SchemaHash must be greater than zero.");
            }

            if (migration.SourceSchemaHash == migration.TargetSchemaHash)
            {
                throw new RFrameworkException(
                    "Binary config migration source and target SchemaHash must differ.");
            }

            lock (SyncRoot)
            {
                if (!Migrations.TryGetValue(
                    migration.RowType, out Dictionary<ulong, IBinaryConfigMigration> rowMigrations))
                {
                    rowMigrations = new Dictionary<ulong, IBinaryConfigMigration>();
                    Migrations.Add(migration.RowType, rowMigrations);
                }

                rowMigrations[migration.SourceSchemaHash] = migration;
            }
        }

        /// <summary>
        /// 获取指定配置行类型和历史 SchemaHash 的迁移器。
        /// </summary>
        public static bool TryGet(
            Type rowType, ulong sourceSchemaHash, out IBinaryConfigMigration migration)
        {
            if (rowType == null || sourceSchemaHash == 0)
            {
                migration = null;
                return false;
            }

            lock (SyncRoot)
            {
                if (Migrations.TryGetValue(
                    rowType, out Dictionary<ulong, IBinaryConfigMigration> rowMigrations))
                {
                    return rowMigrations.TryGetValue(sourceSchemaHash, out migration);
                }

                migration = null;
                return false;
            }
        }

        /// <summary>
        /// 移除指定配置行类型和历史 SchemaHash 的迁移器。
        /// </summary>
        public static void Unregister(Type rowType, ulong sourceSchemaHash)
        {
            if (rowType == null || sourceSchemaHash == 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!Migrations.TryGetValue(
                    rowType, out Dictionary<ulong, IBinaryConfigMigration> rowMigrations))
                {
                    return;
                }

                rowMigrations.Remove(sourceSchemaHash);
                if (rowMigrations.Count == 0)
                {
                    Migrations.Remove(rowType);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (SyncRoot)
            {
                Migrations.Clear();
            }
        }
    }
}
