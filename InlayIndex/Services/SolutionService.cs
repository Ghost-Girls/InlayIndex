using EnvDTE;
using EnvDTE80;
using InlayIndex.Utils;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace InlayIndex.Services
{
    public static class SolutionService
    {
        private static string _cachedSolutionDir;
        private static DateTime _cacheTime;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public static string GetSolutionDirectory()
        {
            if (_cachedSolutionDir != null && (DateTime.Now - _cacheTime) < CacheDuration)
            {
                return _cachedSolutionDir;
            }

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
                if (dte != null && !string.IsNullOrEmpty(dte.Solution?.FileName))
                {
                    var solutionDir = Path.GetDirectoryName(dte.Solution.FileName);
                    LogHelper.WriteDebug($"DTE API 获取解决方案目录：{solutionDir}");
                    _cachedSolutionDir = solutionDir;
                    _cacheTime = DateTime.Now;
                    return solutionDir;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebug($"DTE API 获取解决方案失败：{ex.Message}");
            }

            return null;
        }

        public static void InvalidateCache()
        {
            _cachedSolutionDir = null;
        }
    }
}
