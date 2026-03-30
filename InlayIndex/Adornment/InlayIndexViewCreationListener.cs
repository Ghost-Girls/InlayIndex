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
                LogHelper.WriteViewInfo("SpaceNegotiatingTagger 获取成功");

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
                            // 极短延迟：50ms，实现 VSCode 风格的快速响应
                            await Task.Delay(50, token);
                            
                            if (!token.IsCancellationRequested)
                            {
                                await UpdateTagsAsync(textView, tagger);
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
                                    await UpdateTagsAsync(textView, tagger);
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
                            await UpdateTagsAsync(textView, tagger);
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
                        await UpdateTagsAsync(textView, tagger);
                        LogHelper.WriteViewInfo("UpdateTagsAsync 执行完成");
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

        private async Task UpdateTagsAsync(IWpfTextView textView, InlayIndexImprovedTagger tagger)
        {
            LogHelper.WriteViewInfo("UpdateTagsAsync 被调用了！");
            try
            {
                LogHelper.WriteViewInfo("开始更新标签...");
                var snapshot = textView.TextBuffer.CurrentSnapshot;
                var text = snapshot.GetText();
                string filePath = null;
                var textDoc = textView.TextBuffer.Properties.GetProperty<Microsoft.VisualStudio.Text.ITextDocument>(typeof(Microsoft.VisualStudio.Text.ITextDocument));
                if (textDoc != null)
                {
                    filePath = textDoc.FilePath;
                    LogHelper.WriteViewInfo($"文件路径：{filePath}");
                }

                ParseResult result;
                // 强制使用编辑器快照中的内容，确保与显示内容一致
                LogHelper.WriteViewInfo($"解析代码字符串（来自编辑器快照），长度：{text.Length}，文件：{filePath}");

                result = _parser.ParseCode(text, filePath);

                if (result.Success)
                {
                    LogHelper.WriteViewInfo($"解析成功 - 数组：{result.Arrays.Count}, 枚举：{result.Enums.Count}, 结构体：{result.Structs.Count}");
                    var tags = _generator.GenerateTags(result);
                    LogHelper.WriteViewInfo($"生成 {tags.Count} 个标签，开始更新");
                    tagger.UpdateTags(tags);
                    LogHelper.WriteViewInfo("标签更新完成");
                }
                else
                {
                    LogHelper.WriteError($"解析失败：{result.ErrorMessage}", null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("更新标签时发生异常", ex);
            }
        }
    }
}
