using InlayIndex.Models;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace InlayIndex.Adornment
{
    public class InlayHintManager : IDisposable
    {
        private List<InlayHintTag> _hintTags;
        private bool _isDisposed;

        public event EventHandler TagsUpdated;

        public List<InlayHintTag> HintTags
        {
            get { return _hintTags; }
        }

        public InlayHintManager(ITextBuffer textBuffer)
        {
            _hintTags = new List<InlayHintTag>();
            _isDisposed = false;
        }

        public void UpdateTags(List<InlayHintTag> hintTags)
        {
            InlayIndex.Utils.LogHelper.WriteDebug($"[管理器] UpdateTags：旧 {_hintTags.Count} → 新 {hintTags.Count}");
            _hintTags = new List<InlayHintTag>(hintTags);
            TagsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _hintTags?.Clear();
            }
        }
    }
}