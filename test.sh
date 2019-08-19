#!/usr/bin/env bash
set +x +e
windows() { [[ -n "$WINDIR" ]]; }
projname=FormatStringResource
csproj="$projname.Tests/$projname.Tests.csproj"
testArgs="--logger:trx -p:CollectCoverage=true"
printf "\n>>> \033[1;36mclean TestResults and coverage.json ...\033[0m\n\n"
rm -rf "$projname.Tests/{TestResults,coverage.json}"
printf '\033[1;36mOK\033[0m\n'
printf "\n>>> \033[1;36mdotnet restore --configfile nuget.config $csproj ...\033[0m\n\n"
dotnet restore --configfile nuget.config $csproj
printf "\n>>> \033[1;36mdotnet test $testArgs $csproj ...\033[0m\n\n"
if windows; then
	cmd <<< "dotnet test $testArgs $csproj"
else
	dotnet test $testArgs $csproj
fi
printf "\n\033[1;36mDone. Press any key to exit...\033[0m"
read -n1
printf "\n"
