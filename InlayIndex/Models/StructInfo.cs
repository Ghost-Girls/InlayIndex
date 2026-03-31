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
        
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class StructInfo
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public List<StructFieldInfo> Fields { get; set; }
        
        public int DeclarationStart { get; set; }
        public int DeclarationEnd { get; set; }
        
        public ITextSnapshot Snapshot { get; set; }
    }
}
