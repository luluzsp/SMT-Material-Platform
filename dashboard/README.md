# SMT 物料缺料监控看板

Windows C# 桌面看板，用于汇总 MES 缺料信息与 WMS 库存。需求数量直接使用 MES 缺料记录中的 `DEMAND_QTY`。

## 功能

- MES 缺料明细查询，直接读取 `DEMAND_QTY` 作为需求数量。
- WMS 多库位库存合计及物料描述查询；库存 `getPage` 请求固定传入 `storeStatus=已上架`，`pageSize` 可在设置页配置（默认 200）。
- 看板“物料库位”显示 WMS `getPage` 返回的 `storeCode`；排除规则也按 `storeCode` 匹配，命中记录及其库存数量不参与看板统计。
- 发料状态包括：待发料、已发料、库存不足、发料超时；发料时限可按分钟配置。
- 库存不足和发料超时明细整行使用红色字体提示。
- 缺料明细固定按叫料时间排序，可设置升序或降序。
- 常用分辨率、界面缩放、F11/按钮全屏显示。
- 明细列支持鼠标拖动调整；最右侧“可用库存、发料状态”采用独立保底宽度，不会被前部列挤压。“物料库位”可按多个 `storeCode` 的长度手动加宽，空间不足时可横向滚动。
- 指标卡、表头及明细采用适合电视投屏的加大加粗字体。
- 设置窗口采用与主看板一致的深色商务蓝主题和自绘选项卡。
- 线体/UPH与库位排除表格提供新增、修改、删除和保存本表操作。
- 明细自动循环滚动、滚动速度及到底停留时间。
- 物料描述单行省略、两行省略（默认）或完整换行。
- 可分别配置测试区、生产环境的 MES/WMS IP 或完整接口地址，并选择当前运行环境。
- 设置页可开启演示模式；开启后完全使用内置数据，不访问 MES/WMS。
- 接口超时、HTTP 错误、业务错误和无效 JSON 会弹窗提示并写入本地日志。
- 生产模式下可开关 MES/WMS 详细请求日志；开启时记录系统、操作、请求 URL、请求参数、请求与响应时间、耗时、HTTP 状态、业务结果及响应正文。异常日志始终保留。

## 构建

```powershell
dotnet build .\src\SmtMaterialDashboard.App\SmtMaterialDashboard.App.csproj -c Release
dotnet run --project .\tests\SmtMaterialDashboard.Tests\SmtMaterialDashboard.Tests.csproj -c Release
dotnet publish .\src\SmtMaterialDashboard.App\SmtMaterialDashboard.App.csproj -c Release -r win-x64 --self-contained false
```

本地演示数据启动：

```powershell
dotnet run --project .\src\SmtMaterialDashboard.App\SmtMaterialDashboard.App.csproj -- --demo
```

配置保存在 `%LocalAppData%\SmtMaterialDashboard\settings.json`。
日志保存在 `%LocalAppData%\SmtMaterialDashboard\logs\dashboard-yyyyMMdd.log`。
