#!/usr/bin/env bash
# one line command: 
# array=( win-x64 linux-x64 osx-x64 ); for i in "${array[@]}"; do printf "\n>>> building $i ...\n\n"; dotnet publish -r $i -c Release -p:PublishSingleFile=true; done
set +x +e
echo dotnet-core-3.0: https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0
echo runtime array: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
array=( win-x64 linux-x64 osx-x64 )
config='Release'
echo publish args: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
output='publish'
pubArgs="-p:PublishSingleFile=true --self-contained true"
resArgs='-s https://api.nuget.org/v3/index.json'
declare -i count=${#array[*]}
declare -i num=1
# cleanup
printf '>>> \033[1;36mClean bin folder ...\033[0m\n\n'
[ -d "$output" ] && rm -rf $output
find . -type d \( -iname 'bin' -o -iname 'obj' \) | xargs rm -rf
printf '\033[1;36mOK\033[0m\n'
# build
for runtime in "${array[@]}"; do
	printf "\n>>> \033[1;36m($num/$count) Building $runtime ...\033[0m\n\n"
	printf "\033[1mdotnet restore -r $runtime $resArgs ...\033[0m\n"
	dotnet restore -r $runtime $resArgs
	printf "\033[1mdotnet publish -r $runtime -c $config -o $output/$runtime $pubArgs ...\033[0m\n"
	dotnet publish -r $runtime -c $config -o $output/$runtime $pubArgs
	ret=$?
	if [ $ret = 0 ]; then
		printf '\n\033[1;32m'SUCCESS'\033[0m\n'
	else
		printf '\n\033[1;31m'ERROR: $ret'\033[0m\n'
	fi
	let num+=1
done
# end
printf "\n\033[1;36mAll done. Press any key to exit...\033[0m"
read -n1
printf "\n"
