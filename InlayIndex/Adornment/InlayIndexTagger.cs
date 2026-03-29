using InlayIndex.Utils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;

namespace InlayIndex.Adornment
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("C/C++")]
    [TagType(typeof(IntraTextAdornmentTag))]
    public class InlayIndexTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            InlayIndexTagger tagger;
            // 检查是否已经有 Tagger 存储在属性中
            if (buffer.Properties.ContainsProperty(typeof(InlayIndexTagger)))
            {
                tagger = buffer.Properties.GetProperty<InlayIndexTagger>(typeof(InlayIndexTagger));
                LogHelper.WriteRenderInfo("TaggerProvider: 从属性获取到已存在的 Tagger");
            }
            else
            {
                tagger = new InlayIndexTagger(buffer);
                buffer.Properties.AddProperty(typeof(InlayIndexTagger), tagger);
                LogHelper.WriteRenderInfo("TaggerProvider: 创建并存储新的 Tagger");
            }

            return tagger as ITagger<T>;
        }
    }

    public class InlayIndexTagger : ITagger<IntraTextAdornmentTag>
    {
        private ITextBuffer _textBuffer;
        private List<ITagSpan<IntraTextAdornmentTag>> _tagSpans = new List<ITagSpan<IntraTextAdornmentTag>>();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlayIndexTagger(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;

            _textBuffer.ChangedLowPriority += OnTextBufferChanged;

            LogHelper.WriteRenderInfo($"InlayIndexTagger 创建成功 - 文本缓冲区：{_textBuffer.CurrentSnapshot.Length} 字符");
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            LogHelper.WriteRenderInfo($"文本缓冲区变化 - 旧长度：{e.Before.Length}, 新长度：{e.After.Length}");
            var snapshot = _textBuffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            LogHelper.WriteRenderInfo($"GetTags 被调用，spans.Count={spans.Count}, _tagSpans.Count={_tagSpans.Count}");
            foreach (var tagSpan in _tagSpans)
            {
                yield return tagSpan;
            }
        }

        public void UpdateTags(List<Models.InlayHintTag> hintTags)
        {
            // 确保在 UI 线程上执行
            if (!ThreadHelper.CheckAccess())
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    UpdateTags(hintTags);
                });
                return;
            }

            LogHelper.WriteRenderInfo($"开始更新标签 - 标签数：{hintTags.Count}");
            _tagSpans.Clear();

            var snapshot = _textBuffer.CurrentSnapshot;
            LogHelper.WriteRenderInfo($"当前快照长度：{snapshot.Length} 字符");

            int successCount = 0;
            int failCount = 0;

            // 打印文件前200个字符，方便调试
            string filePreview = snapshot.GetText().Substring(0, Math.Min(200, snapshot.Length));
            LogHelper.WriteRenderInfo($"文件预览：{filePreview}");

            foreach (var hintTag in hintTags)
            {
                try
                {
                    LogHelper.WriteDebug($"创建标签 - 文本：{hintTag.Text}, 原始位置：{hintTag.StartPosition}, 快照长度：{snapshot.Length}");
                    var position = Math.Min(hintTag.StartPosition, snapshot.Length);

                    // 打印位置周围的字符（位置-5 到 位置+10）
                    int contextStart = Math.Max(0, position - 5);
                    int contextEnd = Math.Min(snapshot.Length, position + 15);
                    string context = snapshot.GetText(new Span(contextStart, contextEnd - contextStart));
                    LogHelper.WriteDebug($"位置上下文：[{contextStart}-{contextEnd}]: '{context}'");

                    var span = new SnapshotSpan(snapshot, position, 0);

                    var adornment = CreateAdornment(hintTag);
                    LogHelper.WriteDebug($"装饰元素创建成功 - 类型：{adornment.GetType().Name}");

                    var intraTag = new IntraTextAdornmentTag(
                        adornment,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);

                    var tagSpan = new TagSpan<IntraTextAdornmentTag>(span, intraTag);
                    _tagSpans.Add(tagSpan);
                    successCount++;
                    LogHelper.WriteRenderInfo($"标签创建成功：{hintTag.Text} @ {position}");
                }
                catch (Exception ex)
                {
                    failCount++;
                    LogHelper.WriteError($"标签创建失败 - 文本：{hintTag.Text}, 位置：{hintTag.StartPosition}", ex);
                }
            }

            LogHelper.WriteRenderInfo($"标签更新完成 - 成功：{successCount}, 失败：{failCount}");

            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
            LogHelper.WriteRenderInfo("触发 TagsChanged 事件");
        }

        private System.Windows.UIElement CreateAdornment(Models.InlayHintTag hintTag)
        {
            var textBlock = new TextBlock
            {
                Text = hintTag.Text,
                FontSize = hintTag.FontSize,
                FontWeight = hintTag.FontWeight,
                Foreground = new SolidColorBrush(hintTag.ForegroundColor.Value),
                Background = CreateBackgroundBrush(hintTag),
                Padding = new System.Windows.Thickness(2, 0, 2, 0),
                Margin = new System.Windows.Thickness(1, 0, 1, 2.5),
                //VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            return textBlock;
        }

        private System.Windows.Media.Brush CreateBackgroundBrush(Models.InlayHintTag hintTag)
        {
            if (hintTag.BackgroundColor.HasValue)
            {
                var color = hintTag.BackgroundColor.Value;
                var opacity1 = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
                var alpha = (byte)(255 * (opacity1 / 100.0));
                var colorWithOpacity = System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B);
                return new SolidColorBrush(colorWithOpacity);
            }

            var fgColor = hintTag.ForegroundColor.Value;
            var opacity2 = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
            var bgAlpha = (byte)(255 * (opacity2 / 100.0));
            var bgColor = System.Windows.Media.Color.FromArgb(bgAlpha, fgColor.R, fgColor.G, fgColor.B);
            return new SolidColorBrush(bgColor);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _textBuffer.ChangedLowPriority -= OnTextBufferChanged;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
