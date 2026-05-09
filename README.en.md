# InlayIndex

[![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022%2B-5C2D91.svg)](https://visualstudio.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![VSIX](https://img.shields.io/badge/VSIX-1.0.0-blue.svg)](https://marketplace.visualstudio.com/)

**Read in other languages: [English](README.en.md), [中文](README.md)**

---

**InlayIndex** is a Visual Studio 2022/2026 extension that enhances C/C++ code readability by providing inline hints for **array indices**, **enum values**, and **struct field names** directly within the editor.

Powered by **ClangSharp** (libclang) for precise AST-level code parsing, it brings a code-reading experience comparable to CLion's inlay hints to Visual Studio.

---

## Features

### Array Index Hints
Display `[0]:`, `[1]:`, `[N]:` labels for each element in array initializers, supporting up to 4 dimensions.

```c
// Before: hard to tell which element is which
int matrix[2][3] = { 1, 2, 3, 4, 5, 6 };

// After: indices are displayed inline
int matrix[2][3] = { [0][0]:1, [0][1]:2, [0][2]:3, [1][0]:4, [1][1]:5, [1][2]:6 };
```

| Dimension | Display |
|-----------|---------|
| 1D | `[0]:` `[1]:` `[2]:` |
| 2D | `[0][0]:` `[0][1]:` `[1][0]:` |
| 3D | `[0][0][0]:` `[0][0][1]:` `[0][1][0]:` |
| 4D | `[0][0][0][0]:` `[0][0][0][1]:` |

### Struct Array Hints
```c
struct Point { int x; int y; };
struct Point pts[3] = {
    [0]:{ .x:1, .y:2 },
    [1]:{ .x:3, .y:4 },
    [2]:{ .x:5, .y:6 }
};
```

### Enum Value Hints
Display `NAME=value` labels at enum definitions, showing both explicitly assigned and automatically computed values.

```c
// Before: need to manually count
enum Color { RED, GREEN, BLUE };

// After: values are displayed inline
enum Color { RED=0, GREEN=1, BLUE=2 };
```

### Struct Field Hints
Display `.fieldName:` labels within struct/unions initializers, recursively for nested structures.

```c
struct Point { int x; int y; };
struct Point points[2] = {
    [0]:{ .x:1, .y:2 },
    [1]:{ .x:3, .y:4 }
};
```

### Depth-based Color Coding
Array indices are color-coded by nesting depth using a rainbow scheme, making multi-dimensional structures visually distinguishable.

| Depth | Color |
|-------|-------|
| 0 | Red |
| 1 | Orange |
| 2 | Yellow |
| 3 | Green |
| 4+ | Cyan, Blue, Purple (cycling) |

---

## Requirements

- **Visual Studio**: 2022 (17.0+) or 2026, Community/Professional/Enterprise
- **OS**: Windows 10 / 11 (x64)
- **.NET Framework**: 4.7.2+

---

## Installation

### Via VSIX (Manual)
1. Download the latest `.vsix` from the [Releases](https://github.com/Ghost-Girls/InlayIndex/releases) page
2. Double-click the `.vsix` file
3. Follow the installation wizard
4. Restart Visual Studio

### Build from Source
```bash
git clone https://github.com/Ghost-Girls/InlayIndex.git
cd InlayIndex
```

Open `InlayIndex.slnx` in Visual Studio, then build and deploy:
- **Release**: Build the `InlayIndex` project → produces `.vsix` in `bin/Release/`
- **Debug**: Set `InlayIndex` as startup project → F5 launches Experimental Instance

---

## Configuration

Navigate to **Tools** → **Options** → **InlayIndex** to customize:

### Feature Toggles
| Option | Default | Description |
|--------|---------|-------------|
| Array Index Hints | On | Show `[N]:` labels for array elements |
| Enum Value Hints | On | Show `NAME=value` at enum definitions |
| Struct Field Hints | On | Show `.fieldName:` in struct initializers |

### Style
| Option | Default | Description |
|--------|---------|-------------|
| Theme | Orange | Orange / Blue / Green / High Contrast |
| Font Size | 11pt | Range: 5-12pt |
| Font Weight | Bold | Normal / Medium / SemiBold / Bold |
| Background Opacity | 15% | Range: 0-100% |
| Depth Colors | On | Rainbow color by nesting depth |

### Display Limits
| Option | Default | Description |
|--------|---------|-------------|
| Max Dimensions | 4 | Maximum array dimensions to annotate |
| Max Elements | 1000 | Maximum elements per array |

### Project Awareness
| Option | Default | Description |
|--------|---------|-------------|
| VisualGDB Detection | On | Auto-detect include paths from VisualGDB projects |
| vcxproj Detection | On | Auto-detect include paths from standard vcxproj |
| CMake Detection | Off | Auto-detect includes from CMake projects |

### Performance
| Option | Default | Description |
|--------|---------|-------------|
| Debounce Delay | 500ms | Delay after editing before re-parsing (100-2000ms) |

---

## Architecture

```
         Source Code
              │
              ▼
  ┌──────────────────────┐
  │    ClangParser       │  ◄── ClangSharp (libclang) AST
  │  (CXTranslationUnit) │
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │  InlayHintGenerator  │  ◄── Extract: ArrayInfo, EnumInfo, StructInfo
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │   InlayHintManager   │  ◄── Cache & manage List<InlayHintTag>
  └──────────┬───────────┘
             │ TagsUpdated event
             ▼
  ┌──────────────────────┐
  │  InlayHintTagger     │  ◄── ITagger<IntraTextAdornmentTag>
  │  (GetTags)           │       Create WPF adornments
  └──────────┬───────────┘
             │
             ▼
  ┌──────────────────────┐
  │  Visual Studio       │
  │  Editor Pipeline     │  ◄── Render inline in text view
  └──────────────────────┘
```

### Key Components

| Component | File | Responsibility |
|-----------|------|----------------|
| **ClangParser** | `Parser/ClangParser.cs` | Parse C/C++ code via ClangSharp AST; dual-mode parsing (unsaved file + temp file fallback) |
| **InlayHintGenerator** | `Parser/InlayHintGenerator.cs` | Convert AST results into `InlayHintTag` list with style properties |
| **InlayHintManager** | `Adornment/InlayHintManager.cs` | Thread-safe tag cache with `TagsUpdated` event |
| **InlayHintTagger** | `Adornment/InlayHintTagger.cs` | `ITagger<IntraTextAdornmentTag>` implementation; creates WPF UI elements |
| **InlayIndexViewCreationListener** | `Adornment/InlayIndexViewCreationListener.cs` | View lifecycle; text change debouncing (500ms); retriggers parsing |
| **VisualGDBConfigDetector** | `Parser/VisualGDBConfigDetector.cs` | Auto-detect include paths from VisualGDB/vcxproj/CMake |
| **InlayIndexOptionsPage** | `Options/InlayIndexOptionsPage.cs` | VS Options dialog integration |

---

## Development

### Debug Mode
The `InlayIndex` project launches a **Visual Studio Experimental Instance** (`devenv.exe /rootsuffix Exp`) for debugging — no `.vsix` installation needed:

1. Set `InlayIndex` as the startup project
2. Press **F5**
3. Open a C/C++ file in the experimental instance
4. Set breakpoints in `InlayHintTagger.cs` or `ClangParser.cs`

### Logging
Logs are written to:
```
InlayIndex_YYYYMMDD_HHMMSS.log
```

Three log categories: `[Parse]`, `[Render]`, `[Tag]`, plus `[DEBUG]` for troubleshooting.

### Tech Stack
- **Language**: C# 8.0 (.NET Framework 4.7.2)
- **Framework**: VSIX Extensibility, MEF (Managed Extensibility Framework)
- **Rendering**: `IntraTextAdornmentTag`, WPF (`TextBlock` / `Border`)
- **Parsing**: ClangSharp 16 / libclang 16
- **Build**: Microsoft.VSSDK.BuildTools 17.14+

### Dependencies (NuGet)
| Package | Version |
|---------|---------|
| `ClangSharp` | 16.0.0 |
| `ClangSharp.Interop` | 16.0.0 |
| `libclang.runtime.win-x64` | 16.0.6 |
| `Microsoft.VisualStudio.SDK` | 17.0.32112.339 |
| `Microsoft.VSSDK.BuildTools` | 17.14.2120 |

---

## Known Issues

### Scroll-induced Tag Disappearance
When rapidly scrolling (especially clicking the scrollbar or fast-dragging the thumb), some `IntraTextAdornmentTag` labels may disappear. This is a known limitation of the VS formatting engine's caching behavior with dense adornments (91+ per file).

**Status**: Analysis complete. The `IntraTextAdornmentTag` API is designed for sparse adornments. Long-term solutions under consideration:
- Migrate to `IAdornmentLayer` overlay rendering (tags float above text, cannot push text)
- Wait for `IInlayHintBroker` API maturity for C/C++

See [滚动消失问题分析记录](InlayIndex/Documentation/IntraTextAdornmentTag%E6%BB%9A%E5%8A%A8%E6%B6%88%E5%A4%B1%E9%97%AE%E9%A2%98%E5%88%86%E6%9E%90%E4%B8%8E%E8%A7%A3%E5%86%B3%E8%AE%B0%E5%BD%95.md) for details.

---

## Documentation

Full documentation is available in the [Documentation](InlayIndex/Documentation/) directory:

| Document | Description |
|----------|-------------|
| [Requirements](InlayIndex/Documentation/%E6%95%B0%E7%BB%84%E7%B4%A2%E5%BC%95%20-%20%E6%9E%9A%E4%B8%BE%E5%80%BC%20Inlay%20Hint%20VSIX%20%E6%8F%92%E4%BB%B6%20%E9%9C%80%E6%B1%82%E6%96%87%E6%A1%A3.md) | Full requirements specification |
| [Implementation Guide](InlayIndex/Documentation/IntraTextAdornmentTag%E5%AE%9E%E7%8E%B0%E6%96%B9%E6%A1%88.md) | Technical design of IntraTextAdornmentTag + ClangSharp |
| [Scroll Disappearance Bug](InlayIndex/Documentation/IntraTextAdornmentTag%E6%BB%9A%E5%8A%A8%E6%B6%88%E5%A4%B1%E9%97%AE%E9%A2%98%E5%88%86%E6%9E%90%E4%B8%8E%E8%A7%A3%E5%86%B3%E8%AE%B0%E5%BD%95.md) | Bug analysis: scroll-induced tag loss |
| [Tag Tracking Bug](InlayIndex/Documentation/%E6%A0%87%E7%AD%BE%E8%B7%9F%E8%B8%AA%E9%97%AE%E9%A2%98%E5%88%86%E6%9E%90%E5%92%8C%E8%A7%A3%E5%86%B3%E6%96%B9%E6%A1%88.md) | Bug analysis: tag position offset after editing |
| [Header Pollution Fix](InlayIndex/Documentation/%E7%B3%BB%E7%BB%9F%E5%A4%B4%E6%96%87%E4%BB%B6%E6%9E%9A%E4%B8%BE%E6%B1%A1%E6%9F%93%E4%BF%AE%E5%A4%8D%E6%96%B9%E6%A1%88.md) | Bug fix: system header enum pollution |
| [VisualGDB Config](InlayIndex/Documentation/VisualGDB%E9%85%8D%E7%BD%AE%E8%87%AA%E5%8A%A8%E6%8E%A2%E6%B5%8B%E6%96%B9%E6%A1%88.md) | Auto-detection of include paths for embedded projects |

---

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

### Getting Started
1. Read the [Requirements Doc](InlayIndex/Documentation/%E6%95%B0%E7%BB%84%E7%B4%A2%E5%BC%95%20-%20%E6%9E%9A%E4%B8%BE%E5%80%BC%20Inlay%20Hint%20VSIX%20%E6%8F%92%E4%BB%B6%20%E9%9C%80%E6%B1%82%E6%96%87%E6%A1%A3.md) for feature overview
2. Read the [Implementation Guide](InlayIndex/Documentation/IntraTextAdornmentTag%E5%AE%9E%E7%8E%B0%E6%96%B9%E6%A1%88.md) for architectural details
3. Check the [Bug Analysis docs](InlayIndex/Documentation/) for resolved and known issues
4. Build and debug locally using the Experimental Instance

---

## License

[MIT](LICENSE)

**Publisher**: Ghost-Girls

---

*Made for C/C++ developers who value code readability.*