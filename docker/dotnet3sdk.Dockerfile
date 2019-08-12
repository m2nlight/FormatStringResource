# INSTALL
# (1) download packages
#     - wget
# (2) build dotnet core 3 projects
#     - dotnet core 3.0.100 sdk
#     - libicu, lttng-ust, userspace-rcu
#     - zlib-devel, krb5-devel, ncurses-devel, openssl-devel (linux AOT)
#     - centos-release-scl, llvm-toolset-7 (linux AOT requrie Clang >= 3.9)
# (3) run dotnet-sonarscanner
#     - dotnet core 2.2.7 runtime
#     - which
#     - java-11-openjdk
# (4) git and git LFS support
#     - git
#     - git-lfs
FROM centos:7
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    # LC_ALL=en_US.UTF-8 \
    # LANG=en_US.UTF-8 \
    NUGET_XMLDOC_MODE=skip \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    CppCompilerAndLinker=clang \
    PATH="/root/.dotnet/tools:${PATH}"
RUN yum install -y wget git which libicu lttng-ust userspace-rcu zlib-devel krb5-devel ncurses-devel openssl-devel centos-release-scl java-11-openjdk \
    && yum install -y llvm-toolset-7 \
    && yum autoremove -y \
    && rm -rf /var/cache/yum/* \
    && wget -O gitlfs.rpm https://packagecloud.io/github/git-lfs/packages/el/7/git-lfs-2.8.0-1.el7.x86_64.rpm/download \
    && gitlfs_sha512='1a78423499a1e02f446e09e8dddbd69587dd7b1f28bf3ff72a8204ae82f671bc2a64dc29d4c6a864319234d1a101e84b66f69ed547c407889cf9ba849e63f25e' \
    && echo "$gitlfs_sha512 gitlfs.rpm" | sha512sum -c - \
    && rpm -ivh gitlfs.rpm \
    && git lfs install \
    && rm gitlfs.rpm \
    && wget -O dotnet2rt.tar.gz https://download.visualstudio.microsoft.com/download/pr/dc8dd18d-e165-4f58-a821-d657eea08bf1/efd846172658c27dde2d9eafa7d0082e/dotnet-runtime-2.2.7-linux-x64.tar.gz \
    && dotnet_sha512='5c76eee6dcf89569b40f5d7e87b2daa1ac9e924c6c22f37a7a2498bd96266b93aa95b70537218f9bac6e3992b24d991816afeb185ac6b29ecd3ea9b85201139c' \
    && echo "$dotnet_sha512 dotnet2rt.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -C /usr/share/dotnet -xzf dotnet2rt.tar.gz \
    && rm dotnet2rt.tar.gz \
    && wget -O dotnet3sdk.tar.gz https://download.visualstudio.microsoft.com/download/pr/886b4a4c-30af-454b-8bec-81c72b7b4e1f/d1a0c8de9abb36d8535363ede4a15de6/dotnet-sdk-3.0.100-linux-x64.tar.gz \
    && dotnet_sha512='766da31f9a0bcfbf0f12c91ea68354eb509ac2111879d55b656f19299c6ea1c005d31460dac7c2a4ef82b3edfea30232c82ba301fb52c0ff268d3e3a1b73d8f7' \
    && echo "$dotnet_sha512 dotnet3sdk.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -C /usr/share/dotnet -xzf dotnet3sdk.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet3sdk.tar.gz

# if use sonarscanner cli version, yum install unzip and append follow
    # && sonarscanner_ver='4.2.0.1873' \
    # && wget -O sonarscanner.zip https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-${sonarscanner_ver}-linux.zip \
    # && sonarscanner_sha512='c70e8b4b5fed0708cff1f671dbaaefeff3d6feb07b8cb3d926286d5bb1285a295d79ef3075c3202ac29c6e2b4dad198dbb7558f7e4301ee26115866202e304fe' \
    # && echo "$sonarscanner_sha512  sonarscanner.zip" | sha512sum -c - \
    # && unzip sonarscanner.zip -d /usr/share \
    # && ln -s /usr/share/sonar-scanner-${sonarscanner_ver}-linux/bin/sonar-scanner /usr/bin/sonar-scanner \
    # && ln -s /usr/share/sonar-scanner-${sonarscanner_ver}-linux/bin/sonar-scanner-debug /usr/bin/sonar-scanner-debug \
    # && rm -rf sonarscanner/ sonarscanner.zip

# you can use -v argument to mount host path
#    docker run -ti --rm -v $PWD:/app m2nlight/dotnet3sdk bash -c 'dotnet build -r osx-x64 -c release'
# or execute follow in container shell (support AOT build):
#    docker run -ti --rm -v $PWD:/app m2nlight/dotnet3sdk
#    chmod +x *.sh
#    ./publish_all.sh -r linux-x64 --vs beta.1
#    exit
#    ls -lh publish/**/*
WORKDIR /app
CMD [ "/usr/bin/scl", "enable", "llvm-toolset-7", "bash" ]
