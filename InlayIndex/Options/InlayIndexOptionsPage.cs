using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace InlayIndex.Options
{
    public class InlayIndexOptionsPage : DialogPage
    {
        private static InlayIndexOptionsPage _defaultInstance;
        
        public static InlayIndexOptionsPage Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    _defaultInstance = new InlayIndexOptionsPage();
                }
                return _defaultInstance;
            }
        }
        
        private bool enableArrayIndex = true;
        private bool enableEnumValue = true;
        private bool enableStructField = true;
        private ColorTheme selectedTheme = ColorTheme.Orange;
        private double fontSize = 11;
        private FontWeight fontWeight = FontWeights.Bold;
        private double backgroundOpacity = 15;
        private int maxDimensions = 4;
        private int maxElements = 1000;
        private bool enableC = true;
        private bool enableCpp = true;
        private string logDirectory = @"C:\Users\NexusStudio\source\repos\InlayIndex\InlayIndex\Log";
        private IndexDisplayMode indexDisplayMode = IndexDisplayMode.Simple;

        [Category("功能开关")]
        [DisplayName("启用数组索引标签")]
        [Description("控制是否显示数组索引提示")]
        public bool EnableArrayIndex
        {
            get => enableArrayIndex;
            set => enableArrayIndex = value;
        }

        [Category("功能开关")]
        [DisplayName("启用枚举值标签")]
        [Description("控制是否在枚举定义处显示枚举值")]
        public bool EnableEnumValue
        {
            get => enableEnumValue;
            set => enableEnumValue = value;
        }

        [Category("功能开关")]
        [DisplayName("启用结构体字段名显示")]
        [Description("控制是否显示.x:、.y:等字段名")]
        public bool EnableStructField
        {
            get => enableStructField;
            set => enableStructField = value;
        }

        [Category("样式配置")]
        [DisplayName("颜色主题")]
        [Description("选择提示标签的颜色主题")]
        public ColorTheme SelectedTheme
        {
            get => selectedTheme;
            set => selectedTheme = value;
        }

        [Category("样式配置")]
        [DisplayName("字体大小")]
        [Description("字体大小 (9-14pt)")]
        [DefaultValue(11)]
        public double FontSize
        {
            get => fontSize;
            set => fontSize = System.Math.Max(9, System.Math.Min(14, value));
        }

        [Category("样式配置")]
        [DisplayName("字体粗细")]
        [Description("字体粗细程度")]
        public FontWeightEnum FontWeightEnum { get; set; } = FontWeightEnum.Bold;

        [Category("样式配置")]
        [DisplayName("背景透明度")]
        [Description("背景透明度 (0-100%)")]
        [DefaultValue(15)]
        public double BackgroundOpacity
        {
            get => backgroundOpacity;
            set => backgroundOpacity = System.Math.Max(0, System.Math.Min(100, value));
        }

        [Category("显示限制")]
        [DisplayName("最大显示维度")]
        [Description("显示数组的最大维度 (1-4)")]
        [DefaultValue(4)]
        public int MaxDimensions
        {
            get => maxDimensions;
            set => maxDimensions = System.Math.Max(1, System.Math.Min(4, value));
        }

        [Category("显示限制")]
        [DisplayName("最大元素数量")]
        [Description("单个数组显示的最大元素数量 (100-10000)")]
        [DefaultValue(1000)]
        public int MaxElements
        {
            get => maxElements;
            set => maxElements = System.Math.Max(100, System.Math.Min(10000, value));
        }

        [Category("语言支持")]
        [DisplayName("启用 C 语言")]
        [Description("是否启用 C 语言支持")]
        [DefaultValue(true)]
        public bool EnableC
        {
            get => enableC;
            set => enableC = value;
        }

        [Category("语言支持")]
        [DisplayName("启用 C++ 语言")]
        [Description("是否启用 C++ 语言支持")]
        [DefaultValue(true)]
        public bool EnableCpp
        {
            get => enableCpp;
            set => enableCpp = value;
        }

        [Category("日志配置")]
        [DisplayName("日志目录")]
        [Description("日志文件的存储目录")]
        public string LogDirectory
        {
            get => logDirectory;
            set => logDirectory = value;
        }

        [Category("显示设置")]
        [DisplayName("索引显示模式")]
        [Description("选择数组索引的显示模式：简洁索引（默认）或完整索引")]
        [DefaultValue(IndexDisplayMode.Simple)]
        public IndexDisplayMode IndexDisplayMode
        {
            get => indexDisplayMode;
            set => indexDisplayMode = value;
        }

        public Color GetForegroundColor()
        {
            switch (selectedTheme)
            {
                case ColorTheme.Blue:
                    return Color.FromRgb(0, 100, 230);
                case ColorTheme.Green:
                    return Color.FromRgb(0, 180, 50);
                case ColorTheme.HighContrast:
                    return Color.FromRgb(255, 255, 0);
                case ColorTheme.Orange:
                default:
                    return Color.FromRgb(230, 100, 0);
            }
        }

        public FontWeight GetFontWeight()
        {
            switch (FontWeightEnum)
            {
                case FontWeightEnum.Normal:
                    return FontWeights.Normal;
                case FontWeightEnum.Medium:
                    return FontWeights.Medium;
                case FontWeightEnum.SemiBold:
                    return FontWeights.SemiBold;
                case FontWeightEnum.Bold:
                default:
                    return FontWeights.Bold;
            }
        }
    }

    public enum ColorTheme
    {
        Orange,
        Blue,
        Green,
        HighContrast
    }

    public enum FontWeightEnum
    {
        Normal,
        Medium,
        SemiBold,
        Bold
    }

    public enum IndexDisplayMode
    {
        Simple,
        Full
    }
}
