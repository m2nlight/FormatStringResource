#!/usr/bin/env bash
# one line command:
# runtimes=( win-x64 linux-x64 osx-x64 ); for i in "${runtimes[@]}"; do printf "\n>>> building $i ...\n\n"; dotnet publish -r $i -c Release -p:PublishSingleFile=true; done
set +x +e

csproj=FormatStringResource/FormatStringResource.csproj
runtimes=(win-x64 win-x86 linux-x64 linux-musl-x64 osx-x64)
config='Release'
output='publish'
versionSuffix=''
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_CLI_UI_LANGUAGE=en DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY=1 CppCompilerAndLinker=clang

declare -i r2r=0
declare -i aot=0
declare -i noRmOut=0
declare -i verbose=0
declare -i userRuntimes=0
until [ $# -eq 0 ]; do
	case "$1" in
	--help)
		printf "\nusage: bash ${0##*/} [--r2r | --aot] [--no-rm] [-v|--verbose] [[-r|--runtime osx-x64]... ] [--vs|--version-suffix rc.1] \n\n"
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
	--no-rm)
		noRmOut=1
		;;
	--verbose | -v)
		verbose=1
		;;
	--runtime | -r)
		if [ $userRuntimes -eq 0 ]; then
			userRuntimes=1
			runtimes=()
		fi
		shift
		if [ -z "$1" ] || [[ "$1" =~ ^\- ]]; then
			printf "\nERROR: runtime argument error: $1\n\n"
			exit 1
		fi
		runtimes+=($1)
		;;
	--version-suffix | --vs)
		shift
		if [ -z "$1" ] || [[ "$1" =~ ^\- ]]; then
			printf "\nERROR: version suffix argument error: $1\n\n"
			exit 1
		fi
		versionSuffix=$1
		;;
	*)
		printf "\nERROR: arguments error\nplease run \"bash ${0##*/} --help\" to get usage\n\n"
		exit 1
		;;
	esac
	shift
done

git diff-index --quiet HEAD || {
	printf 'Need to commit!'
	if [ -t 1 ]; then
		printf '\n\033[1;36mPress any key to exit...\033[0m'
		read -n1
		printf '\n'
	fi
	exit 1
}

cat <<EOF
dotnet: https://dotnet.microsoft.com/download
runtimes: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
publish args: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
msbuild variables:
https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties
https://docs.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties
https://docs.microsoft.com/en-us/cpp/build/reference/common-macros-for-build-commands-and-properties
https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-well-known-item-metadata
https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-special-characters
https://docs.microsoft.com/en-us/dotnet/core/tools/csproj#assemblyinfo-properties
EOF

# version 1.0.0-rc.1+aea8dff
commitId=$(git rev-parse --short HEAD) || commitId=''
[ -z "$versionSuffix" ] || versionSuffix="-$versionSuffix"
[ -z "$commitId" ] || versionSuffix="$versionSuffix+$commitId"

declare -i count=${#runtimes[*]}
declare -i num=1
# cleanup
if [ $noRmOut -eq 0 ]; then
	printf '>>> \033[1;36mClean output folders ...\033[0m\n\n'
	[ -d "$output" ] && rm -rf $output
	find . -type d \( -iname 'bin' -o -iname 'obj' \) | xargs rm -rf
	printf '\033[1;36mOK\033[0m\n'
	##dotnet clean
fi

# build
for runtime in "${runtimes[@]}"; do
	# init resArgs and pubArgs
	# -v q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
	[ $verbose -eq 1 ] && resArgs="-v d" || resArgs=''
	[ -z "$versionSuffix" ] && pubArgs='' || pubArgs="--version-suffix \"$versionSuffix\""

	printf "\n>>> \033[1;36m($num/$count) Building $runtime ...\033[0m\n\n"
	if [ $aot -eq 1 ]; then
		resArgs="$resArgs -p:DefineConstants=\"AOT\""
		pubArgs="$resArgs $pubArgs -o $output/$runtime/aot"
	else
		if [ $r2r -eq 1 ]; then
			resArgs="$resArgs -p:DefineConstants=\"R2R\""
			pubArgs="$resArgs $pubArgs -o $output/$runtime/r2r -p:PublishSingleFile=true -p:PublishTrimmed=true"
		else
			pubArgs="$resArgs $pubArgs -o $output/$runtime/bin -p:PublishSingleFile=true -p:PublishTrimmed=true"
		fi

		if [[ $runtime == win* ]]; then
			pubArgs="$pubArgs -p:IncludeNativeLibrariesForSelfExtract=true"
		fi
	fi

	printf "\033[1mdotnet restore --configfile nuget.config -r $runtime $resArgs $csproj ...\033[0m\n"
	dotnet restore --configfile nuget.config -r $runtime $resArgs $csproj

	printf "\033[1mdotnet publish -r $runtime -c $config $pubArgs $csproj ...\033[0m\n"
	dotnet publish -r $runtime -c $config $pubArgs $csproj
	ret=$?
	if [ $ret -eq 0 ]; then
		printf '\n\033[1;32m'SUCCESS'\033[0m\n'
	else
		printf '\n\033[1;31m'"ERROR: $ret"'\033[0m\n'
	fi
	let num+=1
done
# end
if [ -t 1 ]; then
	printf "\n\033[1;36mAll done. Press any key to exit...\033[0m"
	read -n1
	printf "\n"
fi
