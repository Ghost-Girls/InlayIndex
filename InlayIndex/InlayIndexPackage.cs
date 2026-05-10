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

            Options.InlayIndexOptionsPage.SyncFrom(_optionsPage);

            Options.SettingsStore.LoadInto(_optionsPage);
            Options.InlayIndexOptionsPage.SyncFrom(_optionsPage);

#if DEBUG
            if (!string.IsNullOrEmpty(_optionsPage.LogDirectory))
            {
                Utils.LogHelper.SetLogDirectory(_optionsPage.LogDirectory);
            }
#endif

            Utils.LogHelper.WriteLog("=== InlayIndexPackage 初始化完成 ===");
#if DEBUG
            Utils.LogHelper.WriteLog($"日志目录：{_optionsPage.LogDirectory}");
#endif
            Utils.LogHelper.WriteLog($"防抖延迟：{_optionsPage.DebounceDelayMs}ms");

            Options.InlayIndexOptionsPage.FirePackageInitialized(_optionsPage);
            Utils.LogHelper.WriteLog("插件加载成功！");
        }

        public Options.InlayIndexOptionsPage GetOptionsPage()
        {
            return _optionsPage;
        }

        #endregion
    }
}
