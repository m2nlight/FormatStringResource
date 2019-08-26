#!/usr/bin/env bash
IMAGE='m2nlight/dotnet3sdk:alpine'
docker build -t $IMAGE . && docker push $IMAGE