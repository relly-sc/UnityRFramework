using RFramework.Entity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认实体辅助器实现。
    /// 使用纯 Unity API（Instantiate / Destroy）完成实体实例化和销毁。
    /// 资源加载由 IResourceModule 负责，此 Helper 只处理 Instantiate/Create/Release。
    /// </summary>
    public class DefaultEntityHelper : EntityHelperBase
    {
        /// <inheritdoc cref="IEntityHelper.InstantiateEntity"/>
        public override object InstantiateEntity(object entityAsset)
        {
            if (entityAsset == null)
            {
                Log.Error("Entity asset is invalid.");
                return null;
            }

            GameObject prefab = entityAsset as GameObject;
            if (prefab == null)
            {
                Log.Error("Entity asset '{0}' is not a GameObject.", entityAsset);
                return null;
            }

            return Object.Instantiate(prefab);
        }

        /// <inheritdoc cref="IEntityHelper.CreateEntity"/>
        public override IEntity CreateEntity(object entityInstance, IEntityGroup group, object userData)
        {
            if (entityInstance == null)
            {
                Log.Error("Entity instance is invalid.");
                return null;
            }

            GameObject go = entityInstance as GameObject;
            if (go == null)
            {
                Log.Error("Entity instance '{0}' is not a GameObject.", entityInstance);
                return null;
            }

            // 在 GameObject 上添加 Entity 包装器组件
            Entity entity = go.GetOrAddComponent<Entity>();
            return entity;
        }

        /// <inheritdoc cref="IEntityHelper.ReleaseEntity"/>
        public override void ReleaseEntity(object entityAsset, object entityInstance)
        {
            if (entityInstance != null)
            {
                GameObject go = entityInstance as GameObject;
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            // 资源释放由 IResourceModule.UnloadAsset 负责
            // 此处不调用 UnloadAsset，因为 EntityModule 内部会在合适时机统一释放
        }
    }
}
