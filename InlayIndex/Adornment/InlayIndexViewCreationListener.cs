using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using InlayIndex.Parser;
using InlayIndex.Models;
using Microsoft.VisualStudio.Shell;

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
            var optionsPage = InlayIndexPackage.Instance?.GetOptionsPage();
            if (optionsPage == null)
            {
                return;
            }

            _parser = new ClangParser();
            _generator = new InlayHintGenerator(optionsPage);

            var tagger = textView.TextBuffer.Properties.GetProperty<InlayIndexTagger>(typeof(InlayIndexTagger));
            if (tagger == null)
            {
                tagger = new InlayIndexTagger(textView, textView.TextBuffer);
                textView.TextBuffer.Properties.AddProperty(typeof(InlayIndexTagger), tagger);
            }

            textView.TextBuffer.ChangedLowPriority += async (s, e) =>
            {
                await Task.Delay(500); 
                await UpdateTagsAsync(textView, tagger);
            };

            Task.Run(async () => await UpdateTagsAsync(textView, tagger));
        }

        private async Task UpdateTagsAsync(IWpfTextView textView, InlayIndexTagger tagger)
        {
            try
            {
                var snapshot = textView.TextBuffer.CurrentSnapshot;
                var text = snapshot.GetText();
                string filePath = null;
                var textDoc = textView.TextBuffer.Properties.GetProperty<Microsoft.VisualStudio.Text.ITextDocument>(typeof(Microsoft.VisualStudio.Text.ITextDocument));
                if (textDoc != null)
                {
                    filePath = textDoc.FilePath;
                }

                ParseResult result;
                if (!string.IsNullOrEmpty(filePath))
                {
                    result = _parser.ParseFile(filePath);
                }
                else
                {
                    result = _parser.ParseCode(text);
                }

                if (result.Success)
                {
                    var tags = _generator.GenerateTags(result);
                    tagger.UpdateTags(tags);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
