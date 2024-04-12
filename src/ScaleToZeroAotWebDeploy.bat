@echo off

rem cd C:\Work\ArshuSource\Arshu\ArshuCore\ScaleToZero\Arshu.ScaleToZeroAotWeb\src
rem flyctl apps create --machines --name scaletozeroaotweb --org personal
rem flyctl ips allocate-v6 --app scaletozeroaotweb
rem flyctl ips allocate-v4 --shared --app scaletozeroaotweb
rem flyctl deploy --dockerfile Dockerfile --build-only --remote-only --push --image-label latest -a scaletozeroaotweb --no-cache
rem flyctl deploy --dockerfile Dockerfile_Update --build-only --remote-only --push --image-label latest -a scaletozeroaotweb --no-cache
rem start cmd /k fly logs -a scaletozeroaotweb -r sin
rem fly machine run registry.fly.io/scaletozeroaotweb:latest --name scaletozeroaotweb_sin_1 --region sin --port 443:8080/tcp:tls --port 80:8080/tcp:http --env INITIAL_TIME_IN_SEC="10" --env IDLE_TIME_IN_SEC="10" --app scaletozeroaotweb
rem start chrome.exe --new-window --incognito --auto-open-devtools-for-tabs http://scaletozeroaotweb.fly.dev
rem fly machine list -a scaletozeroaotweb
rem fly machine destroy 
rem fly apps destroy scaletozeroaotweb

flyctl deploy --dockerfile Dockerfile --build-only --remote-only --push --image-label latest -a scaletozeroaotweb --no-cache
rem flyctl deploy --dockerfile Dockerfile_Update --build-only --remote-only --push --image-label latest -a scaletozeroaotweb --no-cache

start cmd /k fly logs -a scaletozeroaotweb -r sin
flyctl machine update 908040ef737187 --image registry.fly.io/scaletozeroaotweb:latest --port 443:8080/tcp:tls --port 80:8080/tcp:http --config fly.toml --yes --restart no --env INITIAL_TIME_IN_SEC="10" --env IDLE_TIME_IN_SEC="10" --app scaletozeroaotweb --memory 256
start chrome.exe --new-window --incognito --auto-open-devtools-for-tabs https://scaletozeroaotweb.fly.dev 

pause
