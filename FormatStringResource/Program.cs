using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FormatStringResource
{
    public static class Program
    {
        private static ConsoleColor DefaultForegroundColor;
        private static readonly object SyncLock = new object();
        private const string StdinPipeName = "<Stdin Pipe>";
        private const int PrintFileNotExistCount = 10;
        private static string[]? InputFiles;
        private static bool HasStdinInput;
        private static bool PrintVerbose;
        private static bool Backup = true;
        private static bool NoFormatOutput;
        private static bool DryRun;
        private static bool Quiet;
        private static TextWriter? Logfile;
        private static int OkCount;
        private static int FailCount;
        private static long CostCount;

        private const double GB = 1 << (10 * 3);

#if DEBUG
        // DEBUG members
        private const int DefaultAttachTimeout = 10;
        private static bool IsUnitTest;
        private static bool HasExited;
#endif

        [ExcludeFromCodeCoverage]
        private static void Main(string[] args)
        {
            RegisterExitProcessing();
            ParseDebugArgs(ref args);
            ParseArgs(args);
            FormatFiles();
            PrintCount();
            WriteAndExit(ExitCode.Success);
        }

#if DEBUG
        /// <summary>
        /// Unit test will call this entry
        /// </summary>
        /// <param name="args">like main args</param>
        [ExcludeFromCodeCoverage]
        public static void Test(string[] args)
        {
            // set unit test flag
            IsUnitTest = true;
            HasExited = false;
            // reset default flags
            InputFiles = null;
            HasStdinInput = false;
            PrintVerbose = false;
            Backup = true;
            NoFormatOutput = false;
            DryRun = false;
            Quiet = false;
            Logfile = null;
            OkCount = 0;
            FailCount = 0;
            CostCount = 0;
            // call execute steps:
            // 1. ParseArgs
            // 2. FormatFiles
            // 3. PrintCount
            if (!HasExited) ParseArgs(args);
            if (!HasExited) FormatFiles();
            if (!HasExited) PrintCount();
            // free handlers
            Logfile?.Close();
        }
#endif

        [ExcludeFromCodeCoverage]
        private static void RegisterExitProcessing()
        {
            DefaultForegroundColor = Console.ForegroundColor;
#if WINDOWS
            SetConsoleCtrlHandler(ctrlType =>
            {
                Console.ForegroundColor = DefaultForegroundColor;
                return false; // no stop exiting
            }, true);
#else
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Console.ForegroundColor = DefaultForegroundColor;
            };
#endif
        }

        private static void PrintCount()
        {
            WriteLine(Console.Out, DefaultForegroundColor,
                $"{Environment.NewLine}" +
                $"SUCCESS: {OkCount.ToString("N0")}" +
                $"    FAIL: {FailCount.ToString("N0")}" +
                $"    COST: {(CostCount / 1000f).ToString("N3")}s");
        }

        private static void FormatFiles()
        {
            if ((InputFiles == null || InputFiles.Length == 0) && !HasStdinInput)
            {
                WriteAndExit(ExitCode.FilesNotExist, "no input file");
                return;
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            if (InputFiles != null)
            {
                Parallel.ForEach(InputFiles, FormatFile);
            }

            FormatStdin();
            stopWatch.Stop();
            CostCount = stopWatch.ElapsedMilliseconds;
        }

        private static void FormatStdin()
        {
            if (!HasStdinInput)
            {
                return;
            }

            using var stdIn = Console.OpenStandardInput();
            try
            {
                FormatXml(stdIn, StdinPipeName, isFile: false);
            }
            catch (Exception ex)
            {
                WriteLine(Console.Error, ConsoleColor.Red,
                    $"[FAIL] {StdinPipeName} - {ex.Message}", plusFail: true);
            }
        }

        private static void FormatFile(string filename)
        {
            try
            {
                var fileAccess = DryRun ? FileAccess.Read : FileAccess.ReadWrite;
                using var stream = File.Open(filename, FileMode.Open, fileAccess, FileShare.Read);
                FormatXml(stream, filename, isFile: true);
            }
            catch (Exception ex)
            {
                WriteLine(Console.Error, ConsoleColor.Red,
                    $"[FAIL] {filename} - {ex.Message}", plusFail: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FormatXml(Stream stream, string inputName, bool isFile)
        {
            // check head
            const string xmlHead = "<?xml";
            const string xmlHeadEnd = "?>";
            using var reader = new StreamReader(stream);
            var chars = new char[512];
            var readNum = reader.ReadBlock(chars, 0, chars.Length);
            if (readNum <= 0)
            {
                throw new FormatException("content is empty");
            }

            var span = new ReadOnlySpan<char>(chars, 0, readNum);
            if (!span.StartsWith(xmlHead))
            {
                throw new FormatException("invalid XML format");
            }

            // skip XML description
            var foundIndex = span[xmlHead.Length..].IndexOf(xmlHeadEnd);
            if (foundIndex < 0)
            {
                throw new FormatException("invalid XML format");
            }

            var startIndex = xmlHead.Length + foundIndex + xmlHeadEnd.Length;
            var sb = LoadXmlContent(stream, inputName, startIndex, chars, span, reader);
            var root = ParseXmlContent(inputName, sb);
            WriteXmlFiles(stream, inputName, isFile, root);
        }

        private static StringBuilder LoadXmlContent(Stream stream, string inputName, int startIndex, char[] chars,
            ReadOnlySpan<char> span, StreamReader reader)
        {
            var capacity = 0;
            if (stream.CanSeek)
            {
                var length = stream.Length;
                if (length > GB)
                {
                    WriteLine(Console.Out, ConsoleColor.Yellow,
                        $"[WARN] The content is so big: {inputName}: {length.ToString("N0")} bytes");
                }

                var remainLength = length - startIndex;
                if (remainLength <= int.MaxValue)
                {
                    capacity = (int) remainLength;
                }
            }

            var sb = capacity > 0 ? new StringBuilder(capacity) : new StringBuilder();
            sb.Append(chars, startIndex, span.Length - startIndex);
            sb.Append(reader.ReadToEnd());
            return sb;
        }

        private static XElement ParseXmlContent(string inputName, StringBuilder sb)
        {
            static string GetTextDesc(string? text) =>
                text == null ? "the text is null" : $"text: {text}";

            {
                var loadOptions = NoFormatOutput ? LoadOptions.PreserveWhitespace : LoadOptions.None;
                var root = XElement.Parse(sb.ToString(), loadOptions);
                var q = from item in root.Descendants("Item")
                    let idAttr = item.Attribute("id")
                    where idAttr != null
                    let text = item.Attribute("text")?.ToString()
                    group new NodeItem(text, item) by (string) idAttr
                    into g
                    where g.Count() > 1
                    select g;
                foreach (var g in q)
                {
                    var array = g.ToArray();
                    // find last text is not null for keeping
                    var lastIdx = ^1;
                    if (array[lastIdx].Text == null)
                    {
                        for (int i = array.Length - 2; i >= 0; i--)
                        {
                            ref var current = ref array[i];
                            if (current.Text != null)
                            {
                                // the last item will be kept
                                (current, array[lastIdx]) = (array[lastIdx], current);
                                break;
                            }
                        }
                    }

                    // remove invalid or duplicate items
                    for (int i = 0; i < array.Length - 1; i++)
                    {
                        var (text, item) = array[i];
                        item.Remove();
                        WriteLine(Console.Out,
                            ConsoleColor.Yellow,
                            $"{inputName} -  Removed Item: id: {g.Key} {GetTextDesc(text)}",
                            outputConsole: PrintVerbose);
                    }

                    WriteLine(Console.Out,
                        ConsoleColor.Yellow,
                        $"{inputName} - Reserved Item: id: {g.Key} {GetTextDesc(array[lastIdx].Text)}",
                        outputConsole: PrintVerbose);
                }

                return root;
            }
        }

        private static void WriteXmlFiles(Stream stream, string inputName, bool isFile, XElement root)
        {
            if (isFile && !DryRun)
            {
                if (Backup)
                {
                    var backupFilename = $"{inputName}.bak";
                    if (File.Exists(backupFilename))
                    {
                        var backup2Filename = $"{backupFilename}~";
                        if (!File.Exists(backup2Filename))
                        {
                            File.Move(backupFilename, backup2Filename, true);
                        }
                    }

                    File.Copy(inputName, backupFilename, true);
                }

                var saveOptions = NoFormatOutput ? SaveOptions.DisableFormatting : SaveOptions.None;
                if (stream.CanSeek && stream.CanWrite)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    root.Save(stream, saveOptions);
                    stream.SetLength(stream.Position);
                    stream.Flush();
                }
                else
                {
                    stream.Close();
                    root.Save(inputName, saveOptions);
                }
            }

            WriteLine(Console.Out, ConsoleColor.Green,
                $"[ OK ] {inputName}", plusOk: true);
        }

        private static void WriteLine(TextWriter textWriter,
            ConsoleColor foregroundColor,
            string value,
            bool outputConsole = true,
            bool plusOk = false,
            bool plusFail = false)
        {
            lock (SyncLock)
            {
                if (plusOk)
                {
                    OkCount++;
                }
                else if (plusFail)
                {
                    FailCount++;
                }

                if (outputConsole && !Quiet)
                {
                    Console.ForegroundColor = foregroundColor;
                    textWriter.WriteLine(value);
                }

                Logfile?.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff")} {value}");
            }
        }

        [ExcludeFromCodeCoverage]
        [Conditional("DEBUG")]
        private static void ParseDebugArgs(ref string[] args)
        {
            var filteredArgs = new List<string>(args.Length);
            var attachTimeout = 0;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--")
                {
                    filteredArgs.AddRange(args[i..]);
                    break;
                }

                if (arg == "--debug-attach")
                {
                    attachTimeout = -1;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var result))
                    {
                        attachTimeout = result;
                        i++;
                    }

                    continue;
                }

                filteredArgs.Add(arg);
            }

            // update args
            if (args.Length == filteredArgs.Count)
            {
                return;
            }

            args = filteredArgs.ToArray();

            // wait attach
            if (attachTimeout != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                if (!Console.IsInputRedirected && attachTimeout < 0)
                {
                    WriteLine(Console.Out, DefaultForegroundColor,
                        "DEBUG MODE: Waiting for debugger attach, press any key to continue...");
                    Console.ReadKey(true);
                }
                else
                {
                    if (attachTimeout < 0)
                    {
#if DEBUG
                        attachTimeout = DefaultAttachTimeout;
#endif
                    }

                    var canCursorMove = true;
                    try
                    {
                        var left = Console.CursorLeft;
                        Console.CursorLeft = 0;
                        Console.CursorLeft = left;
                    }
                    catch
                    {
                        canCursorMove = false;
                    }

                    for (int i = 0; i < attachTimeout; i++)
                    {
                        var sec = attachTimeout - i;
                        if (canCursorMove)
                        {
                            WriteLine(Console.Out, DefaultForegroundColor,
                                $"DEBUG MODE: Waiting {sec.ToString("N0")}s for debugger attach...");
                            if (sec > 1)
                            {
                                Console.CursorLeft = 0;
                            }
                            else
                            {
                                WriteLine(Console.Out, DefaultForegroundColor,
                                    "");
                            }
                        }
                        else
                        {
                            WriteLine(Console.Out, DefaultForegroundColor,
                                $"DEBUG MODE: Waiting {sec.ToString("N0")}s for debugger attach...");
                        }

                        Thread.Sleep(1000);
                    }
                }

                WriteLine(Console.Out, DefaultForegroundColor,
                    Debugger.IsAttached ? "Debugger Attached!" : "Debugger no attached, yet");
            }
        }

        private static void ParseArgs(string[] args)
        {
            if (args.Length == 0 && !Console.IsInputRedirected)
            {
                WriteAndExit(ExitCode.ParseError,
                    $"no input file{Environment.NewLine}use --help to get usage");
                return;
            }

            var logFile = "";
            var printVersion = false;
            var appendLog = false;
            var listFromPipe = false;
            var parametersEnding = false;
            var listFiles = new HashSet<string>(args.Length);
            var inputFiles = new HashSet<string>(args.Length);
            var filesNotExist = new HashSet<string>(args.Length);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (!parametersEnding)
                {
                    if (arg == "--")
                    {
                        parametersEnding = true;
                        continue;
                    }

                    if (arg == "--help")
                    {
                        WriteAndExit(ExitCode.Success, GetUsageText());
                        return;
                    }

                    if (arg == "--version")
                    {
                        printVersion = true;
                        continue;
                    }

                    if (arg == "--verbose" ||
                        arg == "-v")
                    {
                        PrintVerbose = true;
                        continue;
                    }

                    if (arg == "--no-backup")
                    {
                        Backup = false;
                        continue;
                    }

                    if (arg == "--no-format")
                    {
                        NoFormatOutput = true;
                        continue;
                    }

                    if (arg == "--dry-run")
                    {
                        DryRun = true;
                        continue;
                    }

                    if (arg == "--append-log")
                    {
                        appendLog = true;
                        continue;
                    }

                    if (arg == "--log" ||
                        arg == "-l")
                    {
                        if (++i == args.Length)
                        {
                            WriteAndExit(ExitCode.ParseError, "lost log filename");
                            return;
                        }

                        var logfileName = args[i];
                        if (logfileName.StartsWith("-", StringComparison.Ordinal))
                        {
                            WriteAndExit(ExitCode.ParseError, "lost log filename");
                            return;
                        }

                        logFile = logfileName;
                        continue;
                    }

                    if (arg == "--list")
                    {
                        if (++i == args.Length)
                        {
                            WriteAndExit(ExitCode.ParseError, "lost list filename");
                            return;
                        }

                        var listFileName = FormatFilename(args[i]);
                        if (!File.Exists(listFileName))
                        {
                            WriteAndExit(ExitCode.ParseError,
                                $"list file {listFileName} not found");
                            return;
                        }

                        listFiles.Add(listFileName);
                        continue;
                    }

                    if (arg == "--list-pipe" ||
                        arg == "-L")
                    {
                        listFromPipe = true;
                        continue;
                    }

                    if (arg == "--quiet" ||
                        arg == "--silent" ||
                        arg == "-q")
                    {
                        Quiet = true;
                        continue;
                    }

                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        WriteAndExit(ExitCode.ParseError);
                        return;
                    }
                }

                var filename = FormatFilename(arg);
                if (!File.Exists(filename))
                {
                    filesNotExist.Add(filename);
                    continue;
                }

                var fi = new FileInfo(filename);
                if (fi.Length > 0)
                {
                    inputFiles.Add(fi.FullName);
                }
            }

            UpdateFlags(printVersion, logFile, appendLog, listFiles, filesNotExist, inputFiles, listFromPipe);
        }

        private static void UpdateFlags(bool printVersion, string logFile, bool appendLog, HashSet<string> listFiles,
            HashSet<string> filesNotExist, HashSet<string> inputFiles, bool listFromPipe)
        {
            if (printVersion)
            {
                WriteAndExit(ExitCode.Success, GetVersionText());
                return;
            }

            ConfigLogFile(logFile, appendLog);
            ConfigListFiles(listFiles, filesNotExist, inputFiles);
            ConfigInRedirected(listFromPipe, filesNotExist, inputFiles);
            ConfigFileNotFound(filesNotExist);

            if (inputFiles.Count == 0 && !HasStdinInput)
            {
                WriteAndExit(ExitCode.ParseError);
                return;
            }

            InputFiles = inputFiles.OrderBy(n => n).ToArray();
        }

        private static void ConfigLogFile(string logFile, bool appendLog)
        {
            if (string.IsNullOrEmpty(logFile))
            {
                return;
            }

            try
            {
                var fileMode = appendLog ? FileMode.Append : FileMode.Create;
                Logfile = new StreamWriter(File.Open(logFile, fileMode, FileAccess.Write, FileShare.Read));
                if (appendLog)
                {
                    const string sp = "----------------------------------------";
                    Logfile.WriteLine(sp);
                }

                Logfile.WriteLine(GetVersionText());
            }
            catch (Exception ex)
            {
                WriteAndExit(ExitCode.ParseError, $"create log file error: {ex.Message}");
            }
        }

        private static void ConfigListFiles(HashSet<string> listFiles, HashSet<string> filesNotExist,
            HashSet<string> inputFiles)
        {
            if (listFiles.Count == 0)
            {
                return;
            }

            try
            {
                foreach (var listFile in listFiles)
                {
                    var listFileInfo = new FileInfo(listFile);
                    if (!listFileInfo.Exists)
                    {
                        WriteAndExit(ExitCode.FilesNotExist,
                            $"the list file not found: {listFileInfo.FullName}");
                        return;
                    }

                    if (listFileInfo.Length >= GB)
                    {
                        WriteLine(Console.Out, ConsoleColor.Yellow,
                            $"[WARN] The list file is so big: {listFileInfo.FullName}: {listFileInfo.Length:N0} bytes");
                    }

                    var readLines = File.ReadLines(listFileInfo.FullName);
                    var lines = readLines.AsParallel().Where(
                        line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
                    foreach (var line in lines)
                    {
                        var filename = FormatFilename(line);
                        if (!File.Exists(filename))
                        {
                            filesNotExist.Add(filename);
                            continue;
                        }

                        var fi = new FileInfo(filename);
                        if (fi.Length > 0)
                        {
                            inputFiles.Add(fi.FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteAndExit(ExitCode.ParseError, $"load list file error: {ex.Message}");
            }
        }

        [ExcludeFromCodeCoverage]
        private static void ConfigInRedirected(bool listFromPipe, HashSet<string> filesNotExist,
            HashSet<string> inputFiles)
        {
            if (!Console.IsInputRedirected)
            {
                return;
            }
#if DEBUG
            if (IsUnitTest)
            {
                HasStdinInput = false;
                return;
            }
#endif
            if (listFromPipe)
            {
                using var reader = new StreamReader(Console.OpenStandardInput());
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var filename = FormatFilename(line);
                    if (!File.Exists(filename))
                    {
                        filesNotExist.Add(filename);
                        continue;
                    }

                    var fi = new FileInfo(filename);
                    if (fi.Length > 0)
                    {
                        inputFiles.Add(fi.FullName);
                    }
                }
            }
            else
            {
                HasStdinInput = true;
            }
        }

        private static void ConfigFileNotFound(HashSet<string> filesNotExist)
        {
            if (filesNotExist.Count == 0)
            {
                return;
            }

            // print files not exist error
            var sb = new StringBuilder();
            sb.AppendFormat("{0} files can't found{1}", filesNotExist.Count.ToString("N0"), Environment.NewLine);
            var lineNumber = 1;
            foreach (var file in filesNotExist)
            {
                if (!PrintVerbose && lineNumber++ > PrintFileNotExistCount)
                {
                    sb.AppendLine("...");
                    break;
                }

                sb.AppendLine(file);
            }

            WriteAndExit(ExitCode.FilesNotExist, sb.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string FormatFilename(string input)
        {
#if WINDOWS
            if (input.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return input;
            }

            return input.Replace('/', '\\');
#else
            return input;
#endif
        }

        [ExcludeFromCodeCoverage]
        private static string GetUsageText()
        {
            var main = AppDomain.CurrentDomain.FriendlyName;
            var usage = $@"usage: {main} [option] [--] <XmlFiles>
       {main} [option] --list <ListFile>

option
      --append-log       append log file if --log enabled
      --dry-run          only run, no save result to file.
                         recommend use --dry-run with --log or --verbose
      --list <file>      load a StringResource.xml paths list from a file.
                         in this file, line head with ""#"" will be skipped
  -L, --list-pipe        load a list from stdin pipe.
                         if has a stdin pipe and no --list-pipe,
                         it will be considered the content of a StringResource.xml,
                         you have to use --verbose or --log to get the results.
  -l, --log <file>       output and overwrite the log file
      --no-backup        don't backup origin file.
                         default will backup origin file to *.bak, 
                         if *.bak existed and *.bak~ is not existed,
                         the *.bak will be rename to *.bak~ before.
      --no-format        don't format output file
  -q, --quiet, --silent  suppress all normal output
  -v, --verbose          output verbose information
      --version          output version information and exit";
#if DEBUG
            usage = $@"{usage}

debug option
        --debug-attach [seconds]  wait debugger attach";
#endif
            return usage;
        }

        [ExcludeFromCodeCoverage]
        private static string GetVersionText()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = string.Format("{1} v{2}{0}{3}",
                Environment.NewLine,
                AppDomain.CurrentDomain.FriendlyName,
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
#if DEBUG
            version =
                $"{version}{Environment.NewLine}Written by {assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company}";
#endif
            return version;
        }

        private static void WriteAndExit(ExitCode exitCode, string appendText = "")
        {
            switch (exitCode)
            {
                case ExitCode.Success:
                    if (!string.IsNullOrEmpty(appendText))
                    {
                        WriteLine(Console.Out, DefaultForegroundColor, appendText);
                    }

                    break;
                case ExitCode.ParseError:
                    WriteLine(Console.Error, DefaultForegroundColor,
                        $"ERROR: arguments error{Environment.NewLine}{appendText}");
                    break;
                case ExitCode.FilesNotExist:
                    WriteLine(Console.Error, DefaultForegroundColor,
                        $"ERROR: Files not exist{Environment.NewLine}{appendText}");
                    break;
                case ExitCode.LoadXmlError:
                    WriteLine(Console.Error, DefaultForegroundColor,
                        $"ERROR: load XML error{Environment.NewLine}{appendText}");
                    break;
                default:
                    if (!string.IsNullOrEmpty(appendText))
                    {
                        WriteLine(Console.Error, DefaultForegroundColor, appendText);
                    }

                    break;
            }

            Logfile?.Close();
#if DEBUG
            if (IsUnitTest)
            {
                WriteLine(Console.Out, DefaultForegroundColor,
                    $"ExitCode: {((int) exitCode).ToString()}");
                HasExited = true;
                return;
            }
#endif
            Environment.Exit((int) exitCode);
        }

#if WINDOWS
        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Global
        [ExcludeFromCodeCoverage]
        [System.Runtime.InteropServices.DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

        public delegate bool HandlerRoutine(CtrlTypes ctrlType);

        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
        // ReSharper restore MemberCanBePrivate.Global
        // ReSharper restore InconsistentNaming
        // ReSharper restore UnusedMember.Global
#endif
    }
}