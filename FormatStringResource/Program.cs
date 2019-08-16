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
    internal class Program
    {
        private static ConsoleColor DefaultForegroundColor;
        private static readonly object SyncLock = new object();
        private static readonly string StdinPipeName = "<Stdin Pipe>";
        private static readonly int PrintFileNotExistCount = 10;
        private static string[] InputFiles;
        private static bool HasStdinInput;
        private static bool PrintVerbose;
        private static bool Backup = true;
        private static bool NoFormatOutput;
        private static bool DryRun;
        private static TextWriter Logfile;
        private static int OKCount;
        private static int FailCount;
        private static long CostCount;
        private const double GB = 1 << (10 * 3);

        // DEBUG members
        private const int DefaultAttachTimeout = 10;

        private static void Main(string[] args)
        {
            RegisterExitProcessing();
            ParseDebugArgs(ref args);
            ParseArgs(args);
            FormatFiles();
            PrintCount();
            WriteAndExit(ExitCode.Success);
        }

        private static void RegisterExitProcessing()
        {
            DefaultForegroundColor = Console.ForegroundColor;
#if WINDOWS
            SetConsoleCtrlHandler(new HandlerRoutine(ctrlType =>
            {
                Console.ForegroundColor = DefaultForegroundColor;
                return false; // no stop exiting
            }), true);
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
                $"{Environment.NewLine}SUCCESS: {OKCount:N0}    FAIL: {FailCount:N0}    COST: {CostCount / 1000f:N3}s");
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
            Parallel.ForEach(InputFiles, filename => FormatFile(filename));
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
            try
            {
                FormatXML(Console.OpenStandardInput(), StdinPipeName, isFile: false);
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
                FormatXML(stream, filename, isFile: true);
            }
            catch (Exception ex)
            {
                WriteLine(Console.Error, ConsoleColor.Red,
                    $"[FAIL] {filename} - {ex.Message}", plusFail: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FormatXML(Stream stream, string inputName, bool isFile)
        {
            // check head
            const string xmlHead = "<?xml";
            const string xmlHeadEnd = "?>";
            using var reader = new StreamReader(stream);
            var chars = new char[512];
            var readNum = reader.ReadBlock(chars, 0, chars.Length);
            if (readNum <= 0)
            {
                throw new Exception("content is empty");
            }
            var span = new ReadOnlySpan<char>(chars, 0, readNum);
            if (!span.StartsWith(xmlHead))
            {
                throw new Exception("invalid xml format");
            }
            // skip xml description
            var foundIndex = span[xmlHead.Length..].IndexOf(xmlHeadEnd);
            if (foundIndex < 0)
            {
                throw new Exception("invalid xml format");
            }
            var startIndex = xmlHead.Length + foundIndex + xmlHeadEnd.Length;
            // load content
            var capacity = 0;
            if (stream.CanSeek)
            {
                var length = stream.Length;
                if (length > GB)
                {
                    WriteLine(Console.Out, ConsoleColor.Yellow,
                        $"[WARN] The content is so big: {inputName}: {length:N0} bytes");
                }
                var remainLength = length - startIndex;
                if (remainLength <= int.MaxValue)
                {
                    capacity = (int)remainLength;
                }
            }
            var sb = capacity > 0 ? new StringBuilder(capacity) : new StringBuilder();
            sb.Append(chars, startIndex, span.Length - startIndex);
            sb.Append(reader.ReadToEnd());
            // parse content
            var loadOptions = NoFormatOutput ? LoadOptions.PreserveWhitespace : LoadOptions.None;
            var root = XElement.Parse(sb.ToString(), loadOptions);
            var q = from item in root.Descendants("Item")
                    let idAttr = item.Attribute("id")
                    where idAttr != null
                    let text = item.Attribute("text")?.ToString()
                    group new NodeItem(text, item) by (string)idAttr into g
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
                        $"{inputName} -  Removed Item: id: {g.Key} {getTextDesc(text)}",
                        onlyLogfile: !PrintVerbose);
                }
                WriteLine(Console.Out,
                        ConsoleColor.Yellow,
                        $"{inputName} - Reserved Item: id: {g.Key} {getTextDesc(array[lastIdx].Text)}",
                   onlyLogfile: !PrintVerbose);
            }
            // file operations
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
                $"[ OK ] {inputName}", plusOK: true);

            return;
            static string getTextDesc(string text) =>
                text == null ? "the text is null" : $"text: {text}";
        }

        private static void WriteLine(TextWriter textWriter,
                                      ConsoleColor foregroundColor,
                                      string value,
                                      bool onlyLogfile = false,
                                      bool plusOK = false,
                                      bool plusFail = false)
        {
            lock (SyncLock)
            {
                if (plusOK)
                {
                    OKCount++;
                }
                else if (plusFail)
                {
                    FailCount++;
                }
                if (!onlyLogfile)
                {
                    Console.ForegroundColor = foregroundColor;
                    textWriter.WriteLine(value);
                }
                if (Logfile != null)
                {
                    Logfile.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff} {value}");
                }
            }
        }

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
                    if (i + 1 < args.Length)
                    {
                        var next = args[i + 1];
                        if (next[0] >= 0x30 && next[0] <= 0x39)
                        {
                            try
                            {
                                attachTimeout = Convert.ToInt32(next);
                                i++;
                            }
                            catch { }
                        }
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
                    Console.WriteLine("DEBUG MODE: Waitting for debugger attach, press any key to continue...");
                    Console.ReadKey(true);
                }
                else
                {
                    if (attachTimeout < 0)
                    {
                        attachTimeout = DefaultAttachTimeout;
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
                            Console.Write($"DEBUG MODE: Waitting {sec:N0}s for debugger attach...");
                            if (sec > 1)
                            {
                                Console.CursorLeft = 0;
                            }
                            else
                            {
                                Console.WriteLine();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"DEBUG MODE: Waitting {sec:N0}s for debugger attach...");
                        }
                        Thread.Sleep(1000);
                    }
                }
                Console.WriteLine(Debugger.IsAttached ? "Debugger Attached!" : "Debugger no attached, yet");
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
            var apppendLog = false;
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
                        apppendLog = true;
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
                        var logfilename = args[i];
                        if (logfilename.StartsWith("-"))
                        {
                            WriteAndExit(ExitCode.ParseError, "lost log filename");
                            return;
                        }
                        logFile = logfilename;
                        continue;
                    }

                    if (arg == "--list")
                    {
                        if (++i == args.Length)
                        {
                            WriteAndExit(ExitCode.ParseError, "lost list filename");
                            return;
                        }
                        var listfilename = FormatFilename(args[i]);
                        if (!File.Exists(listfilename))
                        {
                            WriteAndExit(ExitCode.ParseError,
                                $"list file {listfilename} not found");
                            return;
                        }
                        listFiles.Add(listfilename);
                        continue;
                    }

                    if (arg == "--list-pipe" ||
                        arg == "-L")
                    {
                        listFromPipe = true;
                        continue;
                    }

                    if (arg.StartsWith("-"))
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

            if (printVersion)
            {
                WriteAndExit(ExitCode.Success, GetVersionText());
                return;
            }
            if (!string.IsNullOrEmpty(logFile))
            {
                try
                {
                    var fileMode = apppendLog ? FileMode.OpenOrCreate | FileMode.Append : FileMode.Create;
                    Logfile = new StreamWriter(File.Open(logFile, fileMode, FileAccess.Write, FileShare.Read));
                    if (apppendLog)
                    {
                        Logfile.WriteLine(new string('-', 40));
                    }
                    Logfile.WriteLine(GetVersionText());
                }
                catch (Exception ex)
                {
                    WriteAndExit(ExitCode.ParseError, $"create log file error: {ex.Message}");
                    return;
                }
            }
            if (listFiles.Count > 0)
            {
                try
                {
                    foreach (var listfile in listFiles)
                    {
                        var listFileInfo = new FileInfo(listfile);
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
                        var inputlines = File.ReadLines(listFileInfo.FullName);
                        var lines = inputlines.AsParallel().Where(
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
                    return;
                }
            }
            if (Console.IsInputRedirected)
            {
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
            if (filesNotExist.Count > 0)
            {
                // print files not exist error
                var sb = new StringBuilder();
                sb.AppendFormat("{0:N0} files can't found{1}", filesNotExist.Count, Environment.NewLine);
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
                return;
            }
            InputFiles = inputFiles.OrderBy(n => n).ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string FormatFilename(string input)
        {
#if WINDOWS
            if (input.IndexOf("://") > 0)
            {
                return input;
            }
            return input.Replace('/', '\\');
#else
            return input;
#endif
        }

        private static string GetUsageText()
        {
            var main = AppDomain.CurrentDomain.FriendlyName;
            var usage = $@"usage: {main} [option] [--] <xmlfiles>
       {main} [option] --list <listfile>

option
        --append-log     append log file if --log enabled
        --dry-run        only run, no save result to file.
                         recommend use --dry-run with --log or --verbose
        --list <file>    load a StringResource.xml paths list from a file.
                         in this file, line head with ""#"" will be skipped
    -L, --list-pipe      load a list from stdin pipe.
                         if has a stdin pipe and no --list-pipe,
                         it will be considered the content of a StringResource.xml,
                         you have to use --verbose or --log to get the results.
    -l, --log <file>     output and overwrite the log file
        --no-backup      don't backup origin file.
                         default will backup origin file to *.bak, 
                         if *.bak existed and *.bak~ is not existed,
                         the *.bak will be rename to *.bak~ before.
        --no-format      don't format output file
    -v, --verbose        output verbose information
        --version        output version information and exit";
#if DEBUG
            usage += $@"

debug option
        --debug-attach [seconds]  wait debugger attach";
#endif
            return usage;
        }

        private static string GetVersionText()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = string.Format("{1} v{2}{0}{3}",
                Environment.NewLine,
                AppDomain.CurrentDomain.FriendlyName,
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
#if DEBUG
            version += $"{Environment.NewLine}Written by {assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company}";
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
                        $"ERROR: load xml error{Environment.NewLine}{appendText}");
                    break;
                default:
                    if (!string.IsNullOrEmpty(appendText))
                    {
                        WriteLine(Console.Error, DefaultForegroundColor, appendText);
                    }
                    break;
            }

            if (Logfile != null)
            {
                Logfile.Close();
            }
            Environment.Exit((int)exitCode);
        }

#if WINDOWS
        [System.Runtime.InteropServices.DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
#endif
    }


    internal enum ExitCode
    {
        Success,
        ParseError,
        FilesNotExist,
        LoadXmlError
    }


    internal class NodeItem
    {
        [AllowNull]
        public string Text { get; }
        [DisallowNull]
        public XElement Item { get; }
        public NodeItem(string text, XElement item)
            => (Text, Item) = (text, item);
        public void Deconstruct(out string text, out XElement item)
            => (text, item) = (Text, Item);
    }
}
