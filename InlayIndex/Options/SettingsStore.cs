using System;
using System.IO;
using System.Text;

namespace InlayIndex.Options
{
    internal static class SettingsStore
    {
        private static readonly string SettingsFilePath;

        static SettingsStore()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "NexusStudio", "InlayIndex");
            Directory.CreateDirectory(dir);
            SettingsFilePath = Path.Combine(dir, "settings.json");
        }

        public static void Save(InlayIndexOptionsPage page)
        {
            try
            {
                var json = new StringBuilder();
                json.AppendLine("{");
                json.AppendLine($"  \"enableArrayIndex\": {page.EnableArrayIndex.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"enableEnumValue\": {page.EnableEnumValue.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"enableStructField\": {page.EnableStructField.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"selectedTheme\": \"{page.SelectedTheme}\",");
                json.AppendLine($"  \"fontSize\": {page.FontSize},");
                json.AppendLine($"  \"fontWeightEnum\": \"{page.FontWeightEnum}\",");
                json.AppendLine($"  \"backgroundOpacity\": {page.BackgroundOpacity},");
                json.AppendLine($"  \"backgroundColorHex\": \"{page.BackgroundColorHex}\",");
                json.AppendLine($"  \"maxDimensions\": {page.MaxDimensions},");
                json.AppendLine($"  \"maxElements\": {page.MaxElements},");
                json.AppendLine($"  \"enableC\": {page.EnableC.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"enableCpp\": {page.EnableCpp.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"logDirectory\": \"{EscapeJson(page.LogDirectory)}\",");
                json.AppendLine($"  \"indexDisplayMode\": \"{page.IndexDisplayMode}\",");
                json.AppendLine($"  \"enableDepthColors\": {page.EnableDepthColors.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"depthColors\": \"{EscapeJson(page.DepthColors)}\",");
                json.AppendLine($"  \"enableVisualGDBDetection\": {page.EnableVisualGDBDetection.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"enableVcxprojDetection\": {page.EnableVcxprojDetection.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"enableCmakeDetection\": {page.EnableCmakeDetection.ToString().ToLowerInvariant()},");
                json.AppendLine($"  \"debounceDelayMs\": {page.DebounceDelayMs},");
                json.AppendLine($"  \"useAutoBackgroundColor\": {page.UseAutoBackgroundColor.ToString().ToLowerInvariant()}");
                json.AppendLine("}");

                File.WriteAllText(SettingsFilePath, json.ToString(), Encoding.UTF8);
                System.Diagnostics.Debug.WriteLine($"[InlayIndex] Settings saved to {SettingsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InlayIndex] Settings save failed: {ex.Message}");
            }
        }

        public static bool LoadInto(InlayIndexOptionsPage page)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return false;

                var json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);

                TryGetBool(json, "enableArrayIndex", out var v1); if (v1.HasValue) page.EnableArrayIndex = v1.Value;
                TryGetBool(json, "enableEnumValue", out var v2); if (v2.HasValue) page.EnableEnumValue = v2.Value;
                TryGetBool(json, "enableStructField", out var v3); if (v3.HasValue) page.EnableStructField = v3.Value;

                TryGetString(json, "selectedTheme", out var s1);
                if (s1 != null && Enum.TryParse<ColorTheme>(s1, out var t)) page.SelectedTheme = t;

                TryGetDouble(json, "fontSize", out var d1); if (d1.HasValue) page.FontSize = d1.Value;

                TryGetString(json, "fontWeightEnum", out var s2);
                if (s2 != null && Enum.TryParse<FontWeightEnum>(s2, out var f)) page.FontWeightEnum = f;

                TryGetDouble(json, "backgroundOpacity", out var d2); if (d2.HasValue) page.BackgroundOpacity = d2.Value;
                TryGetString(json, "backgroundColorHex", out var s3); if (s3 != null) page.BackgroundColorHex = s3;
                TryGetInt(json, "maxDimensions", out var i1); if (i1.HasValue) page.MaxDimensions = i1.Value;
                TryGetInt(json, "maxElements", out var i2); if (i2.HasValue) page.MaxElements = i2.Value;
                TryGetBool(json, "enableC", out var v4); if (v4.HasValue) page.EnableC = v4.Value;
                TryGetBool(json, "enableCpp", out var v5); if (v5.HasValue) page.EnableCpp = v5.Value;
                TryGetString(json, "logDirectory", out var s4); if (s4 != null) page.LogDirectory = s4;
                TryGetString(json, "indexDisplayMode", out var s5);
                if (s5 != null && Enum.TryParse<IndexDisplayMode>(s5, out var m)) page.IndexDisplayMode = m;
                TryGetBool(json, "enableDepthColors", out var v6); if (v6.HasValue) page.EnableDepthColors = v6.Value;
                TryGetString(json, "depthColors", out var s6); if (s6 != null) page.DepthColors = s6;
                TryGetBool(json, "enableVisualGDBDetection", out var v7); if (v7.HasValue) page.EnableVisualGDBDetection = v7.Value;
                TryGetBool(json, "enableVcxprojDetection", out var v8); if (v8.HasValue) page.EnableVcxprojDetection = v8.Value;
                TryGetBool(json, "enableCmakeDetection", out var v9); if (v9.HasValue) page.EnableCmakeDetection = v9.Value;
                TryGetInt(json, "debounceDelayMs", out var i3); if (i3.HasValue) page.DebounceDelayMs = i3.Value;
                TryGetBool(json, "useAutoBackgroundColor", out var v10); if (v10.HasValue) page.UseAutoBackgroundColor = v10.Value;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InlayIndex] Settings load failed: {ex.Message}");
                return false;
            }
        }

        private static string EscapeJson(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }

        private static void TryGetBool(string json, string key, out bool? result)
        {
            result = null;
            var search = $"\"{key}\":";
            var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            if (idx < json.Length)
            {
                if (json.Substring(idx, 4).Equals("true", StringComparison.OrdinalIgnoreCase))
                    result = true;
                else if (json.Substring(idx, 5).Equals("false", StringComparison.OrdinalIgnoreCase))
                    result = false;
            }
        }

        private static void TryGetInt(string json, string key, out int? result)
        {
            result = null;
            var search = $"\"{key}\":";
            var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            var end = idx;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end > idx && int.TryParse(json.Substring(idx, end - idx), out var i))
                result = i;
        }

        private static void TryGetDouble(string json, string key, out double? result)
        {
            result = null;
            var search = $"\"{key}\":";
            var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.')) end++;
            if (end > idx && double.TryParse(json.Substring(idx, end - idx), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                result = d;
        }

        private static void TryGetString(string json, string key, out string result)
        {
            result = null;
            var search = $"\"{key}\":\"";
            var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
            idx += search.Length;
            var end = json.IndexOf('"', idx);
            if (end > idx)
            {
                result = json.Substring(idx, end - idx)
                    .Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
        }
    }
}