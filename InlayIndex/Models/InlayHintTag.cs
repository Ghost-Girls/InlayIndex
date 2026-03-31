using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;

namespace InlayIndex.Models
{
    public class InlayHintTag
    {
        public string Text { get; set; }
        
        /// <summary>
        /// 动态跟踪的 Span（ITrackingSpan）
        /// </summary>
        public ITrackingSpan TrackingSpan { get; set; }
        
        /// <summary>
        /// 原始位置（用于调试、缓存和序列化）
        /// </summary>
        public int OriginalStartPosition { get; set; }
        public int OriginalEndPosition { get; set; }
        
        /// <summary>
        /// 兼容属性：映射到 OriginalStartPosition
        /// </summary>
        public int StartPosition 
        { 
            get => OriginalStartPosition; 
            set => OriginalStartPosition = value; 
        }
        
        /// <summary>
        /// 兼容属性：映射到 OriginalEndPosition
        /// </summary>
        public int EndPosition 
        { 
            get => OriginalEndPosition; 
            set => OriginalEndPosition = value; 
        }
        
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
