# Expansion.UI.Demo

`Expansion.UI` 的独立功能示例与验收资源，只依赖框架核心和 `Expansion.UI`，不依赖 TMP、
YooAsset、HybridCLR 或其他第三方插件。

## 导入顺序

1. 导入 `Expansion.UI`。
2. 导入 `Expansion.UI.Demo`。

## 场景

- `UIBindingAndToolsAcceptance`：验证 UI 自动绑定、Prefab 检查、SpriteAtlas 加载和 Safe Area。
- `UIInteractionAcceptance`：验证模态确认队列、Toast、固定高度虚拟列表、红点树和软重启。

## 编辑器入口

- Hierarchy 右键 `UnityRFramework/UI 自动绑定代码生成器`。
- Hierarchy 右键 `UnityRFramework/检查 UI Prefab`。
- Project 视图右键 `UnityRFramework/从所选资源创建 SpriteAtlas`。
- Project 视图右键 `UnityRFramework/检查 SpriteAtlas`。
- `UnityRFramework/Samples/生成 Expansion.UI.Demo 验收场景`：重建交互与红点验收场景。

自检入口均位于 `GameObject/UnityRFramework`，按功能命名，不使用实施阶段编号。
