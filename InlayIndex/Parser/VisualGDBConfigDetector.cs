using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using InlayIndex.Utils;

namespace InlayIndex.Parser
{
    /// <summary>
    /// VisualGDB 项目配置探测器
    /// 自动从 VisualGDB 项目配置中提取 Include 路径和预定义宏
    /// </summary>
    public class VisualGDBConfigDetector
    {
        private readonly bool _enableVisualGDBDetection;
        private readonly bool _enableVcxprojDetection;
        private readonly bool _enableCmakeDetection;
        private string _currentSourceFile;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="enableVisualGDBDetection">是否启用 VisualGDB 配置探测</param>
        /// <param name="enableVcxprojDetection">是否启用普通 vcxproj 配置探测</param>
        /// <param name="enableCmakeDetection">是否启用 CMake 配置探测</param>
        public VisualGDBConfigDetector(
            bool enableVisualGDBDetection = true,
            bool enableVcxprojDetection = true,
            bool enableCmakeDetection = false)
        {
            _enableVisualGDBDetection = enableVisualGDBDetection;
            _enableVcxprojDetection = enableVcxprojDetection;
            _enableCmakeDetection = enableCmakeDetection;
        }

        /// <summary>
        /// 探测项目配置
        /// </summary>
        /// <param name="filePath">当前编辑的文件路径</param>
        /// <returns>项目配置，如果探测失败则返回 null</returns>
        public VisualGDBConfig DetectConfig(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                LogHelper.WriteParseInfo("配置探测：文件路径无效");
                return null;
            }

            LogHelper.WriteParseInfo($"配置探测：开始探测，当前文件：{filePath}");
            
            // 保存当前源文件路径，用于后续添加源文件目录到Include路径
            _currentSourceFile = filePath;

            try
            {
                // 步骤 1：查找解决方案目录（优先使用文件所在目录）
                var solutionDir = FindSolutionDirectory(filePath);
                if (solutionDir == null)
                {
                    LogHelper.WriteParseInfo("配置探测：未找到解决方案目录");
                    return null;
                }

                return DetectConfigFromSolutionDir(solutionDir);
            }
            catch (Exception ex)
            {
                LogHelper.WriteError("配置探测失败", ex);
                return null;
            }
        }

        /// <summary>
        /// 从解决方案目录探测项目配置（供外部传入解决方案路径时使用）
        /// </summary>
        /// <param name="solutionDir">解决方案目录路径</param>
        /// <returns>项目配置，如果探测失败则返回 null</returns>
        public VisualGDBConfig DetectConfigFromSolutionDir(string solutionDir)
        {
            if (string.IsNullOrEmpty(solutionDir) || !Directory.Exists(solutionDir))
            {
                LogHelper.WriteParseInfo($"配置探测：解决方案目录无效：{solutionDir}");
                return null;
            }

            LogHelper.WriteParseInfo($"配置探测：找到解决方案目录：{solutionDir}");

            // 步骤 2：尝试探测 VisualGDB 配置
            if (_enableVisualGDBDetection)
            {
                var visualgdbConfig = TryDetectVisualGDB(solutionDir);
                if (visualgdbConfig != null)
                {
                    return visualgdbConfig;
                }
            }

            // 步骤 3：尝试探测普通 vcxproj 配置
            if (_enableVcxprojDetection)
            {
                var vcxprojConfig = TryDetectVcxproj(solutionDir);
                if (vcxprojConfig != null)
                {
                    return vcxprojConfig;
                }
            }

            // 步骤 4：尝试探测 CMake 配置（可选）
            if (_enableCmakeDetection)
            {
                var cmakeConfig = TryDetectCMake(solutionDir);
                if (cmakeConfig != null)
                {
                    return cmakeConfig;
                }
            }

            LogHelper.WriteParseInfo("配置探测：未找到任何项目配置");
            return null;
        }

