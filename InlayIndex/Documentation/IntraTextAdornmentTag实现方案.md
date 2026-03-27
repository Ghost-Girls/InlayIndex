# Intra-text Adornments 实现方案

## 概述

Intra-text Adornments（文本内装饰器）是 Visual Studio 编辑器提供的一种在文本行内嵌入 UI 元素的技术。本方案用于在 C/C++ 数组初始化语法中，为数组索引和枚举值添加可视化标签。

---

## 技术架构

### 核心组件

```
Array Inline Index/
├── Parsers/
│   ├── ClangAstParser.cs              # ClangSharp AST 解析器
│   ├── ArrayInitializerParser.cs      # 数组初始化解析器
│   └── EnumDefinitionParser.cs        # 枚举定义解析器
├── Tags/
│   ├── ArrayIndexTagger.cs            # 数组索引标签生成器（核心）
│   ├── EnumValueTagger.cs             # 枚举值标签生成器
│   ├── ArrayIndexTaggerProvider.cs    # 标签提供者
│   └── IntraTextAdornment.cs          # 装饰器 UI 控件
├── Models/
│   ├── ArrayInitialization.cs         # 数组初始化数据结构
│   ├── EnumDefinition.cs              # 枚举定义数据结构
│   └── SourceLocation.cs              # 源代码位置映射
├── Utils/
│   └── ErrorHandler.cs                # 日志工具
└── InlayIndexPackage.cs               # VSIX 包入口
```

### 数据流

```
源代码文本
    ↓
ClangAstParser.Parse() - 使用 ClangSharp 解析为 AST
    ↓
CXTranslationUnit (AST 抽象语法树)
    ↓
ArrayInitializerParser.ExtractArrayInitializations()
EnumDefinitionParser.ExtractEnumDefinitions()
    ↓
List<ArrayInitialization>
List<EnumDefinition>
    ↓
ArrayIndexTagger.GetTags()
EnumValueTagger.GetTags()
    ↓
List<TagSpan<IntraTextAdornmentTag>>
    ↓
Visual Studio 编辑器渲染
```

---

## 核心实现

### 1. ClangSharp AST 解析器 (ClangAstParser)

**文件**: `Parsers/ClangAstParser.cs`

**功能**: 使用 ClangSharp (libclang) 解析 C/C++ 代码为抽象语法树 (AST)

**关键方法**:

```csharp
public CXTranslationUnit ParseFile(string filePath, string[] clangArgs = null)
```

**解析流程**:

1. **初始化 Clang 索引**
   ```csharp
   var index = CXIndex.Create();
   ```

2. **解析文件为 AST**
   ```csharp
   var translationUnit = index.ParseTranslationUnit(
       filePath,
       clangArgs,
       CXTranslationUnit_Flags.DetailedPreprocessingRecord
   );
   ```

3. **遍历 AST 提取节点**
   - 访问数组声明节点 (CX_DeclKind.CX_Decl_Var)
   - 访问枚举声明节点 (CX_DeclKind.CX_Decl_Enum)
   - 提取初始化列表和位置信息

**位置映射**:

```csharp
public class SourceLocation
{
    public string FilePath { get; set; }
    public int Line { get; set; }        // 行号 (从 1 开始)
    public int Column { get; set; }      // 列号 (从 1 开始)
    public int Offset { get; set; }      // 字符偏移量
}
```

**优势**:
- 准确的语义分析，理解代码结构
- 支持 C/C++ 所有标准语法
- 处理模板、宏、预处理器
- 支持 C++11/14/17/20/23 新特性

---

### 2. 数组初始化解析器 (ArrayInitializerParser)

**文件**: `Parsers/ArrayInitializerParser.cs`

**功能**: 从 AST 中提取数组初始化信息

**关键方法**:

```csharp
public List<ArrayInitialization> ExtractArrayInitializations(CXTranslationUnit translationUnit)
```

**数据结构**:

