using ClangSharp;
using InlayIndex.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ClangSharp.Interop;

namespace InlayIndex.Parser
{
    public class ClangParser : IDisposable
    {
        private CXIndex _index;
        private bool _disposed = false;

        private static uint GetOffset(CXSourceLocation location)
        {
            location.GetSpellingLocation(out _, out _, out _, out uint offset);
            return offset;
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
        }

        public ParseResult ParseFile(string filePath, string[] compilationArgs = null)
        {
            var result = new ParseResult();
            
            try
            {
                var args = compilationArgs ?? new string[] { "-x", "c++" };
                
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
                    return result;
                }

                result.TranslationUnit = tu;
                result.Success = true;
                
                VisitChildren(tu.Cursor, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public ParseResult ParseCode(string code, string fileName = "temp.cpp", string[] compilationArgs = null)
        {
            var result = new ParseResult();
            
            try
            {
                var args = compilationArgs ?? new string[] { "-x", "c++" };
                
                unsafe
                {
                    fixed (char* filenamePtr = fileName)
                    fixed (char* contentsPtr = code)
                    {
                        var unsavedFile = new CXUnsavedFile[1];
                        unsavedFile[0] = new CXUnsavedFile
                        {
                            Filename = (sbyte*)filenamePtr,
                            Contents = (sbyte*)contentsPtr,
                            Length = (uint)code.Length
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
                                HandleVariableDeclaration(c, res);
                                break;
                            case CXCursorKind.CXCursor_EnumDecl:
                                HandleEnumDeclaration(c, res);
                                break;
                            case CXCursorKind.CXCursor_StructDecl:
                            case CXCursorKind.CXCursor_UnionDecl:
                            case CXCursorKind.CXCursor_ClassDecl:
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
        }

        private void HandleVariableDeclaration(CXCursor cursor, ParseResult result)
        {
            var type = cursor.Type;
            
            if (type.kind == CXTypeKind.CXType_ConstantArray || 
                type.kind == CXTypeKind.CXType_IncompleteArray)
            {
                var arrayInfo = ExtractArrayInfo(cursor, type);
                if (arrayInfo != null)
                {
                    result.Arrays.Add(arrayInfo);
                }
            }
        }

        private void HandleEnumDeclaration(CXCursor cursor, ParseResult result)
        {
            var enumInfo = ExtractEnumInfo(cursor);
            if (enumInfo != null)
            {
                result.Enums.Add(enumInfo);
            }
        }

        private void HandleStructDeclaration(CXCursor cursor, ParseResult result)
        {
            var structInfo = ExtractStructInfo(cursor);
            if (structInfo != null)
            {
                result.Structs.Add(structInfo);
            }
        }

        private ArrayInfo ExtractArrayInfo(CXCursor cursor, CXType type)
        {
            var arrayInfo = new ArrayInfo
            {
                Name = cursor.ToString(),
                TypeName = type.Declaration.ToString(),
                Elements = new List<ArrayElement>(),
                DeclarationStart = (int)GetOffset(cursor.Location),
                DeclarationEnd = (int)GetOffset(cursor.Location)
            };

            var dimensions = new List<int>();
            var currentType = type;
            
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
                var elementType = currentType;
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
                        else if (s.Dimension == 1)
                        {
                            var element = new ArrayElement
                            {
                                Indices = new int[s.CurrentIndices.Length + 1],
                                StartPosition = (int)GetOffset(child.Location),
                                EndPosition = (int)GetOffset(child.Location),
                                Value = child.ToString()
                            };
                            Array.Copy(s.CurrentIndices, element.Indices, s.CurrentIndices.Length);
                            element.Indices[s.CurrentIndices.Length] = s.ChildIndex;
                            
                            s.ArrayInfo.Elements.Add(element);
                            s.ChildIndex++;
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

        private EnumInfo ExtractEnumInfo(CXCursor cursor)
        {
            var enumInfo = new EnumInfo
            {
                Name = cursor.ToString(),
                TypeName = cursor.Type.Declaration.ToString(),
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
                                TypeName = child.Type.Declaration.ToString(),
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
