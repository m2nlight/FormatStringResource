using System;
using System.IO;
using Xunit;

namespace FormatStringResource.Tests
{
    public class FormatTheSampleFile
    {
        [Fact]
        public void TestParseSampleFile()
        {
            const string @in = "StringResource.example.xml";
            const string @out = "StringResource.example.out.xml";
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(new[] { @in });
            sw.Flush();
            var actual = File.ReadAllText(@in);
            var excepted = File.ReadAllText(@out);
            var output = sw.ToString();
            Assert.Contains("SUCCESS: 1", output);
            Assert.Equal(excepted, actual, 
                ignoreLineEndingDifferences: true, 
                ignoreWhiteSpaceDifferences: true);
        }
    }
}
