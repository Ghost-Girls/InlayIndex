using Microsoft.VisualStudio.TestTools.UnitTesting;
using InlayIndex.Parser;
using InlayIndex.Models;
using InlayIndex.Options;
using System.Collections.Generic;

namespace InlayIndex.Tests
{
    [TestClass]
    public class InlayHintGeneratorTests
    {
        private InlayIndexOptionsPage _options;
        private InlayHintGenerator _generator;

        [TestInitialize]
        public void Setup()
        {
            _options = new InlayIndexOptionsPage();
            _generator = new InlayHintGenerator(_options);
        }

        [TestMethod]
        public void GenerateTags_SingleDimensionArray_ShouldGenerateCorrectTags()
        {
            var parseResult = new ParseResult
            {
                Success = true,
                Arrays = new List<ArrayInfo>
                {
                    new ArrayInfo
                    {
                        Name = "arr",
                        Dimensions = ArrayDimension.Dim1,
                        DimensionSizes = new int[] { 3 },
                        Elements = new List<ArrayElement>
                        {
                            new ArrayElement { Indices = new int[] { 0 }, StartPosition = 10, Value = "1" },
                            new ArrayElement { Indices = new int[] { 1 }, StartPosition = 15, Value = "2" },
                            new ArrayElement { Indices = new int[] { 2 }, StartPosition = 20, Value = "3" }
                        }
                    }
                }
            };

            var tags = _generator.GenerateTags(parseResult);

            Assert.AreEqual(3, tags.Count);
            Assert.AreEqual("[0]:", tags[0].Text);
            Assert.AreEqual("[1]:", tags[1].Text);
            Assert.AreEqual("[2]:", tags[2].Text);
        }

        [TestMethod]
        public void GenerateTags_MultiDimensionalArray_ShouldGenerateCorrectTags()
        {
            var parseResult = new ParseResult
            {
                Success = true,
                Arrays = new List<ArrayInfo>
                {
                    new ArrayInfo
                    {
                        Name = "matrix",
                        Dimensions = ArrayDimension.Dim2,
                        DimensionSizes = new int[] { 2, 2 },
                        Elements = new List<ArrayElement>
                        {
                            new ArrayElement { Indices = new int[] { 0, 0 }, StartPosition = 10, Value = "1" },
                            new ArrayElement { Indices = new int[] { 0, 1 }, StartPosition = 15, Value = "2" },
                            new ArrayElement { Indices = new int[] { 1, 0 }, StartPosition = 20, Value = "3" },
                            new ArrayElement { Indices = new int[] { 1, 1 }, StartPosition = 25, Value = "4" }
                        }
                    }
                }
            };

            var tags = _generator.GenerateTags(parseResult);

            Assert.AreEqual(4, tags.Count);
            Assert.AreEqual("[0][0]:", tags[0].Text);
            Assert.AreEqual("[0][1]:", tags[1].Text);
            Assert.AreEqual("[1][0]:", tags[2].Text);
            Assert.AreEqual("[1][1]:", tags[3].Text);
        }

        [TestMethod]
        public void GenerateTags_EnumValues_ShouldGenerateCorrectTags()
        {
            var parseResult = new ParseResult
            {
                Success = true,
                Enums = new List<EnumInfo>
                {
                    new EnumInfo
                    {
                        Name = "Color",
                        Members = new List<EnumMember>
                        {
                            new EnumMember { Name = "RED", Value = 0, HasExplicitValue = false, EndPosition = 10 },
                            new EnumMember { Name = "GREEN", Value = 1, HasExplicitValue = false, EndPosition = 20 },
                            new EnumMember { Name = "BLUE", Value = 2, HasExplicitValue = false, EndPosition = 30 }
                        }
                    }
                }
            };

            var tags = _generator.GenerateTags(parseResult);

            Assert.AreEqual(3, tags.Count);
            Assert.AreEqual("=0", tags[0].Text);
            Assert.AreEqual("=1", tags[1].Text);
            Assert.AreEqual("=2", tags[2].Text);
        }

        [TestMethod]
        public void GenerateTags_ArrayIndexDisabled_ShouldNotGenerateArrayTags()
        {
            _options.EnableArrayIndex = false;
            _generator = new InlayHintGenerator(_options);

            var parseResult = new ParseResult
            {
                Success = true,
                Arrays = new List<ArrayInfo>
                {
                    new ArrayInfo
                    {
                        Name = "arr",
                        Dimensions = ArrayDimension.Dim1,
                        Elements = new List<ArrayElement>
                        {
                            new ArrayElement { Indices = new int[] { 0 }, StartPosition = 10 }
                        }
                    }
                }
            };

            var tags = _generator.GenerateTags(parseResult);

            Assert.AreEqual(0, tags.Count);
        }

        [TestMethod]
        public void GenerateTags_EnumValueDisabled_ShouldNotGenerateEnumTags()
        {
            _options.EnableEnumValue = false;
            _generator = new InlayHintGenerator(_options);

            var parseResult = new ParseResult
            {
                Success = true,
                Enums = new List<EnumInfo>
                {
                    new EnumInfo
                    {
                        Name = "Color",
                        Members = new List<EnumMember>
                        {
                            new EnumMember { Name = "RED", Value = 0, HasExplicitValue = false, EndPosition = 10 }
                        }
                    }
                }
            };

            var tags = _generator.GenerateTags(parseResult);

            Assert.AreEqual(0, tags.Count);
        }

        [TestMethod]
        public void GenerateTags_MaxDimensionsLimit_ShouldRespectLimit()
        {
            _options.MaxDimensions = 2;
            _generator = new InlayHintGenerator(_options);

            var parseResult = new ParseResult
            {
                Success = true,
                Arrays = new List<ArrayInfo>
                {
                    new ArrayInfo
                    {
                        Name = "cube",
                        Dimensions = ArrayDimension.Dim3,
                        Elements = new List<ArrayElement>
                        {
                            new ArrayElement { Indices = new int[] { 0, 0, 0 }, StartPosition = 10 }
                        }
                    }
                }
            };

            var tags = _generator.GenerateTags(parseResult);

            Assert.AreEqual(0, tags.Count);
        }
    }
}
