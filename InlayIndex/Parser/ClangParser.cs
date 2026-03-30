using ClangSharp;
using InlayIndex.Models;
using InlayIndex.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClangSharp.Interop;

namespace InlayIndex.Parser
{
    public class ClangParser : IDisposable
    {
        private CXIndex _index;
        private bool _disposed = false;
        // 保存当前解析的原始 UTF-16 字符串，用于 offset 转换
        private string _currentCode = string.Empty;

        private uint GetOffset(CXSourceLocation location)
        {
            location.GetSpellingLocation(out _, out _, out _, out uint utf8Offset);
            uint result = utf8Offset;
            
            // 将 UTF-8 字节 offset 转换为 UTF-16 字符索引
            if (!string.IsNullOrEmpty(_currentCode))
            {
                try
                {
                    // 获取完整的 UTF-8 字节
                    byte[] fullUtf8 = System.Text.Encoding.UTF8.GetBytes(_currentCode);
                    int safeUtf8Offset = Math.Min((int)utf8Offset, fullUtf8.Length);
                    
                    // 简单可靠的方法：把 UTF-8 前 N 个字节转回字符串，取其长度
                    string utf16UpToOffset = System.Text.Encoding.UTF8.GetString(fullUtf8, 0, safeUtf8Offset);
                    result = (uint)utf16UpToOffset.Length;
                    
                    LogHelper.WriteDebug($"位置转换：UTF-8 offset={utf8Offset} → UTF-16 index={result}");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError($"位置转换失败", ex);
                    result = utf8Offset;
                }
            }
            
            return result;
        }

        private static CXClientData ToClientData(GCHandle handle)
        {
            return new CXClientData(GCHandle.ToIntPtr(handle));
        }

        private static unsafe T FromClientData<T>(void* data)
        {
            return (T)GCHandle.FromIntPtr((IntPtr)data).Target!;
        }

        private class VisitState<T>
        {
            public T Value { get; set; }
            public VisitState(T value) => Value = value;
        }

        private class ArrayElementsState
        {
            public int ChildIndex { get; set; }
            public ArrayInfo ArrayInfo { get; set; }
            public int Dimension { get; set; }
            public int[] CurrentIndices { get; set; }
            
            public ArrayElementsState(int childIndex, ArrayInfo arrayInfo, int dimension, int[] currentIndices)
            {
                ChildIndex = childIndex;
                ArrayInfo = arrayInfo;
                Dimension = dimension;
                CurrentIndices = currentIndices;
            }
        }

        private class EnumState
        {
            public EnumInfo EnumInfo { get; set; }
            public int CurrentValue { get; set; }
            
            public EnumState(EnumInfo enumInfo, int currentValue)
            {
                EnumInfo = enumInfo;
                CurrentValue = currentValue;
            }
        }

        public ClangParser()
        {
            try
            {
                LogHelper.WriteParseInfo("开始初始化 CXIndex...");
                _index = CXIndex.Create();
                
                if (_index == null)
                {
                    LogHelper.WriteError("CXIndex.Create() 返回 null！libclang 可能未正确加载");
                    throw new Exception("Failed to create CXIndex - libclang may not be loaded correctly");
                }
                
                LogHelper.WriteParseInfo("CXIndex 初始化成功");
                LogHelper.WriteParseInfo("ClangParser 初始化成功");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("ClangParser 初始化失败", ex);
                throw;
            }
        }

