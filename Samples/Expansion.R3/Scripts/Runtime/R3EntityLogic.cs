using R3;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>可选实体基类，显示时绑定、隐藏或回收时释放，不依赖对象被销毁。</summary>
    public abstract class R3EntityLogic : EntityLogic
    {
        /// <summary>本次显示周期的订阅集合。</summary>
        protected CompositeDisposable ShowSubscriptions { get; private set; }

        protected sealed override void OnShow(object userData)
        {
            ReleaseSubscriptions();
            ShowSubscriptions = new CompositeDisposable();
            try { OnShowWithR3(userData); }
            catch { ReleaseSubscriptions(); throw; }
        }

        protected sealed override void OnHide(bool isShutdown, object userData)
        {
            try { OnHideWithR3(isShutdown, userData); }
            finally { ReleaseSubscriptions(); }
        }

        protected sealed override void OnRecycle()
        {
            try { OnRecycleWithR3(); }
            finally { ReleaseSubscriptions(); base.OnRecycle(); }
        }

        /// <summary>代替 OnShow：将订阅加入 ShowSubscriptions。</summary>
        protected abstract void OnShowWithR3(object userData);

        /// <summary>代替 OnHide：执行业务收尾。</summary>
        protected virtual void OnHideWithR3(bool isShutdown, object userData) { }

        /// <summary>代替 OnRecycle：执行回收收尾。</summary>
        protected virtual void OnRecycleWithR3() { }

        /// <summary>销毁兜底。派生类若重写此方法，必须调用 base.OnDestroy()。</summary>
        protected virtual void OnDestroy() => ReleaseSubscriptions();

        private void ReleaseSubscriptions()
        {
            CompositeDisposable previous = ShowSubscriptions;
            ShowSubscriptions = null;
            previous?.Dispose();
        }
    }
}
