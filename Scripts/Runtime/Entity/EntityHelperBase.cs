using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 可由 EntityComponent 动态创建的 Unity 实体 Helper 基类。
    /// </summary>
    public abstract class EntityHelperBase : MonoBehaviour, IEntityHelper
    {
        /// <inheritdoc/>
        public abstract object InstantiateEntity(object entityAsset);

        /// <inheritdoc/>
        public abstract IEntity CreateEntity(object entityInstance, IEntityGroup group, object userData);

        /// <inheritdoc/>
        public abstract void ReleaseEntity(object entityAsset, object entityInstance);
    }
}
