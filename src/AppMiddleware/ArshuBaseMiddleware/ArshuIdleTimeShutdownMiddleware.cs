using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable 

namespace Arshu.AppWeb
{
    public class ArshuIdleTimeShutdownMiddleware
    {
        #region App Stoping Event

        public static event EventHandler? OnAppStopping;

        #endregion

        #region Middleware Fields and Constructor

        public static bool Activated = false;

        private readonly RequestDelegate _next;

        private readonly double _initialTimeInSec = 30;
        private double _idleTimeInSec = 30;
        private readonly string _skipUrlPathWordPatterns;
        private DateTime requestStartTime = DateTime.MinValue;
        private Timer? _timer;


        public ArshuIdleTimeShutdownMiddleware(RequestDelegate next, double initialTimeInSec, double idleTimeInSec, string skipUrlPathWordPatterns)
        {
            _next = next;

            _initialTimeInSec = initialTimeInSec;
            _idleTimeInSec = idleTimeInSec;
            _skipUrlPathWordPatterns = skipUrlPathWordPatterns;
            if (idleTimeInSec > 0)
            {
                _timer = new Timer(CheckIdle, null, TimeSpan.FromSeconds(_initialTimeInSec), TimeSpan.FromSeconds(_idleTimeInSec));
            }
        }

        #endregion
   
        #region Invoke HttpContext

        public async Task InvokeAsync(HttpContext httpContext)
        {
            bool skipUrl = false;
            string rawUrl = httpContext.Request.Path;
            //var start = TimeProvider.System.GetTimestamp();
            //var diff = TimeProvider.System.GetLapsedTime(start);
            //diff.TotalMilliseconds

            try
            {
                if (!httpContext.Response.HasStarted)
                {
                    if ((httpContext.WebSockets.IsWebSocketRequest == false)
                        //&& (httpContext.Request.Host.Host != "localhost")
                        //&& (App.AppBase.IsLocalHost(httpContext.Request.Host.Host) == false)
                        )
                    {
                        if (httpContext.Request.Method != "CONNECT")
                        {
                            if (skipUrl == false)
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

                                #region Configure Idle Time from QueryString

                                IQueryCollection queryCollection = httpContext.Request.Query;
                                string? idleTimeQueryString = queryCollection["idletime"];
                                if (string.IsNullOrEmpty(idleTimeQueryString) == true)
                                {
                                    if (int.TryParse(idleTimeQueryString, out int idleTime) == true)
                                    {
                                        if ((idleTime <= 300) && (_timer != null))
                                        {
                                            _idleTimeInSec = idleTime;
                                            _timer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(_idleTimeInSec));
                                            Console.WriteLine("IdleTime to Shutdown Changed to " + idleTime + "secs through QueryString");
                                        }
                                    }
                                }
                                #endregion

                                if (skipUrl == false)
                                {
                                    requestStartTime = DateTime.UtcNow;
                                    Activated = true;
                                }
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
                Console.WriteLine("Error: IdleTimeShutdownMiddleware_InvokeAsync [" + ex.Message + "]");
            }

        }

        #endregion

        #region Check Idle

        private void CheckIdle(object? state)
        {
            if (_timer != null)
            {
                if (Activated == true)
                {
                    _timer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(_idleTimeInSec));
                    if (requestStartTime != DateTime.MinValue)
                    {
                        double lapsedSeconds = DateTime.UtcNow.Subtract(requestStartTime).TotalSeconds;
                        if (lapsedSeconds > _idleTimeInSec)
                        {
                            _timer.Dispose();
                            Task.Run(async () =>
                            {                                
                                bool ret = await App.AppBase.RaiseAppStoppingEvent(typeof(ArshuIdleTimeShutdownMiddleware)).ConfigureAwait(false);
                                if (ret == false)
                                {
                                    RaiseAppStoppingEvent();
                                }
                            });
                        }
                    }
                }
                else
                {
                    _timer.Change(-1, -1);
                    _timer.Dispose();
                    Task.Run( async () =>
                    {
                        bool ret = await App.AppBase.RaiseAppStoppingEvent(typeof(ArshuIdleTimeShutdownMiddleware)).ConfigureAwait(false);
                        if (ret == false)
                        {
                            RaiseAppStoppingEvent();
                        }
                    });
                }
            }
        }

        private void RaiseAppStoppingEvent()
        {
            if (OnAppStopping != null)
            {
                OnAppStopping(this, new EventArgs());
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

    public static class ArshuIdleTimeShutdownMiddlewareExtensions
    {
        public static IApplicationBuilder UseArshuIdleTimeShutdown(
            this IApplicationBuilder builder, double initialTimeInSec, double idleTimeInSec, string skipUrlPathWordPatterns)
        {
            return builder.UseMiddleware<ArshuIdleTimeShutdownMiddleware>(initialTimeInSec, idleTimeInSec, skipUrlPathWordPatterns);
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
