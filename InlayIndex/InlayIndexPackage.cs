using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace InlayIndex
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(InlayIndexPackage.PackageGuidString)]
    [ProvideOptionPage(
        typeof(Options.InlayIndexOptionsPage),
        "InlayIndex",
        "General",
        0,
        0,
        true)]
    [ProvideProfile(
        typeof(Options.InlayIndexOptionsPage),
        "InlayIndex",
        "InlayIndex Settings",
        0,
        0,
        true)]
    public sealed class InlayIndexPackage : AsyncPackage
    {
        public const string PackageGuidString = "e3d85577-453c-4151-ac1d-67800d454a75";

        public static InlayIndexPackage Instance { get; private set; }

        private Options.InlayIndexOptionsPage _optionsPage;

        #region Package Members

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Instance = this;
            _optionsPage = (Options.InlayIndexOptionsPage)GetDialogPage(typeof(Options.InlayIndexOptionsPage));
        }

        public Options.InlayIndexOptionsPage GetOptionsPage()
        {
            return _optionsPage;
        }

        #endregion
    }
}
