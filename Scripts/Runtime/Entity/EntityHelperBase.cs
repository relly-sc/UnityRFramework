using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体辅助器基类。
    /// 继承自 MonoBehaviour 并实现 IEntityHelper 接口，
    /// 使 Runtime 层实体辅助器可以通过 Helper.CreateHelper 统一创建。
    /// </summary>
    public abstract class EntityHelperBase : MonoBehaviour, IEntityHelper
    {
        /// <inheritdoc cref="IEntityHelper.InstantiateEntity"/>
        public abstract object InstantiateEntity(object entityAsset);

        /// <inheritdoc cref="IEntityHelper.CreateEntity"/>
        public abstract IEntity CreateEntity(object entityInstance, IEntityGroup group, object userData);

        /// <inheritdoc cref="IEntityHelper.ReleaseEntity"/>
        public abstract void ReleaseEntity(object entityAsset, object entityInstance);
    }
}