```csharp
public class ArrayInitialization
{
    public string VariableName { get; set; }     // 变量名
    public List<int> Dimensions { get; set; }    // 各维度大小，如 [2, 3, 4]
    public int DimensionCount { get; set; }      // 维度数
    public string ElementType { get; set; }      // 元素类型
    public bool IsStructArray { get; set; }      // 是否为结构体数组
    public List<InitElement> Elements { get; set; } // 初始化元素列表
    public SourceLocation Location { get; set; } // 源代码位置
}

public class InitElement
{
    public List<int> Indices { get; set; }       // 索引路径，如 [0, 1, 2]
    public string Value { get; set; }            // 值，如 "42"
    public List<string> FieldNames { get; set; } // 结构体字段名，如 [".x", ".y"]
    public SourceLocation Location { get; set; } // 元素位置
}
```

**解析逻辑**:

1. **识别数组声明**
   - 遍历 AST 找到所有数组变量声明
   - 提取维度信息和元素类型

2. **解析初始化列表**
   - 访问 `CX_InitializerListExpr` 节点
   - 递归处理多层嵌套初始化
   - 提取每个元素的值和位置

3. **处理结构体数组**
   - 识别结构体类型
   - 提取字段名和字段值
   - 生成 `.x:`、`.y:` 等字段标签

4. **位置计算**
   - 从 AST 节点的 SourceLocation 获取精确位置
   - 转换为字符偏移量用于标签插入

---

### 3. 枚举定义解析器 (EnumDefinitionParser)

**文件**: `Parsers/EnumDefinitionParser.cs`

**功能**: 从 AST 中提取枚举定义和枚举值

**关键方法**:

```csharp
public List<EnumDefinition> ExtractEnumDefinitions(CXTranslationUnit translationUnit)
```

**数据结构**:

```csharp
public class EnumDefinition
{
    public string EnumName { get; set; }         // 枚举类型名
    public List<EnumMember> Members { get; set; } // 枚举成员
    public SourceLocation Location { get; set; } // 定义位置
}

public class EnumMember
{
    public string Name { get; set; }             // 成员名，如 "RED"
    public long Value { get; set; }              // 枚举值，如 0
    public SourceLocation Location { get; set; } // 成员位置
}
```

**解析逻辑**:

1. **识别枚举声明**
   - 遍历 AST 找到所有枚举声明
   - 提取枚举类型名

2. **计算枚举值**
   - 显式指定的值直接使用
   - 未指定的值从 0 开始递增
   - 处理混合指定值的情况

3. **位置映射**
   - 记录每个枚举成员的源代码位置
   - 用于在定义处插入标签

---

### 4. 标签生成器 (ArrayIndexTagger / EnumValueTagger)

**文件**: `Tags/ArrayIndexTagger.cs` / `Tags/EnumValueTagger.cs`

**功能**: 为数组初始化和枚举定义生成标签

**核心接口**:

```csharp
public class ArrayIndexTagger : ITagger<IntraTextAdornmentTag>
{
    public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
}

public class EnumValueTagger : ITagger<IntraTextAdornmentTag>
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

2. **解析代码**
   ```csharp
   var arrays = _arrayParser.Parse(text);
   var enums = _enumParser.Parse(text);
   ```

3. **生成数组索引标签**
   ```csharp
   foreach (var element in array.Elements)
   {
       var insertionPoint = new SnapshotPoint(snapshot, element.Location.Offset);
       var tagSpan = new SnapshotSpan(insertionPoint, 0);
       
       var indexText = FormatIndexText(element.Indices); // 如 "[0][1][2]:"
       var adornment = CreateAdornment(indexText);
       
       var tag = new IntraTextAdornmentTag(adornment, null);
       result.Add(new TagSpan<IntraTextAdornmentTag>(tagSpan, tag));
   }
   ```

4. **生成枚举值标签**
   ```csharp
   foreach (var member in enumDef.Members)
   {
       var insertionPoint = new SnapshotPoint(snapshot, member.Location.Offset + member.Name.Length);
       var tagSpan = new SnapshotSpan(insertionPoint, 0);
       
       var valueText = $"={member.Value}";
       var adornment = CreateAdornment(valueText);
       
       result.Add(new TagSpan<IntraTextAdornmentTag>(tagSpan, tag));
   }
   ```

**动态更新机制**:

```csharp
// 监听文本变化事件
_buffer.Changed += (sender, e) =>
{
    // 标记缓存失效
    _cacheValid = false;
    
    // 触发标签更新
    TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(e.NewSpan));
};
```

---

### 5. 装饰器 UI 控件 (IntraTextAdornment)

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
        Background = new SolidColorBrush(Color.FromArgb(40, 230, 100, 0)), // 半透明背景
        
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
| `Margin` | Thickness | (0,0,2,0) | 外边距 |
| `Padding` | Thickness | (2,0,2,0) | 内边距 |

---

### 6. 标签提供者 (ArrayIndexTaggerProvider)

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

## ClangSharp 集成

### 安装

通过 NuGet 包管理器安装：

```powershell
Install-Package ClangSharp
```

### 使用示例

```csharp
using ClangSharp;

