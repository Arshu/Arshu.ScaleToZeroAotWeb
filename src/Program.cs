using Arshu.App;
using Arshu.AppWeb;
using Markdig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Arshu.ScaleToZeroAotWeb
{
    public class Program
    {
        public static long AppStartTime = AppLogger.AppStartTime;
        public static string RootDirPath { get; private set; } = Directory.GetCurrentDirectory();

        public static async Task Main(string[] args)
        {
            string webRootName = "wwwroot";
            int metricPort = 9091;
            int initialTimeInSec = 30;
            int idleTimeInSec = 30;

            string assemblyName = ArshuBaseConfiguration.GetAssemblyName();

            #region Set Root Dir Path

            RootDirPath = Directory.GetCurrentDirectory();
            if (args.Length > 0)
            {
                string dirArgs = args[0];
                if ((dirArgs.Length > 0) && (dirArgs.StartsWith("--") == false))
                {
                    if (Directory.Exists(dirArgs) == true)
                    {
                        if (dirArgs.StartsWith("..") == true)
                        {
                            DirectoryInfo dirInfo = new DirectoryInfo(RootDirPath + "\\" + dirArgs);
                            string parentDirPath = dirInfo.FullName;
                            if (dirInfo.Exists == true)
                            {
                                RootDirPath = parentDirPath;
                            }
                        }
                        else if (dirArgs.StartsWith(".") == true)
                        {
                            DirectoryInfo dirInfo = new DirectoryInfo(RootDirPath);
                            string currentDirPath = dirInfo.FullName;
                            if (dirInfo.Exists == true)
                            {
                                RootDirPath = currentDirPath;
                            }
                        }
                        else
                        {
                            DirectoryInfo dirInfo = new DirectoryInfo(dirArgs);
                            if (dirInfo.Exists == true)
                            {
                                RootDirPath = dirInfo.FullName;
                            }
                        }
                    }
                }
            }
            else
            {
                DirectoryInfo currentDirInfo = new DirectoryInfo(RootDirPath);
                if (currentDirInfo.Name.Equals("app", StringComparison.OrdinalIgnoreCase) == false)
                {
                    if (currentDirInfo.FullName.Contains("app" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        if (currentDirInfo.Parent != null)
                        {
                            DirectoryInfo[] parentSubDirList = currentDirInfo.Parent.GetDirectories("*", SearchOption.TopDirectoryOnly);
                            foreach (var item in parentSubDirList)
                            {
                                if (item.Name.StartsWith("www", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    if (item.FullName != currentDirInfo.FullName)
                                    {
                                        RootDirPath = currentDirInfo.Parent.FullName;
                                        break;
                                    }
                                }
                            }
                        }

                    }
                }
            }

            #endregion

            WriteLog("Command Line Args [" + string.Join(',', args) + "]");
            WriteLog("Current Directory [" + Directory.GetCurrentDirectory() + "]");
            WriteLog("Root Dir [" + RootDirPath + "]");

            string webFolderPath = Path.Combine(RootDirPath, webRootName);

            WriteLog("Web Folder [" + webFolderPath + "]");

            #region Build Web Application

            WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                Args = args,
                ApplicationName = assemblyName,
                ContentRootPath = RootDirPath,
                WebRootPath = webRootName,
            });

            //builder.Configuration.SetBasePath(RootDirPath);
            //if (File.Exists(Path.Combine(RootDirPath, "appsettings.json")) == true)
            //{
            //    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            //}

            #endregion

            #region Kestrel Configuration

            builder.WebHost.UseKestrelCore();
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ConfigureEndpointDefaults(listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
#if DEBUG
                    listenOptions.UseHttps();
#endif
                });
            });
            //builder.WebHost.UseQuic();

            #endregion

            #region Configure Basic Services

            builder.ConfigureBasicServices(metricPort);

            #endregion




            #region App Builder

            var app = builder.Build();

            #endregion

            WriteLog("After Build Initialize");

            app.UseArshuResponseTime(AppStartTime);

            #region Register Shutdown Middleware

            app.RegisterIdleShutdownMiddleware(initialTimeInSec, idleTimeInSec);

            #endregion

            #region Register Basic Middleware

            app.RegisterBasicMiddlewares(app.Lifetime, metricPort);

            #endregion


            #region Readme Endpoint

            string readmeHtml = await MarkdownUtil.GetHtmlFromMarkdown(RootDirPath, "Readme", "ReadmeTemplate", "ReadmeStyle");
            if (String.IsNullOrEmpty(readmeHtml) == false)
            {
                app.Use(async (context, next) =>
                {
                    await MarkdownUtil.ModifyResponse(context, next, RootDirPath, "ReadmeLink").ConfigureAwait(false);
                });

                app.MapGet("/Readme", async (HttpContext httpContext, CancellationToken ct) =>
                {
                    readmeHtml = await MarkdownUtil.GetHtmlFromMarkdown(RootDirPath, "Readme", "ReadmeTemplate", "ReadmeStyle").ConfigureAwait(false);
                    return TypedResults.Content(content: readmeHtml,
                        contentType: "text/html",
                        statusCode: (int?)HttpStatusCode.OK);
                });

                app.MapGet("/Screenshots/{ImagePath}", (HttpContext httpContext, string imagePath, CancellationToken ct) =>
                {
                    byte[] fileBytes = MarkdownUtil.GetFileBytes(RootDirPath, imagePath, "*.png");
                    if (fileBytes.Length > 0)
                    {
                        MemoryStream stream = new MemoryStream(fileBytes);
                        return Results.Stream(stream, "image/png");
                    }
                    else
                    {
                        return Results.NotFound();
                    }
                });
            }

            #endregion

            #region Register Endpoints

            app.UseDefaultFiles();
            //app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new CaseAwarePhysicalFileProvider(webFolderPath),
                RequestPath = new PathString(""),
            });

            #endregion

            await app.RunAsync().ConfigureAwait(false);
        }

        #region Utilities

        public static void WriteLog(string message)
        {
            AppLogger.WriteLog(message);
        }

        #endregion        
    }

    //https://stackoverflow.com/questions/50096995/make-asp-net-core-server-kestrel-case-sensitive-on-windows
    public class CaseAwarePhysicalFileProvider : IFileProvider
    {
        private readonly PhysicalFileProvider _provider;
        //holds all of the actual paths to the required files
        private static Dictionary<string, string> _paths = new Dictionary<string, string>();

        public bool CaseSensitive { get; set; } = false;

        public CaseAwarePhysicalFileProvider(string root)
        {
            _provider = new PhysicalFileProvider(root);
            _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public CaseAwarePhysicalFileProvider(string root, ExclusionFilters filters)
        {
            _provider = new PhysicalFileProvider(root, filters);
            _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            var actualPath = GetActualFilePath(subpath);
            if (CaseSensitive && actualPath != subpath) return new NotFoundFileInfo(subpath);
            return _provider.GetFileInfo(actualPath);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var actualPath = GetActualFilePath(subpath);
            if (CaseSensitive && actualPath != subpath) return NotFoundDirectoryContents.Singleton;
            return _provider.GetDirectoryContents(actualPath);
        }

        public IChangeToken Watch(string filter) => _provider.Watch(filter);

        // Determines (and caches) the actual path for a file
        private string GetActualFilePath(string path)
        {
            // Check if this has already been matched before
            if (_paths.ContainsKey(path)) return _paths[path];

            // Break apart the path and get the root folder to work from
            var currPath = _provider.Root;
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Start stepping up the folders to replace with the correct cased folder name
            for (var i = 0; i < segments.Length; i++)
            {
                var part = segments[i];
                var last = i == segments.Length - 1;

                // Ignore the root
                if (part.Equals("~")) continue;

                // Process the file name if this is the last segment
                part = last ? GetFileName(part, currPath) : GetDirectoryName(part, currPath);

                // If no matches were found, just return the original string
                if (part == null) return path;

                // Update the actualPath with the correct name casing
                currPath = Path.Combine(currPath, part);
                segments[i] = part;
            }

            // Save this path for later use
            var actualPath = string.Join(Path.DirectorySeparatorChar, segments);
            _paths.Add(path, actualPath);
            return actualPath;
        }

        // Searches for a matching file name in the current directory regardless of case
        private static string? GetFileName(string part, string folder) =>
            new DirectoryInfo(folder).GetFiles().FirstOrDefault(file => file.Name.Equals(part, StringComparison.OrdinalIgnoreCase))?.Name;

        // Searches for a matching folder in the current directory regardless of case
        private static string? GetDirectoryName(string part, string folder) =>
            new DirectoryInfo(folder).GetDirectories().FirstOrDefault(dir => dir.Name.Equals(part, StringComparison.OrdinalIgnoreCase))?.Name;
    }
}
