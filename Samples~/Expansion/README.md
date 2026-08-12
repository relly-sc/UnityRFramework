# Expansion Sample

与框架模块无关、但可直接复用的轻量 Unity 开发组件。该 Sample 不被
`UnityRFramework.Runtime` 引用，按需导入或复制到项目中使用。
命名空间为 `UnityRFramework.Expansion`。

## 目录

```text
Scripts/Runtime/    运行时组件
Scripts/Editor/     组件对应的 Editor Inspector
```

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
