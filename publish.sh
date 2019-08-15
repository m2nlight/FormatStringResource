#!/usr/bin/env bash
# one line command: 
# array=( win-x64 linux-x64 osx-x64 ); for i in "${array[@]}"; do printf "\n>>> building $i ...\n\n"; dotnet publish -r $i -c Release -p:PublishSingleFile=true; done
set +x +e
echo dotnet-core-3.0: https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0
echo runtime array: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
echo publish args: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
array=( win-x64 linux-x64 osx-x64 )
config='Release'
output='publish'
declare -i r2r=0
declare -i aot=0
declare -i noRmOut=0
until [ $# -eq 0 ]; do
	case "$1" in
	--help)
		printf "\nusage: bash ${0##*/} [--r2r | --aot] [--no-rm-output]\n\n"
		exit 0
		;;
	--aot)
		aot=1
		printf '\n\033[1;32m'AOT-BUILD'\033[0m\n\n'
		;;
	--r2r)
		r2r=1
		printf '\n\033[1;32m'R2R-BUILD'\033[0m\n\n'
		;;
	--no-rm-output)
		noRmOut=1
		;;
	*)
		printf "\nERROR: arguments error\nplease run \"bash ${0##*/} --help\" to get usage\n\n"
		exit 1
		;;
	esac
	shift
done
declare -i count=${#array[*]}
declare -i num=1
# cleanup
printf '>>> \033[1;36mClean output folders ...\033[0m\n\n'
[[ $noRmOut -eq 0 && -d "$output" ]] && rm -rf $output
find . -type d \( -iname 'bin' -o -iname 'obj' \) | xargs rm -rf
printf '\033[1;36mOK\033[0m\n'
# build
for runtime in "${array[@]}"; do
	printf "\n>>> \033[1;36m($num/$count) Building $runtime ...\033[0m\n\n"
	printf "\033[1mdotnet restore -r $runtime ...\033[0m\n"
	dotnet restore -r $runtime
	if [ $aot -eq 1 ]; then
		pubArgs="-p:DefineConstants=\"AOT\""		
	elif [ $r2r -eq 1 ]; then
		pubArgs="-p:PublishSingleFile=true -p:PublishTrimmed=true -o $output/$runtime/r2r -p:DefineConstants=\"R2R\""
	else
		pubArgs="-p:PublishSingleFile=true -p:PublishTrimmed=true -o $output/$runtime"
	fi
	printf "\033[1mdotnet publish -r $runtime -c $config $pubArgs ...\033[0m\n"
	dotnet publish -r $runtime -c $config $pubArgs
	ret=$?
	if [ $ret -eq 0 ]; then
		printf '\n\033[1;32m'SUCCESS'\033[0m\n'
	else
		printf '\n\033[1;31m'"ERROR: $ret"'\033[0m\n'
	fi
	let num+=1
done
# end
printf "\n\033[1;36mAll done. Press any key to exit...\033[0m"
read -n1
printf "\n"
