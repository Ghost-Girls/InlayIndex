using InlayIndex.Models;
using InlayIndex.Parser;
using InlayIndex.Utils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace InlayIndex.Adornment
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("C/C++")]
    [TagType(typeof(IntraTextAdornmentTag))]
    public class InlayIndexImprovedTaggerProvider : ITaggerProvider
    {
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            InlayIndexImprovedTagger tagger;
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

    public class InlayIndexImprovedTagger : ITagger<IntraTextAdornmentTag>, IDisposable
    {
        private ITextBuffer _textBuffer;
        private ClangParser _parser;
        private InlayHintGenerator _generator;
        private List<InlayHintTag> _hintTags;  // ✅ 新增：缓存标签（方案 A）
        private bool _isProcessing;
        private bool _isDisposed;
        private const int MaxFileSize = 100 * 1024;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlayIndexImprovedTagger(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
            _parser = new ClangParser();
            var options = new Options.InlayIndexOptionsPage();
            _generator = new InlayHintGenerator(options);
            _hintTags = new List<InlayHintTag>();  // ✅ 初始化缓存
            _isProcessing = false;
            _isDisposed = false;
            
            LogHelper.WriteRenderInfo($"InlayIndexImprovedTagger 创建成功 - 文本缓冲区：{_textBuffer.CurrentSnapshot.Length} 字符");
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            LogHelper.WriteRenderInfo($"GetTags 被调用，spans.Count={spans.Count}");

            if (spans == null || spans.Count == 0)
                yield break;

            if (_isProcessing || _isDisposed)
            {
                LogHelper.WriteRenderInfo("正在处理或已释放，跳过");
                yield break;
            }

            var snapshot = spans[0].Snapshot;
            
            if (snapshot.Length > MaxFileSize)
            {
                LogHelper.WriteRenderInfo($"文件过大 ({snapshot.Length} bytes)，跳过");
                yield break;
            }

            _isProcessing = true;
            List<ITagSpan<IntraTextAdornmentTag>> result = new List<ITagSpan<IntraTextAdornmentTag>>();
            
            try
            {
                // ✅ 方案 A：使用缓存的标签和 ITrackingSpan
                LogHelper.WriteRenderInfo($"使用缓存的标签，数量：{_hintTags.Count}");
                
                foreach (var hintTag in _hintTags)
                {
                    // ✅ 从 ITrackingSpan 获取最新位置
                    if (hintTag.TrackingSpan == null)
                    {
                        // 如果没有 TrackingSpan，使用原始位置（向后兼容）
                        var position = PositionMapper.ClampPosition(snapshot, hintTag.StartPosition);
                        var span = new SnapshotSpan(snapshot, position, 0);
                        
                        if (spans.Any(s => s.IntersectsWith(span)))
                        {
                            var adornment = CreateAdornment(hintTag);
                            var intraTag = new IntraTextAdornmentTag(adornment, null);
                            result.Add(new TagSpan<IntraTextAdornmentTag>(span, intraTag));
                        }
                    }
                    else
                    {
                        // ✅ 使用 ITrackingSpan 获取最新位置（方案 A 核心）
                        var currentSpan = hintTag.TrackingSpan.GetSpan(snapshot);
                        
                        if (spans.Any(s => s.IntersectsWith(currentSpan)))
                        {
                            var adornment = CreateAdornment(hintTag);
                            var intraTag = new IntraTextAdornmentTag(adornment, null);
                            result.Add(new TagSpan<IntraTextAdornmentTag>(currentSpan, intraTag));
                        }
                    }
                }

                LogHelper.WriteRenderInfo($"GetTags 返回完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("GetTags 异常", ex);
            }
            finally
            {
                _isProcessing = false;
            }
            
            // ✅ 在 try-catch-finally 之外 yield return
            foreach (var tag in result)
            {
                yield return tag;
            }
        }

        public void UpdateTags(List<InlayHintTag> hintTags)
        {
            // ✅ 方案 A：保存标签到缓存
            _hintTags = new List<InlayHintTag>(hintTags);
            LogHelper.WriteRenderInfo($"UpdateTags: 已更新缓存，标签数量：{_hintTags.Count}");
        }

        public void RaiseTagsChanged(SnapshotSpan span)
        {
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
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
            if (!_isDisposed)
            {
                _isDisposed = true;
                LogHelper.WriteRenderInfo("InlayIndexImprovedTagger 已释放");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
