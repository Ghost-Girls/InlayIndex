using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace InlayIndex.Models
{
    public enum ArrayDimension
    {
        Dim1 = 1,
        Dim2 = 2,
        Dim3 = 3,
        Dim4 = 4
    }

    public class ArrayElement
    {
        public int[] Indices { get; set; }
        public string Value { get; set; }
        
        // ✅ 保留：原始位置
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        public bool IsStruct { get; set; }
        public List<StructField> Fields { get; set; }
        
        // 临时字段：用于存储元素所在的嵌套层级深度（收集元素时使用）
        public int NestingDepth { get; set; }
        
        // ✅ 新增：动态跟踪的 Span（方案 A 核心）
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class StructField
    {
        public string Name { get; set; }
        public string Value { get; set; }
        
        // ✅ 保留：原始位置
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        public List<StructField> NestedFields { get; set; }
        
        // ✅ 新增：动态跟踪的 Span（方案 A 核心）
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class InitListInfo
    {
        public int[] Indices { get; set; }
        
        // ✅ 保留：原始位置
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        // ✅ 新增：动态跟踪的 Span（方案 A 核心）
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class ArrayInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public ArrayDimension Dimensions { get; set; }
        public int[] DimensionSizes { get; set; }
        public List<ArrayElement> Elements { get; set; }
        public List<InitListInfo> InitLists { get; set; } = new List<InitListInfo>();
        
        // ✅ 保留：原始位置
        public int DeclarationStart { get; set; }
        public int DeclarationEnd { get; set; }
        
        public bool IsStructArray { get; set; }
        public string StructTypeName { get; set; }
        public StructInfo StructInfo { get; set; }
        
        // ✅ 新增：用于创建 ITrackingSpan
        public ITextSnapshot Snapshot { get; set; }
    }
}
