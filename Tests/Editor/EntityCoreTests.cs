using System;
using System.Collections.Generic;
using NUnit.Framework;
using RFramework;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 实体注册、层级、异常清理和更新隔离的核心契约测试。
    /// </summary>
    public sealed class EntityCoreTests
    {
        private IEntityModule module;

        [SetUp]
        public void SetUp()
        {
            module = RFrameworkModuleHost.Get<IEntityModule>();
            module.CreateEntityGroup("Test", 0f, 8, 60f);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                RFrameworkModuleHost.StopAll();
            }
            catch (RFrameworkException)
            {
                // 各测试单独断言生命周期异常，清理阶段只保证模块注册表复位。
            }
        }

        [Test]
        public void AttachRejectsParentCycle()
        {
            TestEntity parent = Register(1);
            Register(2);
            module.AttachEntity(2, 1);

            try
            {
                Assert.Throws<RFrameworkException>(() => module.AttachEntity(1, 2));
                Assert.IsNull(parent.Parent);
            }
            finally
            {
                module.DetachEntity(1);
            }
        }

        [Test]
        public void HideRemovesEntityEvenWhenLifecycleThrows()
        {
            TestEntity entity = Register(1);
            entity.ThrowOnHide = true;

            Assert.Throws<RFrameworkException>(() => module.HideEntity(1));
            Assert.IsFalse(module.HasEntity(1));
            Assert.AreEqual(0, module.GetEntityGroup("Test").EntityCount);
        }

        [Test]
        public void UpdateContinuesAfterOneEntityFails()
        {
            TestEntity failing = Register(1);
            TestEntity healthy = Register(2);
            failing.ThrowOnUpdate = true;

            Assert.Throws<RFrameworkException>(() => RFrameworkModuleHost.Tick(0.1f, 0.1f));
            Assert.AreEqual(1, healthy.UpdateCount);
        }

        [Test]
        public void RegisterRollsBackReentrantHideDuringShow()
        {
            TestEntity entity = new TestEntity();
            entity.ShowAction = () => module.HideEntity(1);

            Assert.Throws<RFrameworkException>(() =>
                module.RegisterEntity(1, "Entity/1", "Test", entity));
            Assert.IsFalse(module.HasEntity(1));
            Assert.AreEqual(0, module.GetEntityGroup("Test").EntityCount);
        }

        [Test]
        public void ShutdownCleansRegistryWhenEntityCallbacksFail()
        {
            IEntityModule stoppedModule = module;
            TestEntity failing = Register(1);
            Register(2);
            failing.ThrowOnHide = true;
            failing.ThrowOnRecycle = true;

            Assert.Throws<RFrameworkException>(() => RFrameworkModuleHost.StopAll());
            Assert.AreEqual(0, RFrameworkModuleHost.Count);

            module = RFrameworkModuleHost.Get<IEntityModule>();
            Assert.AreNotSame(stoppedModule, module);
        }

        private TestEntity Register(long id)
        {
            TestEntity entity = new TestEntity();
            module.RegisterEntity(id, $"Entity/{id}", "Test", entity);
            return entity;
        }

        private sealed class TestEntity : IEntity
        {
            private readonly List<IEntity> children = new List<IEntity>();

            public long Id { get; private set; }
            public EntityStatus Status { get; private set; }
            public string AssetName { get; private set; }
            public object Handle => this;
            public IEntityGroup Group { get; private set; }
            public IEntity Parent { get; private set; }
            public IReadOnlyList<IEntity> Children => children;
            public bool ThrowOnHide { get; set; }
            public bool ThrowOnUpdate { get; set; }
            public bool ThrowOnRecycle { get; set; }
            public int UpdateCount { get; private set; }
            public Action ShowAction { get; set; }

            public void OnInit(long entityId, string assetName, IEntityGroup group, bool isNewInstance,
                object userData)
            {
                Id = entityId;
                AssetName = assetName;
                Group = group;
                Status = EntityStatus.Inited;
            }

            public void OnRecycle()
            {
                if (ThrowOnRecycle)
                {
                    throw new InvalidOperationException("recycle failed");
                }

                Status = EntityStatus.Recycled;
            }

            public void OnShow(object userData)
            {
                ShowAction?.Invoke();
                Status = EntityStatus.Showed;
            }

            public void OnHide(bool isShutdown, object userData)
            {
                if (ThrowOnHide)
                {
                    throw new InvalidOperationException("hide failed");
                }

                Status = EntityStatus.Hidden;
            }

            public void OnAttached(IEntity childEntity, object userData)
            {
                children.Add(childEntity);
            }

            public void OnDetached(IEntity childEntity, object userData)
            {
                children.Remove(childEntity);
            }

            public void OnAttachTo(IEntity parentEntity, object userData)
            {
                Parent = parentEntity;
            }

            public void OnDetachFrom(IEntity parentEntity, object userData)
            {
                Parent = null;
            }

            public void OnUpdate(float elapseSeconds, float realElapseSeconds)
            {
                UpdateCount++;
                if (ThrowOnUpdate)
                {
                    throw new InvalidOperationException("update failed");
                }
            }
        }
    }
}
