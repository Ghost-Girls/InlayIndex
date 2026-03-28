using System.Windows;
using System.Windows.Media;

namespace InlayIndex.Models
{
    public class InlayHintTag
    {
        public string Text { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
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
