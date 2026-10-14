# UnityRFramework Samples

UnityRFramework 官方示例、验收场景与可选扩展。请先安装相同版本的核心包，再通过 Unity Package Manager 按需导入 Sample。

优先从 `UnityRFramework → 安装扩展与示例` 选择功能：安装器先安装该框架版本声明的固定 UPM 依赖，再导入所需 Sample。不同版本会先确认，本地插件不会被覆盖。升级时可选择“同步并重新导入”；请先备份旧导入目录中的项目改动。安装器不在启动时自动更换第三方插件，也不安装未选择的扩展。

Samples 包按 `versions/{框架版本}` 分支安装。直接点击 Package Manager 的 Import 会绕过框架依赖检查，需要自行安装插件。R3 的 NuGet 本体、Obfuz 加密虚拟机与密钥、HybridCLR 本机 Installer 初始化仍需按官方要求准备。

`Expansion.R3` 提供可选响应式事件桥接与 UI/Entity 订阅生命周期管理；先安装 R3 本体和 R3.Unity，再导入此扩展。`Expansion.R3.Demo` 为独立中文 UGUI 示例，导入后从 `UnityRFramework → Expansion → R3 → 打开响应式交互示例` 运行。具体安装与用法见各 Sample 的 README；不要求接入 UniTask 或 MVVM。
