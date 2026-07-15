using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 场景实体绑定组件。将场景中预先放置的对象登记到实体模块，并在场景销毁时自动注销。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Scene Entity Binder")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Entity))]
    public sealed class SceneEntityBinder : MonoBehaviour
    {
        private const string DefaultGroupName = "Scene";

        /// <summary>
        /// 实体编号。为零时使用当前 GameObject 的运行时 InstanceID。
        /// </summary>
        [SerializeField]
        [Tooltip("实体编号。为零时使用当前 GameObject 的运行时 InstanceID。")]
        private long entityId;

        /// <summary>
        /// 实体逻辑名称。留空时使用当前 GameObject 名称。
        /// </summary>
        [SerializeField]
        [Tooltip("实体逻辑名称。留空时使用当前 GameObject 名称。")]
        private string entityName = string.Empty;

        /// <summary>
        /// 目标实体组名称。
        /// </summary>
        [SerializeField]
        [Tooltip("目标实体组名称。建议场景实体使用独立的 Scene 组。")]
        private string groupName = DefaultGroupName;

        /// <summary>
        /// 实体组不存在时是否自动创建无对象池配置的场景实体组。
        /// </summary>
        [SerializeField]
        [Tooltip("实体组不存在时，自动创建无对象池配置的场景实体组。")]
        private bool createGroupIfMissing = true;

        /// <summary>
        /// 是否在 Start 阶段自动登记。
        /// </summary>
        [SerializeField]
        [Tooltip("是否在 Start 阶段自动登记到实体模块。")]
        private bool registerOnStart = true;

        /// <summary>
        /// 已使用的实体组件引用。
        /// </summary>
        private EntityComponent entityComponent;

        /// <summary>
        /// 当前登记使用的实体编号。
        /// </summary>
        private long registeredEntityId;

        /// <summary>
        /// 获取当前是否已登记到实体模块。
        /// </summary>
        public bool IsRegistered
        {
            get
            {
                return entityComponent != null && registeredEntityId != 0 &&
                    entityComponent.HasEntity(registeredEntityId);
            }
        }

        private void Start()
        {
            if (!registerOnStart)
            {
                return;
            }

            try
            {
                Register();
            }
            catch (Exception ex)
            {
                Log.Error("Register scene entity '{0}' failed: {1}", ResolveEntityName(), ex);
            }
        }

        /// <summary>
        /// 将当前场景对象登记到实体模块。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>登记后的实体。</returns>
        public IEntity Register(object userData = null)
        {
            if (IsRegistered)
            {
                return entityComponent.GetEntity(registeredEntityId);
            }

            entityComponent = GameEntry.Entity;
            if (entityComponent == null)
            {
                throw new RFrameworkException("Can not find EntityComponent for scene entity registration.");
            }

            registeredEntityId = entityId != 0 ? entityId : gameObject.GetInstanceID();
            string resolvedGroupName = ResolveGroupName();
            return entityComponent.RegisterSceneEntity(gameObject, registeredEntityId,
                ResolveEntityName(), resolvedGroupName, createGroupIfMissing, userData);
        }

        /// <summary>
        /// 从实体模块注销当前场景实体，不销毁当前 GameObject。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void Unregister(object userData = null)
        {
            if (!IsRegistered)
            {
                registeredEntityId = 0;
                return;
            }

            long currentEntityId = registeredEntityId;
            registeredEntityId = 0;
            entityComponent.UnregisterSceneEntity(currentEntityId, userData);
        }

        private void OnDestroy()
        {
            if (!IsRegistered)
            {
                return;
            }

            long currentEntityId = registeredEntityId;
            try
            {
                Unregister();
            }
            catch (Exception ex)
            {
                Log.Error("Unregister scene entity '{0}' failed: {1}", currentEntityId, ex);
            }
        }

        /// <summary>
        /// 解析当前使用的实体名称。
        /// </summary>
        /// <returns>非空的实体名称。</returns>
        private string ResolveEntityName()
        {
            return string.IsNullOrWhiteSpace(entityName) ? gameObject.name : entityName.Trim();
        }

        /// <summary>
        /// 解析当前使用的实体组名称。
        /// </summary>
        /// <returns>非空的实体组名称。</returns>
        private string ResolveGroupName()
        {
            return string.IsNullOrWhiteSpace(groupName) ? DefaultGroupName : groupName.Trim();
        }
    }
}
