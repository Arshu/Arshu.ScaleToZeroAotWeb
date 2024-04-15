using Arshu.App;
using Arshu.AppWeb;
using Markdig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

            string readmeHtml = await GetHtmlFromMarkdown(RootDirPath, "Readme", "ReadmeTemplate", "ReadmeStyle");
            if (String.IsNullOrEmpty(readmeHtml) == false)
            {
                app.MapGet("/", (HttpContext httpContext, CancellationToken ct) =>
                {
                    return TypedResults.Content(content: readmeHtml,
                      contentType: "text/html",
                      statusCode: (int?)HttpStatusCode.OK);
                });

                app.MapGet("/Readme", (HttpContext httpContext, CancellationToken ct) =>
                {
                    return TypedResults.Content(content: readmeHtml,
                      contentType: "text/html",
                      statusCode: (int?)HttpStatusCode.OK);
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

        private static string SiteTitle = "Hosting App Shell";
        private static string SiteDescription = "Application Shell for Scale To ZeroServerless Multi Region Hosting of Static Web App";
        private static string SiteAuthor = "Sridharan Srinivasan";
        private static MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        private static Dictionary<string, FileInfo[]> _markdownFilesList = new Dictionary<string, FileInfo[]>();
        private static Dictionary<string, FileInfo[]> _htmlFilesList = new Dictionary<string, FileInfo[]>();
        private static Dictionary<string, FileInfo[]> _cssFilesList = new Dictionary<string, FileInfo[]>();
        public static async Task<string> GetHtmlFromMarkdown(string dirPath, string markdownPartialFileName, string htmlPartialFileName, string cssPartialFileName)
        {
            #region Retrieve and Cache Markdown File List

            FileInfo[] markdownFiles = { };
            if (_markdownFilesList.ContainsKey(dirPath) == false)
            {
                DirectoryInfo rootDirInfo = new DirectoryInfo(dirPath);
                markdownFiles = rootDirInfo.GetFiles("*.md", SearchOption.AllDirectories);

                //Cache  the File LIst
                _markdownFilesList.Add(dirPath, markdownFiles);
            }
            else
            {
                markdownFiles = _markdownFilesList[dirPath];
            }

            #endregion

            #region Retrieve and Cache Html File List

            FileInfo[] htmlFiles = { };
            if (_htmlFilesList.ContainsKey(dirPath) == false)
            {
                DirectoryInfo rootDirInfo = new DirectoryInfo(dirPath);
                htmlFiles = rootDirInfo.GetFiles("*.html", SearchOption.AllDirectories);

                _htmlFilesList.Add(dirPath, markdownFiles);
            }
            else
            {
                htmlFiles = _htmlFilesList[dirPath];
            }

            #endregion

            #region Retrieve and Cache Css File List

            FileInfo[] cssFiles = { };
            if (_cssFilesList.ContainsKey(dirPath) == false)
            {
                DirectoryInfo rootDirInfo = new DirectoryInfo(dirPath);
                cssFiles = rootDirInfo.GetFiles("*.css", SearchOption.AllDirectories);

                _cssFilesList.Add(dirPath, markdownFiles);
            }
            else
            {
                cssFiles = _cssFilesList[dirPath];
            }

            #endregion

            #region Retrieve Markdown File Content

            string markdownFileContent = "";
            if (markdownFiles.Length > 0)
            {
                foreach (var itemFileInfo in markdownFiles)
                {
                    if ((itemFileInfo.Name.Contains(markdownPartialFileName, StringComparison.OrdinalIgnoreCase) == true)
                           && (itemFileInfo.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) == true)
                        )
                    {
                        string markedownFilePath = itemFileInfo.FullName;
                        if (File.Exists(markedownFilePath) == true)
                        {
                            string fileContent = await File.ReadAllTextAsync(markedownFilePath).ConfigureAwait(false);
                            if (string.IsNullOrEmpty(fileContent) == false)
                            {
                                markdownFileContent = fileContent;
                                break;
                            }
                        }
                    }
                }
            }

            #endregion

            #region Retrieve Html File Content

            string htmlFileContent = "";
            if (htmlFiles.Length > 0)
            {
                foreach (var itemFileInfo in htmlFiles)
                {
                    if ((itemFileInfo.Name.Contains(htmlPartialFileName, StringComparison.OrdinalIgnoreCase) == true)
                        && (itemFileInfo.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) == true)
                        )
                    {
                        string htmlFilePath = itemFileInfo.FullName;
                        if (File.Exists(htmlFilePath) == true)
                        {
                            string fileContent = await File.ReadAllTextAsync(htmlFilePath).ConfigureAwait(false);
                            if (string.IsNullOrEmpty(fileContent) == false)
                            {
                                htmlFileContent = fileContent;
                                break;
                            }
                        }
                    }
                }
            }

            #endregion

            #region Retrieve Css File Content

            string cssFileContent = "";
            if (cssFiles.Length > 0)
            {
                foreach (var itemFileInfo in cssFiles)
                {
                    if ((itemFileInfo.Name.Contains(cssPartialFileName, StringComparison.OrdinalIgnoreCase) == true)
                        && (itemFileInfo.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true)
                        )
                    {
                        string cssFilePath = itemFileInfo.FullName;
                        if (File.Exists(cssFilePath) == true)
                        {
                            string fileContent = await File.ReadAllTextAsync(cssFilePath).ConfigureAwait(false);
                            if (string.IsNullOrEmpty(fileContent) == false)
                            {
                                cssFileContent = fileContent;
                                break;
                            }
                        }
                    }
                }
            }

            #endregion

            string htmlPageContent = "";
            if (string.IsNullOrEmpty(markdownFileContent) == false)
            {
                #region Convert to Html

                string htmlContent = Markdown.ToHtml(markdownFileContent, _pipeline);

                #endregion

                #region Append Html Content

                if (string.IsNullOrEmpty(htmlFileContent) == false)
                {
                    htmlPageContent = htmlFileContent.Replace("{{MainContent}}", htmlContent);
                    htmlPageContent = htmlPageContent.Replace("{{$SiteTitle}}", SiteTitle);
                    htmlPageContent = htmlPageContent.Replace("{{$SiteDescription}}", SiteDescription);
                    htmlPageContent = htmlPageContent.Replace("{{$SiteAuthor}}", SiteAuthor);
                }
                else
                {
                    htmlPageContent = htmlContent;
                }

                #endregion

                #region Append Css Styles

                if ((string.IsNullOrEmpty(htmlPageContent) == false)
                    && (string.IsNullOrEmpty(cssPartialFileName) == false)
                    )
                {
                    string bodyStartTag = "<body";
                    int idxOfBodyStart = htmlPageContent.IndexOf(bodyStartTag, StringComparison.OrdinalIgnoreCase);
                    if (idxOfBodyStart > -1)
                    {
                        int idxOfBodyStartEnd = htmlPageContent.IndexOf(">", idxOfBodyStart);
                        if (idxOfBodyStartEnd > -1)
                        {
                            string appendBeforeBodyHtml = "<style>" + cssFileContent + "</style>";
                            htmlPageContent = htmlPageContent.Insert(idxOfBodyStart, appendBeforeBodyHtml + Environment.NewLine);
                        }
                    }
                }

                #endregion
            }
            else
            {
                htmlPageContent = "Content is empty";
            }

            return htmlPageContent;
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
