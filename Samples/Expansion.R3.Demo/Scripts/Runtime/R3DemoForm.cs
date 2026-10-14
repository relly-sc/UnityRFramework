using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion.R3Demo
{
    /// <summary>响应式数值、组合条件、搜索防抖及框架事件的 UGUI 示例。</summary>
    public sealed class R3DemoForm : R3UIFormLogic
    {
        [SerializeField] private Text scoreText;
        [SerializeField] private Text eventText;
        [SerializeField] private Text searchResultText;
        [SerializeField] private InputField searchInput;
        [SerializeField] private Toggle allowClaimToggle;
        [SerializeField] private Button claimButton;
        private int searchCount;

        protected override void OnOpenWithR3(object userData)
        {
            var model = (R3DemoController)userData;
            model.Score.Subscribe(value => scoreText.text = "积分：" + value).AddTo(OpenSubscriptions);
            model.Events.Observe<R3DemoSignal>().Subscribe(model.ReceiveEvent).AddTo(OpenSubscriptions);
            model.Score.CombineLatest(allowClaimToggle.OnValueChangedAsObservable(),
                    (score, allowed) => score >= 3 && allowed)
                .Subscribe(allowed => claimButton.interactable = allowed).AddTo(OpenSubscriptions);
            claimButton.OnClickAsObservable().Subscribe(_ => model.Score.Value -= 3).AddTo(OpenSubscriptions);
            searchInput.OnValueChangedAsObservable().DistinctUntilChanged()
                .Debounce(TimeSpan.FromSeconds(0.35))
                .Subscribe(text => searchResultText.text = "搜索结果：" +
                    (string.IsNullOrWhiteSpace(text) ? "无关键词" : text) + "    次数：" + ++searchCount)
                .AddTo(OpenSubscriptions);
            eventText.text = "最近事件：无    累计收到：" + model.Received;
        }

        /// <summary>显示最近事件，固定区域内覆盖更新。</summary>
        internal void ShowEvent(int sequence, int count) =>
            eventText.text = "最近事件：" + sequence + "    累计收到：" + count;
    }
}
