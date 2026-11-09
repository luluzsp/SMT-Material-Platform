# SMT Material Platform

SMT 物料作业平台源码，包含 Android PDA 和 Windows 物料缺料看板。

- `android/`：PDA 物料查询、叫料、发料，当前版本 v1.8.3。
- `dashboard/`：Windows 物料缺料监控看板，当前版本 v1.6.0。
- `prototypes/`：PDA 储位展示原型。

安装包、调试签名、本机配置和构建输出不纳入源码仓库。接口地址、仓库 ID 等运行参数请在软件设置中核对后使用。

看板构建与测试说明见 [`dashboard/README.md`](dashboard/README.md)。PDA 工程使用 Android Gradle 工程结构，测试逻辑位于 `android/manual-tests/`。
