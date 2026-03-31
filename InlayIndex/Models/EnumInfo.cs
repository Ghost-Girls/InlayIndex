using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace InlayIndex.Models
{
    public class EnumMember
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public bool HasExplicitValue { get; set; }
        
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        
        public ITrackingSpan TrackingSpan { get; set; }
    }

    public class EnumInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public List<EnumMember> Members { get; set; }
        
        public int DeclarationStart { get; set; }
        public int DeclarationEnd { get; set; }
        
        public ITextSnapshot Snapshot { get; set; }
    }
}
