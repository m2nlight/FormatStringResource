# this dockerfile will create a image that includes 
# dotnet core 3.0.100-preview8-013656 
# dotnet core 2.2.6 runtime
# sonar-scanner
# https://pkgs.alpinelinux.org/packages
FROM alpine:3.9
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    NUGET_XMLDOC_MODE=skip
RUN apk update \
    && apk add wget curl git bash build-base libintl icu openjdk8-jre unzip vim \
    && rm -rf /var/cache/apk/* \
    && wget -O dotnet.tar.gz https://download.visualstudio.microsoft.com/download/pr/a6b8ba2c-30f2-4bb8-80ed-3f12ac623c41/2455fd6f2369d9a7396bb363482e9047/dotnet-runtime-2.2.6-linux-musl-x64.tar.gz \
    && dotnet_sha512='c4f45ab88ffda26b30c53b1db03e50fe0eaff92d6dd5daff05f4e019fc111405d016a787cadcb3a61df4e973d297a1f63ba2535f3802eff83b2e81b3c31cf0f9' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -C /usr/share/dotnet -xzf dotnet.tar.gz \
    # && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet.tar.gz \
    && wget -O dotnet.tar.gz https://download.visualstudio.microsoft.com/download/pr/f455c93d-abd2-4c4b-89da-39c6dd763eb9/2d17f950ee996f7499c1b6ce463f77e1/dotnet-sdk-3.0.100-preview8-013656-linux-musl-x64.tar.gz \
    && dotnet_sha512='eaec220589c980d0d3e8915673de967426b5202255489c00dc76ed03f7c4fab57abbcb9c5eadc50896127551f42743b0e2eb8b9cd90d9ff09afda12e94e1009e' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -C /usr/share/dotnet -xzf dotnet.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet.tar.gz \
    && mkdir -p /usr/share/sonarscanner \
    && wget -O sonarscanner.zip https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-4.0.0.1744-linux.zip \
    && sonarscaner_sha512='d65f83ea8f33c6f1b687cfe9db95567012dae97d2935ca2014814b364d2f87f81a1e5ab13dcd5ea5b7fda57f3b2d620a2bd862fb2d87c918c8e2f6f6ff2eca29' \
    && echo "$sonarscaner_sha512  sonarscanner.zip" | sha512sum -c - \
    && unzip -q sonarscanner.zip -d /usr/share/sonarscanner \
    && ln -s /usr/share/sonarscanner/sonar-scanner /usr/bin/sonar-scanner \
    && ln -s /usr/share/sonarscanner/sonar-scanner-debug /usr/bin/sonar-scanner-debug \
    && rm sonarscanner.zip
