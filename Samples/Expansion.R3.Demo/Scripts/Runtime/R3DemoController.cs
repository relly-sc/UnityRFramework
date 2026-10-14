using R3;
using RFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion.R3Demo
{
    /// <summary>场景持有数据，窗口仅持有本次打开的订阅。</summary>
    public sealed class R3DemoController : MonoBehaviour
    {
        private const string FormName = "R3DemoPanel";
        [SerializeField] private R3DemoForm form;
        [SerializeField] private Button addScoreButton;
        [SerializeField] private Button sendEventButton;
        [SerializeField] private Button togglePanelButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text statusText;
        private readonly CompositeDisposable subscriptions = new CompositeDisposable();
        /// <summary>场景持有的积分；窗口关闭后仍保留。</summary>
        public ReactiveProperty<int> Score { get; } = new ReactiveProperty<int>(0);
        /// <summary>已发送事件数。</summary>
        public int Sent { get; private set; }
        /// <summary>窗口实际收到事件数。</summary>
        public int Received { get; private set; }
        /// <summary>窗口打开次数。</summary>
        public int OpenCount { get; private set; }
        /// <summary>窗口关闭次数。</summary>
        public int CloseCount { get; private set; }
        /// <summary>本次框架初始化的事件组件。</summary>
        public EventComponent Events { get; private set; }
        /// <summary>窗口是否仍在 UI 模块登记。</summary>
        public bool PanelOpen => GameEntry.UI != null && GameEntry.UI.HasUIForm(FormName);

        private void Start()
        {
            Events = GameEntry.Event;
            if (Events == null || GameEntry.UI == null)
            {
                statusText.text = "未找到框架的 Event / UI 组件";
                return;
            }
            addScoreButton.OnClickAsObservable().Subscribe(_ => AddScore()).AddTo(subscriptions);
            sendEventButton.OnClickAsObservable().Subscribe(_ => SendEvent()).AddTo(subscriptions);
            togglePanelButton.OnClickAsObservable().Subscribe(_ => TogglePanel()).AddTo(subscriptions);
            resetButton.OnClickAsObservable().Subscribe(_ => ResetData()).AddTo(subscriptions);
            TogglePanel();
        }

        /// <summary>增加一积分。</summary>
        public void AddScore() => Score.Value++;

        /// <summary>发送演示事件；窗口关闭期间不接收。</summary>
        public void SendEvent()
        {
            if (Events != null) Events.Fire(new R3DemoSignal(++Sent));
        }

        /// <summary>通过 UI 模块登记/注销同一个场景窗口。</summary>
        public void TogglePanel()
        {
            if (GameEntry.UI == null) return;
            if (PanelOpen)
            {
                GameEntry.UI.UnregisterSceneUIForm(FormName);
                CloseCount++;
            }
            else
            {
                GameEntry.UI.RegisterSceneUIForm(form.gameObject, FormName, UILayer.Panel, userData: this);
                OpenCount++;
            }
        }

        /// <summary>重置积分及事件计数。</summary>
        public void ResetData()
        {
            Score.Value = 0;
            Sent = Received = 0;
            if (PanelOpen) form.ShowEvent(0, 0);
        }

        /// <summary>记录窗口实际收到的事件。</summary>
        internal void ReceiveEvent(R3DemoSignal signal)
        {
            Received++;
            form.ShowEvent(signal.Sequence, Received);
        }

        private void Update()
        {
            if (Events == null) return;
            statusText.text = "界面：" + (PanelOpen ? "已打开" : "已关闭") +
                "    积分：" + Score.Value + "    事件订阅：" + Events.Count<R3DemoSignal>() +
                "\n发送 / 收到：" + Sent + " / " + Received +
                "    打开 / 关闭：" + OpenCount + " / " + CloseCount;
        }

        private void OnDestroy()
        {
            if (GameEntry.UI != null && PanelOpen) GameEntry.UI.UnregisterSceneUIForm(FormName);
            subscriptions.Dispose();
            Score.Dispose();
        }
    }

    /// <summary>示例强类型事件，不依赖新的全局事件总线。</summary>
    public readonly struct R3DemoSignal
    {
        /// <summary>发送序号。</summary>
        public int Sequence { get; }
        /// <summary>创建指定序号的事件。</summary>
        public R3DemoSignal(int sequence) => Sequence = sequence;
    }
}
