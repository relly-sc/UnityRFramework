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
