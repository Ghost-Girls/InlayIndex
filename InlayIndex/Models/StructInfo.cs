using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace InlayIndex.Models
{
    public class StructFieldInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsArray { get; set; }
        public int[] ArrayDimensions { get; set; }
        
        // ✅ 保留：原始位置
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        // ✅ 新增：动态跟踪的 Span（方案 A 核心）
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class StructInfo
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public List<StructFieldInfo> Fields { get; set; }
        
        // ✅ 保留：原始位置
        public int DeclarationStart { get; set; }
        public int DeclarationEnd { get; set; }
        
        // ✅ 新增：用于创建 ITrackingSpan
        public ITextSnapshot Snapshot { get; set; }
    }
}
