# Expansion Sample

与框架模块无关、但可直接复用的轻量 Unity 开发组件。该 Sample 不被
`UnityRFramework.Runtime` 引用，按需导入或复制到项目中使用。
命名空间为 `UnityRFramework.Expansion`。

## 目录

```text
Scripts/Runtime/    运行时组件
Scripts/Editor/     组件对应的 Editor Inspector
```

## UI 自动绑定代码生成器

在 Hierarchy 右键菜单中选择
`UnityRFramework/UI 自动绑定代码生成器`。工具会自动带入右键选中的目标节点。

该工具把用户显式选择的 UI 组件生成到目标 `MonoBehaviour` 的
`XXX.Bindings.g.cs` partial 文件，并在脚本编译后自动回填序列化引用。它不按节点名称
自动扫描，也不修改业务脚本；目标类需要预先声明为 `partial`，场景对象需要先保存场景。

1. 在“目标节点”选择业务脚本所在 GameObject，再从“目标脚本”下拉框明确选择承载字段的
   `MonoBehaviour`；节点上存在多个脚本时不会自动猜测。
2. 添加字段，填写合法且不重复的 C# 字段名。
3. 选择普通字段或数组字段，逐项指定目标节点，再从“组件类型”下拉框明确选择该节点上的
   `Button`、`Text`、`Image`、TMP 组件或其他 `Component`；工具不会默认使用
   `RectTransform`。
4. 工具会读取所选组件公开的 `UnityEvent` 字段和属性，可同时选择多个事件入口；支持
   0 至 4 个事件参数，因而也适用于 UGUI、TMP 和业务自定义交互组件。
5. 检查字段类型、目标路径和代码预览，点击“生成并回填”。

生成的序列化字段是目标 partial 类的一部分，业务代码可直接访问。选择事件入口时，工具会
把组件的持久化事件绑定到生成的公开包装方法；业务 partial 文件只需实现对应的 partial
回调，例如：

```csharp
public partial class LoginPanel
{
    partial void OnSubmitButtonClick()
    {
        Submit();
    }

    partial void OnNameInputValueChanged(string value)
    {
        ValidateName(value);
    }
}
```

基础字段支持任意当前已加载的 `Component` 类型。生成器自身不引用 TMP 程序集；未安装
TMP 时仍可正常编译。若目标业务脚本位于自定义 asmdef，生成 TMP 或其他外部程序集的强类型
字段前，该 asmdef 必须引用对应程序集；工具会在写文件前检查并明确提示缺失引用。重复生成
只覆盖 `.Bindings.g.cs`，不会覆盖业务 partial 文件，且会移除并重建同一目标方法的持久化
事件，避免重复监听。

## UI Prefab 检查器

在 Project 视图选中 Prefab 后使用 `Assets/UnityRFramework/检查 UI Prefab`，或在 Hierarchy
右键 UI 根节点选择 `UnityRFramework/检查 UI Prefab`。结果输出到 Console，检查范围为：

- 丢失脚本和序列化对象引用；
- 同节点重复的 `Graphic Raycast Target`；
- 位于父级 `Selectable` 内、通常不需要参与射线检测的子级 Graphic；
- 带有无效 `CanvasScaler`、排序配置无效或与父 Canvas 排序冲突的嵌套 Canvas。

检查器只报告问题，不自动改 Prefab、场景或用户设置。

## SpriteAtlas 工作流

在 Project 视图选择 Sprite、Sprite 纹理或文件夹，使用
`Assets/UnityRFramework/从所选资源创建 SpriteAtlas`。工具创建 Unity 原生 SpriteAtlas，
加入所选收集对象，并应用适合 UGUI 的基础设置：关闭 Rotation、关闭 Tight Packing、Padding
至少为 4、启用 Include in Build。

使用前还需在 `Edit > Project Settings > Editor > Sprite Packer > Mode` 启用 Sprite Atlas。
该项属于项目级设置，工具只检查并提示，不自动修改。若保持 `Disabled`，图集资源虽然可以加载，
但 `spriteCount` 为 0，`GetSprite` 无法取得任何 Sprite。

选中 SpriteAtlas 后使用 `Assets/UnityRFramework/检查 SpriteAtlas`，可检查空收集项、重复
Sprite 名、Include in Build、Rotation、Tight Packing 和过小 Padding。工具不新增图集资源模块；
运行时继续使用当前 Resource Helper：

```csharp
using UnityEngine;
using UnityEngine.U2D;

const string atlasLocation = "UI/Common"; // Resources 下的相对路径，不带扩展名。
SpriteAtlas atlas = await GameEntry.Resource.LoadAssetAsync<SpriteAtlas>(atlasLocation);
Sprite icon = atlas.GetSprite("IconConfirm");

// 使用结束后，按加载位置和类型归还这一次 SpriteAtlas 引用。
GameEntry.Resource.UnloadAsset<SpriteAtlas>(atlasLocation);
```

`GetSprite` 返回的 Sprite 依赖图集资源；不要在仍显示该 Sprite 时归还 SpriteAtlas。使用 YooAsset
Helper 时，`atlasLocation` 改为收集器生成的 Address；两种 Helper 的引用计数和归还方式一致。

## Safe Area

将 `SafeAreaFitter` 挂在需要避开刘海、圆角和系统手势区域的全屏 UGUI 容器上。其父节点必须
覆盖完整屏幕；组件在运行时根据 `Screen.safeArea` 设置锚点，并在横竖屏、分辨率或安全区域
变化时自动刷新。组件不修改 Canvas、Canvas Scaler 或业务 UI 的其他布局参数。

## UGUI 组件

### ButtonState / ButtonStateGroup

`ButtonState` 管理普通、悬停预览和选中显示，可独立切换，也可加入
`ButtonStateGroup` 形成单选组。组支持初始选中、允许空选和运行时换组。

```csharp
buttonState.SetSelected(true);
buttonState.SetGroup(tabGroup);
tabGroup.Select(buttonState);
tabGroup.ClearSelection();
```

### LongPressButton

`LongPressButton` 继承 UGUI `Button`，保留原生 `onClick`，并提供
`OnLongPress`。达到阈值时立即触发长按并抑制同次普通点击。按住期间一旦移出
按钮，本次长按立即取消并恢复 Normal 状态；移回后不会续接原计时。

```csharp
longPressButton.LongPressDuration = 0.6f;
longPressButton.OnLongPress.AddListener(ShowDetails);
```

### DoubleClickButton

`DoubleClickButton` 继承 UGUI `Button`，使用原生 `onClick` 表示单击，并提供
`OnDoubleClick` 表示双击。首次点击会等待双击判定时间；时间内完成第二次点击时
只触发双击，超时后只触发单击。移出按钮、取消操作、禁用组件或对象失活会清除
待判定点击。

```csharp
doubleClickButton.DoubleClickInterval = 0.3f;
doubleClickButton.onClick.AddListener(SelectItem);
doubleClickButton.OnDoubleClick.AddListener(OpenItem);
```

### TextGradient

`TextGradient` 用于 UGUI `Text` 的垂直颜色渐变。渐变色与原始顶点 RGBA
相乘，保留原始透明度；编辑模式修改颜色时会立即刷新。

```csharp
textGradient.BottomColor = new Color32(40, 80, 160, 255);
textGradient.TopColor = Color.white;
```
