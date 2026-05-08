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
using System.Windows.Threading;

namespace InlayIndex.Adornment
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("C/C++")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class InlayIndexViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

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
                            LogHelper.WriteDebug($"[视图] 生成 {hintTags.Count} 个标签，更新管理器...");

                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            manager.UpdateTags(hintTags);
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("ChangedLowPriority 处理异常", ex);
                        }
                    });
                };

                LogHelper.WriteDebug("[视图] 触发初始解析...");
                var initSnapshot = textView.TextBuffer.CurrentSnapshot;
                ParseAndUpdate(textView, manager, parser, generator, initSnapshot, solutionDir);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("InitializeHints 顶层异常", ex);
            }
        }

        private void ParseAndUpdate(
            IWpfTextView textView,
            InlayHintManager manager,
            ClangParser parser,
            InlayHintGenerator generator,
            ITextSnapshot snapshot,
            string solutionDir)
        {
            try
            {
                LogHelper.WriteDebug($"[视图] ParseAndUpdate 开始，文本长度={snapshot.Length}");

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
                LogHelper.WriteDebug($"[视图] 管理器已更新 → TagsUpdated 事件发送给 Tagger");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("ParseAndUpdate 异常", ex);
            }
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