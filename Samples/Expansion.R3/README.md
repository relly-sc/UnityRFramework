# R3 响应式扩展

`Expansion.R3` 为选择使用 R3 的业务提供事件桥接及生命周期管理，不替换框架的 Event、Timer、Task，也不要求使用 MVVM。仅安装核心时不需要此扩展或 R3。

## 安装依赖

本扩展不内置第三方 DLL。先按 [R3 官方安装说明](https://github.com/Cysharp/R3#unity) 安装以下两部分，再导入 `Expansion.R3`：

1. 使用 NuGetForUnity 安装 NuGet 包 `R3`；开发验证版本为 `1.3.1`。由 NuGet 管理 R3 本体和它的 .NET 依赖。
2. 在 Package Manager 的“通过 Git URL 添加包”中安装 Unity 适配：

   ```text
   https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.1
   ```

**只安装 UPM 的 `R3.Unity` 不会提供 `R3.dll`**，两步都需要。项目已有其他扩展的同名 .NET DLL 时应统一依赖，不能再导入第二份同名程序集。本开发工程复用了已有的 `Microsoft.Bcl.AsyncInterfaces` 和 `System.Runtime.CompilerServices.Unsafe`，不要求消费项目导入 SharpCompress。

`UnityRFramework → 安装扩展与示例` 可以自动安装固定版本的 R3.Unity；R3 本体仍按上述
NuGet 流程安装。工具检查 R3 本体版本，缺失或不匹配时不会导入扩展代码，准备好后再次
点击“安装并导入”即可。不自动引入 NuGetForUnity，也不重复复制项目已有的 .NET DLL。

本扩展和 Demo 不创建 asmdef：运行时脚本随宿主编入 `Assembly-CSharp`，Editor 子目录编入 `Assembly-CSharp-Editor`。`UnityRFramework.Expansion` 是命名空间，不是独立程序集名。

自定义业务 asmdef 不能反向引用默认的 `Assembly-CSharp`。若项目已有自定义业务程序集，需要使用这些扩展类型时，可在明确规划后把导入的扩展源码纳入已有业务程序集，并添加实际需要的 `UnityRFramework.Runtime`、`RFramework.Library`、`R3.Unity` 和预编译 DLL 引用；不要默认给扩展或 Demo 新增 asmdef。R3 自身的第三方程序集不受此规则限制。

## 框架事件转为 R3 流

```csharp
using R3;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;

var subscription = GameEntry.Event.Observe<MyMessage>()
    .Where(message => message.IsRelevant)
    .Subscribe(message => Refresh(message));

// 当前业务作用域结束时释放。
subscription.Dispose();
```

`IEventModule.Observe<T>()` 也可用于纯 C# 业务和测试。创建流不会订阅，每次 `Subscribe` 独立订阅，`Dispose` 退订。它保留框架的派发线程：跨线程消息应从 `FireAsync` 入队，不在工作线程直接 `Fire` 后修改 Unity UI。软重启后不能继续使用旧组件/模块创建的流，应从新实例重新绑定。

## 窗口与实体生命周期

选择继承 `R3UIFormLogic`，在 `OnOpenWithR3` 中绑定本次打开的交互：

```csharp
protected override void OnOpenWithR3(object userData)
{
    model.Score.Subscribe(value => scoreText.text = value.ToString())
        .AddTo(OpenSubscriptions);
}
```

- `OnOpenWithR3` 代替 `OnOpen`；`OnCloseWithR3` 代替 `OnClose`。框架入口回调已封闭，清理不会因忘记调用 base 而漏掉。
- 重新打开时新建订阅集合；关闭和打开失败时释放集合；关闭业务回调抛异常也会清理。`OnPause`/`OnResume` 沿用核心语义，不结束打开周期。
- `R3EntityLogic` 对应 `OnShowWithR3`、`OnHideWithR3`、`OnRecycleWithR3`，订阅加入 `ShowSubscriptions`，隐藏、回收和显示失败均清理。
- 销毁时兜底清理。派生类若重写 `OnDestroy`，必须调用 `base.OnDestroy()`。
- 只把界面/实体自身的订阅加入集合；共享 `ReactiveProperty` 等数据由数据拥有者释放，不能随一个窗口关闭销毁。
- 只用 `AddTo(gameObject)` 通常等销毁才释放，不适合需要随关闭/隐藏结束的订阅。已有业务基类不便更换时，直接用 R3 的 `CompositeDisposable` 在原生命周期成对管理，不必为了扩展强改继承结构。

## 与 UniTask 的分工

R3 处理持续的数据变化、事件过滤和组合；UniTask 处理异步加载/请求。可以在 UniTask 完成后更新业务 `ReactiveProperty`，由 R3 刷新界面；关闭时既取消异步任务，也释放当前订阅。不要将所有 Task API 强改为 UniTask，或把单次加载包装成新的全局事件总线。本扩展不引用 UniTask。

## 测试与示例

8 项自动回归检查位于 `Expansion.R3.Demo/Scripts/Editor/R3IntegrationSelfCheck.cs`，不新增测试 asmdef 或 NUnit 依赖，核心 Tests 不引用 R3。菜单 `UnityRFramework → Expansion → R3 → 检查事件与订阅清理` 覆盖独立订阅、重复释放、UI 开关、实体复用、异常与销毁清理；在独立临时场景执行并恢复原场景。场景和资源独立位于 `Expansion.R3.Demo`，导入后从同一分类打开示例。
