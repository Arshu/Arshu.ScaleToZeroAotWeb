using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

#nullable enable 

namespace Arshu.AppWeb
{
    public class ArshuReplayHeaderMiddleware
    {
        #region Utilities

        public static bool IsLocalHost(string rootDomain)
        {
            return rootDomain.Contains("LOCALHOST", StringComparison.OrdinalIgnoreCase)
                || rootDomain.Contains("127.0.0.1")
                || (rootDomain == "0000:0000:0000:0000:0000:0000:0000:0001")
                || (rootDomain == "0:0:0:0:0:0:0:1")
                || (rootDomain == "::1");
        }

        #endregion

        #region Middleware Fields and Constructor

        public static string SiteDateFormat = "dd.MMM.yyyy HH:mm:ss";

        private readonly RequestDelegate _next;
        private readonly string _skipUrlPathWordPatterns;

        public ArshuReplayHeaderMiddleware(RequestDelegate next, string skipUrlPathWordPatterns)
        {
            _next = next;
            _skipUrlPathWordPatterns = skipUrlPathWordPatterns;
        }

        #endregion

        #region Invoke HttpContext

        public async Task InvokeAsync(HttpContext httpContext)
        {
            bool skipUrl = false;
            string rawUrl = httpContext.Request.Path;
            try
            {
                if (!httpContext.Response.HasStarted)
                {
                    if ((httpContext.WebSockets.IsWebSocketRequest == false) 
                        && (httpContext.Request.Query.Count > 0)
                        && (httpContext.Request.Host.Host != "localhost")
                        && (IsLocalHost(httpContext.Request.Host.Host) == false)
                        )
                    {
                        if (httpContext.Request.Method != "CONNECT")
                        {
                            #region Process Skip Url Maps

                            if ((skipUrl == false) && (string.IsNullOrEmpty(_skipUrlPathWordPatterns) == false))
                            {
                                IList<string> skipUrlPathWordPatternList = GetList(_skipUrlPathWordPatterns, ',', false, "");
                                if (skipUrlPathWordPatternList.Count > 0)
                                {
                                    foreach (var itemSkipUrlPathWordPattern in skipUrlPathWordPatternList)
                                    {
                                        string checkRawUrl = rawUrl.Replace("\\", "/");
                                        string[] rawUrlParts = checkRawUrl.Split('/');
                                        bool foundMatch = false;
                                        foreach (var item in rawUrlParts)
                                        {
                                            if ((item != null) && (item.Length > 0))
                                            {
                                                if (item.ToUpper() == itemSkipUrlPathWordPattern.ToUpper())
                                                {
                                                    foundMatch = true;
                                                    break;
                                                }
                                            }
                                        }
                                        //Match regexMatch = Regex.Match(rawUrl, "(" + itemSkipUrlPathWordPattern + ")");
                                        //if (regexMatch.Success == true)
                                        if (foundMatch == true)
                                        {
                                            skipUrl = true;
                                            if (_next != null)
                                            {
                                                await _next.Invoke(httpContext).ConfigureAwait(false);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            #endregion

                            if (skipUrl == false)
                            {
                                #region Replay to Another App

                                if (httpContext.Request.Query.Count > 0)
                                {
                                    string? redirectApp = httpContext.Request.Query["app"];
                                    if (string.IsNullOrEmpty(redirectApp) == false)
                                    {
                                        string replayHeader = "app=" + redirectApp;
                                        if (httpContext.Response.Headers.ContainsKey("fly-replay") == true)
                                        {
                                            httpContext.Response.Headers.Remove("fly-replay");
                                        }
                                        if (httpContext.Response.Headers.ContainsKey("fly-replay") == false)
                                        {
                                            httpContext.Response.Headers.Append("fly-replay", replayHeader);
                                            httpContext.Response.StatusCode = 200;
                                            httpContext.Response.ContentType = "application/json";

                                            string jsonString = "{\"ServerDateTime\" :\"" + DateTime.UtcNow.ToString(SiteDateFormat) + "\", \"FlyReplayHeader\" : \"" + replayHeader + "\"}";
                                            byte[] responseData = Encoding.UTF8.GetBytes(jsonString);
                                            await httpContext.Response.Body.WriteAsync(responseData, 0, responseData.Length).ConfigureAwait(false);
                                            skipUrl = true;
                                        }
                                    }
                                }


                                #endregion

                                #region Replay to Another Instance

                                if ((skipUrl == false) && (httpContext.Request.Query.Count > 0))
                                {
                                    string? changeInstance = httpContext.Request.Query["instance"];
                                    if (string.IsNullOrEmpty(changeInstance) == false)
                                    {
                                        string replayHeader = "instance=" + changeInstance;
                                        if (httpContext.Response.Headers.ContainsKey("fly-replay") == true)
                                        {
                                            httpContext.Response.Headers.Remove("fly-replay");
                                        }
                                        if (httpContext.Response.Headers.ContainsKey("fly-replay") == false)
                                        {
                                            httpContext.Response.Headers.Append("fly-replay", "instance=" + changeInstance);
                                            httpContext.Response.StatusCode = 200;
                                            httpContext.Response.ContentType = "application/json";

                                            string jsonString = "{\"ServerDateTime\" :\"" + DateTime.UtcNow.ToString(SiteDateFormat) + "\", \"FlyReplayHeader\" : \"" + replayHeader + "\"}";
                                            byte[] responseData = Encoding.UTF8.GetBytes(jsonString);
                                            await httpContext.Response.Body.WriteAsync(responseData, 0, responseData.Length).ConfigureAwait(false);

                                            skipUrl = true;
                                        }
                                    }
                                }

                                #endregion

                                #region Replay to Another Region

                                string? flyRegion = Environment.GetEnvironmentVariable("FLY_REGION");
                                if ((skipUrl == false) && (string.IsNullOrEmpty(flyRegion) == false))
                                {
                                    if (httpContext.Request.Query.Count > 0)
                                    {
                                        string? changeRegion = httpContext.Request.Query["region"];
                                        if ((string.IsNullOrEmpty(changeRegion) == false) && (flyRegion.Equals(changeRegion, StringComparison.OrdinalIgnoreCase) == false))
                                        {
                                            string replayHeader = "region=" + changeRegion;
                                            if (httpContext.Response.Headers.ContainsKey("fly-replay") == true)
                                            {
                                                httpContext.Response.Headers.Remove("fly-replay");
                                            }
                                            if (httpContext.Response.Headers.ContainsKey("fly-replay") == false)
                                            {
                                                httpContext.Response.Headers.Append("fly-replay", "region=" + changeRegion);
                                                httpContext.Response.StatusCode = 200;
                                                httpContext.Response.ContentType = "application/json";

                                                string jsonString = "{\"ServerDateTime\" :\"" + DateTime.UtcNow.ToString(SiteDateFormat) + "\", \"FlyReplayHeader\" : \"" + replayHeader + "\"}";
                                                byte[] responseData = Encoding.UTF8.GetBytes(jsonString);
                                                await httpContext.Response.Body.WriteAsync(responseData, 0, responseData.Length).ConfigureAwait(false);

                                                skipUrl = true;
                                            }
                                        }
                                    }
                                }

                                #endregion
                            }

                            if (skipUrl == false)
                            {
                                if (_next != null)
                                {
                                    await _next(httpContext).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (_next != null)
                        {
                            await _next(httpContext).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ReplayHeaderMiddleware_InvokeAsync [" + ex.Message + "]");
            }
        }

        #endregion

        #region Utility

        private static List<string> GetList(string splitString, char stringSeparator, bool capitalizeString, string appendSuffix = "", string appendPrefix = "")
        {
            List<string> stringList = new List<string>();
            if (string.IsNullOrEmpty(splitString) == false)
            {
                string[] stringArr = splitString.Split(stringSeparator);
                foreach (var item in stringArr)
                {
                    if (string.IsNullOrEmpty(item) == false)
                    {
                        string itemKey = item.Trim();
                        if (string.IsNullOrEmpty(itemKey) == false)
                        {
                            if ((string.IsNullOrEmpty(appendSuffix) == false)
                                && (itemKey.EndsWith(appendSuffix, StringComparison.OrdinalIgnoreCase) == false))
                            {
                                itemKey = itemKey + appendSuffix;
                            }
                            if ((string.IsNullOrEmpty(appendPrefix) == false)
                                && (itemKey.StartsWith(appendPrefix, StringComparison.OrdinalIgnoreCase) == false))
                            {
                                itemKey = appendPrefix + itemKey;
                            }
                            if (capitalizeString == true)
                            {
                                stringList.Add(itemKey.ToUpper());
                            }
                            else
                            {
                                stringList.Add(itemKey);
                            }
                        }
                    }
                }
            }
            return stringList;
        }

        #endregion
    }

    #region Middleware Extension

    public static class ArshuReplayHeaderMiddlewareExtensions
    {
        public static IApplicationBuilder UseArshuReplayHeader(
            this IApplicationBuilder builder, string skipUrlPathWordPatterns)
        {
            return builder.UseMiddleware<ArshuReplayHeaderMiddleware>(skipUrlPathWordPatterns);
        }

        public static Microsoft.AspNetCore.Builder.IApplicationBuilder ShowError(this Microsoft.AspNetCore.Builder.IApplicationBuilder app, string centerMessage, string backgroundColor = "red", string textColor = "black")
        {
            app.Run(async (context) =>
            {
                string htmlTemplate = @"
                        <!DOCTYPE html>
                        <html lang='en'>
                            <head>
                                <meta charset='utf-8'>
                                <meta http-equiv='X-UA-Compatible' content='IE=edge,chrome=1' />
                                <meta content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=0' name='viewport' />
                            </head
                            <body style='background-color:{1};margin:0px; padding:0px;height:100vh;'>
                                <div style='display: flex;min-height: 97vh; align-items: center;justify-content: center;background-color:{1}; margin:0px; padding:0px;'>
                                    <div style='max-width: 90%;color:{2}'>
                                        {0}
                                    </div>
                                </div>
                            </body>
                        </html>
                        ";
                await context.Response.WriteAsync(string.Format(htmlTemplate, centerMessage, backgroundColor, textColor)).ConfigureAwait(false);
            });

            return app;
        }

    }

    #endregion
}

#nullable disable
