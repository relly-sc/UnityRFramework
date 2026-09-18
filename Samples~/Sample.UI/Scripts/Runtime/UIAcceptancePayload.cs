namespace UnityRFramework.Sample.UI
{
    /// <summary>
    /// Sample.UI 演示用强类型打开参数。
    /// </summary>
    public sealed class UIAcceptancePayload
    {
        public UIAcceptancePayload(string title, int sequence)
        {
            Title = title;
            Sequence = sequence;
        }

        public string Title { get; }

        public int Sequence { get; }
    }
}
