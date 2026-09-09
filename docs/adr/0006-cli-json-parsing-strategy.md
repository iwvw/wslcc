# ADR-0006：CLI JSON 输出解析采用多候选字段容错策略

- 状态：Accepted

## 背景

wslc CLI 输出结构为 Docker 风格（`--format json` 每行一个 JSON 对象；空结果无输出或空表）。预览版曾改变输出字段（如 2.9.9 起 `volume list --format json` 字段从两项扩展为 Docker 十项），表明字段名不稳定。当前环境部分命令（container/volume list）报 E_UNEXPECTED，无法在本地捕获真实字段样本。

## 决策

- 解析器（`WslcRunner.RunJsonLinesAsync`）：逐行读取、`{}` 起始、JsonDocument 解析，跳过非法行，返回 JsonElement 流。
- 字段读取使用多候选名称：如容器名 `Names`、ID `ID`/`Id`、镜像等依次尝试；值缺失时回退默认（"-"、""、<none>）。
- 错误判定：合并 stdout/stderr，识别中文"错误代码： X"或英文"Error code: X"（兼容"灾难性故障"），映射为 `WslcCliException.WslcErrorCode`。
- 流式命令（pull/run/logs）按行回调进度，不解析具体进度结构，原样展示给用户，规避结构变化风险。

## 后果

- 正面：字段演进不破坏整体列表；错误码可读；进度展示稳键。
- 负面：部分字段（如尺寸）为文本格式，展示精度有限；后续可据真实样本精化。