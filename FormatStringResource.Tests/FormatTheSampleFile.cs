using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace FormatStringResource.Tests
{
    /// <summary>
    /// Format sample unit tests (note: not support stdin input when unit test)
    /// </summary>
    [CollectionDefinition("FormatTheSampleFile", DisableParallelization = true)]
    public class FormatTheSampleFile
    {
        private const string DefaultStringResourceFile = "StringResource.example.xml";
        private const string DefaultStringResourceTargetFile = "StringResource.example.target.xml";
        private const string DefaultListFile = "listfile.txt";
        private readonly ITestOutputHelper _output;
        public FormatTheSampleFile(ITestOutputHelper output) => _output = output;

        private string Test(params string[] args)
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            Console.SetError(sw);
            Program.Test(args);
            sw.Close();
            var output = sw.ToString();
            _output.WriteLine($"===Console Output Begin===\n{output}\n===Console Output End===");
            return output;
        }

        [Fact]
        public void Test_DefaultSampleFile_Verbose_Success()
        {
            var output = Test("-v", DefaultStringResourceFile);
            var actual = File.ReadAllText(DefaultStringResourceFile);
            var excepted = File.ReadAllText(DefaultStringResourceTargetFile);
            Assert.Contains("SUCCESS: 1", output);
            Assert.Equal(excepted, actual,
                ignoreLineEndingDifferences: true,
                ignoreWhiteSpaceDifferences: true);
        }

        [Fact]
        public void Test_DefaultListFileAsSampleFile_Failed()
        {
            var output = Test(DefaultListFile);
            Assert.Contains("FAIL: 1", output);
        }

        [Fact]
        public void Test_DefaultListFile_NoBackup_Success()
        {
            var output = Test("--list", DefaultListFile, "--no-backup");
            Assert.Contains("SUCCESS: 1", output);
        }

        [Fact]
        public void Test_DefaultSampleFile_DryRun_LogFile_Success()
        {
            var output = Test("--dry-run", "--log", "1.log", DefaultStringResourceFile);
            Assert.Contains("SUCCESS: 1", output);
        }

        [Fact]
        public void Test_DefaultSampleFile_DryRun_AppendLogFile_Success()
        {
            var output = Test("--dry-run", "--log", "1.log", "--append-log", DefaultStringResourceFile);
            Assert.Contains("SUCCESS: 1", output);
        }

        [Fact]
        public void Test_EmptyArgs_Failed()
        {
            var output = Test();
            Assert.Contains("ERROR:", output);
        }

        [Fact]
        public void Test_ArgsParse_Quiet_NoOutput()
        {
            var output = Test("--quiet");
            Assert.Empty(output);
        }

    }
}