        /// <summary>
        /// 尝试探测 VisualGDB 配置
        /// </summary>
        private VisualGDBConfig TryDetectVisualGDB(string solutionDir)
        {
            LogHelper.WriteParseInfo($"配置探测：尝试探测 VisualGDB 配置，起始目录：{solutionDir}");

            string visualgdbDir = null;
            string projectConfigDir = null;

            // 情况 1：检查当前目录是否就是 visualgdb 目录（直接包含项目子目录）
            var hasProjectSubDirs = Directory.GetDirectories(solutionDir)
                .Any(d => Directory.GetFiles(d, "*.vcxproj").Any());
            
            if (hasProjectSubDirs)
            {
                LogHelper.WriteParseInfo($"配置探测：当前目录包含项目子目录（可能是 visualgdb 目录）");
                visualgdbDir = solutionDir;
                projectConfigDir = Directory.GetDirectories(solutionDir)
                    .FirstOrDefault(d => Directory.GetFiles(d, "*.vcxproj").Any());
            }

            // 情况 2：检查当前目录下是否有 visualgdb 子目录
            if (projectConfigDir == null)
            {
                var visualgdbSubDir = Path.Combine(solutionDir, "visualgdb");
                if (Directory.Exists(visualgdbSubDir))
                {
                    LogHelper.WriteParseInfo($"配置探测：找到 visualgdb 子目录：{visualgdbSubDir}");
                    visualgdbDir = visualgdbSubDir;
                    projectConfigDir = Directory.GetDirectories(visualgdbSubDir)
                        .FirstOrDefault(d => Directory.GetFiles(d, "*.vcxproj").Any());
                }
            }

            if (projectConfigDir == null)
            {
                LogHelper.WriteParseInfo($"配置探测：未找到包含 .vcxproj 的项目目录");
                return null;
            }

            LogHelper.WriteParseInfo($"配置探测：找到项目配置目录：{projectConfigDir}");

            // 解析配置文件
            var config = new VisualGDBConfig
            {
                SolutionDir = solutionDir,
                VisualGDBDir = visualgdbDir,
                ProjectDir = projectConfigDir
            };

            // 解析 vcxproj（使用 vcxproj 所在目录作为基础目录）
            var vcxprojFiles = Directory.GetFiles(projectConfigDir, "*.vcxproj");
            if (vcxprojFiles.Length > 0)
            {
                var vcxprojPath = vcxprojFiles[0];
                LogHelper.WriteParseInfo($"配置探测：解析 vcxproj：{Path.GetFileName(vcxprojPath)}");
                
                var includePaths = ExtractIncludePaths(vcxprojPath, projectConfigDir);
                var preprocessorDefs = ExtractPreprocessorDefinitions(vcxprojPath);
                
                config.IncludePaths.AddRange(includePaths);
                config.PreprocessorDefs.AddRange(preprocessorDefs);
                
                LogHelper.WriteParseInfo($"配置探测：从 vcxproj 提取 {includePaths.Count} 个 Include 路径，{preprocessorDefs.Count} 个预定义宏");
            }

            // 解析 MCU.xml（可选）
            var mcuXmlPath = Path.Combine(projectConfigDir, "MCU.xml");
            if (File.Exists(mcuXmlPath))
            {
                LogHelper.WriteParseInfo($"配置探测：解析 MCU.xml");
                
                var sdkPaths = ExtractSdkPaths(mcuXmlPath);
                config.IncludePaths.AddRange(sdkPaths);
                
                LogHelper.WriteParseInfo($"配置探测：从 MCU.xml 提取 {sdkPaths.Count} 个 SDK 路径");
            }

            LogHelper.WriteParseInfo($"配置探测：VisualGDB 探测完成，共 {config.IncludePaths.Count} 个 Include 路径，{config.PreprocessorDefs.Count} 个预定义宏");
            return config;
        }