// 创建 Clang 索引
var index = CXIndex.Create();

// 解析文件
var translationUnit = index.ParseTranslationUnit(
    filePath: "test.cpp",
    commandLineArgs: new[] { "-std=c++20" },
    flags: CXTranslationUnit_Flags.DetailedPreprocessingRecord
);

// 遍历 AST
translationUnit.TranslationUnitDecl.VisitChildren((CXCursor cursor, CXCursor parent) =>
{
    if (cursor.Kind == CXCursorKind.CXCursor_VarDecl)
    {
        // 处理变量声明
        var arrayDecl = cursor;
    }
    else if (cursor.Kind == CXCursorKind.CXCursor_EnumDecl)
    {
        // 处理枚举声明
        var enumDecl = cursor;
    }
    
    return CXChildVisitResult.CXChildVisit_Continue;
});
```

### 位置映射

```csharp
// 获取 AST 节点的源代码位置
var sourceRange = cursor.Extent;
var startLoc = sourceRange.Start;
var endLoc = sourceRange.End;

// 转换为行号/列号
startLoc.GetSpellingLocation(out var file, out var line, out var column, out var offset);

// 转换为字符偏移量（用于标签插入）
int charOffset = ConvertToCharOffset(file.Name, line, column);
```

---

## 调试技巧

### 日志输出

```csharp
ErrorHandler.LogDebug($"添加标签：{indexText} 位置：{element.Location.Offset}");
ErrorHandler.LogWarning($"解析警告：{message}");
ErrorHandler.LogError($"解析错误：{ex.Message}");
```

### 日志文件位置

```
C:\Users\<用户名>\Documents\ArrayInlineIndex\Logs\ArrayInlineIndex_YYYYMMDD.log
```

### 常见调试场景

1. **标签不显示**
   - 检查 AST 节点是否正确提取
   - 检查位置映射是否准确
   - 查看日志中的标签数量

2. **标签位置错误**
   - 验证 AST 节点的 SourceLocation
   - 检查字符偏移量转换是否正确
   - 对比源代码实际字符位置

3. **解析失败**
   - 检查 ClangSharp 版本兼容性
   - 验证编译参数是否正确
   - 查看 Clang 诊断信息

---

## 扩展性

### 添加新的标签类型

1. 创建新的 Tagger 类（如 `StructFieldTagger`）
2. 实现 `ITagger<IntraTextAdornmentTag>` 接口
3. 在 Provider 中注册

### 支持新的语言

1. 修改 Provider 的 `[ContentType]` 特性
2. 调整 ClangSharp 的解析参数
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
- [ClangSharp Documentation](https://github.com/SimonCropp/ClangSharp)
- [libclang API Reference](https://clang.llvm.org/doxygen/group__CINDEX.html)
- [MEF (Managed Extensibility Framework)](https://docs.microsoft.com/en-us/dotnet/framework/mef/)
- [WPF TextBlock Class](https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.textblock)

---

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.0 | 2026-03-27 | 初始版本，使用正则表达式解析 |
| 2.0 | 2026-03-28 | 迁移到 ClangSharp AST 解析，支持 C/C++ 标准语法 |

---

## 总结

Intra-text Adornments 方案结合 ClangSharp AST 解析提供了准确、可扩展的标签显示能力。通过精准的语义分析和位置映射，可以实现高质量的数组索引和枚举值提示。

**关键要点**:
1. ✅ 使用 ClangSharp AST 解析，语义准确
2. ✅ 支持 C/C++ 所有标准语法和新特性
3. ✅ 精准的位置映射，标签显示正确
4. ✅ 提供多种颜色主题和样式配置选项
5. ✅ 动态更新机制，实时响应代码编辑
6. ✅ 完善的日志系统便于调试
