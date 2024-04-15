FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.19 AS build

# Install NativeAOT build prerequisites
RUN apk update && apk add --upgrade --no-cache clang build-base zlib-dev

RUN apk add dos2unix --update-cache --repository http://dl-3.alpinelinux.org/alpine/edge/community/ 

ADD https://github.com/DarthSim/overmind/releases/download/v2.5.1/overmind-v2.5.1-linux-amd64.gz .                                                            
RUN gunzip overmind-v2.5.1-linux-amd64.gz && \                                                                                                                
        chmod +x overmind-v2.5.1-linux-amd64 && \                                                                                                             
        mv overmind-v2.5.1-linux-amd64 /usr/bin/overmind  

WORKDIR /source
COPY . .
RUN dos2unix Procfile

#q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]. The default is minimal
RUN dotnet publish -v normal /p:IsDockerBuild=true -c release -f net8.0 -r linux-musl-x64 --output /app --self-contained true -p:PublishTrimmed=true -p:PublishAot=true -p:PublishSingleFile=false
RUN chmod +x /app/Arshu.ScaleToZeroAotWeb

#---------------------------------------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine3.19
LABEL Author="Sridharan Srinivasan" 

RUN apk upgrade --no-cache --available && \
    apk add curl tmux && \
    rm -rf /var/cache/apk/*
 
USER $APP_UID 
COPY --from=build /usr/bin/overmind /usr/bin/overmind

WORKDIR /app
COPY --from=build /app/Arshu.ScaleToZeroAotWeb /app/appsettings.json .

WORKDIR /app
COPY --from=build /source/Procfile .
COPY ./wwwroot/ /app/wwwroot/
COPY ./Readme.md ./ReadmeStyle.css ./ReadmeLink.html ./ReadmeTemplate.html  /app/

EXPOSE 8080
EXPOSE 9091

#CMD ["/app/Arshu.ScaleToZeroAotWeb", "--urls", "http://0.0.0.0:8080;http://0.0.0.0:9091"]

#"-N", "--any-can-die",
CMD ["/usr/bin/overmind", "start", "-f", "/app/Procfile"]


