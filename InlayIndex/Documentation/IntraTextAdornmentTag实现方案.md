# Intra-text Adornments 实现方案

## 概述

Intra-text Adornments（文本内装饰器）是 Visual Studio 编辑器提供的一种在文本行内嵌入 UI 元素的技术。本方案用于在 C/C++ 数组初始化语法中，为数组索引添加可视化标签。

---

## 技术架构

### 核心组件

```
Array Inline Index/
├── Parsers/
│   ├── RegexArrayParser.cs          # 正则表达式解析器
│   └── DesignatedInitializerDetector.cs  # 初始化器检测器
├── Tags/
│   ├── ArrayIndexTagger.cs          # 标签生成器（核心）
│   ├── ArrayIndexTaggerProvider.cs  # 标签提供者
│   └── IntraTextAdornment.cs        # 装饰器 UI 控件
├── Utils/
│   └── ErrorHandler.cs              # 日志工具
└── Array_Inline_IndexPackage.cs     # VSIX 包入口
```

### 数据流

```
源代码文本
    ↓
RegexArrayParser.ParseArrayInitialization()
    ↓
List<DesignatedInitializer> (解析结果)
    ↓
DesignatedInitializerDetector.Detect()
    ↓
List<ArrayInitialization>
    ↓
ArrayIndexTagger.GetTags()
    ↓
List<TagSpan<IntraTextAdornmentTag>>
    ↓
Visual Studio 编辑器渲染
```

---

## 核心实现

### 1. 解析器 (RegexArrayParser)

**文件**: `Parsers/RegexArrayParser.cs`

**功能**: 使用正则表达式解析 C/C++ 数组初始化列表

**关键方法**:

```csharp
public List<DesignatedInitializer> ParseArrayInitialization(string code, string fileName = "test.cpp")
```

**解析流程**:

1. **匹配数组定义**
   ```csharp
   var arrayPattern = new Regex(
       @"\b(\w+)\s+(\w+)\s*((?:\[\d+\])+)\s*=\s*(\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\});",
       RegexOptions.Multiline | RegexOptions.Compiled
   );
   ```

2. **递归解析初始化列表**
   - `ParseInitList()` - 解析每一层花括号
   - `SplitElements()` - 分割元素（考虑嵌套）
   - `ExtractBraces()` - 提取完整的花括号结构

3. **位置计算**
   - 精确计算每个元素在源代码中的字符偏移量
   - 使用 `StartPosition` 和 `EndPosition` 标记位置

**数据结构**:

```csharp
public class DesignatedInitializer
{
    public List<string> Indices { get; set; }      // 索引路径，如 ["0", "1", "2"]
    public string Value { get; set; }              // 值，如 "42"
    public int StartPosition { get; set; }         // 在源代码中的起始位置
    public int EndPosition { get; set; }           // 在源代码中的结束位置
}
```

---

### 2. 标签生成器 (ArrayIndexTagger)

**文件**: `Tags/ArrayIndexTagger.cs`

**功能**: 为每个数组初始化器生成标签

**核心接口**:

```csharp
public class ArrayIndexTagger : ITagger<IntraTextAdornmentTag>
{
    public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
}
```

**实现步骤**:

1. **获取文本快照**
   ```csharp
   ITextSnapshot snapshot = _buffer.CurrentSnapshot;
   var text = snapshot.GetText();
   ```

2. **解析数组初始化**
   ```csharp
   var initializations = _detector.Detect(text);
   ```

3. **生成标签**
   ```csharp
   foreach (var initializer in initializations)
   {
       var insertionPoint = new SnapshotPoint(snapshot, initializer.StartPosition);
       var tagSpan = new SnapshotSpan(insertionPoint, 0);
       
       var indexText = _detector.FormatDisplayText(initializer);
       var adornment = CreateAdornment(indexText);
       
       var tag = new IntraTextAdornmentTag(adornment, null);
       result.Add(new TagSpan<IntraTextAdornmentTag>(tagSpan, tag));
   }
   ```