        public ParseResult ParseFile(string filePath, string[] compilationArgs = null)
        {
            LogHelper.WriteParseInfo($"开始解析文件：{filePath}");
            LogHelper.WriteParseInfo($"编译参数：{string.Join(" ", compilationArgs ?? new string[] { "-x", "c++" })}");
            
            var result = new ParseResult();
            
            try
            {
                var args = compilationArgs ?? new string[] { "-x", "c++" };
                
                LogHelper.WriteParseInfo("开始解析翻译单元...");
                var tu = CXTranslationUnit.Parse(
                    _index,
                    filePath,
                    args,
                    Array.Empty<CXUnsavedFile>(),
                    CXTranslationUnit_Flags.CXTranslationUnit_None);

                if (tu == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to parse translation unit";
                    LogHelper.WriteError("翻译单元解析失败");
                    return result;
                }

                result.TranslationUnit = tu;
                result.Success = true;
                
                LogHelper.WriteParseInfo($"翻译单元解析成功，开始遍历 AST...");
                VisitChildren(tu.Cursor, result);
                
                LogHelper.WriteParseInfo($"解析完成 - 数组：{result.Arrays.Count}, 枚举：{result.Enums.Count}, 结构体：{result.Structs.Count}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                LogHelper.WriteError("解析过程发生异常", ex);
            }

            return result;
        }

        public ParseResult ParseCode(string code, string fileName = "temp.cpp", string[] compilationArgs = null)
        {
            var result = new ParseResult();
            
            try
            {
                var args = compilationArgs ?? new string[] { "-x", "c++" };
                
                // 保存原始 UTF-16 字符串，用于后续 offset 转换
                _currentCode = code;
                
                // 将C# 字符串（UTF-16）转换为 UTF-8 字节数组（libclang 期望的格式）
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(code);
                LogHelper.WriteParseInfo($"ParseCode: 转换字符串 - UTF-16 长度={code.Length}, UTF-8 字节数={utf8Bytes.Length}");
                LogHelper.WriteParseInfo($"ParseCode: 文件名={fileName}, 编译参数={string.Join(" ", args)}");
                
                // 检查 CXIndex 是否有效
                if (_index == null)
                {
                    LogHelper.WriteError("CXIndex 无效！无法进行解析");
                    result.Success = false;
                    result.ErrorMessage = "CXIndex is not initialized";
                    return result;
                }
                
                unsafe
                {
                    // 方案 1：使用 unsaved file 直接解析内存中的代码
                    fixed (char* filenamePtr = fileName)
                    fixed (byte* contentsPtr = utf8Bytes)
                    {
                        var unsavedFile = new CXUnsavedFile[1];
                        unsavedFile[0] = new CXUnsavedFile
                        {
                            Filename = (sbyte*)filenamePtr,
                            Contents = (sbyte*)contentsPtr,
                            Length = (uint)utf8Bytes.Length
                        };

                        LogHelper.WriteParseInfo("开始调用 CXTranslationUnit.Parse (使用 unsaved file)...");
                        
                        try
                        {
                            var tu = CXTranslationUnit.Parse(
                                _index,
                                fileName,
                                args,
                                unsavedFile,
                                CXTranslationUnit_Flags.CXTranslationUnit_None);

                            if (tu == null)
                            {
                                LogHelper.WriteError("CXTranslationUnit.Parse 返回 null");
                                
                                // 尝试不使用 unsaved file，直接解析
                                LogHelper.WriteParseInfo("尝试不使用 unsaved file 直接解析...");
                                
                                // 保存为临时文件
                                string tempFile = System.IO.Path.GetTempFileName() + ".cpp";
                                System.IO.File.WriteAllText(tempFile, code);
                                
                                try
                                {
                                    tu = CXTranslationUnit.Parse(
                                        _index,
                                        tempFile,
                                        args,
                                        Array.Empty<CXUnsavedFile>(),
                                        CXTranslationUnit_Flags.CXTranslationUnit_None);
                                    
                                    if (tu == null)
                                    {
                                        LogHelper.WriteError("使用临时文件解析也返回 null");
                                        result.Success = false;
                                        result.ErrorMessage = "Failed to parse translation unit - both methods returned null";
                                        return result;
                                    }
                                    
                                    LogHelper.WriteParseInfo("使用临时文件解析成功！");
                                }
                                finally
                                {
                                    try { System.IO.File.Delete(tempFile); } catch { }
                                }
                            }

                            // 检查诊断信息
                            uint diagnosticCount = tu.NumDiagnostics;
                            LogHelper.WriteParseInfo($"Clang 诊断数量：{diagnosticCount}");
                            
                            for (uint i = 0; i < diagnosticCount; i++)
                            {
                                var diagnostic = tu.GetDiagnostic(i);
                                var diagnosticString = diagnostic.ToString();
                                var severity = diagnostic.Severity;
                                
                                // 使用整数值比较：CXDiagnostic_Error=4, CXDiagnostic_Fatal=5
                                if ((int)severity >= 4)
                                {
                                    LogHelper.WriteError($"Clang 错误 [{severity}]: {diagnosticString}");
                                }
                                else
                                {
                                    LogHelper.WriteParseInfo($"Clang 警告 [{severity}]: {diagnosticString}");
                                }
                            }

                            result.TranslationUnit = tu;
                            result.Success = true;
                            
                            VisitChildren(tu.Cursor, result);
                        }
                        catch (Exception parseEx)
                        {
                            LogHelper.WriteError("CXTranslationUnit.Parse 抛出异常", parseEx);
                            result.Success = false;
                            result.ErrorMessage = $"Parse exception: {parseEx.Message}";
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("ParseCode 异常", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private void VisitChildren(CXCursor cursor, ParseResult result)
        {
            LogHelper.WriteDebug($"开始遍历 AST 节点，根节点：{cursor.Kind}");
            VisitChildrenRecursive(cursor, result);
            
            LogHelper.WriteDebug($"准备关联结构体数组和结构体信息，数组数：{result.Arrays.Count}, 结构体数：{result.Structs.Count}");
            
            // 在所有解析完成后，关联结构体数组和对应的结构体信息
            LinkStructArraysWithStructs(result);
            
            LogHelper.WriteDebug("AST 遍历完成");
        }
        
        private void VisitChildrenRecursive(CXCursor cursor, ParseResult result)
        {
            var clientData = GCHandle.Alloc(result);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (c, p, data) =>
                    {
                        var res = FromClientData<ParseResult>(data);
                        if (res == null) return CXChildVisitResult.CXChildVisit_Continue;
                        
                        switch (c.Kind)
                        {
                            case CXCursorKind.CXCursor_VarDecl:
                                LogHelper.WriteDebug($"发现变量声明：{c.ToString()}");
                                HandleVariableDeclaration(c, res);
                                break;
                            case CXCursorKind.CXCursor_EnumDecl:
                                LogHelper.WriteDebug($"发现枚举声明：{c.ToString()}");
                                HandleEnumDeclaration(c, res);
                                break;
                            case CXCursorKind.CXCursor_StructDecl:
                            case CXCursorKind.CXCursor_UnionDecl:
                            case CXCursorKind.CXCursor_ClassDecl:
                                LogHelper.WriteDebug($"发现结构体/类声明：{c.ToString()}");
                                HandleStructDeclaration(c, res);
                                break;
                        }

                        // 递归处理当前节点的子节点
                        VisitChildrenRecursive(c, res);

                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
            }
            finally
            {
                clientData.Free();
            }
        }
        
        private void LinkStructArraysWithStructs(ParseResult result)
        {
            foreach (var arrayInfo in result.Arrays)
            {
                if (arrayInfo.IsStructArray && !string.IsNullOrEmpty(arrayInfo.StructTypeName))
                {
                    LogHelper.WriteDebug($"尝试关联结构体数组：{arrayInfo.Name}, StructTypeName={arrayInfo.StructTypeName}");
                    foreach (var structInfo in result.Structs)
                    {
                        LogHelper.WriteDebug($"  比较结构体：{structInfo.Name}");
                        // 移除可能的 "struct " 前缀进行比较
                        string cleanTypeName = arrayInfo.StructTypeName.StartsWith("struct ") ? 
                            arrayInfo.StructTypeName.Substring("struct ".Length) : 
                            arrayInfo.StructTypeName;
                        
                        if (structInfo.Name == cleanTypeName)
                        {
                            arrayInfo.StructInfo = structInfo;
                            LogHelper.WriteDebug($"关联结构体信息到数组：{arrayInfo.Name} -> {structInfo.Name}, 字段数：{structInfo.Fields.Count}");
                            break;
                        }
                    }
                }
            }
        }

        private void HandleVariableDeclaration(CXCursor cursor, ParseResult result)
        {
            var type = cursor.Type;
            
            if (type.kind == CXTypeKind.CXType_ConstantArray || 
                type.kind == CXTypeKind.CXType_IncompleteArray)
            {
                LogHelper.WriteDebug($"处理数组变量：{cursor.ToString()}, 类型：{type.kind}");
                var arrayInfo = ExtractArrayInfo(cursor, type);
                if (arrayInfo != null)
                {
                    result.Arrays.Add(arrayInfo);
                    LogHelper.WriteParseInfo($"提取数组：{arrayInfo.Name}, 维度：{arrayInfo.Dimensions}, 大小：{string.Join("x", arrayInfo.DimensionSizes)}");
                }
            }
        }

        private void HandleEnumDeclaration(CXCursor cursor, ParseResult result)
        {
            LogHelper.WriteDebug($"处理枚举：{cursor.ToString()}");
            var enumInfo = ExtractEnumInfo(cursor);
            if (enumInfo != null)
            {
                result.Enums.Add(enumInfo);
                LogHelper.WriteParseInfo($"提取枚举：{enumInfo.Name}, 成员数：{enumInfo.Members.Count}");
            }
        }

        private void HandleStructDeclaration(CXCursor cursor, ParseResult result)
        {
            LogHelper.WriteDebug($"处理结构体：{cursor.ToString()}");
            var structInfo = ExtractStructInfo(cursor);
            if (structInfo != null)
            {
                result.Structs.Add(structInfo);
                LogHelper.WriteParseInfo($"提取结构体：{structInfo.Name}, 字段数：{structInfo.Fields.Count}");
            }
        }

        private ArrayInfo ExtractArrayInfo(CXCursor cursor, CXType type)
        {
            var arrayInfo = new ArrayInfo
            {
                Name = cursor.ToString(),
                TypeName = type.kind.ToString(), // 先暂时用 type.kind，避免调用 type.Declaration
                Elements = new List<ArrayElement>(),
                DeclarationStart = (int)GetOffset(cursor.Location),
                DeclarationEnd = (int)GetOffset(cursor.Location)
            };

            var dimensions = new List<int>();
            var currentType = type;
            
            // 检查最内层的元素类型是否是结构体/类
            CXType elementType = currentType;
            while (elementType.kind == CXTypeKind.CXType_ConstantArray || 
                   elementType.kind == CXTypeKind.CXType_IncompleteArray)
            {
                elementType = elementType.ArrayElementType;
            }
            
            // 检查元素类型是否是记录类型（结构体/类/联合），排除枚举
            if (elementType.kind == CXTypeKind.CXType_Record ||
                elementType.kind == CXTypeKind.CXType_Elaborated)
            {
                // 对于 Elaborated 类型，需要进一步检查它是否是枚举
                bool isEnum = false;
                if (elementType.kind == CXTypeKind.CXType_Elaborated)
                {
                    // 获取 Elaborated 类型的实际类型
                    var namedType = elementType.NamedType;
                    if (namedType.kind == CXTypeKind.CXType_Enum)
                    {
                        isEnum = true;
                    }
                }
                
                // 如果不是枚举，才认为是结构体数组
                if (!isEnum)
                {
                    arrayInfo.IsStructArray = true;
                    arrayInfo.StructTypeName = elementType.Spelling.CString;
                    LogHelper.WriteDebug($"识别到结构体数组：{arrayInfo.Name}, 元素类型：{arrayInfo.StructTypeName}");
                }
                else
                {
                    LogHelper.WriteDebug($"识别到枚举数组：{arrayInfo.Name}, 元素类型：{elementType.Spelling.CString}");
                }
            }
            
            // 收集数组维度信息
            currentType = type;
            while (currentType.kind == CXTypeKind.CXType_ConstantArray || 
                   currentType.kind == CXTypeKind.CXType_IncompleteArray)
            {
                if (currentType.kind == CXTypeKind.CXType_ConstantArray)
                {
                    dimensions.Add((int)currentType.ArraySize);
                }
                else
                {
                    dimensions.Add(-1); 
                }
                currentType = currentType.ArrayElementType;
            }

            arrayInfo.DimensionSizes = dimensions.ToArray();
            arrayInfo.Dimensions = (ArrayDimension)dimensions.Count;

            if (dimensions.Count > 0 && dimensions[0] == -1)
            {
                var incompleteArrayElementType = currentType;
                var state = new VisitState<int>(0);
                
                var clientData = GCHandle.Alloc(state);
                try
                {
                    unsafe
                    {
                        CXCursorVisitor visitor = (child, parent, data) =>
                        {
                            var s = FromClientData<VisitState<int>>(data);
                            if (s != null && child.Kind == CXCursorKind.CXCursor_InitListExpr)
                            {
                                s.Value = CountInitListElements(child);
                                return CXChildVisitResult.CXChildVisit_Break;
                            }
                            return CXChildVisitResult.CXChildVisit_Continue;
                        };
                        cursor.VisitChildren(visitor, ToClientData(clientData));
                    }
                }
                finally
                {
                    clientData.Free();
                }

                if (state.Value > 0)
                {
                    arrayInfo.DimensionSizes[0] = state.Value;
                }
            }

            LogHelper.WriteDebug($"ExtractArrayInfo - 开始查找 InitListExpr，数组：{arrayInfo.Name}");
            CXCursor? foundInitList = FindInitListExprRecursive(cursor, arrayInfo.Name);
            if (foundInitList.HasValue)
            {
                LogHelper.WriteDebug($"ExtractArrayInfo - 找到 InitListExpr，数组：{arrayInfo.Name}");
                ExtractArrayElements(foundInitList.Value, arrayInfo, dimensions.Count, new int[] { });
            }

            return arrayInfo;
        }

        private CXCursor? FindInitListExprRecursive(CXCursor cursor, string arrayName)
        {
            CXCursor? result = null;
            var state = new VisitState<CXCursor?>(null);
            var clientData = GCHandle.Alloc(state);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (child, parent, data) =>
                    {
                        var s = FromClientData<VisitState<CXCursor?>>(data);
                        LogHelper.WriteDebug($"FindInitListExprRecursive - 发现子节点：{child.Kind}, 数组：{arrayName}");
                        
                        if (s != null)
                        {
                            if (child.Kind == CXCursorKind.CXCursor_InitListExpr)
                            {
                                s.Value = child;
                                return CXChildVisitResult.CXChildVisit_Break;
                            }
                            else if (child.Kind == CXCursorKind.CXCursor_UnexposedExpr)
                            {
                                // 递归搜索 UnexposedExpr 的子节点
                                var nestedResult = FindInitListExprRecursive(child, arrayName);
                                if (nestedResult.HasValue)
                                {
                                    s.Value = nestedResult.Value;
                                    return CXChildVisitResult.CXChildVisit_Break;
                                }
                            }
                        }
                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
                result = state.Value;
            }
            finally
            {
                clientData.Free();
            }
            return result;
        }

        private int CountInitListElements(CXCursor cursor)
        {
            var state = new VisitState<int>(0);
            var clientData = GCHandle.Alloc(state);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (child, parent, data) =>
                    {
                        var s = FromClientData<VisitState<int>>(data);
                        if (s != null) s.Value++;
                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
            }
            finally
            {
                clientData.Free();
            }
            return state.Value;
        }

        private void ExtractArrayElements(CXCursor cursor, ArrayInfo arrayInfo, int dimension, int[] currentIndices)
        {
            LogHelper.WriteDebug($"ExtractArrayElements - 数组：{arrayInfo.Name}, 维度：{dimension}, 当前索引长度：{currentIndices.Length}");
            
            if (dimension == 0)
            {
                LogHelper.WriteDebug($"ExtractArrayElements - 维度为0，返回");
                return;
            }

            // 临时列表用于收集所有元素（不带索引）
            if (currentIndices.Length == 0)
            {
                LogHelper.WriteDebug($"ExtractArrayElements - 根层级，开始收集元素");
                // 只有在根层级才进行元素收集和统一索引分配
                var tempElements = new List<ArrayElement>();
                CollectArrayElementsAndInitLists(cursor, arrayInfo, dimension, currentIndices, tempElements, 0);

                LogHelper.WriteDebug($"ExtractArrayElements - 收集到 {tempElements.Count} 个元素");
                // 为所有元素分配正确的索引，考虑嵌套打断效果
                AssignSmartIndices(tempElements, arrayInfo.DimensionSizes, arrayInfo, arrayInfo.IsStructArray);
            }
        }

        private void CollectArrayElementsAndInitLists(CXCursor cursor, ArrayInfo arrayInfo, int dimension, int[] currentIndices, List<ArrayElement> tempElements, int nestingDepth)
        {
            // 先记录当前初始化列表的位置（仅当不是最内层时）
            if (currentIndices.Length > 0)
            {
                uint offset = GetOffset(cursor.Location);
                int searchStart = (int)offset;
                int bracePosition = -1;
                
                // 可靠地寻找左大括号的位置
                if (!string.IsNullOrEmpty(_currentCode) && searchStart > 0)
                {
                    // 从当前位置向左寻找，最多往前找200个字符
                    int searchLimit = Math.Max(0, searchStart - 200);
                    
                    for (int i = searchStart; i >= searchLimit; i--)
                    {
                        char c = _currentCode[i];
                        if (c == '{')
                        {
                            bracePosition = i;
                            break;
                        }
                    }
                }
                
                // 如果没有找到左大括号，就使用原始位置
                int finalPosition = bracePosition >= 0 ? bracePosition : searchStart;
                
                var initList = new InitListInfo
                {
                    Indices = (int[])currentIndices.Clone(),
                    StartPosition = finalPosition,
                    EndPosition = finalPosition
                };
                
                arrayInfo.InitLists.Add(initList);
                LogHelper.WriteDebug($"提取初始化列表 - 索引：[{string.Join("][", currentIndices)}], 原始位置：{searchStart}, 最终位置：{finalPosition}");
            }

            // 简化的访问器 - 只收集元素和嵌套深度
            var state = new VisitState<int>(0);
            var clientData = GCHandle.Alloc(state);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (child, parent, data) =>
                    {
                        var s = FromClientData<VisitState<int>>(data);
                        if (s == null) return CXChildVisitResult.CXChildVisit_Continue;
                        
                        if (child.Kind == CXCursorKind.CXCursor_InitListExpr)
                        {
                            // 对于初始化列表，我们需要构建 newIndices 来记录初始化列表的位置，但这只是用于 InitListInfo，不影响元素收集
                            var newIndices = new int[currentIndices.Length + 1];
                            Array.Copy(currentIndices, newIndices, currentIndices.Length);
                            newIndices[currentIndices.Length] = s.Value;
                            
                            // 递归处理嵌套初始化列表，增加嵌套深度
                            CollectArrayElementsAndInitLists(child, arrayInfo, dimension, newIndices, tempElements, nestingDepth + 1);
                            s.Value++;
                        }
                        else if (tempElements != null)
                        {
                            // 处理元素 - 收集到 tempElements 中
                            uint offset = GetOffset(child.Location);
                            int adjustedOffset = (int)offset;
                            
                            // 改进的位置调整：找到元素值的起始位置
                            if (!string.IsNullOrEmpty(_currentCode))
                            {
                                // 首先向左找，跳过可能的空格、逗号、制表符
                                while (adjustedOffset > 0)
                                {
                                    char c = _currentCode[adjustedOffset];
                                    if (c == ' ' || c == ',' || c == '\t' || c == '\n' || c == '\r')
                                    {
                                        adjustedOffset--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                
                                // 然后确保我们在一个单词的起始位置（向左找非单词字符）
                                while (adjustedOffset > 0)
                                {
                                    char c = _currentCode[adjustedOffset - 1];
                                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                                    {
                                        adjustedOffset--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            
                            var element = new ArrayElement
                            {
                                Indices = new int[] { }, // 稍后分配
                                StartPosition = adjustedOffset,
                                EndPosition = adjustedOffset,
                                Value = child.ToString(),
                                NestingDepth = nestingDepth // 记录嵌套深度
                            };
                            
                            tempElements.Add(element);
                            LogHelper.WriteDebug($"收集元素 - 值：{element.Value}, 位置：{element.StartPosition}, 嵌套深度：{element.NestingDepth}");
                            s.Value++;
                        }

                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
            }
            finally
            {
                clientData.Free();
            }
        }

        private void AssignSmartIndices(List<ArrayElement> elements, int[] dimensionSizes, ArrayInfo arrayInfo, bool isStructArray = false)
        {
            int[] currentIndices = new int[dimensionSizes.Length];
            int lastNestingDepth = -1;
            bool indicesValid = true; // 标记索引是否仍然有效

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                
                // 如果不是结构体数组且索引已经无效，就跳过所有后续元素
                if (!isStructArray && !indicesValid)
                {
                    LogHelper.WriteDebug($"跳过元素（索引已无效）- 值：{element.Value}, 位置：{element.StartPosition}");
                    continue;
                }
                
                // 只有非结构体数组才检查是否超出数组大小
                if (!isStructArray && !IsIndexValid(currentIndices, dimensionSizes))
                {
                    LogHelper.WriteDebug($"跳过超出数组大小的元素 - 值：{element.Value}, 位置：{element.StartPosition}");
                    indicesValid = false;
                    continue;
                }

                // 检查嵌套深度变化 - 如果从嵌套中出来，需要重置到更高维度
                if (lastNestingDepth >= 0 && element.NestingDepth < lastNestingDepth)
                {
                    // 从嵌套出来了，重置当前维度索引
                    ResetIndicesAfterNesting(currentIndices, element.NestingDepth, dimensionSizes);
                }

                // 分配当前索引
                element.Indices = (int[])currentIndices.Clone();
                arrayInfo.Elements.Add(element);
                LogHelper.WriteDebug($"提取数组元素 - 索引：[{string.Join("][", element.Indices)}], 值：{element.Value}, 位置：{element.StartPosition}, 嵌套深度：{element.NestingDepth}");

                // 递增索引（对于结构体数组，我们不检查是否有效）
                if (isStructArray)
                {
                    // 对于结构体数组，正常递增索引，但允许索引超出（用于后续处理）
                    IncrementIndices(currentIndices, dimensionSizes);
                }
                else
                {
                    // 对于普通数组，递增索引并检查是否有效
                    indicesValid = IncrementIndices(currentIndices, dimensionSizes);
                }
                
                // 更新上一个嵌套深度
                lastNestingDepth = element.NestingDepth;
            }
        }

        private bool IsIndexValid(int[] indices, int[] dimensionSizes)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] >= dimensionSizes[i])
                {
                    return false;
                }
            }
            return true;
        }

        private void ResetIndicesAfterNesting(int[] indices, int targetDepth, int[] dimensionSizes)
        {
            // 从 targetDepth + 1 的维度开始递增
            int startDimension = targetDepth;
            if (startDimension >= 0 && startDimension < indices.Length)
            {
                // 递增当前维度
                indices[startDimension]++;
                
                // 重置更低的维度为0
                for (int i = startDimension + 1; i < indices.Length; i++)
                {
                    indices[i] = 0;
                }
            }
        }

        private bool IncrementIndices(int[] indices, int[] dimensionSizes)
        {
            // 从最内层开始递增
            for (int i = indices.Length - 1; i >= 0; i--)
            {
                indices[i]++;
                if (indices[i] < dimensionSizes[i])
                {
                    return true; // 成功递增，索引有效
                }
                // 超出当前维度，进位
                indices[i] = 0;
            }
            // 所有维度都溢出了，索引无效
            return false;
        }

        private int[] CalculateFlatIndices(int[] baseIndices, int elementIndex, int[] dimensionSizes)
        {
            int[] result = new int[dimensionSizes.Length];
            int remaining = elementIndex;
            
            // 从最内层开始计算索引
            for (int i = dimensionSizes.Length - 1; i >= 0; i--)
            {
                int size = dimensionSizes[i];
                result[i] = remaining % size;
                remaining = remaining / size;
            }
            
            return result;
        }

        private EnumInfo ExtractEnumInfo(CXCursor cursor)
        {
            var enumInfo = new EnumInfo
            {
                Name = cursor.ToString(),
                TypeName = cursor.Type.kind.ToString(), // 先暂时用 type.kind，避免调用 type.Declaration
                Members = new List<EnumMember>(),
                DeclarationStart = (int)GetOffset(cursor.Location),
                DeclarationEnd = (int)GetOffset(cursor.Location)
            };

            var state = new EnumState(enumInfo, 0);
            var clientData = GCHandle.Alloc(state);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (child, parent, data) =>
                    {
                        var s = FromClientData<EnumState>(data);
                        if (s == null) return CXChildVisitResult.CXChildVisit_Continue;
                        
                        if (child.Kind == CXCursorKind.CXCursor_EnumConstantDecl)
                        {
                            uint startOffset = GetOffset(child.Location);
                            int startPos = (int)startOffset;
                            int endPos = startPos;
                            
                            // 查找成员名称的结束位置（向右找逗号、等号或右大括号）
                            if (!string.IsNullOrEmpty(_currentCode) && startPos < _currentCode.Length)
                            {
                                string memberName = child.ToString();
                                int searchLimit = Math.Min(_currentCode.Length, startPos + memberName.Length + 10);
                                
                                for (int i = startPos; i < searchLimit; i++)
                                {
                                    char c = _currentCode[i];
                                    if (c == ',' || c == '=' || c == '}' || c == ' ')
                                    {
                                        endPos = i;
                                        break;
                                    }
                                }
                            }
                            
                            var member = new EnumMember
                            {
                                Name = child.ToString(),
                                StartPosition = startPos,
                                EndPosition = endPos
                            };

                            var enumValue = child.Evaluate;
                            if (enumValue.Handle != IntPtr.Zero && enumValue.Kind == CXEvalResultKind.CXEval_Int)
                            {
                                member.Value = (int)enumValue.AsLongLong;
                                member.HasExplicitValue = true;
                                s.CurrentValue = member.Value + 1;
                            }
                            else
                            {
                                member.Value = s.CurrentValue;
                                member.HasExplicitValue = false;
                                s.CurrentValue++;
                            }
                            enumValue.Dispose();

                            s.EnumInfo.Members.Add(member);
                        }

                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
            }
            finally
            {
                clientData.Free();
            }

            return enumInfo;
        }

        private StructInfo ExtractStructInfo(CXCursor cursor)
        {
            var structInfo = new StructInfo
            {
                Name = cursor.ToString(),
                Kind = cursor.Kind.ToString(),
                Fields = new List<StructFieldInfo>(),
                DeclarationStart = (int)GetOffset(cursor.Location),
                DeclarationEnd = (int)GetOffset(cursor.Location)
            };

            var state = new VisitState<StructInfo>(structInfo);
            var clientData = GCHandle.Alloc(state);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (child, parent, data) =>
                    {
                        var s = FromClientData<VisitState<StructInfo>>(data);
                        if (s == null) return CXChildVisitResult.CXChildVisit_Continue;
                        
                        var currentStructInfo = s.Value;
                        if (child.Kind == CXCursorKind.CXCursor_FieldDecl)
                        {
                            var field = new StructFieldInfo
                            {
                                Name = child.ToString(),
                                TypeName = child.Type.kind.ToString(), // 先暂时用 type.kind，避免调用 type.Declaration
                                IsArray = child.Type.kind == CXTypeKind.CXType_ConstantArray,
                                StartPosition = (int)GetOffset(child.Location),
                                EndPosition = (int)GetOffset(child.Location)
                            };

                            if (field.IsArray)
                            {
                                var dimensions = new List<int>();
                                var currentType = child.Type;
                                while (currentType.kind == CXTypeKind.CXType_ConstantArray)
                                {
                                    dimensions.Add((int)currentType.ArraySize);
                                    currentType = currentType.ArrayElementType;
                                }
                                field.ArrayDimensions = dimensions.ToArray();
                            }

                            currentStructInfo.Fields.Add(field);
                        }

                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
            }
            finally
            {
                clientData.Free();
            }

            return structInfo;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _index.Dispose();
                }
                _disposed = true;
            }
        }
    }

    public class ParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public CXTranslationUnit TranslationUnit { get; set; }
        public List<ArrayInfo> Arrays { get; set; } = new List<ArrayInfo>();
        public List<EnumInfo> Enums { get; set; } = new List<EnumInfo>();
        public List<StructInfo> Structs { get; set; } = new List<StructInfo>();
    }
}
