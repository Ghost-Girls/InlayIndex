using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
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
        private double fontSize = 7;
        private FontWeight fontWeight = FontWeights.Bold;
        private double backgroundOpacity = 15;
        private int maxDimensions = 4;
        private int maxElements = 1000;
        private bool enableC = true;
        private bool enableCpp = true;
        private string logDirectory = @"C:\Users\NexusStudio\source\repos\InlayIndex\InlayIndex\Log";
        private IndexDisplayMode indexDisplayMode = IndexDisplayMode.Simple;
        private bool enableDepthColors = true;
        private string depthColors = "#FF0000,#FF8000,#FFFF00,#00FF00,#00FFFF,#0000FF,#8000FF";
        private bool enableVisualGDBDetection = true;
        private bool enableVcxprojDetection = true;
        private bool enableCmakeDetection = false;

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
            set => fontSize = System.Math.Max(5, System.Math.Min(12, value));
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

        [Category("样式配置")]
        [DisplayName("启用深度颜色")]
        [Description("根据数组的深度给镶嵌提示上色")]
        [DefaultValue(true)]
        public bool EnableDepthColors
        {
            get => enableDepthColors;
            set => enableDepthColors = value;
        }

        [Category("样式配置")]
        [DisplayName("深度颜色")]
        [Description("为数组的每个层级自定义颜色，使用逗号分隔的CSS颜色值")]
        [DefaultValue("#FF0000,#FF8000,#FFFF00,#00FF00,#00FFFF,#0000FF,#8000FF")]
        public string DepthColors
        {
            get => depthColors;
            set => depthColors = value;
        }

        [Category("工程感知")]
        [DisplayName("启用 VisualGDB 配置探测")]
        [Description("自动从 VisualGDB 项目配置中提取 Include 路径和宏定义")]
        [DefaultValue(true)]
        public bool EnableVisualGDBDetection
        {
            get => enableVisualGDBDetection;
            set => enableVisualGDBDetection = value;
        }

        [Category("工程感知")]
        [DisplayName("启用 vcxproj 配置探测")]
        [Description("自动从普通 vcxproj 文件中提取 Include 路径")]
        [DefaultValue(true)]
        public bool EnableVcxprojDetection
        {
            get => enableVcxprojDetection;
            set => enableVcxprojDetection = value;
        }

        [Category("工程感知")]
        [DisplayName("启用 CMake 配置探测")]
        [Description("自动从 CMakeLists.txt 中解析配置")]
        [DefaultValue(false)]
        public bool EnableCmakeDetection
        {
            get => enableCmakeDetection;
            set => enableCmakeDetection = value;
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

        public List<Color> GetDepthColors()
        {
            var colors = new List<Color>();
            
            if (!enableDepthColors || string.IsNullOrEmpty(depthColors))
            {
                return colors;
            }
            
            var colorStrings = depthColors.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var colorString in colorStrings)
            {
                var colorStr = colorString.Trim();
                if (ColorConverter.ConvertFromString(colorStr) is Color color)
                {
                    colors.Add(color);
                }
            }
            
            // 如果没有有效的颜色，返回默认的彩虹色
            if (colors.Count == 0)
            {
                colors.Add(Color.FromRgb(255, 0, 0));     // 红
                colors.Add(Color.FromRgb(255, 128, 0));   // 橙
                colors.Add(Color.FromRgb(255, 255, 0));   // 黄
                colors.Add(Color.FromRgb(0, 255, 0));     // 绿
                colors.Add(Color.FromRgb(0, 255, 255));   // 青
                colors.Add(Color.FromRgb(0, 0, 255));     // 蓝
                colors.Add(Color.FromRgb(128, 0, 255));   // 紫
            }
            
            return colors;
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
