using System;
using System.Collections.Generic;
using R3;
using RFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion.R3Demo.Editor
{
    /// <summary>独立的事件与订阅清理回归检查，不增加示例程序集或 NUnit 依赖。</summary>
    internal sealed class R3IntegrationSelfCheck
    {
        private IEventModule events;
        private GameObject owner;

        [MenuItem("UnityRFramework/Expansion/R3/检查事件与订阅清理")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode，再检查事件与订阅清理。");
            var check = new R3IntegrationSelfCheck();
            Action[] cases =
            {
                check.EventStream_IsLazy_AndDisposesEachSubscriberIndependently,
                check.EventStream_RejectsNullSource,
                check.UI_CloseAndReopen_ReleaseSubscriptionsWithoutDestroyingObject,
                check.UI_PauseAndResume_DoNotEndOpenScope,
                check.UI_FailedOpenAndClose_StillReleaseSubscriptions,
                check.Entity_HideAndRecycle_ReleaseSubscriptionsOnReuse,
                check.Entity_FailedShowAndHide_StillReleaseSubscriptions,
                check.Destroy_ReleasesUIAndEntitySubscriptions
            };
            Scene previous = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                foreach (Action test in cases)
                {
                    check.SetUp();
                    try { test(); }
                    catch (Exception error) { throw new InvalidOperationException("[R3 订阅检查] " + test.Method.Name, error); }
                    finally { UnityEngine.Object.DestroyImmediate(check.owner); }
                }
                Debug.Log("[R3 事件与订阅清理] 8/8 项通过。");
            }
            finally
            {
                EditorSceneManager.CloseScene(scratch, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private void SetUp()
        {
            // 独立模块实例，不清空编辑器中可能已初始化的框架 Host。
            Type type = typeof(IEventModule).Assembly.GetType("RFramework.EventModule", true);
            events = (IEventModule)Activator.CreateInstance(type, true);
            owner = new GameObject("R3 subscription test");
        }

        private void EventStream_IsLazy_AndDisposesEachSubscriberIndependently()
        {
            Observable<int> stream = events.Observe<int>();
            Equal(0, events.Count<int>());
            int first = 0, second = 0;
            using IDisposable a = stream.Where(x => x > 0).Subscribe(x => first += x);
            using IDisposable b = stream.Subscribe(x => second += x);
            events.Fire(3);
            Equal(3, first);
            Equal(3, second);
            a.Dispose();
            a.Dispose();
            Equal(1, events.Count<int>());
            events.Fire(2);
            Equal(3, first);
            Equal(5, second);
            b.Dispose();
            Equal(0, events.Count<int>());
        }

        private void EventStream_RejectsNullSource()
        {
            Throws<ArgumentNullException>(() => ((IEventModule)null).Observe<int>());
            Throws<ArgumentNullException>(() => ((EventComponent)null).Observe<int>());
        }

        private void UI_CloseAndReopen_ReleaseSubscriptionsWithoutDestroyingObject()
        {
            R3TestForm logic = owner.AddComponent<R3TestForm>();
            UIForm form = owner.AddComponent<UIForm>();
            IUIForm lifecycle = form;
            lifecycle.OnInit(null);
            for (int i = 0; i < 3; i++)
            {
                lifecycle.OnOpen(events);
                Equal(1, events.Count<int>());
                events.Fire(1);
                lifecycle.OnClose(null);
                Equal(0, events.Count<int>());
                events.Fire(1);
            }
            Equal(3, logic.Received);
        }

        private void UI_PauseAndResume_DoNotEndOpenScope()
        {
            owner.AddComponent<R3TestForm>();
            IUIForm form = owner.AddComponent<UIForm>();
            form.OnInit(null);
            form.OnOpen(events);
            form.OnPause();
            Equal(1, events.Count<int>());
            form.OnResume();
            Equal(1, events.Count<int>());
            form.OnClose(null);
            Equal(0, events.Count<int>());
        }

        private void UI_FailedOpenAndClose_StillReleaseSubscriptions()
        {
            R3TestForm logic = owner.AddComponent<R3TestForm>();
            IUIForm form = owner.AddComponent<UIForm>();
            form.OnInit(null);
            logic.ThrowOnOpen = true;
            Throws<InvalidOperationException>(() => form.OnOpen(events));
            Equal(0, events.Count<int>());
            logic.ThrowOnOpen = false;
            form.OnOpen(events);
            logic.ThrowOnClose = true;
            Throws<InvalidOperationException>(() => form.OnClose(null));
            Equal(0, events.Count<int>());
        }

        private void Entity_HideAndRecycle_ReleaseSubscriptionsOnReuse()
        {
            R3TestEntity entity = owner.AddComponent<R3TestEntity>();
            entity.Show(events);
            events.Fire(1);
            entity.Hide();
            Equal(0, events.Count<int>());
            entity.Show(events);
            entity.Show(events);
            Equal(1, events.Count<int>());
            events.Fire(1);
            entity.Recycle();
            Equal(0, events.Count<int>());
            Equal(2, entity.Received);
        }

        private void Entity_FailedShowAndHide_StillReleaseSubscriptions()
        {
            R3TestEntity entity = owner.AddComponent<R3TestEntity>();
            entity.ThrowOnShow = true;
            Throws<InvalidOperationException>(() => entity.Show(events));
            Equal(0, events.Count<int>());
            entity.ThrowOnShow = false;
            entity.Show(events);
            entity.ThrowOnHide = true;
            Throws<InvalidOperationException>(() => entity.Hide());
            Equal(0, events.Count<int>());
        }

        private void Destroy_ReleasesUIAndEntitySubscriptions()
        {
            owner.AddComponent<R3TestEntity>().Show(events);
            IUIForm form = owner.AddComponent<UIForm>();
            owner.AddComponent<R3TestForm>();
            form.OnInit(null);
            form.OnOpen(events);
            Equal(2, events.Count<int>());
            UnityEngine.Object.DestroyImmediate(owner);
            Equal(0, events.Count<int>());
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"[R3 订阅检查] 预期 {expected}，实际 {actual}。");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("[R3 订阅检查] 未抛出预期异常：" + typeof(T).Name);
        }
    }

    [ExecuteAlways]
    public sealed class R3TestForm : R3UIFormLogic
    {
        public int Received;
        public bool ThrowOnOpen;
        public bool ThrowOnClose;
        protected override void OnOpenWithR3(object data)
        {
            ((IEventModule)data).Observe<int>().Subscribe(_ => Received++).AddTo(OpenSubscriptions);
            if (ThrowOnOpen) throw new InvalidOperationException("Open failed");
        }
        protected override void OnCloseWithR3(object data)
        {
            if (ThrowOnClose) throw new InvalidOperationException("Close failed");
        }
    }

    [ExecuteAlways]
    public sealed class R3TestEntity : R3EntityLogic
    {
        public int Received;
        public bool ThrowOnShow;
        public bool ThrowOnHide;
        public void Show(IEventModule source) => OnShow(source);
        public void Hide() => OnHide(false, null);
        public void Recycle() => OnRecycle();
        protected override void OnShowWithR3(object data)
        {
            ((IEventModule)data).Observe<int>().Subscribe(_ => Received++).AddTo(ShowSubscriptions);
            if (ThrowOnShow) throw new InvalidOperationException("Show failed");
        }
        protected override void OnHideWithR3(bool isShutdown, object data)
        {
            if (ThrowOnHide) throw new InvalidOperationException("Hide failed");
        }
    }
}