**关键属性**:

| 属性 | 类型 | 说明 |
|------|------|------|
| `_buffer` | ITextBuffer | 文本缓冲区引用 |
| `_detector` | DesignatedInitializerDetector | 解析器实例 |
| `_cachedInitializations` | List<ArrayInitialization> | 缓存的解析结果 |
| `_lastParsedVersion` | int | 上次解析的文档版本号 |

---

### 3. 装饰器 UI 控件 (IntraTextAdornment)

**文件**: `Tags/IntraTextAdornment.cs`

**功能**: 创建可视化的标签 UI 元素

**样式属性**:

```csharp
private TextBlock CreateTextBlock(string text)
{
    return new TextBlock
    {
        Text = text,
        
        // 字体属性
        FontFamily = new FontFamily("Consolas"),
        FontSize = 11,
        FontWeight = FontWeights.Bold,
        
        // 颜色属性
        Foreground = new SolidColorBrush(Color.FromRgb(230, 100, 0)), // 橙色
        
        // 背景属性
        Background = new SolidColorBrush(Color.FromArgb(40, 230, 100, 0)), // 半透明橙色背景
        
        // 边距和内边距
        Margin = new Thickness(0, 0, 2, 0),
        Padding = new Thickness(2, 0, 2, 0),
        
        // 其他属性
        Cursor = Cursors.Hand,
        ToolTip = "Array Index: " + text
    };
}
```

**可配置的样式属性**:

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `FontFamily` | FontFamily | "Consolas" | 字体家族 |
| `FontSize` | double | 11 | 字体大小（磅） |
| `FontWeight` | FontWeight | Bold | 字体粗细 |
| `Foreground` | Brush | RGB(230,100,0) | 前景色（文字颜色） |
| `Background` | Brush | ARGB(40,230,100,0) | 背景色（Alpha=40 半透明） |
| `Margin` | Thickness | (0,0,2,0) | 外边距（左，上，右，下） |
| `Padding` | Thickness | (2,0,2,0) | 内边距 |
| `Cursor` | Cursor | Hand | 鼠标悬停时的光标 |
| `ToolTip` | string | - | 工具提示文本 |

---

### 4. 标签提供者 (ArrayIndexTaggerProvider)

**文件**: `Tags/ArrayIndexTaggerProvider.cs`

**功能**: 创建 Tagger 实例

**特性**:

```csharp
[Export(typeof(ITaggerProvider))]
[TagType(typeof(IntraTextAdornmentTag))]
[ContentType("cpp")]
[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
class ArrayIndexTaggerProvider : ITaggerProvider
{
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
}
```

**配置说明**:

| 特性 | 说明 |
|------|------|
| `[ContentType("cpp")]` | 仅对 C++ 文件生效 |
| `[TextViewRole(PrimaryDocument)]` | 仅在主文档视图中生效 |
| `[TagType(typeof(IntraTextAdornmentTag))]` | 指定标签类型 |

---

## 样式配置

### 颜色方案

#### 方案 1: 橙色主题（默认）
```csharp
Foreground = Color.FromRgb(230, 100, 0)      // 橙色文字
Background = Color.FromArgb(40, 230, 100, 0) // 半透明橙色背景
```

#### 方案 2: 蓝色主题
```csharp
Foreground = Color.FromRgb(0, 100, 230)      // 蓝色文字
Background = Color.FromArgb(40, 0, 100, 230) // 半透明蓝色背景
```

#### 方案 3: 绿色主题
```csharp
Foreground = Color.FromRgb(0, 180, 50)       // 绿色文字
Background = Color.FromArgb(40, 0, 180, 50)  // 半透明绿色背景
```

#### 方案 4: 高对比度
```csharp
Foreground = Color.FromRgb(255, 255, 0)      // 黄色文字
Background = Color.FromArgb(80, 0, 0, 0)     // 半透明黑色背景
```

### 字体配置

