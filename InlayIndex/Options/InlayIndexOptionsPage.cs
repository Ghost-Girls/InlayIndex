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
        private InlayIndexOptionsPageControl _pageControl;
        private System.Windows.Forms.Integration.ElementHost _elementHost;

        public static event EventHandler<InlayIndexOptionsPage> SettingsApplied;
        private static event EventHandler<InlayIndexOptionsPage> PackageInitialized;

        public static InlayIndexOptionsPage Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    _defaultInstance = new InlayIndexOptionsPage();
                    SettingsStore.LoadInto(_defaultInstance);
                }
                return _defaultInstance;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected override System.Windows.Forms.IWin32Window Window
        {
            get
            {
                if (_pageControl == null)
                {
                    _pageControl = new InlayIndexOptionsPageControl();
                    _pageControl.OptionsPage = this;
                }
                if (_elementHost == null)
                {
                    _elementHost = new System.Windows.Forms.Integration.ElementHost { Child = _pageControl };
                }
                return _elementHost;
            }
        }

        protected override void OnApply(PageApplyEventArgs args)
        {
            _pageControl?.SaveToPage();
            base.OnApply(args);
            SyncFrom(this);
            SettingsStore.Save(this);
            SettingsApplied?.Invoke(this, this);
        }

        public static void SubscribePackageInit(EventHandler<InlayIndexOptionsPage> handler)
        {
            PackageInitialized += handler;
        }

        public static void UnsubscribePackageInit(EventHandler<InlayIndexOptionsPage> handler)
        {
            PackageInitialized -= handler;
        }

        internal static void FirePackageInitialized(InlayIndexOptionsPage page)
        {
            PackageInitialized?.Invoke(null, page);
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();
            SettingsStore.LoadInto(this);
            UIStrings.SwitchLanguage(this.UILanguageSetting);
        }

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();
            SettingsStore.Save(this);
        }

        public static void SyncFrom(InlayIndexOptionsPage source)
        {
            var def = Default;
            def.enableArrayIndex = source.enableArrayIndex;
            def.enableEnumValue = source.enableEnumValue;
            def.enableStructField = source.enableStructField;
            def.selectedTheme = source.selectedTheme;
            def.fontSize = source.fontSize;
            def.fontWeight = source.fontWeight;
            def.FontWeightEnum = source.FontWeightEnum;
            def.backgroundOpacity = source.backgroundOpacity;
            def.backgroundColorHex = source.backgroundColorHex;
            def.maxDimensions = source.maxDimensions;
            def.maxElements = source.maxElements;
            def.enableC = source.enableC;
            def.enableCpp = source.enableCpp;
#if DEBUG
            def.logDirectory = source.logDirectory;
#endif
            def.indexDisplayMode = source.indexDisplayMode;
            def.enableDepthColors = source.enableDepthColors;
            def.depthColors = source.depthColors;
            def.enableVisualGDBDetection = source.enableVisualGDBDetection;
            def.enableVcxprojDetection = source.enableVcxprojDetection;
            def.enableCmakeDetection = source.enableCmakeDetection;
            def.debounceDelayMs = source.debounceDelayMs;
            def.useAutoBackgroundColor = source.useAutoBackgroundColor;
            def.uiLanguage = source.uiLanguage;
            UIStrings.SwitchLanguage(def.uiLanguage);
        }

        private bool enableArrayIndex = true;
        private bool enableEnumValue = true;
        private bool enableStructField = true;
        private ColorTheme selectedTheme = ColorTheme.Orange;
        private double fontSize = 7;
        private FontWeight fontWeight = FontWeights.Bold;
        private double backgroundOpacity = 80;
        private string backgroundColorHex = "#101020";
        private int maxDimensions = 10;
        private int maxElements = 10000;
        private bool enableC = true;
        private bool enableCpp = true;
#if DEBUG
        private string logDirectory = @"C:\Users\NexusStudio\source\repos\InlayIndex\InlayIndex\Log";
