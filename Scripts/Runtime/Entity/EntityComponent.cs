using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体组件。作为 EntityModule 的运行时包装层，绑定 Unity 生命周期，
    /// 转发所有实体操作到 EntityModule。
    /// Update/Shutdown 由 BaseComponent → RFrameworkModuleEntry 统一调度，
    /// 本组件不写 Update/OnDestroy。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Entity")]
    [DisallowMultipleComponent]
    public sealed class EntityComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 实体模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IEntityModule entityModule;

        /// <summary>
        /// 实体辅助器类型名称，通过 Inspector 配置。
        /// 默认指向不存在的类名，创建失败时输出 Error 日志，
        /// 用户需通过 Expansion 层提供实现或运行时 SetHelper 替换。
        /// </summary>
        [SerializeField] private string entityHelperTypeName = "UnityRFramework.Runtime.DefaultEntityHelper";

        /// <summary>
        /// 获取当前已加载的实体数量。
        /// </summary>
        public int EntityCount
        {
            get { return entityModule != null ? entityModule.EntityCount : 0; }
        }

        /// <summary>
        /// 获取当前实体组数量。
        /// </summary>
        public int EntityGroupCount
        {
            get { return entityModule != null ? entityModule.EntityGroupCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();

            entityModule = RFrameworkModuleEntry.GetModule<IEntityModule>();
            if (entityModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(IEntityModule));
                return;
            }

            // 注入依赖模块（通过接口调用，无需类型转换）
            IResourceModule resourceModule = RFrameworkModuleEntry.GetModule<IResourceModule>();
            IEventModule eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            IPoolModule poolModule = RFrameworkModuleEntry.GetModule<IPoolModule>();
            entityModule.SetDependencies(resourceModule, eventModule, poolModule);

            // 创建并注入实体辅助器
            EntityHelperBase entityHelper = Helper.CreateHelper<EntityHelperBase>(entityHelperTypeName, null);
            if (entityHelper != null)
            {
                entityModule.SetHelper(entityHelper);
                entityHelper.transform.SetParent(transform);
            }
        }

        /// <summary>
        /// 运行时替换实体辅助器。
        /// </summary>
        /// <param name="helper">新的实体辅助器实例。</param>
        public void SetHelper(IEntityHelper helper)
        {
            entityModule.SetHelper(helper);
        }

        /// <inheritdoc cref="IEntityModule.CreateEntityGroup"/>
        public IEntityGroup CreateEntityGroup(string name, float autoReleaseInterval, int capacity,
            float expireTime, int priority, IEntityGroupHelper groupHelper = null)
        {
            return entityModule.CreateEntityGroup(name, autoReleaseInterval, capacity, expireTime,
                priority, groupHelper);
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

        /// <inheritdoc cref="IEntityModule.GetAllEntityGroups"/>
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
        /// 将场景中已有的对象登记到实体模块。
        /// 场景实体参与实体组、查询、更新和父子附加，但模块不会回收或销毁对象。
        /// </summary>
        /// <param name="entityInstance">场景中的实体对象。</param>
        /// <param name="entityId">实体编号，全局唯一且不能为零。</param>
        /// <param name="entityName">实体逻辑名称。</param>
        /// <param name="groupName">目标实体组名称。</param>
        /// <param name="createGroupIfMissing">实体组不存在时是否创建无对象池配置的场景实体组。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>登记后的实体。</returns>
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
                    throw new RFrameworkException($"Entity group '{groupName}' is not exist.");
                }

                entityModule.CreateEntityGroup(groupName, 0f, 0, 0f, 0);
            }

            Entity entity = entityInstance.GetOrAddComponent<Entity>();
            return entityModule.RegisterEntity(entityId, entityName, groupName, entity, userData);
        }

        /// <summary>
        /// 从实体模块注销场景实体，不回收或销毁当前对象。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
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

        /// <inheritdoc cref="IEntityModule.GetEntity"/>
        public IEntity GetEntity(long entityId)
        {
            return entityModule.GetEntity(entityId);
        }

        /// <inheritdoc cref="IEntityModule.HasEntity"/>
        public bool HasEntity(long entityId)
        {
            return entityModule.HasEntity(entityId);
        }
    }
}
