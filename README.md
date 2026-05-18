# AI 智剪 Windows 版

> 海灵智剪 Windows 原生客户端 — 集图片生成、视频生成于一体的 AI 创意工具

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

本仓库是 [AI-zhijian (macOS 版)](https://github.com/ShadyUnderLight/AI-zhijian) 的 Windows 移植版本，使用 .NET 8.0 + WPF 重写。

---

## ✨ 功能

| 模块 | 功能 | 后端 |
|------|------|------|
| 🖼️ 图片生成 | GPT-Image-2 文生图，支持渠道/画幅/分辨率/质量 | `gpt-image-2/text-to-image` |
| 🍌 Banana 图片 | Gemini 图生图/文生图，支持参考图+提示词 | `media/banana` |
| 🎬 Seedance 视频 | Seedance 2.0 视频生成，支持参考图/首尾帧/音频 | `seedance20/submit` |
| 🎥 Wan 视频 | Wan2.2 图生视频 | `media/wan2-image-to-video` |
| 🌐 Veo 视频 | 5 种模式 (text/image/reference/start_end/extend)，4 种渠道 | `veo-video/submit` |
| 🤖 Grok 视频 | 5 种模式，支持 6-30s 时长，3 种渠道 | `grok-video/submit` |
| ⏳ 任务队列 | 实时显示活跃任务，自动轮询状态，并发控制 | — |

## 🖥️ 界面

```
┌──────────────────────────────────────────────────┐
│  AI 智剪 — 用户名 (角色)              [设置] [退出]│
├───────────┬──────────────────────────────────────┤
│  📊 仪表盘 │                                      │
│  🖼️ 图片   │    功能内容区域                        │
│  🎬 Seedance│                                    │
│  🍌 Banana │                                      │
│  🎥 Wan    │                                      │
│  🌐 Veo    │                                      │
│  🤖 Grok   │                                      │
│  ────────│                                      │
│  ⏳ 任务   │                                      │
│  🎨 画廊   │                                      │
└───────────┴──────────────────────────────────────┘
```

## 🏗️ 技术栈

| 层 | 技术 | macOS 原版 |
|---|---|---|
| UI | WPF (XAML) | SwiftUI |
| 网络 | HttpClient + CookieContainer | URLSession |
| 会话 | Cookie 自动管理 | HTTPCookieStorage |
| 凭据存储 | Windows Credential Manager | macOS Keychain |
| 构建 | MSBuild / dotnet CLI | XcodeGen + xcodebuild |
| 最低系统 | Windows 10 (1809+) / .NET 8.0 | macOS 14.0 |
| 语言 | C# 12 | Swift 6.0 |

## 📦 项目结构

```
AIZhijian/
├── AIZhijian.sln                  # 解决方案
└── AIZhijian/
    ├── AIZhijian.csproj           # 项目文件
    ├── App.xaml / App.xaml.cs     # 应用入口
    ├── Models/
    │   ├── DataModels.cs          # API 数据模型 (DTO)
    │   ├── GenerationModels.cs    # 生成任务模型
    │   ├── WorkflowModels.cs      # 工作流 DAG 模型
    │   └── PresetModels.cs        # 预设模型
    ├── Services/
    │   ├── ApiService.cs          # API 网络层 (11 个接口)
    │   ├── CredentialStore.cs     # Windows 凭据管理器
    │   ├── GenerationQueueStore.cs # 任务队列管理
    │   ├── GenerationTaskExecutor.cs # 任务提交与轮询
    │   ├── WorksStore.cs          # 作品画廊
    │   └── VeoRules.cs            # Veo 规则引擎
    ├── ViewModels/
    │   └── MainViewModel.cs       # 主窗口 ViewModel
    ├── Views/
    │   ├── MainWindow.xaml/.cs     # 主窗口 (侧边栏导航)
    │   ├── LoginPage.xaml/.cs      # 登录
    │   ├── DashboardPage.xaml/.cs  # 仪表盘
    │   ├── ImageGenPage.xaml/.cs   # 图片生成
    │   ├── BananaPage.xaml/.cs     # Banana 图片
    │   ├── SeedancePage.xaml/.cs   # Seedance 视频
    │   ├── WanPage.xaml/.cs        # Wan 视频
    │   ├── VeoPage.xaml/.cs        # Veo 视频
    │   ├── GrokPage.xaml/.cs       # Grok 视频
    │   ├── TaskListPage.xaml/.cs   # 任务队列
    │   ├── SettingsPage.xaml/.cs   # 设置
    │   └── WorksGalleryPage.xaml/.cs # 作品画廊
    ├── Converters/
    │   └── Converters.cs          # 值转换器
    └── Resources/
        └── Styles.xaml            # 全局样式
```

## 🚀 编译运行

### 环境要求

- Windows 10 1809+ 或 Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (推荐) 或 VS Code

### 编译

```powershell
# 1. 进入项目目录
cd AIZhijian

# 2. 还原依赖
dotnet restore

# 3. 编译
dotnet build -c Release

# 4. 运行
dotnet run --project AIZhijian
```

### 或者用 Visual Studio

```
打开 AIZhijian.sln → 按 F5 运行
```

## 🔐 安全说明

- 密码使用 **Windows Credential Manager** 加密存储，不写入注册表或明文文件
- 取消"记住登录"或退出登录会清除已保存的凭据
- API 通信使用 HTTP，生产环境建议部署 HTTPS

## macOS 版 vs Windows 版 对比

| 功能 | macOS (SwiftUI) | Windows (WPF) |
|---|---|---|
| 图片生成 (GPT-Image-2) | ✔ | ✔ |
| Banana 图生图 | ✔ | ✔ |
| Seedance 视频 | ✔ | ✔ |
| Wan 视频 | ✔ | ✔ |
| Veo 视频 (5 模式) | ✔ | ✔ |
| Grok 视频 | ✔ | ✔ |
| 任务队列 + 轮询 | ✔ | ✔ |
| 登录记住密码 | ✔ (Keychain) | ✔ (Credential Manager) |
| API 服务器切换 | ✔ | ✔ |
| 作品画廊 | ✔ | ✔ |
| 工作流画布编辑器 | ✔ | 待实现 |

## 📝 后续计划

- [ ] 工作流画布编辑器
- [ ] 视频本地播放 (MediaElement)
- [ ] 批量生成队列分组
- [ ] 任务本地缓存持久化
- [ ] 一键打包 exe

## 📄 协议

MIT License — 基于原项目 [ShadyUnderLight/AI-zhijian](https://github.com/ShadyUnderLight/AI-zhijian)
