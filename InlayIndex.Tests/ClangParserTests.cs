using Microsoft.VisualStudio.TestTools.UnitTesting;
using InlayIndex.Parser;
using InlayIndex.Models;

namespace InlayIndex.Tests
{
    [TestClass]
    public class ClangParserTests
    {
        private ClangParser _parser;

        [TestInitialize]
        public void Setup()
        {
            _parser = new ClangParser();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _parser?.Dispose();
        }

        [TestMethod]
        public void ParseCode_SimpleArray_ShouldParseSuccessfully()
        {
            string code = @"
                int arr[3] = { 1, 2, 3 };
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Arrays.Count);
            Assert.AreEqual("arr", result.Arrays[0].Name);
        }

        [TestMethod]
        public void ParseCode_MultiDimensionalArray_ShouldParseSuccessfully()
        {
            string code = @"
                int matrix[2][3] = {
                    { 1, 2, 3 },
                    { 4, 5, 6 }
                };
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Arrays.Count);
            Assert.AreEqual(2, result.Arrays[0].DimensionSizes[0]);
            Assert.AreEqual(3, result.Arrays[0].DimensionSizes[1]);
        }

        [TestMethod]
        public void ParseCode_EnumDefinition_ShouldParseSuccessfully()
        {
            string code = @"
                enum Color { RED, GREEN, BLUE };
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Enums.Count);
            Assert.AreEqual("Color", result.Enums[0].Name);
            Assert.AreEqual(3, result.Enums[0].Members.Count);
        }

        [TestMethod]
        public void ParseCode_EnumWithExplicitValues_ShouldParseSuccessfully()
        {
            string code = @"
                enum StatusCode { 
                    OK = 200,
                    CREATED = 201,
                    NOT_FOUND = 404
                };
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Enums.Count);
            Assert.AreEqual(200, result.Enums[0].Members[0].Value);
            Assert.AreEqual(201, result.Enums[0].Members[1].Value);
            Assert.AreEqual(404, result.Enums[0].Members[2].Value);
        }

        [TestMethod]
        public void ParseCode_StructDefinition_ShouldParseSuccessfully()
        {
            string code = @"
                struct Point {
                    int x;
                    int y;
                };
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Structs.Count);
            Assert.AreEqual("Point", result.Structs[0].Name);
            Assert.AreEqual(2, result.Structs[0].Fields.Count);
        }

        [TestMethod]
        public void ParseCode_EmptyArray_ShouldParseSuccessfully()
        {
            string code = @"
                int empty[] = { };
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public void ParseCode_InvalidCode_ShouldHandleGracefully()
        {
            string code = @"
                int invalid = ;
            ";

            var result = _parser.ParseCode(code, "test.cpp");

            Assert.IsFalse(result.Success);
        }
    }
}
