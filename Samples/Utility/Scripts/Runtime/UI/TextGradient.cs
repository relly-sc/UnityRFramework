using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Samples
{
    /// <summary>
    /// 为 UGUI Text 顶点应用垂直颜色渐变。
    /// 渐变色与 Text 原始顶点颜色相乘，因此会保留原始颜色和 Alpha。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI/Text Gradient")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class TextGradient : BaseMeshEffect
    {
        [SerializeField]
        [Tooltip("文字顶部的渐变颜色。")]
        private Color32 topColor = Color.white;

        [SerializeField]
        [Tooltip("文字底部的渐变颜色。")]
        private Color32 bottomColor = Color.black;

        /// <summary>
        /// 获取或设置顶部渐变颜色。
        /// </summary>
        public Color32 TopColor
        {
            get { return topColor; }
            set
            {
                if (topColor.Equals(value))
                {
                    return;
                }

                topColor = value;
                graphic?.SetVerticesDirty();
            }
        }

        /// <summary>
        /// 获取或设置底部渐变颜色。
        /// </summary>
        public Color32 BottomColor
        {
            get { return bottomColor; }
            set
            {
                if (bottomColor.Equals(value))
                {
                    return;
                }

                bottomColor = value;
                graphic?.SetVerticesDirty();
            }
        }

        /// <inheritdoc />
        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive()
                || vertexHelper == null
                || vertexHelper.currentVertCount == 0)
            {
                return;
            }

            float bottomY = float.PositiveInfinity;
            float topY = float.NegativeInfinity;
            UIVertex vertex = default;
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);
                float positionY = vertex.position.y;
                if (positionY < bottomY)
                {
                    bottomY = positionY;
                }

                if (positionY > topY)
                {
                    topY = positionY;
                }
            }

            float height = topY - bottomY;
            bool hasHeight = height > Mathf.Epsilon;
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);
                float factor = hasHeight
                    ? Mathf.Clamp01((vertex.position.y - bottomY) / height)
                    : 0.5f;
                Color32 gradientColor = Color32.Lerp(
                    bottomColor,
                    topColor,
                    factor);
                vertex.color = Multiply(vertex.color, gradientColor);
                vertexHelper.SetUIVertex(vertex, index);
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            graphic?.SetVerticesDirty();
        }

        private static Color32 Multiply(Color32 source, Color32 tint)
        {
            return new Color32(
                MultiplyChannel(source.r, tint.r),
                MultiplyChannel(source.g, tint.g),
                MultiplyChannel(source.b, tint.b),
                MultiplyChannel(source.a, tint.a));
        }

        private static byte MultiplyChannel(byte left, byte right)
        {
            return (byte)((left * right + 127) / 255);
        }
    }
}
