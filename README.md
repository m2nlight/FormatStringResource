# FormatStringResource

Format StringResource.xml files

## Environment

- dotnet 3 (preview 7 and later)
- Windows/Linux/macOS

## Build

Run `publish.sh` will make win-x64, linux-x64 and osx-x64 executable file in
`publish` folder.

If you working on Windows and installed git-bash, the `publish.sh` can be run.

## Run

__Windows__

```text
C:\> FormatStringResource.exe --log 1.log StringResource.xml
# or #
C:\> type StringResource.xml | FormatStringResource.exe --log 1.log
```

Support list file

```text
C:\> FormatStringResource.exe --log 1.log --list listfile.txt
# or #
C:\> type listfile.txt | FormatStringResource.exe --log 1.log -L
```

__MacOS/Linux__

```text
./FormatStringResource --log 1.log StringResource.xml
# or #
cat StringResource.xml | ./FormatStringResource --log 1.log
```

Support list file

```text
./FormatStringResource --log 1.log --list listfile.txt
# or #
cat listfile.txt | ./FormatStringResource --log 1.log -L
```

Get help via `--help` argument.
