using InlayIndex.Models;
using InlayIndex.Options;
using InlayIndex.Utils;
using System;
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

        public List<InlayHintTag> GenerateTags(ParseResult parseResult)
        {
            LogHelper.WriteTagInfo($"开始生成标签 - 数组：{parseResult.Arrays.Count}, 枚举：{parseResult.Enums.Count}, 结构体：{parseResult.Structs.Count}");
            var tags = new List<InlayHintTag>();

            if (_options.EnableArrayIndex)
            {
                LogHelper.WriteTagInfo("生成数组索引标签...");
                tags.AddRange(GenerateArrayIndexTags(parseResult.Arrays));
            }

            if (_options.EnableEnumValue)
            {
                LogHelper.WriteTagInfo("生成枚举值标签...");
                tags.AddRange(GenerateEnumValueTags(parseResult.Enums));
            }

            if (_options.EnableStructField)
            {
                LogHelper.WriteTagInfo("生成结构体字段标签...");
                tags.AddRange(GenerateStructFieldTags(parseResult.Structs));
            }

            LogHelper.WriteTagInfo($"标签生成完成，共生成 {tags.Count} 个标签");
            return tags;
        }

        private List<InlayHintTag> GenerateArrayIndexTags(List<ArrayInfo> arrays)
        {
            var tags = new List<InlayHintTag>();
            LogHelper.WriteDebug($"处理 {arrays.Count} 个数组");

            foreach (var array in arrays)
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
                    tags.AddRange(GenerateStructArrayTags(array));
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

            foreach (var element in array.Elements)
            {
                var indexText = BuildIndexText(element.Indices);
                
                var tag = new InlayHintTag
                {
                    Text = $"{indexText}:",
                    StartPosition = element.StartPosition,
                    EndPosition = element.StartPosition,
                    Type = InlayHintType.ArrayIndex,
                    ForegroundColor = _options.GetForegroundColor(),
                    FontSize = _options.FontSize,
                    FontWeight = _options.GetFontWeight(),
                    BackgroundOpacity = _options.BackgroundOpacity
                };

                tags.Add(tag);
            }

            return tags;
        }

        private List<InlayHintTag> GenerateStructArrayTags(ArrayInfo array)
        {
            var tags = new List<InlayHintTag>();
            var structElements = new Dictionary<int, List<ArrayElement>>();

            foreach (var element in array.Elements)
            {
                if (element.Indices.Length == (int)array.Dimensions - 1)
                {
                    var structIndex = element.Indices[element.Indices.Length - 1];
                    if (!structElements.ContainsKey(structIndex))
                    {
                        structElements[structIndex] = new List<ArrayElement>();
                    }
                    structElements[structIndex].Add(element);
                }
            }

            foreach (var kvp in structElements)
            {
                var structIndex = kvp.Key;
                var elements = kvp.Value;

                if (elements.Count > 0)
                {
                    var firstElement = elements[0];
                    var indexText = $"[{structIndex}]";

                    var tag = new InlayHintTag
                    {
                        Text = $"{indexText}:",
                        StartPosition = firstElement.StartPosition,
                        EndPosition = firstElement.StartPosition,
                        Type = InlayHintType.ArrayIndex,
                        ForegroundColor = _options.GetForegroundColor(),
                        FontSize = _options.FontSize,
                        FontWeight = _options.GetFontWeight(),
                        BackgroundOpacity = _options.BackgroundOpacity
                    };

                    tags.Add(tag);

                    if (_options.EnableStructField)
                    {
                        for (int i = 0; i < elements.Count; i++)
                        {
                            var element = elements[i];
                            var fieldName = GetFieldNameByIndex(array, i);
                            
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
                }
            }

            return tags;
        }

        private string GetFieldNameByIndex(ArrayInfo array, int index)
        {
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

        private List<InlayHintTag> GenerateEnumValueTags(List<EnumInfo> enums)
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

        private List<InlayHintTag> GenerateStructFieldTags(List<StructInfo> structs)
        {
            var tags = new List<InlayHintTag>();
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
                        var tag = new InlayHintTag
                        {
                            Text = $".{field.Name}:",
                            StartPosition = field.StartPosition,
                            EndPosition = field.EndPosition,
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
