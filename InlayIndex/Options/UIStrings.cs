using System;
using System.ComponentModel;
using System.Windows;

namespace InlayIndex.Options
{
    public enum UILanguage
    {
        Chinese,
        English
    }

    public static class UIStrings
    {
        public static UILanguage CurrentLanguage { get; set; } = UILanguage.Chinese;

        public static event EventHandler LanguageChanged;

        public static void SwitchLanguage(UILanguage lang)
        {
            if (CurrentLanguage == lang) return;
            CurrentLanguage = lang;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        private static string T(string zh, string en) => CurrentLanguage == UILanguage.Chinese ? zh : en;

        // === 页面标题 ===
        public static string PageTitle => T("InlayIndex 设置", "InlayIndex Settings");

        // === GroupBox 标题 ===
        public static string GroupFeatureToggles => T("功能开关", "Feature Toggles");
        public static string GroupStyle => T("样式配置", "Style");
        public static string GroupDisplayLimits => T("显示限制", "Display Limits");
        public static string GroupLanguageSupport => T("语言支持", "Language Support");
        public static string GroupDisplayPerformance => T("显示设置 & 性能", "Display & Performance");
        public static string GroupProjectAwareness => T("工程感知", "Project Awareness");
        public static string GroupLogConfig => T("日志配置", "Log Config");
        public static string GroupUILanguage => T("界面语言", "UI Language");

        // === CheckBox 内容 ===
        public static string ChkArrayIndex => T("启用数组索引标签", "Enable Array Index Hints");
        public static string ChkEnumValue => T("启用枚举值标签", "Enable Enum Value Hints");
        public static string ChkStructField => T("启用结构体字段名显示", "Enable Struct Field Hints");
        public static string ChkAutoBgColor => T("自动根据前景色生成深色背景色", "Auto background from foreground");
        public static string ChkDepthColors => T("启用深度颜色", "Enable Depth Colors");
        public static string LabelDepthColorsEnable => T("启用深度颜色：", "Depth Colors:");
        public static string ChkEnableC => T("启用 C 语言", "Enable C Language");
        public static string ChkEnableCpp => T("启用 C++ 语言", "Enable C++ Language");
        public static string ChkVisualGDB => T("启用 VisualGDB 配置探测", "Enable VisualGDB Detection");
        public static string ChkVcxproj => T("启用 vcxproj 配置探测", "Enable vcxproj Detection");
        public static string ChkCmake => T("启用 CMake 配置探测", "Enable CMake Detection");

        // === ComboBoxItem 文本 ===
        public static string ThemeOrange => T("橙色", "Orange");
        public static string ThemeBlue => T("蓝色", "Blue");
        public static string ThemeGreen => T("绿色", "Green");
        public static string ThemeHighContrast => T("高对比度", "High Contrast");
        public static string FontWeightNormal => T("普通", "Normal");
        public static string FontWeightMedium => T("中等", "Medium");
        public static string FontWeightSemiBold => T("半粗体", "SemiBold");
        public static string FontWeightBold => T("粗体", "Bold");
        public static string IndexModeSimple => T("简洁索引", "Simple");
        public static string IndexModeFull => T("完整索引", "Full");
        public static string LanguageChinese => T("中文", "Chinese");
        public static string LanguageEnglish => T("英文", "English");

        // === Label 文本 (冒号结尾) ===
        public static string LabelTheme => T("颜色主题：", "Theme:");
        public static string LabelFontSize => T("字体大小：", "Font Size:");
        public static string LabelFontWeight => T("字体粗细：", "Font Weight:");
        public static string LabelBgOpacity => T("背景透明度：", "Opacity:");
        public static string LabelBgColor => T("标签背景色：", "Background Color:");
        public static string LabelDepthColors => T("深度颜色：", "Depth Colors:");
        public static string LabelMaxDimensions => T("最大显示维度：", "Max Dimensions:");
        public static string LabelMaxElements => T("最大元素数量：", "Max Elements:");
        public static string LabelIndexMode => T("索引显示模式：", "Index Mode:");
        public static string LabelDebounce => T("防抖延迟 (ms)：", "Debounce Delay (ms):");
        public static string LabelLogDir => T("日志目录：", "Log Directory:");
        public static string LabelUILanguage => T("界面语言：", "UI Language:");

        // === 按钮 ===
        public static string BtnReset => T("恢复默认设置", "Reset to Defaults");

        // === 状态消息 ===
        public static string StatusResetDone => T("已恢复默认设置，请点击「确定」应用更改", "Reset to defaults, click OK to apply");
    }
}