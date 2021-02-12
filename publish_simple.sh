#!/usr/bin/env bash
yes | ./publish.sh -r win-x64 -r win-x86 -r linux-x64 -r linux-musl-x64 -r osx-x64 || true
yes | ./publish_R2R.sh -r win-x64 -r win-x86 || true
yes | ./publish_AOT.sh -r win-x64 || true