#### 推荐字体大小
- **小字体**: 9-10pt (适合紧凑布局)
- **中字体**: 11-12pt (默认，适合大多数情况)
- **大字体**: 13-14pt (适合演示或高分辨率屏幕)

#### 字体粗细
- `FontWeights.Normal` - 正常
- `FontWeights.Medium` - 中等
- `FontWeights.SemiBold` - 半粗
- `FontWeights.Bold` - 粗体（默认）

---

## 性能优化

### 1. 缓存机制

```csharp
// 缓存上一次的解析结果
private List<ArrayInitialization> _cachedInitializations;
private int _lastParsedVersion = -1;

// 使用缓存
if (_cachedInitializations != null && _lastParsedVersion == snapshot.Version.VersionNumber)
{
    // 使用缓存，避免重复解析
}
```

### 2. 位置去重

```csharp
var addedPositions = new HashSet<int>();
if (!addedPositions.Add(initializer.StartPosition))
    continue; // 跳过重复位置
```

### 3. 延迟更新

```csharp
private const int UpdateDelay = 500; // 500ms 延迟
private Timer _updateTimer;

// 延迟触发标签更新
_updateTimer.Change(UpdateDelay, Timeout.Infinite);
```

---

## 调试技巧

### 日志输出

```csharp
ErrorHandler.LogDebug($"添加标签：{indexText} 位置：{initializer.StartPosition}");
ErrorHandler.LogWarning($"解析警告：{message}");
ErrorHandler.LogError($"解析错误：{ex.Message}");
```

### 日志文件位置

```
C:\Users\<用户名>\Documents\ArrayInlineIndex\Logs\ArrayInlineIndex_YYYYMMDD.log
```

### 常见调试场景

1. **标签不显示**
   - 检查 `StartPosition` 是否正确
   - 检查 `spans` 范围检查是否过于严格
   - 查看日志中的标签数量

2. **标签位置错误**
   - 验证 `ParseInitList` 中的位置计算
   - 检查 `globalOffset` 是否正确传递
   - 对比源代码实际字符位置

3. **标签重复**
   - 检查 `addedPositions` 去重逻辑
   - 验证缓存机制是否正常工作

---

## 扩展性

### 添加新的样式

1. 修改 `IntraTextAdornment.CreateTextBlock()` 方法
2. 添加配置选项到 `OptionsPage`
3. 在用户设置中保存样式偏好

### 支持新的语言

1. 修改 `ArrayIndexTaggerProvider` 的 `[ContentType]` 特性
2. 调整 `RegexArrayParser` 的正则表达式
3. 测试新语言的解析效果

### 添加交互功能

```csharp
textBlock.MouseLeftButtonDown += (s, e) =>
{
    // 点击标签时的处理逻辑
    MessageBox.Show($"索引：{text}");
};
```

---

## 参考资料

- [Visual Studio Editor SDK](https://docs.microsoft.com/en-us/visualstudio/extensibility/editor)
- [Intra-text Adornments Sample](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/IntraTextAdornments)
- [MEF (Managed Extensibility Framework)](https://docs.microsoft.com/en-us/dotnet/framework/mef/)
- [WPF TextBlock Class](https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.textblock)

---

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.0 | 2026-03-27 | 初始版本，使用 CppAst 解析 |
| 2.0 | 2026-03-27 | 迁移到正则表达式解析 |
| 2.1 | 2026-03-27 | 修复位置计算问题 |

---

## 总结

Intra-text Adornments 方案提供了高度可定制的文本内嵌 UI 能力，适合需要显示复杂格式的场景。通过合理配置样式属性和优化性能，可以实现既美观又高效的数组索引标签显示。

**关键要点**:
1. ✅ 使用正则表达式解析，无需外部依赖
2. ✅ 精确计算字符位置，确保标签正确显示
3. ✅ 使用缓存和去重机制优化性能
4. ✅ 提供多种颜色主题和样式配置选项
5. ✅ 完善的日志系统便于调试
