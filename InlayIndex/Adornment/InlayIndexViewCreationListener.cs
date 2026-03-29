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

            InlayIndexTagger tagger = null;

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
                LogHelper.WriteViewInfo("准备获取 Tagger...");
                // 从属性获取或创建 Tagger
                if (textView.TextBuffer.Properties.ContainsProperty(typeof(InlayIndexTagger)))
                {
                    tagger = textView.TextBuffer.Properties.GetProperty<InlayIndexTagger>(typeof(InlayIndexTagger));
                    LogHelper.WriteViewInfo("使用已存在的 Tagger");
                }
                else
                {
                    LogHelper.WriteViewInfo("创建新的 Tagger");
                    tagger = new InlayIndexTagger(textView.TextBuffer);
                    textView.TextBuffer.Properties.AddProperty(typeof(InlayIndexTagger), tagger);
                }

                LogHelper.WriteViewInfo("准备注册文本变化事件...");
                textView.TextBuffer.ChangedLowPriority += (s, e) =>
                {
                    LogHelper.WriteViewInfo("文本缓冲区变化事件触发");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(500);
                            await UpdateTagsAsync(textView, tagger);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteError("文本变化事件处理时发生异常", ex);
                        }
                    });
                };
                LogHelper.WriteViewInfo("文本变化事件注册成功");
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

        private async Task UpdateTagsAsync(IWpfTextView textView, InlayIndexTagger tagger)
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
                // 详细诊断：打印开头和结尾的字符，检查 BOM 和换行符
                LogHelper.WriteViewInfo($"解析代码字符串（来自编辑器快照），长度：{text.Length}，文件：{filePath}");
                LogHelper.WriteViewInfo($"前 20 个字符（带索引）：");
                for (int i = 0; i < Math.Min(20, text.Length); i++)
                {
                    char c = text[i];
                    string desc = char.IsControl(c) ? $"\\x{(int)c:X2}" : c.ToString();
                    LogHelper.WriteViewInfo($"  [{i}]: '{desc}'");
                }

                // 检查换行符
                int crCount = 0, lfCount = 0, crlfCount = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        crlfCount++;
                        i++;
                    }
                    else if (text[i] == '\r') crCount++;
                    else if (text[i] == '\n') lfCount++;
                }
                LogHelper.WriteViewInfo($"换行符统计：CR={crCount}, LF={lfCount}, CRLF={crlfCount}");

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
