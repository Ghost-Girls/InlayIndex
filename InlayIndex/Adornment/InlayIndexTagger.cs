using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Adornments;

namespace InlayIndex.Adornment
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("C/C++")]
    [TagType(typeof(IntraTextAdornmentTag))]
    public class InlayIndexTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal ITextBufferFactoryService TextBufferFactoryService { get; set; }

        [Import]
        internal ITextEditorFactoryService TextEditorFactoryService { get; set; }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer == buffer)
            {
                return new InlayIndexTagger(textView, buffer) as ITagger<T>;
            }
            return null;
        }
    }

    public class InlayIndexTagger : ITagger<IntraTextAdornmentTag>
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private List<IntraTextAdornmentTag> _tags = new List<IntraTextAdornmentTag>();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlayIndexTagger(ITextView textView, ITextBuffer textBuffer)
        {
            _textView = textView;
            _textBuffer = textBuffer;

            _textBuffer.ChangedLowPriority += OnTextBufferChanged;
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            var snapshot = _textBuffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            // IntraTextAdornmentTag 不需要通过 GetTags 返回
            // 它们会直接添加到文本视图的 adornment layer
            yield break;
        }

        public void UpdateTags(List<Models.InlayHintTag> hintTags)
        {
            _tags.Clear();

            var snapshot = _textBuffer.CurrentSnapshot;

            foreach (var hintTag in hintTags)
            {
                try
                {
                    var position = Math.Min(hintTag.StartPosition, snapshot.Length);
                    var span = new SnapshotSpan(snapshot, position, 0);

                    var adornment = CreateAdornment(hintTag);

                    var intraTag = new IntraTextAdornmentTag(
                        adornment,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null);

                    _tags.Add(intraTag);
                }
                catch (Exception)
                {
                }
            }

            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
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
                Margin = new System.Windows.Thickness(1, 0, 1, 0)
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
