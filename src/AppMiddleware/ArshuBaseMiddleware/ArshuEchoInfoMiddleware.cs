using Arshu.App;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace Arshu.AppWeb
{
    public class ArshuEchoInfoMiddleware
    {
        #region Middleware Fields and Constructor

        private readonly RequestDelegate _next;
        public ArshuEchoInfoMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        #endregion

        #region Invoke HttpContext
      
        public async Task Invoke(HttpContext httpContext)
        {
            long serverRequestStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try
            {
                if (!httpContext.Response.HasStarted)
                {
                    if (httpContext.WebSockets.IsWebSocketRequest == false)
                    {
                        if (httpContext.Request.Method != "CONNECT")
                        {                           
                            string rawUrl = httpContext.Request.Path;
                            string rootDomain = httpContext.Request.Host.Host;
                            Dictionary<string, string> responseJson = new Dictionary<string, string>();

                            #region Echo Info 

                            if (rawUrl.Contains("EchoInfo", StringComparison.CurrentCulture) == true)
                            {
                                httpContext.Response.StatusCode = 200;
                                httpContext.Response.ContentType = "application/json";

                                responseJson.Add("ServerDateTime", DateTime.UtcNow.ToString(App.AppBase.SiteDateFormat));

                                //Fly-Client-IP
                                if (string.IsNullOrEmpty(httpContext.Request.Headers["Fly-Client-IP"]) == false)
                                {
                                    string xForwardedFor = httpContext.Request.Headers["Fly-Client-IP"].ToString();
                                    responseJson.Add("X-FlyClient-IP", xForwardedFor);
                                }
                                //X-Forwarded-For
                                if (string.IsNullOrEmpty(httpContext.Request.Headers["X-Forwarded-For"]) == false)
                                {
                                    string xForwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
                                    responseJson.Add("X-Forwarded-For", xForwardedFor);
                                }
                                if (httpContext.Connection != null)
                                {
                                    if (httpContext.Connection.RemoteIpAddress != null)
                                    {
                                        string remoteIP = httpContext.Connection.RemoteIpAddress.ToString();
                                        responseJson.Add("X-RemoteIP", remoteIP);
                                    }
                                }

                                try
                                {
                                    string serverExternalIp6String = (await new HttpClient().GetStringAsync("http://ipv6.icanhazip.com").ConfigureAwait(false)).Replace("\\r\\n", "").Replace("\\n", "").Trim();
                                    if (string.IsNullOrEmpty(serverExternalIp6String) == false)
                                    {
                                        responseJson.Add("X-ServerIpv6", serverExternalIp6String);
                                    }

                                    string serverExternalIp4String = (await new HttpClient().GetStringAsync("http://ipv4.icanhazip.com").ConfigureAwait(false)).Replace("\\r\\n", "").Replace("\\n", "").Trim();
                                    if (string.IsNullOrEmpty(serverExternalIp4String) == false)
                                    {
                                        responseJson.Add("X-ServerIpv4", serverExternalIp4String);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.WriteLog("Error : EchoInfoMiddleware-Get External IP -" + ex.Message);
                                }
                                var jsonString = App.AppBase.GetJson(responseJson);
                                byte[] responseData = Encoding.UTF8.GetBytes(jsonString);
                                await httpContext.Response.Body.WriteAsync(responseData, 0, responseData.Length).ConfigureAwait(false);
                            }
                            else if (_next != null)
                            {
                                await _next.Invoke(httpContext).ConfigureAwait(false);
                            }
                            #endregion

                        }
                        else if (_next != null)
                        {
                            await _next.Invoke(httpContext).ConfigureAwait(false);
                        }
                    }
                    else if ((httpContext.WebSockets.IsWebSocketRequest) && (_next != null))
                    {
                        await _next.Invoke(httpContext).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.WriteLog("Error : ArshuEchoInfoMiddleware_InvokeAsync - " + ex.Message);
            }
        }

        #endregion
    }

    #region Middleware Extension

    public static class ArshuEchoInfoMiddlewareExtensions
    {
        public static Microsoft.AspNetCore.Builder.IApplicationBuilder UseArshuEchoInfo(this Microsoft.AspNetCore.Builder.IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ArshuEchoInfoMiddleware>();
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