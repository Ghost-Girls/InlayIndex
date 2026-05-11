using System.Windows;
using System.Windows.Controls;

namespace InlayIndex.Options
{
    public partial class InlayIndexOptionsPageControl : UserControl
    {
        internal InlayIndexOptionsPage OptionsPage;

        private const string DefaultBgColor = "#101020";
        private const string DefaultDepthColors = "#E1461E,#FF8000,#FFFF00,#00FF00,#00FFFF,#0000FF,#8000FF";

        public InlayIndexOptionsPageControl()
        {
            InitializeComponent();
            UIStrings.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() => ApplyUIStrings());
        }

        private void ApplyUIStrings()
        {
            lblPageTitle.Text = UIStrings.PageTitle;

            grpUILanguage.Header = UIStrings.GroupUILanguage;
            lblUILanguage.Text = UIStrings.LabelUILanguage;
            SetComboItemText(cmbUILanguage, "Chinese", UIStrings.LanguageChinese);
            SetComboItemText(cmbUILanguage, "English", UIStrings.LanguageEnglish);

            grpFeatureToggles.Header = UIStrings.GroupFeatureToggles;
            chkEnableArrayIndex.Content = UIStrings.ChkArrayIndex;
            chkEnableEnumValue.Content = UIStrings.ChkEnumValue;
            chkEnableStructField.Content = UIStrings.ChkStructField;

            grpStyle.Header = UIStrings.GroupStyle;
            lblTheme.Text = UIStrings.LabelTheme;
            SetComboItemText(cmbTheme, "Orange", UIStrings.ThemeOrange);
            SetComboItemText(cmbTheme, "Blue", UIStrings.ThemeBlue);
            SetComboItemText(cmbTheme, "Green", UIStrings.ThemeGreen);
            SetComboItemText(cmbTheme, "HighContrast", UIStrings.ThemeHighContrast);

            lblFontSize.Text = UIStrings.LabelFontSize;
            lblFontWeight.Text = UIStrings.LabelFontWeight;
            SetComboItemText(cmbFontWeight, "Normal", UIStrings.FontWeightNormal);
            SetComboItemText(cmbFontWeight, "Medium", UIStrings.FontWeightMedium);
            SetComboItemText(cmbFontWeight, "SemiBold", UIStrings.FontWeightSemiBold);
            SetComboItemText(cmbFontWeight, "Bold", UIStrings.FontWeightBold);

            lblBgOpacity.Text = UIStrings.LabelBgOpacity;
            lblBgColor.Text = UIStrings.LabelBgColor;
            chkAutoBgColor.Content = UIStrings.ChkAutoBgColor;
            lblDepthColorsEnable.Text = UIStrings.LabelDepthColorsEnable;
            chkEnableDepthColors.Content = UIStrings.ChkDepthColors;
            lblDepthColors.Text = UIStrings.LabelDepthColors;

            grpDisplayLimits.Header = UIStrings.GroupDisplayLimits;
            lblMaxDims.Text = UIStrings.LabelMaxDimensions;
            lblMaxElements.Text = UIStrings.LabelMaxElements;

            grpLanguageSupport.Header = UIStrings.GroupLanguageSupport;
            chkEnableC.Content = UIStrings.ChkEnableC;
            chkEnableCpp.Content = UIStrings.ChkEnableCpp;

            grpDisplayPerformance.Header = UIStrings.GroupDisplayPerformance;
            lblIndexMode.Text = UIStrings.LabelIndexMode;
            SetComboItemText(cmbIndexDisplay, "Simple", UIStrings.IndexModeSimple);
            SetComboItemText(cmbIndexDisplay, "Full", UIStrings.IndexModeFull);
            lblDebounce.Text = UIStrings.LabelDebounce;

            grpProjectAwareness.Header = UIStrings.GroupProjectAwareness;
            chkEnableVgdb.Content = UIStrings.ChkVisualGDB;
            chkEnableVcxproj.Content = UIStrings.ChkVcxproj;
            chkEnableCmake.Content = UIStrings.ChkCmake;

            grpLogConfig.Header = UIStrings.GroupLogConfig;
            lblLogDir.Text = UIStrings.LabelLogDir;

            btnReset.Content = UIStrings.BtnReset;
        }

