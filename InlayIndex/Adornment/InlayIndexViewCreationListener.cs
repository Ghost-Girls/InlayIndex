using InlayIndex.Parser;
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

        public void TextViewCreated(IWpfTextView textView)
        {
            LogHelper.WriteViewInfo($"视图创建 - 文件：{textView.TextSnapshot.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument))?.FilePath ?? "未知"}");
            LogHelper.WriteViewInfo($"文本视图长度：{textView.TextSnapshot.Length} 字符");

            // 先尝试从包获取选项页，如果没有则用默认值
            var optionsPage = InlayIndexPackage.Instance?.GetOptionsPage();
            if (optionsPage == null)
            {
                LogHelper.WriteViewInfo("包尚未初始化，使用默认配置");
                optionsPage = Options.InlayIndexOptionsPage.Default;
            }
            else
            {
                LogHelper.WriteViewInfo("选项页加载成功");
            }

            InlayIndexImprovedTagger tagger = null;

            try
            {
                LogHelper.WriteViewInfo("准备创建 ClangParser...");
                _parser = new ClangParser();
                LogHelper.WriteViewInfo("ClangParser 创建成功");

                LogHelper.WriteViewInfo("准备创建 InlayHintGenerator...");
                _generator = new InlayHintGenerator(optionsPage);
                LogHelper.WriteViewInfo("InlayHintGenerator 创建成功");

                LogHelper.WriteViewInfo("解析器和生成器初始化完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("创建解析器或生成器时发生异常", ex);
                return;
            }

            try
            {
                LogHelper.WriteViewInfo("准备获取 ImprovedTagger...");
                // 从属性获取或创建 Tagger
                if (textView.TextBuffer.Properties.ContainsProperty(typeof(InlayIndexImprovedTagger)))
                {
                    tagger = textView.TextBuffer.Properties.GetProperty<InlayIndexImprovedTagger>(typeof(InlayIndexImprovedTagger));
                    LogHelper.WriteViewInfo("使用已存在的 ImprovedTagger");
                }
                else
                {
                    LogHelper.WriteViewInfo("创建新的 ImprovedTagger");
                    tagger = new InlayIndexImprovedTagger(textView.TextBuffer);
                    textView.TextBuffer.Properties.AddProperty(typeof(InlayIndexImprovedTagger), tagger);
                }
                LogHelper.WriteViewInfo("ImprovedTagger 获取成功");

                LogHelper.WriteViewInfo("准备注册文本变化事件...");
                // 使用一个取消令牌源来管理延迟更新
                System.Threading.CancellationTokenSource cancellationTokenSource = null;
                
                textView.TextBuffer.ChangedLowPriority += (s, e) =>
                {
                    LogHelper.WriteViewInfo("文本缓冲区变化事件触发");
                    
                    // 取消之前的延迟任务
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
                            // 延迟：500ms，与 DEMO 项目一致
                            await Task.Delay(500, token);
                            
                            if (!token.IsCancellationRequested)
                            {
                                // 直接触发 TagsChanged 事件，让 Tagger 重新解析
                                TriggerTagsChanged(textView, tagger);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            LogHelper.WriteViewInfo("更新任务被取消");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("文本变化事件处理时发生异常", ex);
                        }
                    });
                };
                LogHelper.WriteViewInfo("文本变化事件注册成功");

                // 注册文件保存事件
                var textDoc = textView.TextBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
                if (textDoc != null)
                {
                    textDoc.FileActionOccurred += (s, e) =>
                    {
                        if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
                        {
                            LogHelper.WriteViewInfo("文件保存事件触发");
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // 直接触发 TagsChanged 事件
                                    TriggerTagsChanged(textView, tagger);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteError("文件保存事件处理时发生异常", ex);
                                }
                            });
                        }
                    };
                    LogHelper.WriteViewInfo("文件保存事件注册成功");
                }

                // 注册视图获得焦点事件
                textView.GotAggregateFocus += (s, e) =>
                {
                    LogHelper.WriteViewInfo("视图获得焦点事件触发");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 直接触发 TagsChanged 事件
                            TriggerTagsChanged(textView, tagger);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("视图获得焦点事件处理时发生异常", ex);
                        }
                    });
                };
                LogHelper.WriteViewInfo("视图获得焦点事件注册成功");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("处理 Tagger 时发生异常", ex);
                return;
            }

            LogHelper.WriteViewInfo("开始初次更新标签");
            try
            {
                LogHelper.WriteViewInfo("准备启动 Task.Run");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        LogHelper.WriteViewInfo("Task.Run 内部开始执行");
                        // 初次加载时触发标签更新
                        TriggerTagsChanged(textView, tagger);
                        LogHelper.WriteViewInfo("TagsChanged 触发完成");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteError("Task.Run 内部发生异常", ex);
                    }
                });
                LogHelper.WriteViewInfo("Task.Run 已启动");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("启动 Task.Run 时发生异常", ex);
            }
        }

        private void TriggerTagsChanged(IWpfTextView textView, InlayIndexImprovedTagger tagger)
        {
            try
            {
                var snapshot = textView.TextBuffer.CurrentSnapshot;
                var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
                
                // 触发 TagsChanged 事件，让 Tagger 的 GetTags 方法重新解析
                tagger.RaiseTagsChanged(fullSpan);
                
                LogHelper.WriteViewInfo($"TagsChanged 事件已触发 - 快照长度：{snapshot.Length}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("触发 TagsChanged 事件时发生异常", ex);
            }
        }
    }
}
