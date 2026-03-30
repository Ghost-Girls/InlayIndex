using InlayIndex.Models;
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
    public class InlayIndexImprovedTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            InlayIndexImprovedTagger tagger;
            // 检查是否已经有 Tagger 存储在属性中
            if (buffer.Properties.ContainsProperty(typeof(InlayIndexImprovedTagger)))
            {
                tagger = buffer.Properties.GetProperty<InlayIndexImprovedTagger>(typeof(InlayIndexImprovedTagger));
                LogHelper.WriteRenderInfo("ImprovedTaggerProvider: 从属性获取到已存在的 Tagger");
            }
            else
            {
                tagger = new InlayIndexImprovedTagger(buffer);
                buffer.Properties.AddProperty(typeof(InlayIndexImprovedTagger), tagger);
                LogHelper.WriteRenderInfo("ImprovedTaggerProvider: 创建并存储新的 Tagger");
            }

            return tagger as ITagger<T>;
        }
    }

    public class InlayIndexImprovedTagger : ITagger<IntraTextAdornmentTag>
    {
        private ITextBuffer _textBuffer;
        private List<InlayHintTag> _hintTags = new List<InlayHintTag>();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlayIndexImprovedTagger(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
            LogHelper.WriteRenderInfo($"InlayIndexImprovedTagger 创建成功 - 文本缓冲区：{_textBuffer.CurrentSnapshot.Length} 字符");
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            LogHelper.WriteRenderInfo($"GetTags 被调用，spans.Count={spans.Count}, _hintTags.Count={_hintTags.Count}");
            
            if (_hintTags.Count == 0)
                yield break;

            var snapshot = spans.Count > 0 ? spans[0].Snapshot : _textBuffer.CurrentSnapshot;
            
            foreach (var hintTag in _hintTags)
            {
                ITagSpan<IntraTextAdornmentTag> tagSpan = null;
                try
                {
                    var position = PositionMapper.ClampPosition(snapshot, hintTag.StartPosition);
                    var span = new SnapshotSpan(snapshot, position, 0);
                    
                    // 检查是否与请求的 spans 有交集
                    bool hasIntersection = false;
                    foreach (var requestSpan in spans)
                    {
                        if (span.IntersectsWith(requestSpan))
                        {
                            hasIntersection = true;
                            break;
                        }
                    }
                    
                    if (!hasIntersection)
                        continue;

                    var adornment = CreateAdornment(hintTag);

                    var intraTag = new IntraTextAdornmentTag(
                        adornment,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);

                    tagSpan = new TagSpan<IntraTextAdornmentTag>(span, intraTag);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError($"创建标签失败 - 文本：{hintTag.Text}, 位置：{hintTag.StartPosition}", ex);
                }
                
                if (tagSpan != null)
                {
                    yield return tagSpan;
                }
            }
        }

        public void UpdateTags(List<InlayHintTag> hintTags)
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

            LogHelper.WriteRenderInfo($"开始更新标签 - 旧标签数：{_hintTags.Count}, 新标签数：{hintTags.Count}");
            
            var snapshot = _textBuffer.CurrentSnapshot;
            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            
            // 先清除所有标签，触发 TagsChanged
            _hintTags.Clear();
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
            
            // 然后设置新标签
            _hintTags = new List<InlayHintTag>(hintTags);
            
            // 再次触发 TagsChanged 来显示新标签
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
            
            LogHelper.WriteRenderInfo("标签更新完成");
        }

        private System.Windows.UIElement CreateAdornment(InlayHintTag hintTag)
        {
            var textBlock = new TextBlock
            {
                Text = hintTag.Text,
                FontSize = hintTag.FontSize,
                FontWeight = hintTag.FontWeight,
                Foreground = new SolidColorBrush(hintTag.ForegroundColor.Value),
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
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