        private static void SetComboItemText(ComboBox combo, string tag, string text)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag as string == tag)
                {
                    item.Content = text;
                    return;
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (OptionsPage == null)
                return;

            UIStrings.CurrentLanguage = OptionsPage.UILanguageSetting;
            ApplyUIStrings();

            chkEnableArrayIndex.IsChecked = OptionsPage.EnableArrayIndex;
            chkEnableEnumValue.IsChecked = OptionsPage.EnableEnumValue;
            chkEnableStructField.IsChecked = OptionsPage.EnableStructField;

            var themeItem = FindItemByTag(cmbTheme, OptionsPage.SelectedTheme.ToString());
            if (themeItem != null) cmbTheme.SelectedItem = themeItem;

            sldFontSize.Value = OptionsPage.FontSize;

            var fwItem = FindItemByTag(cmbFontWeight, OptionsPage.FontWeightEnum.ToString());
            if (fwItem != null) cmbFontWeight.SelectedItem = fwItem;

            sldBgOpacity.Value = OptionsPage.BackgroundOpacity;

            txtBgColor.Text = string.IsNullOrEmpty(OptionsPage.BackgroundColorHex) ? DefaultBgColor : OptionsPage.BackgroundColorHex;
            chkAutoBgColor.IsChecked = OptionsPage.UseAutoBackgroundColor;
            txtBgColor.IsEnabled = !OptionsPage.UseAutoBackgroundColor;
            chkEnableDepthColors.IsChecked = OptionsPage.EnableDepthColors;
            txtDepthColors.Text = string.IsNullOrEmpty(OptionsPage.DepthColors) ? DefaultDepthColors : OptionsPage.DepthColors;

            sldMaxDims.Value = OptionsPage.MaxDimensions;
            sldMaxElements.Value = OptionsPage.MaxElements;

            chkEnableC.IsChecked = OptionsPage.EnableC;
            chkEnableCpp.IsChecked = OptionsPage.EnableCpp;

            var idxItem = FindItemByTag(cmbIndexDisplay, OptionsPage.IndexDisplayMode.ToString());
            if (idxItem != null) cmbIndexDisplay.SelectedItem = idxItem;

            chkEnableVgdb.IsChecked = OptionsPage.EnableVisualGDBDetection;
            chkEnableVcxproj.IsChecked = OptionsPage.EnableVcxprojDetection;
            chkEnableCmake.IsChecked = OptionsPage.EnableCmakeDetection;

#if DEBUG
            txtLogDir.Text = OptionsPage.LogDirectory ?? "";
#endif

            sldDebounce.Value = OptionsPage.DebounceDelayMs;
#if !DEBUG
            grpLogConfig.Visibility = Visibility.Collapsed;
#endif

            var langItem = FindItemByTag(cmbUILanguage, UIStrings.CurrentLanguage.ToString());
            if (langItem != null) cmbUILanguage.SelectedItem = langItem;
        }

        internal void SaveToPage()
        {
            if (OptionsPage == null)
                return;

            OptionsPage.EnableArrayIndex = chkEnableArrayIndex.IsChecked ?? true;
            OptionsPage.EnableEnumValue = chkEnableEnumValue.IsChecked ?? true;
            OptionsPage.EnableStructField = chkEnableStructField.IsChecked ?? true;

            var selTheme = cmbTheme.SelectedItem as ComboBoxItem;
            if (selTheme != null && System.Enum.TryParse<ColorTheme>(selTheme.Tag as string, out var theme))
                OptionsPage.SelectedTheme = theme;

            OptionsPage.FontSize = sldFontSize.Value;

            var selFw = cmbFontWeight.SelectedItem as ComboBoxItem;
            if (selFw != null && System.Enum.TryParse<FontWeightEnum>(selFw.Tag as string, out var fw))
                OptionsPage.FontWeightEnum = fw;

            OptionsPage.BackgroundOpacity = sldBgOpacity.Value;

            var bgColor = (txtBgColor.Text ?? "").Trim();
            OptionsPage.BackgroundColorHex = string.IsNullOrEmpty(bgColor) ? DefaultBgColor : bgColor;

            OptionsPage.EnableDepthColors = chkEnableDepthColors.IsChecked ?? true;

            var depthColors = (txtDepthColors.Text ?? "").Trim();
            OptionsPage.DepthColors = string.IsNullOrEmpty(depthColors) ? DefaultDepthColors : depthColors;

            OptionsPage.MaxDimensions = (int)sldMaxDims.Value;
            OptionsPage.MaxElements = (int)sldMaxElements.Value;

            OptionsPage.EnableC = chkEnableC.IsChecked ?? true;
            OptionsPage.EnableCpp = chkEnableCpp.IsChecked ?? true;

            var selIdx = cmbIndexDisplay.SelectedItem as ComboBoxItem;
            if (selIdx != null && System.Enum.TryParse<IndexDisplayMode>(selIdx.Tag as string, out var idxMode))
                OptionsPage.IndexDisplayMode = idxMode;

            OptionsPage.EnableVisualGDBDetection = chkEnableVgdb.IsChecked ?? true;
            OptionsPage.EnableVcxprojDetection = chkEnableVcxproj.IsChecked ?? true;
            OptionsPage.EnableCmakeDetection = chkEnableCmake.IsChecked ?? false;

#if DEBUG
            OptionsPage.LogDirectory = txtLogDir.Text ?? "";
#endif

            OptionsPage.DebounceDelayMs = (int)sldDebounce.Value;
            OptionsPage.UseAutoBackgroundColor = chkAutoBgColor.IsChecked ?? false;

            var selLang = cmbUILanguage.SelectedItem as ComboBoxItem;
            if (selLang != null && System.Enum.TryParse<UILanguage>(selLang.Tag as string, out var lang))
                OptionsPage.UILanguageSetting = lang;
        }

        private void cmbUILanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var selLang = cmbUILanguage.SelectedItem as ComboBoxItem;
            if (selLang != null && System.Enum.TryParse<UILanguage>(selLang.Tag as string, out var lang))
            {
                UIStrings.SwitchLanguage(lang);
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new InlayIndexOptionsPage();

            chkEnableArrayIndex.IsChecked = defaults.EnableArrayIndex;
            chkEnableEnumValue.IsChecked = defaults.EnableEnumValue;
            chkEnableStructField.IsChecked = defaults.EnableStructField;

            var themeItem = FindItemByTag(cmbTheme, defaults.SelectedTheme.ToString());
            if (themeItem != null) cmbTheme.SelectedItem = themeItem;

            sldFontSize.Value = defaults.FontSize;

            var fwItem = FindItemByTag(cmbFontWeight, defaults.FontWeightEnum.ToString());
            if (fwItem != null) cmbFontWeight.SelectedItem = fwItem;

            sldBgOpacity.Value = defaults.BackgroundOpacity;

            txtBgColor.Text = defaults.BackgroundColorHex;
            chkAutoBgColor.IsChecked = defaults.UseAutoBackgroundColor;
            txtBgColor.IsEnabled = !defaults.UseAutoBackgroundColor;
            chkEnableDepthColors.IsChecked = defaults.EnableDepthColors;
            txtDepthColors.Text = defaults.DepthColors;

            sldMaxDims.Value = defaults.MaxDimensions;
            sldMaxElements.Value = defaults.MaxElements;

            chkEnableC.IsChecked = defaults.EnableC;
            chkEnableCpp.IsChecked = defaults.EnableCpp;

            var idxItem = FindItemByTag(cmbIndexDisplay, defaults.IndexDisplayMode.ToString());
            if (idxItem != null) cmbIndexDisplay.SelectedItem = idxItem;

            chkEnableVgdb.IsChecked = defaults.EnableVisualGDBDetection;
            chkEnableVcxproj.IsChecked = defaults.EnableVcxprojDetection;
            chkEnableCmake.IsChecked = defaults.EnableCmakeDetection;

#if DEBUG
            txtLogDir.Text = defaults.LogDirectory;
#endif

            sldDebounce.Value = defaults.DebounceDelayMs;

            var langItem = FindItemByTag(cmbUILanguage, defaults.UILanguageSetting.ToString());
            if (langItem != null) cmbUILanguage.SelectedItem = langItem;

            txtStatus.Text = UIStrings.StatusResetDone;
        }

        private void chkAutoBgColor_Changed(object sender, RoutedEventArgs e)
        {
            var isAuto = chkAutoBgColor.IsChecked ?? false;
            txtBgColor.IsEnabled = !isAuto;
        }

        private void HexTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete ||
                e.Key == System.Windows.Input.Key.Left || e.Key == System.Windows.Input.Key.Right ||
                e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down ||
                e.Key == System.Windows.Input.Key.Home || e.Key == System.Windows.Input.Key.End ||
                e.Key == System.Windows.Input.Key.Tab || e.Key == System.Windows.Input.Key.Enter ||
                e.Key == System.Windows.Input.Key.Escape)
                return;

            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
                return;

            var ch = KeyToChar(e.Key, e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift));
            if (ch == null)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            var tb = (System.Windows.Controls.TextBox)sender;
            var start = tb.SelectionStart;
            var len = tb.SelectionLength;
            var text = tb.Text;
            if (len > 0)
                text = text.Remove(start, len);
            text = text.Insert(start, ch.Value.ToString());
            tb.Text = text;
            tb.SelectionStart = start + 1;
            tb.SelectionLength = 0;
        }

        private static char? KeyToChar(System.Windows.Input.Key key, bool shift)
        {
            if (key >= System.Windows.Input.Key.A && key <= System.Windows.Input.Key.Z)
                return shift ? (char)('A' + (key - System.Windows.Input.Key.A)) : (char)('a' + (key - System.Windows.Input.Key.A));

            if (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
                return shift ? ")!@#$%^&*("[(int)(key - System.Windows.Input.Key.D0)] : (char)('0' + (key - System.Windows.Input.Key.D0));

            if (key >= System.Windows.Input.Key.NumPad0 && key <= System.Windows.Input.Key.NumPad9)
                return (char)('0' + (key - System.Windows.Input.Key.NumPad0));

            if (key == System.Windows.Input.Key.Space) return ' ';

            switch (key)
            {
                case System.Windows.Input.Key.OemMinus: return shift ? '_' : '-';
                case System.Windows.Input.Key.OemPlus: return shift ? '+' : '=';
                case System.Windows.Input.Key.OemPeriod: return shift ? '>' : '.';
                case System.Windows.Input.Key.OemComma: return shift ? '<' : ',';
                case System.Windows.Input.Key.OemQuestion: return shift ? '?' : '/';
                case System.Windows.Input.Key.OemOpenBrackets: return shift ? '{' : '[';
                case System.Windows.Input.Key.OemCloseBrackets: return shift ? '}' : ']';
                case System.Windows.Input.Key.OemQuotes: return shift ? '"' : '\'';
                case System.Windows.Input.Key.OemSemicolon: return shift ? ':' : ';';
                case System.Windows.Input.Key.OemBackslash: return shift ? '|' : '\\';
                case System.Windows.Input.Key.OemTilde: return shift ? '~' : '`';
                case System.Windows.Input.Key.Divide: return '/';
                case System.Windows.Input.Key.Multiply: return '*';
                case System.Windows.Input.Key.Subtract: return '-';
                case System.Windows.Input.Key.Add: return '+';
                case System.Windows.Input.Key.Decimal: return '.';
            }

            return null;
        }

        private static ComboBoxItem FindItemByTag(ComboBox combo, string tag)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag as string == tag)
                    return item;
            }
            return null;
        }
    }
}