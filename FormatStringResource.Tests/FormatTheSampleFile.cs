using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace FormatStringResource.Tests
{
    /// <summary>
    /// Format sample unit tests (note: not support stdin input when unittest)
    /// </summary>
    [CollectionDefinition("FormatTheSampleFile", DisableParallelization = true)]
    public class FormatTheSampleFile
    {
        private readonly ITestOutputHelper _output;

        public FormatTheSampleFile(ITestOutputHelper output)
            => _output = output;

        [Fact]
        public void TestParseSampleFile_Success()
        {
            const string @in = "StringResource.example.xml";
            const string @out = "StringResource.example.out.xml";
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(new[] { "-v", @in });
            sw.Flush();
            var actual = File.ReadAllText(@in);
            var excepted = File.ReadAllText(@out);
            var output = sw.ToString();
            _output.WriteLine(output);
            Assert.Contains("SUCCESS: 1", output);
            Assert.Equal(excepted, actual,
                ignoreLineEndingDifferences: true,
                ignoreWhiteSpaceDifferences: true);
        }

        [Fact]
        public void TestParseSampleFile_Failed()
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(new[] { "listfile.txt" });
            sw.Flush();
            var output = sw.ToString();
            _output.WriteLine(output);
            Assert.Contains("FAIL: 1", output);
        }

        [Fact]
        public void TestParseSampleFile_FromList_Success()
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(new[] { "--list", "listfile.txt" });
            sw.Flush();
            var output = sw.ToString();
            _output.WriteLine(output);
            Assert.Contains("SUCCESS: 1", output);
        }

        [Fact]
        public void TestParseSampleFile_DryRun()
        {
            const string @in = "StringResource.example.xml";
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(new[] { "--dry-run", @in });
            sw.Flush();
            var output = sw.ToString();
            _output.WriteLine(output);
            Assert.Contains("SUCCESS: 1", output);
        }

        [Fact]
        public void Test_ArgsParse_Failed()
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(new[] { "not_existed_file.xml" });
            sw.Flush();
            var output = sw.ToString();
            _output.WriteLine(output);
            Assert.Contains("ERROR: Files not exist", output);
        }
    }
}