#endif
        private IndexDisplayMode indexDisplayMode = IndexDisplayMode.Simple;
        private bool enableDepthColors = true;
        private string depthColors = "#E1461E,#FF8000,#FFFF00,#00FF00,#00FFFF,#0000FF,#8000FF";
        private bool enableVisualGDBDetection = true;
        private bool enableVcxprojDetection = true;
        private bool enableCmakeDetection = false;
        private int debounceDelayMs = 200;
        private bool useAutoBackgroundColor = false;
        private UILanguage uiLanguage = UILanguage.Chinese;

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

        [Category("样式配置")]
        [DisplayName("标签背景色")]
        [Description("标签背景颜色，格式 #RRGGBB，默认 #101020 (暗黑)")]
        [DefaultValue("#101020")]
        public string BackgroundColorHex
        {
            get => backgroundColorHex;
            set => backgroundColorHex = value ?? "#101020";
        }

        [Category("显示限制")]
        [DisplayName("最大显示维度")]
        [Description("显示数组的最大维度 (1-10)")]
        [DefaultValue(10)]
        public int MaxDimensions
        {
            get => maxDimensions;
            set => maxDimensions = System.Math.Max(1, System.Math.Min(10, value));
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

#if DEBUG
        [Category("日志配置")]
        [DisplayName("日志目录")]
        [Description("日志文件的存储目录")]
        public string LogDirectory
        {
            get => logDirectory;
            set => logDirectory = value;
        }
#endif

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
        [DefaultValue("#E1461E,#FF8000,#FFFF00,#00FF00,#00FFFF,#0000FF,#8000FF")]
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

        [Category("性能配置")]
        [DisplayName("防抖延迟")]
        [Description("编辑后等待多久才重新解析 (100-2000ms)。数值越小响应越快但越耗CPU")]
        [DefaultValue(200)]
        public int DebounceDelayMs
        {
            get => debounceDelayMs;
            set => debounceDelayMs = System.Math.Max(100, System.Math.Min(2000, value));
        }

        [Category("样式配置")]
        [DisplayName("自动背景色")]
        [Description("根据前景色自动生成深色背景色，替代手动输入的背景色")]
        [DefaultValue(false)]
        public bool UseAutoBackgroundColor
        {
            get => useAutoBackgroundColor;
            set => useAutoBackgroundColor = value;
        }

        [Category("界面语言")]
        [DisplayName("界面语言")]
        [Description("选择选项页面的显示语言")]
        [DefaultValue(UILanguage.Chinese)]
        public UILanguage UILanguageSetting
        {
            get => uiLanguage;
            set => uiLanguage = value;
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

        public Color GetBackgroundColor()
        {
            if (useAutoBackgroundColor)
                return GetDerivedBackgroundColor(GetForegroundColor());

            try
            {
                var hex = backgroundColorHex?.TrimStart('#') ?? "101020";
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return Color.FromRgb(60, 60, 72);
        }

        public static Color GetDerivedBackgroundColor(Color foreground)
        {
            double r = foreground.R / 255.0;
            double g = foreground.G / 255.0;
            double b = foreground.B / 255.0;

            double max = System.Math.Max(r, System.Math.Max(g, b));
            double min = System.Math.Min(r, System.Math.Min(g, b));
            double l = (max + min) / 2.0;

            if (max == min)
            {
                byte gray = (byte)(0.15 * 255);
                return Color.FromRgb(gray, gray, gray);
            }

            double s = l <= 0.5
                ? (max - min) / (max + min)
                : (max - min) / (2.0 - max - min);

            double h;
            if (r >= g && r >= b)
                h = (g - b) / (max - min);
            else if (g >= r && g >= b)
                h = 2.0 + (b - r) / (max - min);
            else
                h = 4.0 + (r - g) / (max - min);

            h *= 60;
            if (h < 0) h += 360;

            double newL = 0.12;
            double newS = System.Math.Min(1.0, s * 1.4);

            double q = newL < 0.5 ? newL * (1 + newS) : newL + newS - newL * newS;
            double p = 2 * newL - q;

            byte R = (byte)(HueToRgb(p, q, h / 360.0 + 1.0 / 3.0) * 255);
            byte G = (byte)(HueToRgb(p, q, h / 360.0) * 255);
            byte B = (byte)(HueToRgb(p, q, h / 360.0 - 1.0 / 3.0) * 255);

            return Color.FromRgb(R, G, B);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
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
                colors.Add(Color.FromRgb(225, 70, 30));     // 红
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
