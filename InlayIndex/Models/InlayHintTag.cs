using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;

namespace InlayIndex.Models
{
    public class InlayHintTag
    {
        public string Text { get; set; }
        
        // ✅ 新增：动态跟踪的 Span（方案 A 核心）
        public ITrackingSpan TrackingSpan { get; set; }
        
        // ✅ 保留：原始位置（用于调试、缓存和序列化）
        public int OriginalStartPosition { get; set; }
        public int OriginalEndPosition { get; set; }
        
        // ✅ 兼容属性：直接映射到 OriginalStartPosition/OriginalEndPosition
        public int StartPosition 
        { 
            get => OriginalStartPosition; 
            set => OriginalStartPosition = value; 
        }
        
        public int EndPosition 
        { 
            get => OriginalEndPosition; 
            set => OriginalEndPosition = value; 
        }
        
        // 原有的其他属性
        public InlayHintType Type { get; set; }
        public Color? ForegroundColor { get; set; }
        public Color? BackgroundColor { get; set; }
        public double FontSize { get; set; }
        public FontWeight FontWeight { get; set; }
        public double BackgroundOpacity { get; set; }
    }

    public enum InlayHintType
    {
        ArrayIndex,
        EnumValue,
        StructField
    }
}
