using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace InlayIndex.Models
{
    public class EnumMember
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public bool HasExplicitValue { get; set; }
        
        // ✅ 保留：原始位置
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        // ✅ 新增：动态跟踪的 Span（方案 A 核心）
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class EnumInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public List<EnumMember> Members { get; set; }
        
        // ✅ 保留：原始位置
        public int DeclarationStart { get; set; }
        public int DeclarationEnd { get; set; }
        
        // ✅ 新增：用于创建 ITrackingSpan
        public ITextSnapshot Snapshot { get; set; }
    }
}
