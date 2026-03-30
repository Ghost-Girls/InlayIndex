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

            List<ITagSpan<IntraTextAdornmentTag>> result = new List<ITagSpan<IntraTextAdornmentTag>>();
            _isProcessing = true;
            try
            {
                var text = snapshot.GetText();

                if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
                {
                    LogHelper.WriteRenderInfo("文本太短，跳过解析");
                    yield break;
                }

                LogHelper.WriteRenderInfo($"开始解析文本，长度：{text.Length}");
                
                // 使用更完整的编译参数，确保 Clang 能正确解析 C++ 代码
                var compilationArgs = new string[] 
                { 
                    "-x", "c++",
                    "-std=c++17",
                    "-ferror-limit=0"
                };
                var parseResult = _parser.ParseCode(text, "temp.cpp", compilationArgs);
                
                if (!parseResult.Success)
                {
                    LogHelper.WriteError($"解析失败：{parseResult.ErrorMessage}", null);
                    yield break;
                }

                var hintTags = _generator.GenerateTags(parseResult);
                LogHelper.WriteRenderInfo($"解析完成，找到 {hintTags.Count} 个标签");
                
                foreach (var hintTag in hintTags)
                {
                    if (hintTag.StartPosition < 0 || hintTag.StartPosition > snapshot.Length)
                        continue;

                    var position = PositionMapper.ClampPosition(snapshot, hintTag.StartPosition);
                    var span = new SnapshotSpan(snapshot, position, 0);

                    if (spans.Any(s => s.IntersectsWith(span)))
                    {
                        var adornment = CreateAdornment(hintTag);
                        var intraTag = new IntraTextAdornmentTag(adornment, null);
                        result.Add(new TagSpan<IntraTextAdornmentTag>(span, intraTag));
                    }
                }

                LogHelper.WriteRenderInfo($"准备返回 {result.Count} 个标签");
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
            // 方案 B 不需要这个方法，因为 GetTags 会重新解析
            // 保留这个方法以兼容接口
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
