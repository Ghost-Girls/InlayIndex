using InlayIndex.Models;
using InlayIndex.Parser;
using InlayIndex.Utils;
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
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            InlayIndexImprovedTagger tagger;
            if (buffer.Properties.ContainsProperty(typeof(InlayIndexImprovedTagger)))
            {
                tagger = buffer.Properties.GetProperty<InlayIndexImprovedTagger>(typeof(InlayIndexImprovedTagger));
            }
            else
            {
                tagger = new InlayIndexImprovedTagger(buffer);
                buffer.Properties.AddProperty(typeof(InlayIndexImprovedTagger), tagger);
            }

            return tagger as ITagger<T>;
        }
    }

    public class InlayIndexImprovedTagger : ITagger<IntraTextAdornmentTag>, IDisposable
    {
        private ITextBuffer _textBuffer;
        private ClangParser _parser;
        private InlayHintGenerator _generator;
        private List<InlayHintTag> _hintTags;
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
            _hintTags = new List<InlayHintTag>();
            _isProcessing = false;
            _isDisposed = false;
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans == null || spans.Count == 0)
                yield break;

            if (_isProcessing || _isDisposed)
                yield break;

            var snapshot = spans[0].Snapshot;
            
            if (snapshot.Length > MaxFileSize)
                yield break;

            _isProcessing = true;
            List<ITagSpan<IntraTextAdornmentTag>> result = new List<ITagSpan<IntraTextAdornmentTag>>();
            
            try
            {
                foreach (var hintTag in _hintTags)
                {
                    SnapshotSpan span;
                    
                    if (hintTag.TrackingSpan == null)
                    {
                        var position = PositionMapper.ClampPosition(snapshot, hintTag.StartPosition);
                        span = new SnapshotSpan(snapshot, position, 0);
                    }
                    else
                    {
                        span = hintTag.TrackingSpan.GetSpan(snapshot);
                    }
                    
                    if (spans.Any(s => s.IntersectsWith(span)))
                    {
                        var adornment = CreateAdornment(hintTag);
                        var intraTag = new IntraTextAdornmentTag(adornment, null);
                        result.Add(new TagSpan<IntraTextAdornmentTag>(span, intraTag));
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("GetTags 异常", ex);
            }
            finally
            {
                _isProcessing = false;
            }
            
            foreach (var tag in result)
            {
                yield return tag;
            }
        }

        public void UpdateTags(List<InlayHintTag> hintTags)
        {
            _hintTags = new List<InlayHintTag>(hintTags);
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
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
