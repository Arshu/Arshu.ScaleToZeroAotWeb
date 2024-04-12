using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Arshu.AppWeb
{
    public class ArshuResponseTimeMiddleware
    {
        #region Constants/Fields

        // Name of the Response Header, Custom Headers starts with "X-"  
        private const string RESPONSE_HEADER_RESPONSE_TIME = "X-Response-Time-ms";
        private const string RESPONSE_HEADER_FIRST_RESPONSE_TIME = "X-First-Response-Time-ms";

        // Handle to the next Middleware in the pipeline  
        private readonly RequestDelegate _next;
        private long _appStartTime = 0;

        #endregion

        #region Constructor

        public ArshuResponseTimeMiddleware(RequestDelegate next, long appStartTime)
        {
            _next = next;
            _appStartTime = appStartTime;
        }

        #endregion

        #region Invoke 

        private static bool firstTime = false;
        private static double firstRequestLapsedTime = 0;
        public Task InvokeAsync(HttpContext context)
        {
            // Start the Timer using Stopwatch  
            var watch = new Stopwatch();
            watch.Start();

            context.Response.OnStarting(() =>
            {
                // Stop the timer information and calculate the time   
                watch.Stop();

                var responseTimeForCompleteRequest = watch.ElapsedMilliseconds;

                if (firstTime == false)
                {
                    firstTime = true;
                    firstRequestLapsedTime = Stopwatch.GetElapsedTime(_appStartTime).TotalMilliseconds;
                }
                // Add the Response time information in the Response headers.   
                context.Response.Headers[RESPONSE_HEADER_FIRST_RESPONSE_TIME] = firstRequestLapsedTime.ToString();
                context.Response.Headers[RESPONSE_HEADER_RESPONSE_TIME] = responseTimeForCompleteRequest.ToString();

                return Task.CompletedTask;
            });

            // Call the next delegate/middleware in the pipeline   
            return this._next(context);
        }

        #endregion
    }

    #region Middleware Extension

    public static class ArshuResponseTimeMiddlewareExtensions
    {
        public static IApplicationBuilder UseArshuResponseTime(
            this IApplicationBuilder builder, long appStartTime)
        {
            return builder.UseMiddleware<ArshuResponseTimeMiddleware>(appStartTime);
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
