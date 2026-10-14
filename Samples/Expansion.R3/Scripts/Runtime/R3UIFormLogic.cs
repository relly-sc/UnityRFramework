using R3;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>可选 UI 基类，自动管理每次打开期间的 R3 订阅；暂停不结束打开周期。</summary>
    public abstract class R3UIFormLogic : UIFormLogic
    {
        /// <summary>将本次打开的订阅 AddTo 此集合；不要用于跨界面共享数据的所有权。</summary>
        protected CompositeDisposable OpenSubscriptions { get; private set; }

        protected sealed override void OnOpen(object userData)
        {
            ReleaseSubscriptions();
            OpenSubscriptions = new CompositeDisposable();
            try { OnOpenWithR3(userData); }
            catch { ReleaseSubscriptions(); throw; }
        }

        protected sealed override void OnClose(object userData)
        {
            try { OnCloseWithR3(userData); }
            finally { ReleaseSubscriptions(); }
        }

        /// <summary>代替 OnOpen：绑定数据与交互，将订阅加入 OpenSubscriptions。</summary>
        protected abstract void OnOpenWithR3(object userData);

        /// <summary>代替 OnClose：执行业务收尾；异常也不会跳过订阅清理。</summary>
        protected virtual void OnCloseWithR3(object userData) { }

        /// <summary>销毁兜底。派生类若重写此方法，必须调用 base.OnDestroy()。</summary>
        protected virtual void OnDestroy() => ReleaseSubscriptions();

        private void ReleaseSubscriptions()
        {
            CompositeDisposable previous = OpenSubscriptions;
            OpenSubscriptions = null;
            previous?.Dispose();
        }
    }
}
