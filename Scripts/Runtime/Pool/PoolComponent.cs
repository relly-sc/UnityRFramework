using System;

using RFramework;
using RFramework.Pool;

using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 对象池组件。
    /// 作为 PoolModule 的运行时包装层，绑定 Unity 生命周期，
    /// 暴露 Inspector 可查看的模块引用，转发所有池操作到 PoolModule。
    /// </summary>
    /// <remarks>
    /// 设计约束：
    /// 1. 本组件不包含业务逻辑——所有逻辑在 PoolModule 中。
    /// 2. 本组件仅负责创建 Module、缓存引用、转发调用。
    /// 3. 保持 100 行以内——若膨胀说明逻辑泄漏进了 Component。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Pool")]
    [DisallowMultipleComponent]
    public sealed class PoolComponent : UnityRFrameworkComponent
    {
        private IPoolModule poolModule;

        /// <summary>
        /// 获取当前管理的对象池数量。
        /// </summary>
        public int PoolCount
        {
            get { return poolModule != null ? poolModule.PoolCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();
            poolModule = RFrameworkModuleEntry.GetModule<IPoolModule>();
        }

        /// <inheritdoc cref="IPoolModule.CreatePool{T}"/>
        public IObjectPool<T> CreatePool<T>(
            string name,
            Func<T> createFunc,
            Action<T> onSpawn = null,
            Action<T> onUnspawn = null,
            Action<T> onDestroy = null,
            int capacity = 64) where T : class
        {
            return poolModule.CreatePool(name, createFunc, onSpawn, onUnspawn, onDestroy, capacity);
        }

        /// <summary>
        /// 创建 GameObject 对象池（便捷方法）。
        /// 自动处理 Instantiate / SetActive / SetParent / Destroy，
        /// 无需手动传递 createFunc / onSpawn / onUnspawn / onDestroy。
        /// </summary>
        /// <param name="name">对象池名称，全局唯一。</param>
        /// <param name="prefab">预制体，用于 Instantiate 创建新实例。</param>
        /// <param name="parent">回收时挂载的父节点，为 null 时不修改父节点。</param>
        /// <param name="prewarmCount">预热数量，默认 0。</param>
        /// <param name="capacity">池容量上限，默认 64。</param>
        /// <returns>创建的对象池实例。</returns>
        /// <example>
        /// <code>
        /// var bulletPool = GameEntry.Pool.CreateGameObjectPool("Bullet", bulletPrefab, parent: bulletRoot, prewarmCount: 20);
        /// var bullet = bulletPool.Spawn();
        /// bulletPool.Unspawn(bullet);
        /// </code>
        /// </example>
        public IObjectPool<GameObject> CreateGameObjectPool(
            string name,
            GameObject prefab,
            Transform parent = null,
            int prewarmCount = 0,
            int capacity = 64)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException("prefab");
            }

            IObjectPool<GameObject> pool = poolModule.CreatePool<GameObject>(
                name,
                createFunc: () =>
                {
                    GameObject go = Object.Instantiate(prefab, parent);
                    go.name = name;
                    return go;
                },
                onSpawn: go =>
                {
                    go.SetActive(true);
                },
                onUnspawn: go =>
                {
                    go.SetActive(false);
                    if (parent != null)
                    {
                        go.transform.SetParent(parent);
                    }
                },
                onDestroy: go =>
                {
                    Object.Destroy(go);
                },
                capacity: capacity);

            if (prewarmCount > 0)
            {
                pool.Prewarm(prewarmCount);
            }

            return pool;
        }

        /// <inheritdoc cref="IPoolModule.DestroyPool"/>
        public bool DestroyPool(string name)
        {
            return poolModule.DestroyPool(name);
        }

        /// <inheritdoc cref="IPoolModule.GetPool{T}"/>
        public IObjectPool<T> GetPool<T>(string name) where T : class
        {
            return poolModule.GetPool<T>(name);
        }

        /// <inheritdoc cref="IPoolModule.ReleaseAllUnused"/>
        public void ReleaseAllUnused()
        {
            poolModule.ReleaseAllUnused();
        }
    }
}
