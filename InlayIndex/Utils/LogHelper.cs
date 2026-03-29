using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace InlayIndex.Utils
{
    public static class LogHelper
    {
        private static string LogFilePath;
        private static readonly object LockObject = new object();
        private static bool IsInitialized = false;
        // private static IVsOutputWindowPane OutputPane;
        private static LogLevel MinLogLevel = LogLevel.Debug;
        private static string _customLogDirectory;

        static LogHelper()
        {
            InitializeLogger();
        }

        public static void SetLogDirectory(string logDirectory)
        {
            _customLogDirectory = logDirectory;
            // 重新初始化日志系统，使用新目录
            IsInitialized = false;
            InitializeLogger();
        }

        private static void InitializeLogger()
        {
            if (IsInitialized)
                return;

            lock (LockObject)
            {
                if (IsInitialized)
                    return;

                try
                {
                    string logDir;

                    // 优先使用自定义目录
                    if (!string.IsNullOrEmpty(_customLogDirectory))
                    {
                        logDir = _customLogDirectory;
                    }
                    else
                    {
                        // 默认使用用户指定的目录
                        logDir = @"C:\Users\NexusStudio\source\repos\InlayIndex\InlayIndex\Log";
                    }

                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    // 每次启动创建新文件，时间戳到秒
                    string logFileName = $"InlayIndex_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                    LogFilePath = Path.Combine(logDir, logFileName);

                    IsInitialized = true;

                    // 写入日志头
                    WriteToFile(LogLevel.Info, "=== InlayIndex 插件日志开始 ===");
                    WriteToFile(LogLevel.Info, $"开始时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    WriteToFile(LogLevel.Info, $"日志文件：{LogFilePath}");
                    WriteToFile(LogLevel.Info, "===========================================");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[InlayIndex] 日志初始化失败: {ex.Message}");
                    // 尝试桌面作为备用
                    try
                    {
                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        LogFilePath = Path.Combine(desktopPath, $"InlayIndex_FALLBACK_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                        IsInitialized = true;
                    }
                    catch { }
                }
            }
        }

        private static void WriteToFile(LogLevel level, string message, Exception ex = null)
        {
            if (level < MinLogLevel)
                return;

            if (string.IsNullOrEmpty(LogFilePath))
                return;

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss}] [{1}]", DateTime.Now, level.ToString().ToUpperInvariant());
                sb.AppendLine($" {message}");

                if (ex != null)
                {
                    sb.AppendLine($"Exception: {ex.GetType().Name}");
                    sb.AppendLine($"Message: {ex.Message}");
                    sb.AppendLine($"StackTrace: {ex.StackTrace}");
                }

                string logMessage = sb.ToString();

                // 同时输出到调试窗口
                Debug.Write(logMessage);

                // 写入文件
                lock (LockObject)
                {
                    File.AppendAllText(LogFilePath, logMessage, Encoding.UTF8);
                }

                // 尝试输出到 OutputWindow (暂时禁用)
                // OutputPane?.OutputString(logMessage);
            }
            catch
            {
                // 如果日志写入失败，静默处理
            }
        }

        public static void WriteLog(string message, LogLevel level = LogLevel.Info)
        {
            WriteToFile(level, message);
        }

        public static void WriteLog(string message, params object[] args)
        {
            WriteLog(string.Format(message, args));
        }

        public static void WriteError(string message, Exception ex = null)
        {
            WriteToFile(LogLevel.Error, message, ex);
        }

        public static void WriteWarning(string message)
        {
            WriteLog(message, LogLevel.Warning);
        }

        public static void WriteDebug(string message)
        {
            WriteLog(message, LogLevel.Debug);
        }

        public static void WriteParseInfo(string message)
        {
            WriteLog($"[解析] {message}", LogLevel.Info);
        }

        public static void WriteTagInfo(string message)
        {
            WriteLog($"[标签] {message}", LogLevel.Info);
        }

        public static void WriteRenderInfo(string message)
        {
            WriteLog($"[渲染] {message}", LogLevel.Info);
        }

        public static void WriteViewInfo(string message)
        {
            WriteLog($"[视图] {message}", LogLevel.Info);
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
