using EnvDTE;
using EnvDTE80;
using InlayIndex.Parser;
using InlayIndex.Services;
using InlayIndex.Utils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace InlayIndex.Adornment
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("C/C++")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class InlayIndexViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        private ClangParser _parser;
        private InlayHintGenerator _generator;
        private string _solutionDir;

        public void TextViewCreated(IWpfTextView textView)
        {
            var filePath = textView.TextSnapshot.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument))?.FilePath ?? "未知";
            LogHelper.WriteViewInfo($"视图创建 - 文件：{filePath}");

            _solutionDir = ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return GetSolutionDirectoryFromDTE();
            });

            var optionsPage = InlayIndexPackage.Instance?.GetOptionsPage() ?? Options.InlayIndexOptionsPage.Default;

            InlayIndexImprovedTagger tagger = null;

            try
            {
                _parser = new ClangParser();
                _generator = new InlayHintGenerator(optionsPage);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("创建解析器或生成器时发生异常", ex);
                return;
            }

            try
            {
                if (textView.TextBuffer.Properties.ContainsProperty(typeof(InlayIndexImprovedTagger)))
                {
                    tagger = textView.TextBuffer.Properties.GetProperty<InlayIndexImprovedTagger>(typeof(InlayIndexImprovedTagger));
                }
                else
                {
                    tagger = new InlayIndexImprovedTagger(textView.TextBuffer);
                    textView.TextBuffer.Properties.AddProperty(typeof(InlayIndexImprovedTagger), tagger);
                }

                System.Threading.CancellationTokenSource cancellationTokenSource = null;
                
                textView.TextBuffer.ChangedLowPriority += (s, e) =>
                {
                    if (cancellationTokenSource != null)
                    {
                        cancellationTokenSource.Cancel();
                        cancellationTokenSource.Dispose();
                    }
                    
                    cancellationTokenSource = new System.Threading.CancellationTokenSource();
                    var token = cancellationTokenSource.Token;
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(500, token);
                            
                            if (!token.IsCancellationRequested)
                            {
                                var snapshot = textView.TextBuffer.CurrentSnapshot;
                                var text = snapshot.GetText();
                                var docPath = textView.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument))?.FilePath;
                                
                                var compilationArgs = new string[] 
                                { 
                                    "-x", "c++",
                                    "-std=c++17",
                                    "-ferror-limit=0"
                                };
                                var parseResult = _parser.ParseCode(text, "temp.cpp", compilationArgs, snapshot, docPath, _solutionDir);
                                
                                if (parseResult.Success)
                                {
                                    var hintTags = _generator.GenerateTags(parseResult, snapshot);
                                    tagger.UpdateTags(hintTags);
                                    
                                    var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
                                    tagger.RaiseTagsChanged(fullSpan);
                                }
                                else
                                {
                                    LogHelper.WriteError($"解析失败：{parseResult.ErrorMessage}", null);
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("文本变化事件处理时发生异常", ex);
                        }
                    });
                };

                var textDoc = textView.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
                if (textDoc != null)
                {
                    textDoc.FileActionOccurred += (s, e) =>
                    {
                        if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    TriggerTagsChanged(textView, tagger);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteError("文件保存事件处理时发生异常", ex);
                                }
                            });
                        }
                    };
                }

                textView.GotAggregateFocus += (s, e) =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            TriggerTagsChanged(textView, tagger);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("视图获得焦点事件处理时发生异常", ex);
                        }
                    });
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("处理 Tagger 时发生异常", ex);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var snapshot = textView.TextBuffer.CurrentSnapshot;
                    var text = snapshot.GetText();
                    var docPath = textView.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument))?.FilePath;
                    
                    var compilationArgs = new string[] 
                    { 
                        "-x", "c++",
                        "-std=c++17",
                        "-ferror-limit=0"
                    };
                    var parseResult = _parser.ParseCode(text, "temp.cpp", compilationArgs, snapshot, docPath, _solutionDir);
                    
                    if (parseResult.Success)
                    {
                        var hintTags = _generator.GenerateTags(parseResult, snapshot);
                        tagger.UpdateTags(hintTags);
                        
                        var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
                        tagger.RaiseTagsChanged(fullSpan);
                    }
                    else
                    {
                        LogHelper.WriteError($"解析失败：{parseResult.ErrorMessage}", null);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError("初次加载标签时发生异常", ex);
                }
            });
        }

        private void TriggerTagsChanged(IWpfTextView textView, InlayIndexImprovedTagger tagger)
        {
            try
            {
                var snapshot = textView.TextBuffer.CurrentSnapshot;
                var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
                tagger.RaiseTagsChanged(fullSpan);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("触发 TagsChanged 事件时发生异常", ex);
            }
        }

        private string GetSolutionDirectoryFromDTE()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = ServiceProvider.GetService(typeof(DTE)) as DTE2;
                if (dte != null && !string.IsNullOrEmpty(dte.Solution?.FileName))
                {
                    var solutionDir = System.IO.Path.GetDirectoryName(dte.Solution.FileName);
                    LogHelper.WriteDebug($"DTE API 获取解决方案目录：{solutionDir}");
                    return solutionDir;
                }
                else
                {
                    LogHelper.WriteDebug("DTE API：当前无打开的解决方案");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebug($"DTE API 调用失败：{ex.Message}");
            }
            return null;
        }
    }
}
