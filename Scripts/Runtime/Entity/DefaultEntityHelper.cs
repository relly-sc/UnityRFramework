using RFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 基于 Unity Instantiate/Destroy 的默认实体 Helper。
    /// </summary>
    public sealed class DefaultEntityHelper : EntityHelperBase
    {
        /// <inheritdoc/>
        public override object InstantiateEntity(object entityAsset)
        {
            if (!(entityAsset is GameObject prefab))
            {
                throw new RFrameworkException("Entity asset is not a GameObject.");
            }

            return Object.Instantiate(prefab);
        }

        /// <inheritdoc/>
        public override IEntity CreateEntity(object entityInstance, IEntityGroup group, object userData)
        {
            if (!(entityInstance is GameObject instance))
            {
                throw new RFrameworkException("Entity instance is not a GameObject.");
            }

            return instance.GetOrAddComponent<Entity>();
        }

        /// <inheritdoc/>
        public override void ReleaseEntity(object entityAsset, object entityInstance)
        {
            if (entityInstance is GameObject instance)
            {
                Object.Destroy(instance);
            }
        }
    }
}
