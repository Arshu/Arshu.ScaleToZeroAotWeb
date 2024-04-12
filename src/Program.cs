using Arshu.App;
using Arshu.AppWeb;
using Markdig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;
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


            #region Get Readme Html

            string readmeHtml = "";
            DirectoryInfo rootDirInfo = new DirectoryInfo(RootDirPath);
            FileInfo[] markdownFiles = rootDirInfo.GetFiles("*.md", SearchOption.AllDirectories);
            foreach (var itemFileInfo in markdownFiles)
            {
                if (itemFileInfo.Name.Contains("Readme", StringComparison.OrdinalIgnoreCase) ==true)
                {
                    string readmePath = itemFileInfo.FullName;
                    if (File.Exists(readmePath) == true)
                    {
                        string readmeContent = await File.ReadAllTextAsync(readmePath).ConfigureAwait(false);
                        if (string.IsNullOrEmpty(readmeContent) == false)
                        {
                            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                            readmeHtml = Markdown.ToHtml(readmeContent, pipeline);
                        }
                        else
                        {
                            readmeHtml = "Readme Content is empty";
                        }
                    }
                    else
                    {
                        readmeHtml = "Readme File does not exist";
                    }
                    break;
                }
            }

            #endregion

            #region Register Endpoints

            if (String.IsNullOrEmpty(readmeHtml) == false)
            {
                app.MapGet("/", (HttpContext httpContext, CancellationToken ct) =>
                {
                    return TypedResults.Content(content: readmeHtml,
                      contentType: "text/html",
                      statusCode: (int?)HttpStatusCode.OK);
                });
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

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
}
