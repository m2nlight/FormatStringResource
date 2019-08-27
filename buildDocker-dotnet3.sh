#!/usr/bin/env bash
set -e
MY_IMAGE='m2nlight/dotnet3sdk:latest'
echo '>>> BUILDING ...'
docker build -t $MY_IMAGE -f Dockerfile-dotnet3 .
echo '>>> BUILD COMPLETED'
docker images $MY_IMAGE
echo '>>> PUSHING ...'
docker push $MY_IMAGE
echo '>>> DONE'

