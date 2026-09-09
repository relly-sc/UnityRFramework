# UI 模块验收示例

`Sample.UI` 是内置 UI 模块的独立 UGUI 验收场景，不依赖 `Sample.Demo`、TMP 或任何
Expansion。

## 打开方式

1. 打开 `GameAssets/Scenes/UIAcceptance.unity`。
2. 进入 Play Mode。
3. 依次打开 Panel A、Panel B、Full-screen Popup 和 Independent Canvas，再使用 Close Top 返回。

## 验收内容

- Panel A 与 Panel B 位于相同逻辑层，后打开者显示在上方。
- Full-screen Popup 位于更高层，打开时暂停并隐藏下层窗口，关闭后恢复原栈顶。
- Close Top 始终关闭当前栈顶；空栈时不报错。
- Scene HUD 由场景持有，只参与 UI 栈与生命周期，不由 Helper 销毁或卸载。
- 面板显示 `UIAcceptancePayload` 强类型参数中的标题和序号。
- UI Prefab 挂到 `UIComponent` 配置的层级容器，Prefab 自身 RectTransform 参数不被覆盖。
- Independent Canvas 的 Prefab 根节点自带 Canvas，会进入独立 Canvas 层级；其 Canvas 参数
  由 Prefab 自己维护，框架不覆盖。
- 软重启后不遗留窗口或加载任务。验证软重启时需将本场景放在 Build Settings 第 0 位。

## UIComponent 配置

场景直接使用 `UnityRFramework.prefab` 自带的默认 Canvas、普通层级和独立 Canvas 层级。
使用者可以在 `UIComponent` Inspector 中手动配置：

- `UI Root`：未匹配专用层级时使用的公共 RectTransform 根节点。
- `Layer Roots`：`Window Layer` 与专用 RectTransform 容器的精确映射。
- `Independent Canvas Root`：根节点自带 Canvas 的 UI 未匹配专用层级时使用的持久化根节点。
- `Independent Canvas Layer Roots`：根节点自带 Canvas 的 UI 使用的层级映射。

专用容器应是 UI Root 的直接子节点，每层使用不同容器。框架会按层级值排列容器，但不会
覆盖业务 UI 的锚点、尺寸、位置、缩放、组件参数或事件绑定。

只有 Prefab 根节点上的 Canvas 会被识别为独立 Canvas UI。其 Render Mode、Camera、
CanvasScaler 和排序参数由 Prefab 自己维护。

按钮交互需要一个有效的 `EventSystem` 和 Input Module。Sample 所在项目应按当前启用的旧输入
系统或新输入系统配置对应模块，并确保场景中只有一个 EventSystem。

## 验收标准

- Editor 中连续打开、返回、关闭、全屏覆盖和软重启行为正确。
- 16:9、宽屏和竖屏 Game View 下控件可操作，层级关系不变。
- Player 可启动、关闭和软重启，Console 无未处理异常。
- UPM 全新项目只导入 `Sample.UI` 时可独立编译运行。
