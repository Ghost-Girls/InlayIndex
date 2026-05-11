using InlayIndex.Models;
using InlayIndex.Options;
using InlayIndex.Utils;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace InlayIndex.Parser
{
    public class InlayHintGenerator
    {
        private InlayIndexOptionsPage _options;

        public InlayHintGenerator(InlayIndexOptionsPage options)
        {
            _options = options;
            LogHelper.WriteTagInfo($"InlayHintGenerator 初始化完成");
            LogHelper.WriteTagInfo($"配置 - 数组索引：{_options.EnableArrayIndex}, 枚举值：{_options.EnableEnumValue}, 结构体字段：{_options.EnableStructField}");
        }

        public List<InlayHintTag> GenerateTags(ParseResult parseResult, Microsoft.VisualStudio.Text.ITextSnapshot snapshot = null)
        {
            LogHelper.WriteTagInfo($"开始生成标签 - 数组：{parseResult.Arrays.Count}, 枚举：{parseResult.Enums.Count}, 结构体：{parseResult.Structs.Count}");
            var tags = new List<InlayHintTag>();

            if (_options.EnableArrayIndex)
            {
                LogHelper.WriteTagInfo("生成数组索引标签...");
                tags.AddRange(GenerateArrayIndexTags(parseResult, snapshot));
            }

            if (_options.EnableEnumValue)
            {
                LogHelper.WriteTagInfo("生成枚举值标签...");
                tags.AddRange(GenerateEnumValueTags(parseResult.Enums, snapshot));
            }

            if (_options.EnableStructField)
            {
                LogHelper.WriteTagInfo("生成结构体字段标签...");
                tags.AddRange(GenerateStructFieldTags(parseResult.Structs, snapshot));
            }

            LogHelper.WriteTagInfo($"标签生成完成，共生成 {tags.Count} 个标签");
            return tags;
        }

        private List<InlayHintTag> GenerateArrayIndexTags(ParseResult parseResult, Microsoft.VisualStudio.Text.ITextSnapshot snapshot = null)
        {
            var tags = new List<InlayHintTag>();
            LogHelper.WriteDebug($"处理 {parseResult.Arrays.Count} 个数组");

            foreach (var array in parseResult.Arrays)
            {
                LogHelper.WriteDebug($"处理数组：{array.Name}, 维度：{array.Dimensions}, 元素数：{array.Elements.Count}");

                if (array.Dimensions > (ArrayDimension)_options.MaxDimensions)
                {
                    LogHelper.WriteDebug($"跳过数组 {array.Name} - 维度 {_options.MaxDimensions}");
                    continue;
                }

                if (array.Elements.Count > _options.MaxElements)
                {
                    LogHelper.WriteDebug($"跳过数组 {array.Name} - 元素数超过限制");
                    continue;
                }

                if (array.IsStructArray && _options.EnableStructField)
                {
                    LogHelper.WriteDebug($"生成结构体数组标签：{array.Name}");
                    tags.AddRange(GenerateStructArrayTags(array, parseResult.Structs));
                }
                else
                {
                    LogHelper.WriteDebug($"生成普通数组标签：{array.Name}");
                    tags.AddRange(GenerateSimpleArrayTags(array));
                }
            }

            LogHelper.WriteDebug($"数组索引标签生成完成：{tags.Count} 个");
            return tags;
        }

        private List<InlayHintTag> GenerateSimpleArrayTags(ArrayInfo array)
        {
            var tags = new List<InlayHintTag>();
            var positionCounts = new Dictionary<int, int>();
            var depthColors = _options.GetDepthColors();

            // 第一遍：统计每个位置的出现次数
            foreach (var initList in array.InitLists)
            {
                if (!positionCounts.ContainsKey(initList.StartPosition))
                {
                    positionCounts[initList.StartPosition] = 0;
                }
                positionCounts[initList.StartPosition]++;
            }
            foreach (var element in array.Elements)
            {
                if (!positionCounts.ContainsKey(element.StartPosition))
                {
                    positionCounts[element.StartPosition] = 0;
                }
                positionCounts[element.StartPosition]++;
            }

            // 第二遍：只生成出现次数为1的标签
            foreach (var initList in array.InitLists)
            {
                if (positionCounts[initList.StartPosition] != 1)
                {
                    continue;
                }
                
                string indexText;
                if (_options.IndexDisplayMode == Options.IndexDisplayMode.Full)
                {
                    // 完整索引模式：显示完整的多维索引
                    indexText = BuildIndexText(initList.Indices);
                }
                else
                {
                    // 简洁索引模式：只显示当前维度的最后一个索引
                    indexText = $"[{initList.Indices[initList.Indices.Length - 1]}]";
                }

                // 计算深度（使用索引的长度作为深度）
                int depth = initList.Indices.Length - 1;
                var color = GetColorByDepth(depth, depthColors);

                var tag = new InlayHintTag
                {
                    Text = $"{indexText}:",
                    StartPosition = initList.StartPosition,
                    EndPosition = initList.StartPosition,
                    TrackingSpan = initList.TrackingSpan,  // ✅ 使用 ITrackingSpan
                    Type = InlayHintType.ArrayIndex,
                    ForegroundColor = color,
                    FontSize = _options.FontSize,
                    FontWeight = _options.GetFontWeight(),
                    BackgroundOpacity = _options.BackgroundOpacity
                };

                tags.Add(tag);
            }

            // 生成数组元素的标签
            foreach (var element in array.Elements)
            {
                if (positionCounts[element.StartPosition] != 1)
                {
                    continue;
                }
                
                var indexText = BuildIndexText(element.Indices);

                // 根据元素的深度选择颜色
                var color = GetColorByDepth(element.Depth, depthColors);

                var tag = new InlayHintTag
                {
                    Text = $"{indexText}:",
                    StartPosition = element.StartPosition,
                    EndPosition = element.StartPosition,
                    TrackingSpan = element.TrackingSpan,  // ✅ 使用 ITrackingSpan
                    Type = InlayHintType.ArrayIndex,
                    ForegroundColor = color,
                    FontSize = _options.FontSize,
                    FontWeight = _options.GetFontWeight(),
                    BackgroundOpacity = _options.BackgroundOpacity
                };

                tags.Add(tag);
            }

            return tags;
        }

        private List<InlayHintTag> GenerateStructArrayTags(ArrayInfo array, List<StructInfo> structs)
        {
            var tags = new List<InlayHintTag>();
            var positionCounts = new Dictionary<int, int>();
            var depthColors = _options.GetDepthColors();

            // 第一遍：统计每个位置的出现次数
            foreach (var initList in array.InitLists)
            {
                if (!positionCounts.ContainsKey(initList.StartPosition))
                {
                    positionCounts[initList.StartPosition] = 0;
                }
                positionCounts[initList.StartPosition]++;
            }
            foreach (var element in array.Elements)
            {
                if (!positionCounts.ContainsKey(element.StartPosition))
                {
                    positionCounts[element.StartPosition] = 0;
                }
                positionCounts[element.StartPosition]++;
            }

            // 首先，生成外层初始化列表的标签
            foreach (var initList in array.InitLists)
            {
                if (positionCounts[initList.StartPosition] != 1)
                {
                    continue;
                }
                
                string indexText;
                if (_options.IndexDisplayMode == Options.IndexDisplayMode.Full)
                {
                    // 完整索引模式：显示完整的多维索引
                    indexText = BuildIndexText(initList.Indices);
                }
                else
                {
                    // 简洁索引模式：只显示当前维度的最后一个索引
                    indexText = $"[{initList.Indices[initList.Indices.Length - 1]}]";
                }

                // 计算深度（使用索引的长度作为深度）
                int depth = initList.Indices.Length - 1;
                var color = GetColorByDepth(depth, depthColors);

                var initListTag = new InlayHintTag
                {
                    Text = $"{indexText}:",
                    StartPosition = initList.StartPosition,
                    EndPosition = initList.StartPosition,
                    Type = InlayHintType.ArrayIndex,
                    ForegroundColor = color,
                    FontSize = _options.FontSize,
                    FontWeight = _options.GetFontWeight(),
                    BackgroundOpacity = _options.BackgroundOpacity
                };

                tags.Add(initListTag);
            }

            // 确定每个结构体有多少个字段
            int fieldsPerStruct = 0;
            StructInfo targetStructInfo = null;
            
            // 首先找到对应的结构体信息
            if (array.StructInfo != null)
            {
                targetStructInfo = array.StructInfo;
                fieldsPerStruct = targetStructInfo.Fields.Count;
            }
            else if (!string.IsNullOrEmpty(array.StructTypeName))
            {
                string cleanTypeName = array.StructTypeName.StartsWith("struct ") ? 
                    array.StructTypeName.Substring("struct ".Length) : 
                    array.StructTypeName;
                
                foreach (var structInfo in structs)
                {
                    if (structInfo.Name == cleanTypeName)
                    {
                        targetStructInfo = structInfo;
                        fieldsPerStruct = targetStructInfo.Fields.Count;
                        break;
                    }
                }
            }
            
            // 如果找不到结构体信息，就不生成字段标签
            if (fieldsPerStruct == 0)
            {
                LogHelper.WriteDebug($"找不到结构体信息，不生成字段标签：{array.Name}");
                return tags;
            }

            // 生成结构体内部字段的标签
            if (_options.EnableStructField)
            {
                // 按顺序分组：每 fieldsPerStruct 个元素一组
                for (int i = 0; i < array.Elements.Count; i++)
                {
                    var element = array.Elements[i];
                    
                    if (positionCounts[element.StartPosition] != 1)
                    {
                        continue;
                    }
                    
                    int fieldIndex = i % fieldsPerStruct;
                    
                    string fieldName;
                    if (targetStructInfo != null && fieldIndex < targetStructInfo.Fields.Count)
                    {
                        fieldName = targetStructInfo.Fields[fieldIndex].Name;
                    }
                    else
                    {
                        fieldName = $"field{fieldIndex}";
                    }

                    var fieldTag = new InlayHintTag
                    {
                        Text = $".{fieldName}:",
                        StartPosition = element.StartPosition,
                        EndPosition = element.StartPosition,
                        Type = InlayHintType.StructField,
                        ForegroundColor = _options.GetForegroundColor(),
                        FontSize = _options.FontSize,
                        FontWeight = _options.GetFontWeight(),
                        BackgroundOpacity = _options.BackgroundOpacity
                    };

                    tags.Add(fieldTag);
                }
            }

            return tags;
        }

        private string GetFieldNameByIndex(ArrayInfo array, List<StructInfo> structs, int index)
        {
            // 首先尝试从 array.StructInfo 中查找（如果已关联）
            if (array.StructInfo != null && index < array.StructInfo.Fields.Count)
            {
                return array.StructInfo.Fields[index].Name;
            }
            
            // 否则，从 structs 列表中查找匹配的结构体
            if (!string.IsNullOrEmpty(array.StructTypeName))
            {
                // 移除可能的 "struct " 前缀
                string cleanTypeName = array.StructTypeName.StartsWith("struct ") ? 
                    array.StructTypeName.Substring("struct ".Length) : 
                    array.StructTypeName;
                
                foreach (var structInfo in structs)
                {
                    if (structInfo.Name == cleanTypeName)
                    {
                        if (index < structInfo.Fields.Count)
                        {
                            return structInfo.Fields[index].Name;
                        }
                        break;
                    }
                }
            }
            
            // 如果找不到，使用默认名称
            return $"field{index}";
        }

        private string BuildIndexText(int[] indices)
        {
            var sb = new StringBuilder();
            foreach (var index in indices)
            {
                sb.Append($"[{index}]");
            }
            return sb.ToString();
        }

        private System.Windows.Media.Color GetColorByDepth(int depth, List<System.Windows.Media.Color> depthColors)
        {
            if (depthColors == null || depthColors.Count == 0)
            {
                return _options.GetForegroundColor();
            }
            
            // 使用深度作为颜色数组的索引，循环使用
            int colorIndex = depth % depthColors.Count;
            return depthColors[colorIndex];
        }

        private List<InlayHintTag> GenerateEnumValueTags(List<EnumInfo> enums, Microsoft.VisualStudio.Text.ITextSnapshot snapshot = null)
        {
            var tags = new List<InlayHintTag>();

            foreach (var enumInfo in enums)
            {
                foreach (var member in enumInfo.Members)
                {
                    if (!member.HasExplicitValue)
                    {
                        var tag = new InlayHintTag
                        {
                            Text = $"={member.Value}",
                            StartPosition = member.EndPosition,
                            EndPosition = member.EndPosition,
                            TrackingSpan = member.TrackingSpan,  // ✅ 使用 ITrackingSpan
                            Type = InlayHintType.EnumValue,
                            ForegroundColor = _options.GetForegroundColor(),
                            FontSize = _options.FontSize,
                            FontWeight = _options.GetFontWeight(),
                            BackgroundOpacity = _options.BackgroundOpacity
                        };

                        tags.Add(tag);
                    }
                }
            }

            return tags;
        }

        private List<InlayHintTag> GenerateStructFieldTags(List<StructInfo> structs, Microsoft.VisualStudio.Text.ITextSnapshot snapshot = null)
        {
            var tags = new List<InlayHintTag>();
            var seenPositions = new HashSet<int>();
            LogHelper.WriteDebug($"处理 {structs.Count} 个结构体");

            foreach (var structInfo in structs)
            {
                LogHelper.WriteDebug($"处理结构体：{structInfo.Name}, 字段数：{structInfo.Fields.Count}");

                foreach (var field in structInfo.Fields)
                {
                    if (field.IsArray)
                    {
                        LogHelper.WriteDebug($"处理结构体数组字段：{field.Name}");
                        // TODO: 处理结构体数组字段
                    }
                    else
                    {
                        if (!seenPositions.Add(field.StartPosition))
                        {
                            LogHelper.WriteDebug($"跳过重复字段标签：{field.Name} 位置：{field.StartPosition}");
                            continue;
                        }

                        var tag = new InlayHintTag
                        {
                            Text = $".{field.Name}:",
                            StartPosition = field.StartPosition,
                            EndPosition = field.EndPosition,
                            TrackingSpan = field.TrackingSpan,  // ✅ 使用 ITrackingSpan
                            Type = InlayHintType.StructField,
                            ForegroundColor = _options.GetForegroundColor(),
                            FontSize = _options.FontSize,
                            FontWeight = _options.GetFontWeight(),
                            BackgroundOpacity = _options.BackgroundOpacity
                        };

                        tags.Add(tag);
                        LogHelper.WriteDebug($"生成结构体字段标签：{tag.Text}");
                    }
                }
            }

            LogHelper.WriteDebug($"结构体字段标签生成完成：{tags.Count} 个");
            return tags;
        }
    }
}
