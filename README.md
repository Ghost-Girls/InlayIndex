# InlayIndex

[![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022%2B-5C2D91.svg)](https://visualstudio.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![VSIX](https://img.shields.io/badge/VSIX-1.0.0-blue.svg)](https://marketplace.visualstudio.com/)

**English: [English](README.en.md)**

---

**InlayIndex** 是一款 Visual Studio 2022/2026 扩展插件，通过在编辑器内直接嵌入**数组索引**、**枚举值**和**结构体字段名**的代码内联提示（Inlay Hint），显著提升 C/C++ 代码可读性。

基于 **ClangSharp**（libclang）实现精确的 AST 级别代码解析，将媲美 CLion 的内联提示体验带到 Visual Studio。

---

## 功能特性

### 数组索引提示
在数组初始化列表中显示 `[0]:`、`[1]:`、`[N]:` 标签，最高支持 4 维数组。

```c
// 之前：很难分辨哪个元素对应哪个索引
int matrix[2][3] = { 1, 2, 3, 4, 5, 6 };

// 之后：索引内联显示
int matrix[2][3] = { [0][0]:1, [0][1]:2, [0][2]:3, [1][0]:4, [1][1]:5, [1][2]:6 };
```

| 维度 | 显示格式 |
|------|---------|
| 一维 | `[0]:` `[1]:` `[2]:` |
| 二维 | `[0][0]:` `[0][1]:` `[1][0]:` |
| 三维 | `[0][0][0]:` `[0][0][1]:` `[0][1][0]:` |
| 四维 | `[0][0][0][0]:` `[0][0][0][1]:` |

### 结构体数组提示
```c
struct Point { int x; int y; };
struct Point pts[3] = {
    [0]:{ .x:1, .y:2 },
    [1]:{ .x:3, .y:4 },
    [2]:{ .x:5, .y:6 }
};
```

### 枚举值提示
在枚举定义处显示 `NAME=value` 标签，同时支持显式赋值和自动计算的枚举值。

```c
// 之前：需要手动数
enum Color { RED, GREEN, BLUE };

// 之后：值内联显示
enum Color { RED=0, GREEN=1, BLUE=2 };
```

### 结构体字段提示
在结构体/联合体初始化时显示 `.fieldName:` 标签，递归支持嵌套结构体。

```c
struct Point { int x; int y; };
struct Point points[2] = {
    [0]:{ .x:1, .y:2 },
    [1]:{ .x:3, .y:4 }
};
```

### 深度颜色编码
数组索引根据嵌套深度自动应用彩虹色方案，多维结构一目了然。

| 深度 | 颜色 |
|------|------|
| 0 | 红色 |
| 1 | 橙色 |
| 2 | 黄色 |
| 3 | 绿色 |
| 4+ | 青色、蓝色、紫色（循环） |

---

## 环境要求

- **Visual Studio**: 2022 (17.0+) 或 2026，Community/Professional/Enterprise
- **操作系统**: Windows 10 / 11 (x64)
- **.NET Framework**: 4.7.2+

---

## 安装方式

### 通过 VSIX 安装
1. 从 [Releases](https://github.com/Ghost-Girls/InlayIndex/releases) 页面下载最新 `.vsix` 文件
2. 双击运行 `.vsix` 文件
3. 按照安装向导完成安装
4. 重启 Visual Studio

### 源码编译
```bash
git clone https://github.com/Ghost-Girls/InlayIndex.git
cd InlayIndex
```

用 Visual Studio 打开 `InlayIndex.slnx`，然后编译发布：
- **Release**：编译 `InlayIndex` 项目 → 在 `bin/Release/` 生成 `.vsix`
- **Debug**：将 `InlayIndex` 设为启动项目 → F5 启动实验实例

---

## 配置选项

打开 **工具** → **选项** → **InlayIndex** 进行自定义：

### 功能开关
| 选项 | 默认 | 说明 |
|------|------|------|
| 数组索引提示 | 开启 | 显示 `[N]:` 格式的数组元素标签 |
| 枚举值提示 | 开启 | 在枚举定义处显示 `NAME=value` |
| 结构体字段提示 | 开启 | 在结构体初始化时显示 `.fieldName:` |

### 样式配置
| 选项 | 默认 | 说明 |
|------|------|------|
| 主题 | 橙色 | 橙色 / 蓝色 / 绿色 / 高对比度 |
| 字体大小 | 11pt | 范围：5-12pt |
| 字体粗细 | 粗体 | 正常 / 中等 / 半粗 / 粗体 |
| 背景透明度 | 15% | 范围：0-100% |
| 深度颜色 | 开启 | 按嵌套层级显示彩虹色 |

### 显示限制
| 选项 | 默认 | 说明 |
|------|------|------|
| 最大维度 | 4 | 最多标注几维数组 |
| 最大元素数 | 1000 | 单个数组标注的最大元素量 |

### 工程感知
| 选项 | 默认 | 说明 |
|------|------|------|
| VisualGDB 探测 | 开启 | 自动从 VisualGDB 项目提取 Include 路径 |
| vcxproj 探测 | 开启 | 自动从标准 vcxproj 提取 Include 路径 |
| CMake 探测 | 关闭 | 自动从 CMake 项目提取配置 |

### 性能配置
| 选项 | 默认 | 说明 |
|------|------|------|
| 防抖延迟 | 500ms | 编辑后等待多久才重新解析（100-2000ms） |

---

## 架构设计

```
         源代码文本
              │
              ▼
  ┌──────────────────────┐
  │    ClangParser       │  ◄── ClangSharp (libclang) AST 解析
  │  (CXTranslationUnit) │
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │  InlayHintGenerator  │  ◄── 提取：ArrayInfo, EnumInfo, StructInfo
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │   InlayHintManager   │  ◄── 缓存与管理 List<InlayHintTag>
  └──────────┬───────────┘
             │ TagsUpdated 事件
             ▼
  ┌──────────────────────┐
  │  InlayHintTagger     │  ◄── ITagger<IntraTextAdornmentTag>
  │  (GetTags)           │       创建 WPF 装饰器元素
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │  Visual Studio       │
  │  编辑器管道          │  ◄── 在文本视图中内联渲染
  └──────────────────────┘
```

### 核心组件

| 组件 | 文件 | 职责 |
|------|------|------|
| **ClangParser** | `Parser/ClangParser.cs` | 基于 ClangSharp AST 解析 C/C++ 代码；双重解析策略（unsaved file + 临时文件兜底） |
| **InlayHintGenerator** | `Parser/InlayHintGenerator.cs` | 将 AST 解析结果转换为带样式属性的 `InlayHintTag` 列表 |
| **InlayHintManager** | `Adornment/InlayHintManager.cs` | 线程安全的标签缓存，提供 `TagsUpdated` 事件 |
| **InlayHintTagger** | `Adornment/InlayHintTagger.cs` | `ITagger<IntraTextAdornmentTag>` 实现；创建 WPF UI 元素 |
| **InlayIndexViewCreationListener** | `Adornment/InlayIndexViewCreationListener.cs` | 视图生命周期管理；文本变化防抖（500ms）；触发重新解析 |
| **VisualGDBConfigDetector** | `Parser/VisualGDBConfigDetector.cs` | 自动从 VisualGDB/vcxproj/CMake 探测 Include 路径 |
| **InlayIndexOptionsPage** | `Options/InlayIndexOptionsPage.cs` | VS 选项对话框集成 |

---

## 开发指南

### 调试模式
`InlayIndex` 项目会启动 **Visual Studio 实验实例**（`devenv.exe /rootsuffix Exp`）用于调试，无需安装 `.vsix`：

1. 将 `InlayIndex` 设为启动项目
2. 按 **F5**
3. 在实验实例中打开一个 C/C++ 文件
4. 在 `InlayHintTagger.cs` 或 `ClangParser.cs` 中设置断点

### 日志系统
日志输出到：
```
InlayIndex_YYYYMMDD_HHMMSS.log
```

四种日志类别：`[解析]`（Parse）、`[渲染]`（Render）、`[标签]`（Tag）、`[DEBUG]`（调试）。

### 技术栈
- **语言**: C# 8.0 (.NET Framework 4.7.2)
- **框架**: VSIX Extensibility, MEF（Managed Extensibility Framework）
- **渲染**: `IntraTextAdornmentTag`，WPF（`TextBlock` / `Border`）
- **解析**: ClangSharp 16 / libclang 16
- **构建**: Microsoft.VSSDK.BuildTools 17.14+

### NuGet 依赖
| 包名 | 版本 |
|------|------|
| `ClangSharp` | 16.0.0 |
| `ClangSharp.Interop` | 16.0.0 |
| `libclang.runtime.win-x64` | 16.0.6 |
| `Microsoft.VisualStudio.SDK` | 17.0.32112.339 |
| `Microsoft.VSSDK.BuildTools` | 17.14.2120 |

---

## 已知问题

### 滚动导致标签消失
快速滚动编辑器时（尤其是点击滚动条空白区瞬跳或快速拖拽滑块），部分 `IntraTextAdornmentTag` 标签可能消失。这是 VS 格式化引擎在密集装饰（每文件 91+ 个标签）场景下的缓存优化限制。

**状态**：根因分析已完成。`IntraTextAdornmentTag` API 设计用于稀疏装饰场景。长期方案考虑：
- 迁移到 `IAdornmentLayer` 覆盖层渲染（标签浮在文本上方，不能撑开文本）
- 等待 `IInlayHintBroker` API 对 C/C++ 的成熟支持

详见 [滚动消失问题分析记录](InlayIndex/Documentation/IntraTextAdornmentTag%E6%BB%9A%E5%8A%A8%E6%B6%88%E5%A4%B1%E9%97%AE%E9%A2%98%E5%88%86%E6%9E%90%E4%B8%8E%E8%A7%A3%E5%86%B3%E8%AE%B0%E5%BD%95.md)

---

## 相关文档

完整文档在 [Documentation](InlayIndex/Documentation/) 目录：

| 文档 | 说明 |
|------|------|
| [需求文档](InlayIndex/Documentation/%E6%95%B0%E7%BB%84%E7%B4%A2%E5%BC%95%20-%20%E6%9E%9A%E4%B8%BE%E5%80%BC%20Inlay%20Hint%20VSIX%20%E6%8F%92%E4%BB%B6%20%E9%9C%80%E6%B1%82%E6%96%87%E6%A1%A3.md) | 完整需求规格说明书 |
| [实现方案](InlayIndex/Documentation/IntraTextAdornmentTag%E5%AE%9E%E7%8E%B0%E6%96%B9%E6%A1%88.md) | IntraTextAdornmentTag + ClangSharp 技术设计 |
| [滚动消失问题](InlayIndex/Documentation/IntraTextAdornmentTag%E6%BB%9A%E5%8A%A8%E6%B6%88%E5%A4%B1%E9%97%AE%E9%A2%98%E5%88%86%E6%9E%90%E4%B8%8E%E8%A7%A3%E5%86%B3%E8%AE%B0%E5%BD%95.md) | 滚动导致标签丢失的根因分析 |
| [标签跟踪问题](InlayIndex/Documentation/%E6%A0%87%E7%AD%BE%E8%B7%9F%E8%B8%AA%E9%97%AE%E9%A2%98%E5%88%86%E6%9E%90%E5%92%8C%E8%A7%A3%E5%86%B3%E6%96%B9%E6%A1%88.md) | 编辑后标签位置偏移的修复方案 |
| [头文件污染修复](InlayIndex/Documentation/%E7%B3%BB%E7%BB%9F%E5%A4%B4%E6%96%87%E4%BB%B6%E6%9E%9A%E4%B8%BE%E6%B1%A1%E6%9F%93%E4%BF%AE%E5%A4%8D%E6%96%B9%E6%A1%88.md) | 系统头文件枚举污染修复 |
| [VisualGDB 配置](InlayIndex/Documentation/VisualGDB%E9%85%8D%E7%BD%AE%E8%87%AA%E5%8A%A8%E6%8E%A2%E6%B5%8B%E6%96%B9%E6%A1%88.md) | 嵌入式项目 Include 路径自动探测方案 |

---

## 贡献指南

欢迎贡献代码和提交 Issue！

### 快速上手
1. 阅读[需求文档](InlayIndex/Documentation/%E6%95%B0%E7%BB%84%E7%B4%A2%E5%BC%95%20-%20%E6%9E%9A%E4%B8%BE%E5%80%BC%20Inlay%20Hint%20VSIX%20%E6%8F%92%E4%BB%B6%20%E9%9C%80%E6%B1%82%E6%96%87%E6%A1%A3.md)了解功能概览
2. 阅读[实现方案](InlayIndex/Documentation/IntraTextAdornmentTag%E5%AE%9E%E7%8E%B0%E6%96%B9%E6%A1%88.md)了解架构设计
3. 查看 [Bug 分析文档](InlayIndex/Documentation/) 了解已解决问题和已知问题
4. 使用实验实例进行本地构建和调试

---

## 许可证

[MIT](LICENSE)

**发布者**: Ghost-Girls

---

*为重视代码可读性的 C/C++ 开发者而做。*