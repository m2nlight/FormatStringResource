#!/usr/bin/env bash
if [ -t 1 ]; then
	yes|./publish.sh "$@" \
	&& yes|./publish_R2R.sh "$@" \
	&& yes|./publish_AOT.sh "$@"
else
	./publish.sh "$@" \
	&& ./publish_R2R.sh "$@" \
	&& ./publish_AOT.sh "$@"
fi
