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
            _index = CXIndex.Create();
            LogHelper.WriteParseInfo("ClangParser 初始化成功");
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
                
                // 将 C# 字符串（UTF-16）转换为 UTF-8 字节数组（libclang 期望的格式）
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(code);
                LogHelper.WriteParseInfo($"ParseCode: 转换字符串 - UTF-16 长度={code.Length}, UTF-8 字节数={utf8Bytes.Length}");
                
                unsafe
                {
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

                        var tu = CXTranslationUnit.Parse(
                            _index,
                            fileName,
                            args,
                            unsavedFile,
                            CXTranslationUnit_Flags.CXTranslationUnit_None);

                        if (tu == null)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Failed to parse translation unit";
                            return result;
                        }

                        result.TranslationUnit = tu;
                        result.Success = true;
                        
                        VisitChildren(tu.Cursor, result);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private void VisitChildren(CXCursor cursor, ParseResult result)
        {
            LogHelper.WriteDebug($"开始遍历 AST 节点，根节点：{cursor.Kind}");
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

                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(visitor, ToClientData(clientData));
                }
            }
            finally
            {
                clientData.Free();
            }
            LogHelper.WriteDebug("AST 遍历完成");
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
            
            // 检查元素类型是否是记录类型（结构体/类/联合）
            if (elementType.kind == CXTypeKind.CXType_Record ||
                elementType.kind == CXTypeKind.CXType_Elaborated)
            {
                arrayInfo.IsStructArray = true;
                arrayInfo.StructTypeName = elementType.Spelling.CString;
                LogHelper.WriteDebug($"识别到结构体数组：{arrayInfo.Name}, 元素类型：{arrayInfo.StructTypeName}");
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

            var arrayState = new VisitState<ArrayInfo>(arrayInfo);
            var arrayClientData = GCHandle.Alloc(arrayState);
            try
            {
                unsafe
                {
                    CXCursorVisitor arrayVisitor = (child, parent, data) =>
                    {
                        var state = FromClientData<VisitState<ArrayInfo>>(data);
                        if (state != null && child.Kind == CXCursorKind.CXCursor_InitListExpr)
                        {
                            ExtractArrayElements(child, state.Value, dimensions.Count, new int[] { });
                            return CXChildVisitResult.CXChildVisit_Break;
                        }
                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(arrayVisitor, ToClientData(arrayClientData));
                }
            }
            finally
            {
                arrayClientData.Free();
            }

            return arrayInfo;
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
            if (dimension == 0)
            {
                return;
            }

            // 记录当前初始化列表的位置（仅当不是最内层时）
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

            // 用于扁平化初始化的元素索引计算
            int[] flatIndices = (int[])currentIndices.Clone();
            int elementCount = 0;
            bool hasNestedInitList = false;

            // 先检查是否有嵌套的 InitListExpr
            var checkState = new VisitState<bool>(false);
            var checkClientData = GCHandle.Alloc(checkState);
            try
            {
                unsafe
                {
                    CXCursorVisitor checkVisitor = (child, parent, data) =>
                    {
                        var s = FromClientData<VisitState<bool>>(data);
                        if (s != null && child.Kind == CXCursorKind.CXCursor_InitListExpr)
                        {
                            s.Value = true;
                            return CXChildVisitResult.CXChildVisit_Break;
                        }
                        return CXChildVisitResult.CXChildVisit_Continue;
                    };
                    cursor.VisitChildren(checkVisitor, ToClientData(checkClientData));
                }
            }
            finally
            {
                hasNestedInitList = checkState.Value;
                checkClientData.Free();
            }

            var state = new ArrayElementsState(0, arrayInfo, dimension, currentIndices);
            var clientData = GCHandle.Alloc(state);
            try
            {
                unsafe
                {
                    CXCursorVisitor visitor = (child, parent, data) =>
                    {
                        var s = FromClientData<ArrayElementsState>(data);
                        if (s == null) return CXChildVisitResult.CXChildVisit_Continue;
                        
                        if (child.Kind == CXCursorKind.CXCursor_InitListExpr)
                        {
                            var newIndices = new int[s.CurrentIndices.Length + 1];
                            Array.Copy(s.CurrentIndices, newIndices, s.CurrentIndices.Length);
                            newIndices[s.CurrentIndices.Length] = s.ChildIndex;

                            ExtractArrayElements(child, s.ArrayInfo, s.Dimension - 1, newIndices);
                            s.ChildIndex++;
                        }
                        else if (!hasNestedInitList || s.Dimension == 1)
                        {
                            // 处理元素 - 如果没有嵌套初始化列表或者是最内层维度
                            uint offset = GetOffset(child.Location);
                            int adjustedOffset = (int)offset;
                            
                            // 简单调整：向左微调，跳过空白字符
                            if (!string.IsNullOrEmpty(_currentCode) && adjustedOffset > 0)
                            {
                                adjustedOffset = Math.Max(0, adjustedOffset - 3); // 先向左移3个位置
                                
                                // 继续向左寻找非空白字符的位置
                                while (adjustedOffset > 0)
                                {
                                    char c = _currentCode[adjustedOffset];
                                    if (c == ' ' || c == ',' || c == '\t')
                                    {
                                        adjustedOffset--;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            
                            // 计算完整的多维索引
                            int[] fullIndices;
                            if (s.Dimension == 1)
                            {
                                // 正常的嵌套初始化情况
                                fullIndices = new int[s.CurrentIndices.Length + 1];
                                Array.Copy(s.CurrentIndices, fullIndices, s.CurrentIndices.Length);
                                fullIndices[s.CurrentIndices.Length] = s.ChildIndex;
                            }
                            else
                            {
                                // 扁平化初始化的情况
                                fullIndices = CalculateFlatIndices(flatIndices, elementCount, arrayInfo.DimensionSizes);
                            }
                            
                            var element = new ArrayElement
                            {
                                Indices = fullIndices,
                                StartPosition = adjustedOffset,
                                EndPosition = adjustedOffset,
                                Value = child.ToString()
                            };
                            
                            LogHelper.WriteDebug($"提取数组元素 - 索引：[{string.Join("][", fullIndices)}], 值：{element.Value}, 原始位置：{offset}, 调整后位置：{adjustedOffset}");
                            
                            s.ArrayInfo.Elements.Add(element);
                            s.ChildIndex++;
                            elementCount++;
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

        private int[] CalculateFlatIndices(int[] baseIndices, int elementIndex, int[] dimensionSizes)
        {
            int[] indices = (int[])baseIndices.Clone();
            int remaining = elementIndex;
            
            // 从最内层开始计算索引
            for (int i = dimensionSizes.Length - 1; i >= indices.Length; i--)
            {
                int size = dimensionSizes[i];
                indices = new int[] { remaining % size }.Concat(indices).ToArray();
                remaining = remaining / size;
            }
            
            return indices;
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
                            var member = new EnumMember
                            {
                                Name = child.ToString(),
                                StartPosition = (int)GetOffset(child.Location),
                                EndPosition = (int)GetOffset(child.Location)
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
