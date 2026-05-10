using InlayIndex.Models;
using InlayIndex.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InlayIndex.Adornment
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("C/C++")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    [TagType(typeof(IntraTextAdornmentTag))]
    public class InlayHintTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            if (typeof(T) == typeof(IntraTextAdornmentTag))
            {
                return (ITagger<T>)(object)new InlayHintTagger(textView, buffer);
            }
            return null;
        }
    }

    public class InlayHintTagger : ITagger<IntraTextAdornmentTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly Dictionary<int, Border> _elementCache = new Dictionary<int, Border>();
        private InlayHintManager _manager;
        private bool _subscribed;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlayHintTagger(ITextView view, ITextBuffer buffer)
        {
            _buffer = buffer;
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed)
                return;

            if (_buffer.Properties.TryGetProperty(typeof(InlayHintManager), out InlayHintManager manager))
            {
                _manager = manager;
                manager.TagsUpdated += OnTagsUpdated;
                _subscribed = true;
                LogHelper.WriteDebug("[Tagger-B] 订阅 TagsUpdated 成功");
            }
        }

        private void OnTagsUpdated(object sender, EventArgs e)
        {
            LogHelper.WriteDebug("[Tagger-B] TagsUpdated → 清空元素缓存，触发 TagsChanged");
            _elementCache.Clear();
            var snapshot = _buffer.CurrentSnapshot;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            TrySubscribe();

            if (_manager == null)
                yield break;

            var snapshot = spans[0].Snapshot;
            var hintTags = _manager.HintTags;

            if (hintTags == null || hintTags.Count == 0)
                yield break;

            foreach (var hint in hintTags)
            {
                int pos;
                if (hint.TrackingSpan != null)
                {
                    var trackedSpan = hint.TrackingSpan.GetSpan(snapshot);
                    pos = trackedSpan.Start.Position;
                }
                else
                {
                    pos = PositionMapper.ClampPosition(snapshot, hint.StartPosition);
                }

                if (pos < 0 || pos > snapshot.Length)
                    continue;

                var span = new SnapshotSpan(snapshot, pos, 0);

                bool inVisibleRange = false;
                foreach (var visibleSpan in spans)
                {
                    if (span.Start.Position >= visibleSpan.Start.Position &&
                        span.Start.Position <= visibleSpan.End.Position)
                    {
                        inVisibleRange = true;
                        break;
                    }
                }
                if (!inVisibleRange)
                    continue;

                if (!_elementCache.TryGetValue(hint.StartPosition, out var element))
                {
                    element = CreateAdornmentElement(hint);
                    _elementCache[hint.StartPosition] = element;
                }
                var tag = new IntraTextAdornmentTag(element, null, PositionAffinity.Predecessor);
                yield return new TagSpan<IntraTextAdornmentTag>(span, tag);
            }
        }

        private Border CreateAdornmentElement(InlayHintTag hintTag)
        {
            var textBlock = new TextBlock
            {
                Text = hintTag.Text,
                FontSize = hintTag.FontSize,
                FontWeight = hintTag.FontWeight,
                Foreground = new SolidColorBrush(hintTag.ForegroundColor.Value),
                Padding = new Thickness(2, 0, 2, 1.25),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Child = textBlock,
                Background = CreateBackgroundBrush(hintTag),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(1, 0, 1, 2.5)
            };

            return border;
        }

        private Brush CreateBackgroundBrush(InlayHintTag hintTag)
        {
            if (hintTag.BackgroundColor.HasValue)
            {
                var color = hintTag.BackgroundColor.Value;
                var opacity = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
                var alpha = (byte)(255 * (opacity / 100.0));
                var colorWithOpacity = Color.FromArgb(alpha, color.R, color.G, color.B);
                return new SolidColorBrush(colorWithOpacity);
            }

            var bgColor = InlayIndex.Options.InlayIndexOptionsPage.Default.GetBackgroundColor();
            var bgOpacity = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
            var bgAlpha = (byte)(255 * (bgOpacity / 100.0));
            return new SolidColorBrush(Color.FromArgb(bgAlpha, bgColor.R, bgColor.G, bgColor.B));
        }
    }
}