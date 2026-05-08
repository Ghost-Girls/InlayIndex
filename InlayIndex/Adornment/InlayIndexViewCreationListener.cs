using EnvDTE;
using EnvDTE80;
using InlayIndex.Models;
using InlayIndex.Parser;
using InlayIndex.Utils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace InlayIndex.Adornment
{
    internal sealed class InlayIndexAdornmentLayerRegistration
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("InlayIndexHints")]
        [Order(After = PredefinedAdornmentLayers.Text)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;
    }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("C/C++")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class InlayIndexViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        private const string AdornmentLayerName = "InlayIndexHints";
        private const double AdornmentLeftMargin = 4.0;

        public void TextViewCreated(IWpfTextView textView)
        {
            LogHelper.WriteViewInfo($"视图创建 - 文件：{GetFilePath(textView)}");

            textView.VisualElement.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => InitializeHints(textView)));
        }

        private void InitializeHints(IWpfTextView textView)
        {
            try
            {
                LogHelper.WriteDebug("[视图] InitializeHints 开始（Loaded 后）");

                var solutionDir = GetSolutionDirectory();
                LogHelper.WriteDebug($"[视图] 解决方案目录：{solutionDir ?? "null"}");

                var optionsPage = InlayIndexPackage.Instance?.GetOptionsPage()
                    ?? Options.InlayIndexOptionsPage.Default;

                ClangParser parser;
                InlayHintGenerator generator;
                try
                {
                    parser = new ClangParser();
                    generator = new InlayHintGenerator(optionsPage);
                    LogHelper.WriteDebug("[视图] ClangParser + InlayHintGenerator 创建成功");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError("创建解析器/生成器异常", ex);
                    return;
                }

                IAdornmentLayer layer;
                try
                {
                    layer = textView.GetAdornmentLayer(AdornmentLayerName);
                    LogHelper.WriteDebug("[视图] GetAdornmentLayer 成功");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError("GetAdornmentLayer 异常", ex);
                    return;
                }

                InlayHintManager manager;
                try
                {
                    manager = textView.TextBuffer.Properties.GetOrCreateSingletonProperty(
                        typeof(InlayHintManager),
                        () => new InlayHintManager(textView.TextBuffer));
                    LogHelper.WriteDebug($"[视图] InlayHintManager 获取成功");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError("创建 InlayHintManager 异常", ex);
                    return;
                }

                System.Threading.CancellationTokenSource cts = null;

                textView.TextBuffer.ChangedLowPriority += (s, e) =>
                {
                    if (cts != null) { cts.Cancel(); cts.Dispose(); }
                    cts = new System.Threading.CancellationTokenSource();
                    var token = cts.Token;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(500, token);
                            if (token.IsCancellationRequested) return;

                            var snapshot = textView.TextBuffer.CurrentSnapshot;
                            LogHelper.WriteDebug($"[视图] ChangedLowPriority 重解析：版本={snapshot.Version.VersionNumber}");

                            var text = snapshot.GetText();
                            var docPath = GetFilePath(textView);
                            var compilationArgs = new[] { "-x", "c++", "-std=c++17", "-ferror-limit=0" };
                            var parseResult = parser.ParseCode(text, "temp.cpp", compilationArgs, snapshot, docPath, solutionDir);

                            if (!parseResult.Success)
                            {
                                LogHelper.WriteError($"解析失败：{parseResult.ErrorMessage}", null);
                                return;
                            }

                            var hintTags = generator.GenerateTags(parseResult, snapshot);
                            LogHelper.WriteDebug($"[视图] 生成 {hintTags.Count} 个标签，切换主线程放置...");

                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            manager.UpdateTags(hintTags);
                            PlaceAllHints(layer, manager, snapshot, textView);
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("ChangedLowPriority 处理异常", ex);
                        }
                    });
                };

                textView.LayoutChanged += (s, e) =>
                {
                    try
                    {
                        var layoutLayer = textView.GetAdornmentLayer(AdornmentLayerName);
                        if (textView.TextBuffer.Properties.TryGetProperty(typeof(InlayHintManager), out InlayHintManager layoutManager))
                        {
                            LogHelper.WriteDebug("[视图] LayoutChanged → 仅重新放置（不重解析）");
                            PlaceAllHints(layoutLayer, layoutManager, textView.TextSnapshot, textView);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteError("LayoutChanged 处理异常", ex);
                    }
                };

                LogHelper.WriteDebug("[视图] 触发初始解析...");
                var initSnapshot = textView.TextBuffer.CurrentSnapshot;
                ParseAndPlace(textView, layer, manager, parser, generator, initSnapshot, solutionDir);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("InitializeHints 顶层异常", ex);
            }
        }

        private void ParseAndPlace(
            IWpfTextView textView,
            IAdornmentLayer layer,
            InlayHintManager manager,
            ClangParser parser,
            InlayHintGenerator generator,
            ITextSnapshot snapshot,
            string solutionDir)
        {
            try
            {
                LogHelper.WriteDebug($"[视图] ParseAndPlace 开始，文本长度={snapshot.Length}");

                var text = snapshot.GetText();
                var docPath = GetFilePath(textView);

                var compilationArgs = new[] { "-x", "c++", "-std=c++17", "-ferror-limit=0" };
                var parseResult = parser.ParseCode(text, "temp.cpp", compilationArgs, snapshot, docPath, solutionDir);

                LogHelper.WriteDebug($"[视图] ParseCode 完成，成功={parseResult.Success}");

                if (!parseResult.Success)
                {
                    LogHelper.WriteError($"解析失败：{parseResult.ErrorMessage}", null);
                    return;
                }

                var hintTags = generator.GenerateTags(parseResult, snapshot);
                LogHelper.WriteDebug($"[视图] 生成 {hintTags.Count} 个标签");

                manager.UpdateTags(hintTags);
                PlaceAllHints(layer, manager, snapshot, textView);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("ParseAndPlace 异常", ex);
            }
        }

        private void PlaceAllHints(IAdornmentLayer layer, InlayHintManager manager, ITextSnapshot snapshot, IWpfTextView textView)
        {
            try
            {
                layer.RemoveAllAdornments();

                var lines = textView.TextViewLines;
                if (lines == null || !lines.IsValid) return;

                int count = 0;
                foreach (var hint in manager.HintTags)
                {
                    int pos;
                    if (hint.TrackingSpan != null)
                    {
                        pos = hint.TrackingSpan.GetSpan(snapshot).Start.Position;
                    }
                    else
                    {
                        pos = PositionMapper.ClampPosition(snapshot, hint.StartPosition);
                    }
                    if (pos < 0 || pos > snapshot.Length) continue;

                    var pt = new SnapshotPoint(snapshot, pos);
                    var line = lines.GetTextViewLineContainingBufferPosition(pt);

                    if (line == null) continue;
                    if (line.VisibilityState != Microsoft.VisualStudio.Text.Formatting.VisibilityState.FullyVisible)
                        continue;

                    var span = new SnapshotSpan(snapshot, pos, 0);
                    var element = CreateAdornment(hint);

                    System.Windows.Controls.Canvas.SetLeft(element, line.TextRight + AdornmentLeftMargin);
                    System.Windows.Controls.Canvas.SetTop(element, line.TextTop);

                    layer.AddAdornment(
                        AdornmentPositioningBehavior.OwnerControlled,
                        span,
                        null,
                        element,
                        null);
                    count++;
                }

                manager.TrimAdornmentCache(new SnapshotSpan(snapshot, 0, snapshot.Length));
                LogHelper.WriteDebug($"[渲染] PlaceAllHints：放置 {count} 个 hint 到 IAdornmentLayer (OwnerControlled + Manual Canvas)");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("PlaceAllHints 异常", ex);
            }
        }

        private System.Windows.Controls.Border CreateAdornment(InlayHintTag hintTag)
        {
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = hintTag.Text,
                FontSize = hintTag.FontSize,
                FontWeight = hintTag.FontWeight,
                Foreground = new System.Windows.Media.SolidColorBrush(hintTag.ForegroundColor.Value),
                Padding = new System.Windows.Thickness(2, 0, 2, 1.25),
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            var border = new System.Windows.Controls.Border
            {
                Child = textBlock,
                Background = CreateBackgroundBrush(hintTag),
                CornerRadius = new System.Windows.CornerRadius(3),
                Margin = new System.Windows.Thickness(1, 0, 1, 2.5)
            };

            return border;
        }

        private System.Windows.Media.Brush CreateBackgroundBrush(InlayHintTag hintTag)
        {
            if (hintTag.BackgroundColor.HasValue)
            {
                var color = hintTag.BackgroundColor.Value;
                var opacity1 = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
                var alpha = (byte)(255 * (opacity1 / 100.0));
                var colorWithOpacity = System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B);
                return new System.Windows.Media.SolidColorBrush(colorWithOpacity);
            }

            var fgColor = hintTag.ForegroundColor.Value;
            var opacity2 = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
            var bgAlpha = (byte)(255 * (opacity2 / 100.0));
            var bgColor = System.Windows.Media.Color.FromArgb(bgAlpha, fgColor.R, fgColor.G, fgColor.B);
            return new System.Windows.Media.SolidColorBrush(bgColor);
        }

        private static string GetFilePath(IWpfTextView textView)
        {
            try
            {
                return textView.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument))?.FilePath ?? "未知";
            }
            catch { return "未知"; }
        }

        private string GetSolutionDirectory()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = ServiceProvider.GetService(typeof(DTE)) as DTE2;
                if (dte != null && !string.IsNullOrEmpty(dte.Solution?.FileName))
                    return System.IO.Path.GetDirectoryName(dte.Solution.FileName);
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebug($"DTE API 失败：{ex.Message}");
            }
            return null;
        }
    }
}
