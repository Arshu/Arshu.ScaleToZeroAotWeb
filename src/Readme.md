# Open Source Scale To Zero NativeAot App Shell for Static Web Hosting in fly.io cheaply

## Main Features

## AutoShutdown Serverless Deployment in fly.io Machines
Can be deployed in fly.io machines across all regions but charged based on actual usage since the runtime will auto shutdown after a idle time of 10 seconds
Configure using Environment Variable --env INITIAL_TIME_IN_SEC="10" --env IDLE_TIME_IN_SEC="10"

## View Server Instance Information
Will echo information about the web instance on querying using /Echo

## Access Other Region Instance
If hosted in multiple regions, can use ?region=sin querystring to replay to the instance in the specified region

## View Server Performance on Cold Start/Warm Start
Can check the performance of the instance in fly.io logs

# Related Projects

## Comming Soon : Open Source Scale to Zero Native Aot Api Shell for Static Api Hosting in fly.io cheaply with Swagger
## Comming Soon : Open Source Frontend using Declarative UI with Affordable Self Hostable Backend App Shell with Performance Measurement for Static Web/Api/App Hosting and Fly Management Utilities

# Online Demo

Readme.md Page on root access of domain
#### Main : https://scaletozeroaotweb.fly.dev

Index.html Page of the Published Astro Web Site (https://github.com/Charca/astro-blog-template) copied to wwwroot
#### Main : https://scaletozeroaotweb.fly.dev/Index.html

# Getting Started
## Prereqisite Install flyctl from https://fly.io for your respective environment

## Running Locally
Clone the repo and run the appropriate **Arshu.ScaleToZeroAotWeb** asp.net core project for the respective platform

## Running from Docker

Run the Docker Image **arshucs/ScaleToZeroAotWeb** as below
#### docker run --publish 8080:8080 arshucs/ScaleToZeroAotWeb:latest

## Deploying to fly.io Serverless Machines

Fly.io provides free/paid options which should be more than sufficient to host any web app at low cost since the machine will auto shutdown after a idle time configured using Environment Flag

#### Prerequisite is Install the <a href=https://fly.io/docs/hands-on/install-flyctl/>Fly Command Line</a> Program from fly site and login using the fly command line program using fly auth login

# Uses Fly Machines and not Fly Apps
### Replace [appname] with your own app name
### Replace [orgname] with your own org name eg. personal

<pre>

Initial Step 1 : Create a Fly App for Machines
flyctl apps create --machines --name [appname] --org [orgname]
Optional to Delete the [appname] after testing
rem flyctl apps destroy [appname] --yes

Initial Step 2 : Allocation IPv4 and IPv6 for the App
flyctl ips allocate-v6 --app [appname]
flyctl ips allocate-v4 --shared --app [appname]
flyctl ips list --app [appname]

Initial Step 3 : Create a Fly Machine [NOT A FLY APP]
flyctl apps create --machines --name [appname] --org [orgname]

Initila Step 4 : Upload the Docker Image to Fly Docker Registry
flyctl deploy --dockerfile Dockerfile --build-only --remote-only --push --image-label latest -a [appname]

Initial Step 5 : Deploy the Machine to Fly
flyctl machine run registry.fly.io/[appname]:latest --name [appname]-sin-1 --region sin --port 443:8080/tcp:tls --port 80:8080/tcp:http --env INITIAL_TIME_IN_SEC="30" --env IDLE_TIME_IN_SEC="30" --config fly.toml --app [appname]
Repeat Step 5 for each region you want to deploy the app

Update Step 6
After every change in the src deploy the docker image to Fly Docker Registry and Update 
flyctl deploy --dockerfile Dockerfile --build-only --remote-only --push --image-label latest -a [appname]
Retrive the Machine ID
flyctl machine list --app [appname]
Update the Machine
flyctl machine update [machineID] --image registry.fly.io/[appname]:latest --port 443:8080/tcp:tls --port 80:8080/tcp:http --env INITIAL_TIME_IN_SEC="150" --env IDLE_TIME_IN_SEC="150" --config fly.toml --app [appname]
Stop the Machine
flyctl machine stop [machineID]
Destroy the Machine
flyctl machine destroy [machineID] --force

</pre>

# Hosting your own Content

## Replace the wwwfolder with your own static web content.
	eg. For Compatible Astro Projects, copy the dist folder on create using npm run build

## Comment out the following lines in program.cs to Stop showing this Readme

<pre>

    app.MapGet("/", (HttpContext httpContext, CancellationToken ct) => {
        return TypedResults.Content(content: readmeHtml,
            contentType: "text/html",
            statusCode: (int?)HttpStatusCode.OK);
    });

</pre>
