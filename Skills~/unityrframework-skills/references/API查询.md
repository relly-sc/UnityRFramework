# API 查询与版本核对

## 先找到当前安装

1. 读取项目 Packages/manifest.json 与 packages-lock.json，确认 com.relly-sc.unityrframework 和可选 com.relly-sc.unityrframework.samples 的来源与版本。
2. 在 Packages/com.relly-sc.unityrframework、实际 Library/PackageCache 目录、嵌入包或 Assets/UnityRFramework 中定位核心包。目录只是候选；选择项目真正使用的一份，不混用不同版本源码。
3. 在已安装 Samples 包的 samples 清单及 Assets/Samples 下定位已导入内容。开发仓库 Samples、发布包 Samples~、导入后的版本目录不同；不要写死 displayName 或版本路径。
4. 找不到依赖时先明确未安装/未导入，不生成引用不存在类型的代码。远端最新文档不能替代本项目已安装版本的契约。

下文路径相对核心包根目录；复制安装到 Agent 的技能目录只放指导文件，不是框架 API 所在目录。

## 查询顺序与职责

| 位置 | 用途 |
| --- | --- |
| 当前 README 的模块/扩展章节 | 先理解选型、配置、限制与示例。 |
| Scripts/Runtime/GameEntry.cs | 确认真正可用的业务入口和返回的 Component 类型。 |
| Scripts/Runtime/<模块>/<模块>Component.cs | 确认业务调用签名、重载、参数名/默认值及初始化。 |
| Library/RFramework/RFramework/<模块>/I<模块>Module.cs | 核对底层契约、泛型约束与结果类型；不直接套作 Runtime 方法名。 |
| 相应 Module、Helper、逻辑基类及数据类型 | 有疑问时追查实际解析、所有权、线程和失败行为。 |
| 项目已导入 Sample 的 README、运行时脚本和 Tests | 看完整调用关系及验证范围，不默认安装 Sample 或复制 Demo 常量。 |

文档与源码冲突时以当前实际实现为准，说明冲突，不为了匹配文档生成不存在的方法。例如当前 Runtime 的 Timer 入口是 RegisterTimer，不因为旧例子写 Register 就照抄。

## 检索示例

若环境提供 rg，可在已经找到的包根目录使用；没有命令行工具时用 Agent 的文件搜索或 IDE 同等功能。

```text
rg --files Scripts/Runtime Library/RFramework/RFramework
rg -n "public .*SaveAutoAsync|public .*LoadAsync" Scripts/Runtime/Storage
rg -n "OpenUIFormAsync|CloseUIForm|RegisterSceneUIForm" Scripts/Runtime/UI Library/RFramework/RFramework/UI
rg -n "RegisterTimer|CancelTimer|CreateRepeat" Scripts/Runtime/Timer Library/RFramework/RFramework/Timer
```

在当前导入的扩展根中检索实际类/接口，先看 public 声明，再追调用者；不要依靠 GameFramework、XFramework 或其他同名模块推断参数。

## 生成调用前的检查

- 核对类型全名、命名空间、所在程序集和引用；若接口与 Component 都有同名方法，确认最终业务代码调用的是哪一层。
- 检查返回 Task、结果对象、bool 或句柄各自表示什么；是否会抛异常，取消如何表达，失败后是否允许继续。
- 检查入口的初始化前置条件、资源/实例所有权、回调线程、同步/异步限制以及对应关闭/释放接口。
- 泛型行类型、存档 DTO、userData、事件类型由项目明确提供，不能把文档示意类型当成核心已有类型。
- Unity/UI 生命周期方法复制时检查真实基类、override 可见性及 base 调用；不要把 UIFormLogic 猜成 UIFormBase，也不要把 FsmStateBase 或 ProcedureStateBase 当成 MonoBehaviour。
- 涉及第三方 SDK 当前政策、版本兼容或源码中没有的具体调用时，只补充官方资料并注明来源，不能覆盖当前扩展的真实实现边界。
- 扩展未带 README 时，以已导入源码与 package samples 清单核实是否可用；开发仓库有文件不等于消费包已暴露可导入项。

在解释代码或排查问题时提供本项目实际的文件与位置，不给使用者一个无法访问的开发仓库绝对路径。
