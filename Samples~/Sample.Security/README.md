# Sample.Security

Protected 基础数值的独立 UGUI 验收 Sample，仅依赖 UnityRFramework 核心。

## 使用方法

1. 在 Unity 中执行 `UnityRFramework/Samples/生成安全数值验收场景`。
2. 打开 `GameAssets/Scenes/SecurityAcceptance.unity` 并运行。
3. 点击“增加 10”，确认普通整数和 ProtectedInt 都按预期变化。
4. 使用 Cheat Engine 扫描普通整数 `1000`，点击“增加 10”后扫描 `1010`，确认普通值可作为对照定位。
5. ProtectedInt 的真实存储字段是掩码、编码值和校验值，不应以相同的明文精确扫描流程直接定位真实值。
6. 在确认值类型能运行后，再进行受控内存修改，确认界面显示篡改事件；不要把该能力理解为不可绕过的反作弊方案。

“重新生成保护编码”会用当前值生成新的随机编码；它不是密钥轮换，也不会改变业务数据。
