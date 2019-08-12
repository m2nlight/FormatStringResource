#!/usr/bin/env bash
set -e
MY_IMAGE='m2nlight/dotnet3sdk:latest'
MY_DOCKERFILE='dotnet3sdk.Dockerfile'

declare -i no_push=0
until [ $# -eq 0 ]; do
	case "$1" in
	--help)
		printf "usage: bash ${0##*/} [--no-push]\n"
		exit 0
		;;
	--no-push)
		no_push=1
		;;
	*)
		printf "Argument error. --help to get usage.\n"
		exit 1
		;;
	esac
	shift
done

echo '>>> BUILDING ...'
docker build -t $MY_IMAGE -f $MY_DOCKERFILE .

echo '>>> BUILD COMPLETED'
docker images $MY_IMAGE

if [ $no_push -eq 0 ]; then
	echo '>>> PUSHING ...'
	docker push $MY_IMAGE
fi
echo '>>> DONE'