        /// <summary>
        /// 尝试探测普通 vcxproj 配置
        /// </summary>
        private VisualGDBConfig TryDetectVcxproj(string solutionDir)
        {
            LogHelper.WriteParseInfo($"配置探测：尝试探测普通 vcxproj 配置...");

            // 在解决方案目录中查找 .vcxproj 文件
            var vcxprojFiles = Directory.GetFiles(solutionDir, "*.vcxproj", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\visualgdb\\")) // 排除 VisualGDB 目录中的 vcxproj
                .ToList();

            if (vcxprojFiles.Count == 0)
            {
                LogHelper.WriteParseInfo($"配置探测：在 {solutionDir} 中未找到 .vcxproj 文件");
                return null;
            }

            // 取第一个找到的 vcxproj 文件
            var vcxprojPath = vcxprojFiles[0];
            var projectDir = Path.GetDirectoryName(vcxprojPath);

            LogHelper.WriteParseInfo($"配置探测：找到 vcxproj：{vcxprojPath}");

            var config = new VisualGDBConfig
            {
                SolutionDir = solutionDir,
                ProjectDir = projectDir
            };

            var includePaths = ExtractIncludePaths(vcxprojPath, solutionDir);
            var preprocessorDefs = ExtractPreprocessorDefinitions(vcxprojPath);

            config.IncludePaths.AddRange(includePaths);
            config.PreprocessorDefs.AddRange(preprocessorDefs);

            // 添加源文件所在目录作为 Include 路径（支持 #include "../inc/xxx.h" 相对路径）
            if (!string.IsNullOrEmpty(_currentSourceFile))
            {
                var sourceFileDir = Path.GetDirectoryName(_currentSourceFile);
                if (!string.IsNullOrEmpty(sourceFileDir) && Directory.Exists(sourceFileDir))
                {
                    if (!config.IncludePaths.Contains(sourceFileDir))
                    {
                        config.IncludePaths.Add(sourceFileDir);
                        LogHelper.WriteParseInfo($"配置探测：添加源文件目录作为 Include 路径：{sourceFileDir}");
                    }
                }
            }

            LogHelper.WriteParseInfo($"配置探测：vcxproj 探测完成，共 {config.IncludePaths.Count} 个 Include 路径，{config.PreprocessorDefs.Count} 个预定义宏");
            return config;
        }

        /// <summary>
        /// 尝试探测 CMake 配置
        /// </summary>
        private VisualGDBConfig TryDetectCMake(string solutionDir)
        {
            LogHelper.WriteParseInfo($"配置探测：尝试探测 CMake 配置...");

            // 查找 CMakeLists.txt
            var cmakeListsPath = FindCMakeListsFile(solutionDir);
            if (cmakeListsPath == null)
            {
                LogHelper.WriteParseInfo($"配置探测：未找到 CMakeLists.txt");
                return null;
            }

            LogHelper.WriteParseInfo($"配置探测：找到 CMakeLists.txt：{cmakeListsPath}");

            // TODO: 解析 CMakeLists.txt 提取 Include 路径
            // 这是一个复杂的任务，暂时返回 null
            LogHelper.WriteParseInfo("配置探测：CMake 解析暂未实现");
            return null;
        }

        /// <summary>
        /// 向上遍历目录树，查找 CMakeLists.txt 文件
        /// </summary>
        private string FindCMakeListsFile(string startDir)
        {
            var currentDir = startDir;
            
            while (!string.IsNullOrEmpty(currentDir))
            {
                var cmakeListsPath = Path.Combine(currentDir, "CMakeLists.txt");
                if (File.Exists(cmakeListsPath))
                {
                    return cmakeListsPath;
                }

                // 向上一级目录
                currentDir = Path.GetDirectoryName(currentDir);
            }

            return null;
        }

        /// <summary>
        /// 向上遍历目录树，查找 .sln 文件
        /// </summary>
        private string FindSolutionDirectory(string filePath)
        {
            var currentDir = Path.GetDirectoryName(filePath);
            LogHelper.WriteParseInfo($"配置探测：查找解决方案目录，起始目录：{currentDir}");
            
            while (!string.IsNullOrEmpty(currentDir))
            {
                LogHelper.WriteParseInfo($"配置探测：检查目录：{currentDir}");
                
                // 查找 .sln 文件
                var slnFiles = Directory.GetFiles(currentDir, "*.sln");
                if (slnFiles.Length > 0)
                {
                    LogHelper.WriteParseInfo($"配置探测：找到 .sln 文件：{slnFiles[0]}");
                    return currentDir;
                }

                // 查找 visualgdb 目录（备用方案）
                var visualgdbDir = Path.Combine(currentDir, "visualgdb");
                LogHelper.WriteParseInfo($"配置探测：检查 VisualGDB 目录：{visualgdbDir}");
                if (Directory.Exists(visualgdbDir))
                {
                    LogHelper.WriteParseInfo($"配置探测：找到 VisualGDB 目录：{visualgdbDir}");
                    return currentDir;
                }

                // 向上一级目录
                var parentDir = Path.GetDirectoryName(currentDir);
                LogHelper.WriteParseInfo($"配置探测：向上一级目录：{parentDir ?? "(null)"}");
                currentDir = parentDir;
            }

            LogHelper.WriteParseInfo("配置探测：已到达根目录，未找到解决方案目录");
            return null;
        }

        /// <summary>
        /// 从 vcxproj 文件中提取 Include 路径
        /// </summary>
        /// <param name="vcxprojPath">vcxproj 文件路径</param>
        /// <param name="baseDir">基础目录（用于解析相对路径）</param>
        private List<string> ExtractIncludePaths(string vcxprojPath, string baseDir)
        {
            var includePaths = new List<string>();

            try
            {
                var doc = XDocument.Load(vcxprojPath);
                var projectDir = Path.GetDirectoryName(vcxprojPath);

                // 提取 ItemDefinitionGroup/ClCompile/AdditionalIncludeDirectories
                var ns = doc.Root?.Name.Namespace;
                var includeElements = doc.Descendants(ns + "ItemDefinitionGroup")
                    .Elements(ns + "ClCompile")
                    .Elements(ns + "AdditionalIncludeDirectories")
                    .ToList();

                LogHelper.WriteParseInfo($"配置探测：找到 {includeElements.Count} 个 AdditionalIncludeDirectories 元素");

                foreach (var includeElem in includeElements)
                {
                    var includeValue = includeElem.Value;
                    
                    if (string.IsNullOrWhiteSpace(includeValue))
                        continue;

                    LogHelper.WriteParseInfo($"配置探测：AdditionalIncludeDirectories 原始值：{includeValue.Substring(0, Math.Min(300, includeValue.Length))}");

                    // 分割多个路径（用分号分隔）
                    var paths = includeValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    LogHelper.WriteParseInfo($"配置探测：分割后得到 {paths.Length} 个路径");

                    foreach (var path in paths)
                    {
                        var trimmedPath = path.Trim().ToString();
                        if (string.IsNullOrWhiteSpace(trimmedPath))
                            continue;

                        // 展开环境变量（使用 baseDir 作为基础目录）
                        var expandedPath = ExpandMacros(trimmedPath, baseDir);
                        LogHelper.WriteParseInfo($"配置探测：路径 '{trimmedPath}' → 展开后 '{expandedPath}'");
                        
                        if (!string.IsNullOrWhiteSpace(expandedPath) && Directory.Exists(expandedPath))
                        {
                            includePaths.Add(expandedPath);
                            LogHelper.WriteParseInfo($"配置探测：✓ 添加有效路径：{expandedPath}");
                        }
                        else
                        {
                            LogHelper.WriteParseInfo($"配置探测：✗ 路径不存在：{expandedPath}");
                        }
                    }
                }

                // 如果通过精确路径没找到，尝试通用查找
                if (includeElements.Count == 0)
                {
                    LogHelper.WriteParseInfo("配置探测：尝试通用查找 AdditionalIncludeDirectories...");
                    var fallbackElements = doc.Descendants()
                        .Where(e => e.Name.LocalName == "AdditionalIncludeDirectories")
                        .ToList();

                    LogHelper.WriteParseInfo($"配置探测：通用查找找到 {fallbackElements.Count} 个 AdditionalIncludeDirectories 元素");

                    foreach (var includeElem in fallbackElements)
                    {
                        var includeValue = includeElem.Value;
                        
                        if (string.IsNullOrWhiteSpace(includeValue))
                            continue;

                        LogHelper.WriteParseInfo($"配置探测：AdditionalIncludeDirectories 原始值：{includeValue.Substring(0, Math.Min(300, includeValue.Length))}");

                        var paths = includeValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var path in paths)
                        {
                            var trimmedPath = path.Trim().ToString();
                            if (string.IsNullOrWhiteSpace(trimmedPath))
                                continue;

                            var expandedPath = ExpandMacros(trimmedPath, baseDir);
                            
                            if (!string.IsNullOrWhiteSpace(expandedPath) && Directory.Exists(expandedPath))
                            {
                                includePaths.Add(expandedPath);
                                LogHelper.WriteParseInfo($"配置探测：✓ 添加有效路径：{expandedPath}");
                            }
                            else
                            {
                                LogHelper.WriteParseInfo($"配置探测：✗ 路径不存在：{expandedPath}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError($"解析 vcxproj Include 路径失败：{vcxprojPath}", ex);
            }

            LogHelper.WriteParseInfo($"配置探测：Include 路径提取完成，共 {includePaths.Count} 个有效路径");
            return includePaths.Distinct().ToList();
        }

        /// <summary>
        /// 从 vcxproj 文件中提取预定义宏
        /// </summary>
        private List<string> ExtractPreprocessorDefinitions(string vcxprojPath)
        {
            var definitions = new List<string>();

            try
            {
                var doc = XDocument.Load(vcxprojPath);

                // 提取 PreprocessorDefinitions
                var defElements = doc.Descendants()
                    .Where(e => e.Name.LocalName == "PreprocessorDefinitions")
                    .Select(e => e.Value);

                foreach (var defValue in defElements)
                {
                    if (string.IsNullOrWhiteSpace(defValue))
                        continue;

                    // 分割多个宏（用分号分隔）
                    var defs = defValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var def in defs)
                    {
                        var trimmedDef = def.Trim().ToString();
                        if (!string.IsNullOrWhiteSpace(trimmedDef))
                        {
                            definitions.Add(trimmedDef);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError($"解析 vcxproj 预定义宏失败：{vcxprojPath}", ex);
            }

            return definitions.Distinct().ToList();
        }

        /// <summary>
        /// 从 MCU.xml 中提取 SDK 路径
        /// </summary>
        private List<string> ExtractSdkPaths(string mcuXmlPath)
        {
            var sdkPaths = new List<string>();

            try
            {
                var doc = XDocument.Load(mcuXmlPath);
                var projectDir = Path.GetDirectoryName(mcuXmlPath);

                // 提取常见的 SDK 路径标签
                var pathElements = doc.Descendants()
                    .Where(e => 
                        e.Name.LocalName.Contains("Path") || 
                        e.Name.LocalName.Contains("Directory") ||
                        e.Name.LocalName.Contains("SDK"))
                    .Select(e => e.Value);

                foreach (var pathValue in pathElements)
                {
                    if (string.IsNullOrWhiteSpace(pathValue))
                        continue;

                    var expandedPath = ExpandMacros(pathValue.Trim(), projectDir);
                    
                    if (!string.IsNullOrWhiteSpace(expandedPath) && Directory.Exists(expandedPath))
                    {
                        sdkPaths.Add(expandedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteError($"解析 MCU.xml SDK 路径失败：{mcuXmlPath}", ex);
            }

            return sdkPaths.Distinct().ToList();
        }

        /// <summary>
        /// 展开 MSBuild 宏（如 $(ProjectDir)）
        /// </summary>
        private string ExpandMacros(string path, string baseDir)
        {
            // 替换常见的 MSBuild 宏
            var expanded = path
                .Replace("$(ProjectDir)", baseDir + Path.DirectorySeparatorChar)
                .Replace("$(SolutionDir)", baseDir + Path.DirectorySeparatorChar)
                .Replace("$(MSBuildProjectDirectory)", baseDir + Path.DirectorySeparatorChar);

            // 展开环境变量
            expanded = Environment.ExpandEnvironmentVariables(expanded);

            // 规范化路径
            try
            {
                // 如果是相对路径，先与 baseDir 组合
                if (!Path.IsPathRooted(expanded))
                {
                    expanded = Path.Combine(baseDir, expanded);
                }
                expanded = Path.GetFullPath(expanded);
            }
            catch
            {
                // 如果路径无效，返回原始值
            }

            return expanded;
        }
    }

    /// <summary>
    /// VisualGDB 项目配置
    /// </summary>
    public class VisualGDBConfig
    {
        /// <summary>
        /// 解决方案目录
        /// </summary>
        public string SolutionDir { get; set; }

        /// <summary>
        /// VisualGDB 配置目录
        /// </summary>
        public string VisualGDBDir { get; set; }

        /// <summary>
        /// 项目配置目录（包含 vcxproj）
        /// </summary>
        public string ProjectDir { get; set; }

        /// <summary>
        /// Include 路径列表
        /// </summary>
        public List<string> IncludePaths { get; set; } = new List<string>();

        /// <summary>
        /// 预定义宏列表
        /// </summary>
        public List<string> PreprocessorDefs { get; set; } = new List<string>();

        /// <summary>
        /// 生成 Clang 编译参数
        /// </summary>
        public List<string> GetClangArgs()
        {
            var args = new List<string>();

            // 添加 Include 路径
            foreach (var path in IncludePaths)
            {
                if (Directory.Exists(path))
                {
                    args.Add($"-I{path}");
                }
            }

            // 添加预定义宏
            foreach (var def in PreprocessorDefs)
            {
                args.Add($"-D{def}");
            }

            return args;
        }
    }
}
