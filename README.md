# FormatStringResource

Format StringResource.xml files

## Environment

- [dotnet core 3 (preview 7 and later)](https://dotnet.microsoft.com/download/dotnet-core/3.0)
- Windows/Linux/macOS

## Build

- Run `publish.sh` will make win-x64, linux-x64 and osx-x64 executable file in
`publish` folder.
- Run `publish_R2R.sh` will try to make ReadyToRun executable file in
`publish/$runtime/r2r` folder.
- Run `publish_AOT.sh` will try to make AOT executable file in
`FormatStringResource/bin/Release/netcoreapp3.0/$runtime/native` folder.

If you working on Windows and installed [git-bash](https://git-scm.com/download/win), the `publish.sh` can be run.

### UnitTest

Call `test.sh` will run unit test and output coverage.

### SonarQube

Call `sonarscanner.sh` will run dotnet sonarscanner.

#### Requirement

- dotnet core 2
- set ~/.dotnet/tools to PATH
- a sonarqube server
  and got the project key(this example is FormatStringResource) and login token

#### Usage

```sh
bash sonarscanner.sh FormatStringResource http://10.134.25.140:9000 84467e15b5edac31b021c135a439b08226406f35 
```

## Run

### Windows

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

### macOS/Linux

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
