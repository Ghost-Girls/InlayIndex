using InlayIndex.Models;
using InlayIndex.Options;
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
        }

        public List<InlayHintTag> GenerateTags(ParseResult parseResult)
        {
            var tags = new List<InlayHintTag>();

            if (_options.EnableArrayIndex)
            {
                tags.AddRange(GenerateArrayIndexTags(parseResult.Arrays));
            }

            if (_options.EnableEnumValue)
            {
                tags.AddRange(GenerateEnumValueTags(parseResult.Enums));
            }

            return tags;
        }

        private List<InlayHintTag> GenerateArrayIndexTags(List<ArrayInfo> arrays)
        {
            var tags = new List<InlayHintTag>();

            foreach (var array in arrays)
            {
                if (array.Dimensions > (ArrayDimension)_options.MaxDimensions)
                {
                    continue;
                }

                if (array.Elements.Count > _options.MaxElements)
                {
                    continue;
                }

                if (array.IsStructArray && _options.EnableStructField)
                {
                    tags.AddRange(GenerateStructArrayTags(array));
                }
                else
                {
                    tags.AddRange(GenerateSimpleArrayTags(array));
                }
            }

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
    }
}
