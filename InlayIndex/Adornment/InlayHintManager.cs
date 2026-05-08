using InlayIndex.Models;
using InlayIndex.Parser;
using InlayIndex.Utils;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InlayIndex.Adornment
{
    public class InlayHintManager : IDisposable
    {
        private ITextBuffer _textBuffer;
        private List<InlayHintTag> _hintTags;
        private Dictionary<SnapshotSpan, UIElement> _adornmentCache;
        private ITextSnapshot _currentSnapshot;
        private bool _isDisposed;
        private int _renderGeneration;

        public event EventHandler TagsUpdated;

        public List<InlayHintTag> HintTags
        {
            get { return _hintTags; }
        }

        public ITextSnapshot CurrentSnapshot
        {
            get { return _currentSnapshot; }
        }

        public InlayHintManager(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
            _hintTags = new List<InlayHintTag>();
            _adornmentCache = new Dictionary<SnapshotSpan, UIElement>();
            _currentSnapshot = textBuffer.CurrentSnapshot;
            _isDisposed = false;

            _textBuffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            _currentSnapshot = e.After;
            _adornmentCache.Clear();
        }

        public void UpdateTags(List<InlayHintTag> hintTags)
        {
            LogHelper.WriteDebug($"[管理器] UpdateTags：旧 {_hintTags.Count} → 新 {hintTags.Count}");
            _hintTags = new List<InlayHintTag>(hintTags);
            _adornmentCache.Clear();
            TagsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public List<InlayHintTag> GetHintsForSpan(SnapshotSpan visibleSpan)
        {
            var result = new List<InlayHintTag>();

            foreach (var hintTag in _hintTags)
            {
                int position;
                if (hintTag.TrackingSpan != null)
                {
                    var span = hintTag.TrackingSpan.GetSpan(visibleSpan.Snapshot);
                    position = span.Start;
                }
                else
                {
                    position = PositionMapper.ClampPosition(visibleSpan.Snapshot, hintTag.StartPosition);
                }

                if (position >= visibleSpan.Start.Position && position <= visibleSpan.End.Position)
                    result.Add(hintTag);
            }

            return result;
        }

        public UIElement GetOrCreateAdornment(InlayHintTag hintTag, ITextSnapshot snapshot)
        {
            int gen = System.Threading.Interlocked.Increment(ref _renderGeneration);

            var spanOnRequested = new SnapshotSpan(snapshot, hintTag.StartPosition, 0);
            var spanOnCache = spanOnRequested.TranslateTo(_currentSnapshot, SpanTrackingMode.EdgeExclusive);

            UIElement adornment;
            if (!_adornmentCache.TryGetValue(spanOnCache, out adornment))
            {
                adornment = CreateAdornment(hintTag);
                adornment.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                adornment.Arrange(new Rect(0, 0, adornment.DesiredSize.Width, adornment.DesiredSize.Height));
                _adornmentCache[spanOnCache] = adornment;
                LogHelper.WriteDebug($"[渲染] Adorn[G{gen}] 新建：pos={hintTag.StartPosition}，文本='{hintTag.Text}'，cacheSize={_adornmentCache.Count}");
            }

            return adornment;
        }

        public void TrimAdornmentCache(SnapshotSpan visibleSpan)
        {
            if (_adornmentCache == null || _adornmentCache.Count == 0)
                return;

            var snapshot = _currentSnapshot;
            List<SnapshotSpan> toRemove = new List<SnapshotSpan>();

            foreach (var kv in _adornmentCache)
            {
                var translated = kv.Key.TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
                if (!translated.IntersectsWith(visibleSpan))
                    toRemove.Add(kv.Key);
            }

            foreach (var span in toRemove)
                _adornmentCache.Remove(span);

            if (toRemove.Count > 0)
                LogHelper.WriteDebug($"[渲染] TrimAdornmentCache：移除 {toRemove.Count} 个不可见项，剩余 {_adornmentCache.Count} 项（可见范围：{visibleSpan.Start.Position}-{visibleSpan.End.Position}）");
        }

        private UIElement CreateAdornment(InlayHintTag hintTag)
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
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 2.5)
            };

            return border;
        }

        private Brush CreateBackgroundBrush(InlayHintTag hintTag)
        {
            if (hintTag.BackgroundColor.HasValue)
            {
                var color = hintTag.BackgroundColor.Value;
                var opacity1 = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
                var alpha = (byte)(255 * (opacity1 / 100.0));
                var colorWithOpacity = Color.FromArgb(alpha, color.R, color.G, color.B);
                return new SolidColorBrush(colorWithOpacity);
            }

            var fgColor = hintTag.ForegroundColor.Value;
            var opacity2 = Math.Max(0, Math.Min(100, hintTag.BackgroundOpacity));
            var bgAlpha = (byte)(255 * (opacity2 / 100.0));
            var bgColor = Color.FromArgb(bgAlpha, fgColor.R, fgColor.G, fgColor.B);
            return new SolidColorBrush(bgColor);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (_textBuffer != null)
                    _textBuffer.Changed -= OnBufferChanged;
                _adornmentCache?.Clear();
                _adornmentCache = null;
                _hintTags?.Clear();
            }
        }
    }
}
