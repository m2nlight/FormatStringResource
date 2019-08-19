using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace FormatStringResource.Tests
{
    public class FormatTheSampleFile
    {
        private readonly ITestOutputHelper _output;

        public FormatTheSampleFile(ITestOutputHelper output)
            => _output = output;

        [Fact]
        public void TestParseSampleFile()
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
    }
}
