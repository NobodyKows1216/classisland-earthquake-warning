# ClassIsland 地震预警

一个挂在 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 2.x 主界面上的地震预警小组件。它实时读取中国地震台网（CENC）的地震预警与最新地震信息；有预警时整行变成红色，显示震级、震中、预估烈度、发震时刻和避险提示，并可调用 ClassIsland 的语音服务播报。

![预警状态示例](docs/preview-alert.png)

## 功能

- **实时地震预警**：默认每 2 秒轮询一次 CENC 地震预警（数据由 [Wolfx Open API](https://api.wolfx.jp) 转发）。
- **醒目的预警状态**：发震后一定时间内（默认 120 秒，可调）整行显示红色渐变，包含震级、震中、报数、预估最大烈度、深度、发震时刻。
- **横波倒计时**：在设置中填写教室经纬度后，按震中距估算 S 波到达时间，显示“横波 N 秒后到达（约 K km）”；不填则显示“发震已过 N 秒”。
- **最新与次近地震**：没有预警时显示中国地震台网最新地震的震级、地点、时间、深度/烈度，右侧显示再上一次地震的简要信息。
- **语音播报**：新预警首次出现时，通过 ClassIsland 的 TTS 服务播报“地震预警，某地发生 X 级地震，预计最大烈度 Y，请注意避险”。可在设置中关闭或测试。
- **断线重试**：网络异常时保留最后一次数据，并显示“更新失败”，自动重试。
- 组件按 ClassIsland 主界面行高（约 40 px）设计，两行紧凑排版，不会撑高主界面。

## 安装

### 方式一：插件包

从本仓库的 Releases 下载 `ClassIsland.EarthquakeWarning.cipx`，在 ClassIsland 的「应用设置 → 插件」中安装，然后重启应用。

### 方式二：手动复制

把构建输出复制到 ClassIsland 的插件目录：

```text
<ClassIsland 数据目录>\Plugins\classisland.earthquake.warning\
├── manifest.yml
├── icon.png
├── README.md
└── ClassIsland.EarthquakeWarning.dll
```

重启 ClassIsland，打开「应用设置 → 组件」，在组件库中找到「地震预警」，把它拖到主界面行即可。

组件 Id 为 `e7c4f0a2-0d7b-4a2e-9f5e-6b1c8d3a9f21`，也可以直接编辑 `Config/ComponentLayouts/Default.json` 把它加入某一行的 `Children`。

## 设置

- 刷新间隔（1–10 秒，默认 2 秒）
- 预警有效时长（30–600 秒，默认 120 秒）
- 语音播报开关
- 是否显示“次近地震”
- 所在位置的纬度 / 经度（可选，用于横波到达倒计时）
- 测试语音

## 数据与免责声明

- 数据来源：中国地震台网（CENC），通过 [Wolfx Open API](https://api.wolfx.jp) 公开接口获取。
- 本组件仅用于班级信息展示，**不能替代官方地震预警终端**。地震发生时请以官方发布和学校应急预案为准。
- 网络请求仅访问 `api.wolfx.jp`，不上传任何本机信息。

## 构建

需要 .NET 8 SDK：

```bash
dotnet build -c Release
```

生成插件包 `.cipx`：把 `manifest.yml`、`icon.png`、`README.md` 和 `bin/Release/net8.0/ClassIsland.EarthquakeWarning.dll` 放在同一目录，压缩为 zip 后把后缀改为 `.cipx`（安装包本质上是一个 zip）。
