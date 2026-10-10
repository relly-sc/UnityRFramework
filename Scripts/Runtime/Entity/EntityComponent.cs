using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体模块的 Unity 入口，负责 Helper 创建和公开 API 转发。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Entity")]
    [DisallowMultipleComponent]
    public sealed class EntityComponent : UnityRFrameworkComponent
    {
        [SerializeField]
        [Tooltip("实体 Helper 类型。默认实现使用 Unity Instantiate/Destroy。")]
        private string entityHelperTypeName = "UnityRFramework.Runtime.DefaultEntityHelper";

        private IEntityModule entityModule;

        /// <inheritdoc cref="IEntityModule.EntityCount"/>
        public int EntityCount => entityModule?.EntityCount ?? 0;

        /// <inheritdoc cref="IEntityModule.LoadingEntityCount"/>
        public int LoadingEntityCount => entityModule?.LoadingEntityCount ?? 0;

        /// <inheritdoc cref="IEntityModule.EntityGroupCount"/>
        public int EntityGroupCount => entityModule?.EntityGroupCount ?? 0;

        protected override void Awake()
        {
            base.Awake();
            entityModule = RFrameworkModuleHost.Get<IEntityModule>();
            if (entityModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(IEntityModule));
                return;
            }

            entityModule.SetDependencies(
                RFrameworkModuleHost.Get<IResourceModule>(),
                RFrameworkModuleHost.Get<IEventModule>());

            EntityHelperBase entityHelper = ComponentFactory.Create<EntityHelperBase>(entityHelperTypeName, null);
            if (entityHelper == null)
            {
                Log.Error("Can not create entity helper '{0}'.", entityHelperTypeName);
                return;
            }

            entityHelper.transform.SetParent(transform, false);
            entityModule.SetHelper(entityHelper);
        }

        /// <inheritdoc cref="IEntityModule.SetHelper"/>
        public void SetHelper(IEntityHelper helper)
        {
            entityModule.SetHelper(helper);
        }

        /// <inheritdoc cref="IEntityModule.CreateEntityGroup"/>
        public IEntityGroup CreateEntityGroup(string name, float autoReleaseInterval, int capacity,
            float expireTime)
        {
            return entityModule.CreateEntityGroup(name, autoReleaseInterval, capacity, expireTime);
        }

        /// <inheritdoc cref="IEntityModule.DestroyEntityGroup"/>
        public bool DestroyEntityGroup(string name)
        {
            return entityModule.DestroyEntityGroup(name);
        }

        /// <inheritdoc cref="IEntityModule.HasEntityGroup"/>
        public bool HasEntityGroup(string name)
        {
            return entityModule.HasEntityGroup(name);
        }

        /// <inheritdoc cref="IEntityModule.GetEntityGroup"/>
        public IEntityGroup GetEntityGroup(string name)
        {
            return entityModule.GetEntityGroup(name);
        }

        /// <inheritdoc cref="IEntityModule.GetAllEntityGroups()"/>
        public IEntityGroup[] GetAllEntityGroups()
        {
            return entityModule.GetAllEntityGroups();
        }

        /// <inheritdoc cref="IEntityModule.GetAllEntityGroups(List{IEntityGroup})"/>
        public void GetAllEntityGroups(List<IEntityGroup> results)
        {
            entityModule.GetAllEntityGroups(results);
        }

        /// <inheritdoc cref="IEntityModule.ShowEntityAsync"/>
        public Task<IEntity> ShowEntityAsync(long entityId, string assetName, string groupName,
            uint priority = 0, object userData = null, CancellationToken ct = default)
        {
            return entityModule.ShowEntityAsync(entityId, assetName, groupName, priority, userData, ct);
        }

        /// <summary>
        /// 将场景内已有对象登记为外部实体；模块不会销毁该对象。
        /// </summary>
        /// <param name="entityInstance">场景实体对象。</param>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityName">实体名称或资源标识。</param>
        /// <param name="groupName">目标实体组名称。</param>
        /// <param name="createGroupIfMissing">实体组不存在时是否自动创建。</param>
        /// <param name="userData">业务自定义数据。</param>
        /// <returns>完成登记和显示的实体。</returns>
        public IEntity RegisterSceneEntity(GameObject entityInstance, long entityId, string entityName,
            string groupName, bool createGroupIfMissing = false, object userData = null)
        {
            if (entityInstance == null)
            {
                throw new RFrameworkException("Scene entity instance is invalid.");
            }

            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new RFrameworkException("Scene entity name is invalid.");
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new RFrameworkException("Scene entity group name is invalid.");
            }

            if (!entityModule.HasEntityGroup(groupName))
            {
                if (!createGroupIfMissing)
                {
                    throw new RFrameworkException($"Entity group '{groupName}' does not exist.");
                }

                entityModule.CreateEntityGroup(groupName, 0f, 0, 0f);
            }

            Entity entity = entityInstance.GetOrAddComponent<Entity>();
            return entityModule.RegisterEntity(entityId, entityName, groupName, entity, userData);
        }

        /// <inheritdoc cref="IEntityModule.UnregisterEntity"/>
        public void UnregisterSceneEntity(long entityId, object userData = null)
        {
            entityModule.UnregisterEntity(entityId, userData);
        }

        /// <inheritdoc cref="IEntityModule.HideEntity"/>
        public void HideEntity(long entityId, object userData = null)
        {
            entityModule.HideEntity(entityId, userData);
        }

        /// <inheritdoc cref="IEntityModule.HideAllLoadedEntities"/>
        public void HideAllLoadedEntities(object userData = null)
        {
            entityModule.HideAllLoadedEntities(userData);
        }

        /// <inheritdoc cref="IEntityModule.HideAllLoadingEntities"/>
        public void HideAllLoadingEntities()
        {
            entityModule.HideAllLoadingEntities();
        }

        /// <inheritdoc cref="IEntityModule.AttachEntity"/>
        public void AttachEntity(long childEntityId, long parentEntityId, object userData = null)
        {
            entityModule.AttachEntity(childEntityId, parentEntityId, userData);
        }

        /// <inheritdoc cref="IEntityModule.DetachEntity"/>
        public void DetachEntity(long childEntityId, object userData = null)
        {
            entityModule.DetachEntity(childEntityId, userData);
        }

        /// <inheritdoc cref="IEntityModule.DetachChildEntities"/>
        public void DetachChildEntities(long parentEntityId, object userData = null)
        {
            entityModule.DetachChildEntities(parentEntityId, userData);
        }

        /// <inheritdoc cref="IEntityModule.HasEntity"/>
        public bool HasEntity(long entityId)
        {
            return entityModule.HasEntity(entityId);
        }

        /// <inheritdoc cref="IEntityModule.GetEntity"/>
        public IEntity GetEntity(long entityId)
        {
            return entityModule.GetEntity(entityId);
        }

        /// <inheritdoc cref="IEntityModule.GetAllLoadedEntities()"/>
        public IEntity[] GetAllLoadedEntities()
        {
            return entityModule.GetAllLoadedEntities();
        }

        /// <inheritdoc cref="IEntityModule.GetAllLoadedEntities(List{IEntity})"/>
        public void GetAllLoadedEntities(List<IEntity> results)
        {
            entityModule.GetAllLoadedEntities(results);
        }

        /// <inheritdoc cref="IEntityModule.GetAllLoadingEntityIds"/>
        public long[] GetAllLoadingEntityIds()
        {
            return entityModule.GetAllLoadingEntityIds();
        }

        /// <inheritdoc cref="IEntityModule.IsLoadingEntity"/>
        public bool IsLoadingEntity(long entityId)
        {
            return entityModule.IsLoadingEntity(entityId);
        }

        /// <inheritdoc cref="IEntityModule.IsValidEntity"/>
        public bool IsValidEntity(IEntity entity)
        {
            return entityModule.IsValidEntity(entity);
        }

        /// <inheritdoc cref="IEntityModule.GetParentEntity"/>
        public IEntity GetParentEntity(long childEntityId)
        {
            return entityModule.GetParentEntity(childEntityId);
        }

        /// <inheritdoc cref="IEntityModule.GetChildEntityCount"/>
        public int GetChildEntityCount(long parentEntityId)
        {
            return entityModule.GetChildEntityCount(parentEntityId);
        }

        /// <inheritdoc cref="IEntityModule.GetChildEntities"/>
        public IReadOnlyList<IEntity> GetChildEntities(long parentEntityId)
        {
            return entityModule.GetChildEntities(parentEntityId);
        }
    }
}
