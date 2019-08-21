#!/usr/bin/env bash
set +xe
windows() { [[ -n "$WINDIR" ]]; }
sonarproject="${1:-FormatStringResource}"
sonarserver="${2:-http://10.134.25.140:9000}"
sonartoken="${3:-84467e15b5edac31b021c135a439b08226406f35}"
nugetarg='--configfile nuget.config'
testdir='FormatStringResource.Tests'
testproj="$testdir/FormatStringResource.Tests.csproj"
testargs="--logger:trx -p:CollectCoverage=true -p:CoverletOutputFormat=opencover $testproj"
sonarbegin="begin /k:$sonarproject /d:sonar.host.url=$sonarserver /d:sonar.login=$sonartoken /d:sonar.cs.opencover.reportsPaths=$testdir/coverage.opencover.xml /d:sonar.coverage.exclusions=\"**Test*.cs\""
sonarend="end /d:sonar.login=$sonartoken"
#start
if windows; then
	printf "usage: bash ${0##*\\} <sonarproject> <sonarserver> <sonartoken>\n"
else
	printf "usage: bash ${0##*/} <sonarproject> <sonarserver> <sonartoken>\n"
fi
printf "\nsonarproject: $sonarproject\nsonarserver: $sonarserver\nsonartoken: $sonartoken\n"
#check dotnet-sonarscanner
printf '\n>>> \033[1;36mCheck dotnet-sonarscanner ...\033[0m\n\n'
dotnet tool list --global | grep dotnet-sonarscanner
if [ $? -ne 0 ]; then
	printf 'try to install dotnet-sonarscanner...\n'
	dotnet tool install --global dotnet-sonarscanner $nugetarg
fi
printf "\ntip: follow command can update dotnet-sonarscanner:\n  dotnet tool update --global dotnet-sonarscanner $nugetarg\n"
#clean
printf '\n>>> \033[1;36mClean output folders ...\033[0m\n'
find . -type d \( -iname 'bin' -o -iname 'obj' -o -iname 'TestResults' \) | xargs rm -rf
printf '\n\033[1;36mOK\033[0m\n'
printf "\n>>> \033[1mdotnet restore $nugetarg\033[0m\n"
dotnet restore $nugetarg
if windows; then
	printf "\n>>> \033[1mdotnet test $testargs ...\033[0m\n"
	cmd <<< "dotnet test $testargs"
	printf '\n>>> \033[1mdotnet build-server shutdown ...\033[0m\n'
	dotnet build-server shutdown
	cmd <<< "dotnet sonarscanner $sonarbegin"
	printf '\n>>> \033[1mdotnet build ...\033[0m\n'
	dotnet build
	cmd <<< "dotnet sonarscanner $sonarend"
else
	printf "\n>>> \033[1mdotnet test $testargs ...\033[0m\n"
	dotnet test $testargs
	printf '\n>>> \033[1mdotnet build-server shutdown ...\033[0m\n'
	dotnet build-server shutdown
	dotnet sonarscanner $sonarbegin
	printf '\n>>> \033[1mdotnet build ...\033[0m\n'
	dotnet build
	dotnet sonarscanner $sonarend
fi
printf '\n\033[1;36mDone. Press any key to exit...\033[0m'
read -n1
printf '\n'
