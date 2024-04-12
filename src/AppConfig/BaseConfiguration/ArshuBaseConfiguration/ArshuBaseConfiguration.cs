using Arshu.App;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace Arshu.AppWeb
{  
    public static class ArshuBaseConfiguration
    {
        public const bool SkipReadFromFileSystem = false;

        #region App Shudown EventHook

        public static event ActionHandler? OnAppShutdown;

        public static async Task<bool> RaiseAppShutdownEvent(Type sourceType, object? actionArg = null)
        {
            bool ret = false;
            if (OnAppShutdown != null)
            {
                ret = await OnAppShutdown(sourceType, "Shutdown", actionArg).ConfigureAwait(false);
            }
            return ret;
        }

        #endregion

        #region General Information

        public static string GetAssemblyName()
        {
            string assemblyName = "Arshu";
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                string? entryAssemblyName = entryAssembly.GetName().Name;
                if (entryAssemblyName != null)
                {
                    assemblyName = entryAssemblyName;
                }
            }
            return assemblyName;
        }

        public static string GetAppName()
        {
            string appName = "Arshu";

            string assemblyName = GetAssemblyName();
            if (assemblyName != null)
            {
                appName = assemblyName;
            }

            string? envAppName = Environment.GetEnvironmentVariable("FLY_APP_NAME");
            if (string.IsNullOrEmpty(envAppName) == false)
            {
                appName = envAppName;
            }

            return appName;
        }

        public static string GetVersionInfo()
        {
            string versionInfo = "1";
            Version? assemblyVersion = typeof(App.AppBase).Assembly.GetName().Version;
            if (assemblyVersion != null)
            {
                versionInfo = assemblyVersion.ToString(4);
            }
            return versionInfo;
        }

        #endregion

        #region Security Information
      
        private static byte[] GetCertBytes(string certFilePath, string certName, string certNamePassword, string appDataPath)
        {
            byte[] certBytes = { };

            string certFileName = Path.GetFileName(certFilePath);

            if ((File.Exists(certFilePath) == true) && (ArshuBaseConfiguration.SkipReadFromFileSystem == false))
            {
                #region Retrieve from File System

                if (File.Exists(certFilePath) == true)
                {
                    certBytes = File.ReadAllBytes(certFilePath);
                }

                if (certBytes.Length > 0)
                {
                    AppLogger.WriteLog("Retrieved App Certificate [" + certFileName + "] from File System");
                }

                #endregion
            }
            else
            {
                #region Retrieve from Embedded Resource (Data Pap or Direct)

                Assembly resourceAssembly = typeof(ArshuBaseConfiguration).Assembly;
                if (resourceAssembly != null)
                {
                    string[] manifestResourceList = resourceAssembly.GetManifestResourceNames();
                    for (int i = 0; i < manifestResourceList.Length; i++)
                    {
                        var itemResource = manifestResourceList[i];
                        if (itemResource.EndsWith(certFileName, StringComparison.CurrentCulture) == true)
                        {
                            using (Stream? resourceStream = resourceAssembly.GetManifestResourceStream(itemResource))
                            {
                                if (resourceStream != null)
                                {
                                    byte[] resourceBytes = new byte[resourceStream.Length];
                                    using (var binaryReader = new BinaryReader(resourceStream))
                                    {
                                        binaryReader.Read(resourceBytes, 0, resourceBytes.Length);
                                        binaryReader.Dispose();
                                        certBytes = resourceBytes;
                                        AppLogger.WriteLog("Retrieved App Certificate [" + itemResource + "] from Embedded Resource");
                                        break;
                                    }
                                }
                            }
                        }
                        else if (itemResource.EndsWith("data.pap", StringComparison.CurrentCulture) == true)
                        {
                            using (Stream? resourceStream = resourceAssembly.GetManifestResourceStream(itemResource))
                            {
                                if (resourceStream != null)
                                {
                                    using (ZipArchive appZipStorer = new ZipArchive(resourceStream, ZipArchiveMode.Read))
                                    {
                                        List<ZipArchiveEntry> zipFileList = new List<ZipArchiveEntry>();
                                        zipFileList.AddRange(appZipStorer.Entries);
                                        zipFileList.Sort((x, y) => x.FullName.CompareTo(y.FullName));

                                        if (zipFileList.Count > 0)
                                        {
                                            for (int n = 0; n < zipFileList.Count; n++)
                                            {
                                                var zipItem = zipFileList[n];
                                                if (zipItem.FullName.Contains(certFileName) == true)
                                                {
                                                    using (MemoryStream msZipItem = new MemoryStream())
                                                    {
                                                        //appZipStorer.ExtractFile(zipItem, msZipItem);
                                                        zipItem.Open().CopyTo(msZipItem);
                                                        certBytes = msZipItem.ToArray();
                                                        string fileName = Path.GetFileName(zipItem.FullName);
                                                        AppLogger.WriteLog("Retrieved App Certificate [" + fileName + "] from Embedded AppData Resource");
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                #endregion
            }

            #region Create and Save to Cert File Path

            if ((certBytes.Length == 0)
                && (string.IsNullOrEmpty(certName) == false)
                && (string.IsNullOrEmpty(certNamePassword) == false)
                && (string.IsNullOrEmpty(appDataPath) == false))
            {
                #region App Certificate

                using (RSA rsa = RSA.Create())
                {
                    var appReq = new CertificateRequest($"cn={certName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    var appCert = appReq.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
                    certBytes = appCert.Export(X509ContentType.Pfx, certNamePassword.ToString());

                    if (Directory.Exists(appDataPath) == false)
                    {
                        Directory.CreateDirectory(appDataPath);
                    }
                    File.WriteAllBytes(certFilePath, certBytes);
                    AppLogger.WriteLog("Saved App Certificate [" + certFileName + "] to File System");
                }

                #endregion
            }

            #endregion

            return certBytes;
        }

        public static X509Certificate2 GetSelfCert(string appDataPath, bool useAssemblyCertificate)
        {
            string appName = GetAppName();
            string appAssemblyName = GetAssemblyName();

            string appNamePassword = AppBase.GetHashValue(appName);
            string appAssemblyNamePassword = AppBase.GetHashValue(appAssemblyName);

            string certAppFileName = appName + ".pfx";
            string certAppFilePath = Path.Combine(appDataPath, certAppFileName);

            string certAssemblyFileName = appAssemblyName + ".pfx";
            string certAssemblyFilePath = Path.Combine(appDataPath, certAssemblyFileName);

            byte[] certBytes = { };

            #region App or Assembly Certificate

            if (useAssemblyCertificate == false)
            {
                certBytes = GetCertBytes(certAppFilePath, appName, appNamePassword, appDataPath);
            }
            else
            {
                certBytes = GetCertBytes(certAssemblyFilePath, appAssemblyName, appAssemblyNamePassword, appDataPath);
            }

            #endregion

            #region Create the Certificate

            string certPassword = appNamePassword;
            if (useAssemblyCertificate == true) { certPassword = appAssemblyNamePassword; }

            X509Certificate2 x509Certificate = new X509Certificate2(certBytes, certPassword);

            #endregion

            return x509Certificate;
        }

        #endregion

        #region Service Configuration

        public static void ConfigureBasicServices(this IHostApplicationBuilder builder, int overrideMetricsPort = -1)
        {
            #region Initialize

            string appName = GetAppName();
            string appAssemblyName = GetAssemblyName();
            string versionInfo = GetVersionInfo();

            #endregion

            #region Set Env Metrics Config

            int metricsPort = 9091;
            string? metricsPortTxt = Environment.GetEnvironmentVariable("METRICS_PORT");
            if (string.IsNullOrEmpty(metricsPortTxt) == false)
            {
                if (int.TryParse(metricsPortTxt, out int metricsPortVal) == true)
                {
                    metricsPort = metricsPortVal;
                }
            }

            if (overrideMetricsPort > -1)
            {
                metricsPort = overrideMetricsPort;
            }

            #endregion


            #region Log Configuration

            builder.Logging.ClearProviders();
            //builder.Logging.AddConsole();

            #endregion

            #region Routing Configuration

            builder.Services.AddRoutingCore();
            //builder.Services.Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"));

            #endregion

            #region Compression Configuration

            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });

            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.SmallestSize;
            });

            #endregion

            #region Forward Headers/Cookie

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                //options.ForwardedForHeaderName = "Fly-Client-IP";
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Any, 0)); // This needed to be added
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.IPv6Any, 0)); // This needed to be added
                //options.KnownNetworks.Clear();
                //options.KnownProxies.Clear();
            });

            builder.Services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            #endregion

            #region Use CORS

            //if (string.IsNullOrEmpty(appConfig.AllowedOrigins) == false)
            //{
            //    app.UseCors(x => x
            //        .WithMethods("GET, POST")
            //        .AllowAnyHeader()
            //        //.SetIsOriginAllowed(origin => true) // allow any origin
            //        .WithOrigins(appConfig.AllowedOrigins)); // Allow only this origin can also have multiple origins separated with comma
            //        .DisallowCredentials());
            //}

            #endregion

            #region Json Configuration

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                //options.SerializerOptions.TypeInfoResolverChain.Insert(0, BaseJsonSerializerContext.Default);
            });

            #endregion

            AppLogger.WriteLog("After Kestrel/Cookie Basic Config");

            #region Metrics Configuration

            if (metricsPort >= 0)
            {
                builder.Services.AddOpenTelemetry()
                    .ConfigureResource(resource => resource
                        .AddService(serviceName: builder.Environment.ApplicationName))
                    .WithMetrics(builder =>
                    {
                        builder.AddAspNetCoreInstrumentation();
                        builder.AddPrometheusExporter(opt =>
                        {
                            opt.ScrapeResponseCacheDurationMilliseconds = 500;
                            opt.ScrapeEndpointPath = "/metrics";
                        });
                        builder.AddMeter(appName, versionInfo);
                    });
            }

            #endregion

            AppLogger.WriteLog("After Metric Config");
        }

        #endregion

        #region Middleware Registration

        private const double MinInitialTimeInSec = 0;
        private const double MinIdleTimeInSec = 0.1;
        public static void RegisterIdleShutdownMiddleware(this WebApplication app, double overrideInitialTimeInSec = -1, double overrideIdleTimeInSec = -1)
        {
            #region Set Env Idle Shutdown Config

            double initialTimeInSec = 0;
            string? initialTimeInSecTxt = Environment.GetEnvironmentVariable("INITIAL_TIME_IN_SEC");
            if (string.IsNullOrEmpty(initialTimeInSecTxt) == false)
            {
                if (double.TryParse(initialTimeInSecTxt, out double initialTimeInSecVal) == true)
                {
                    initialTimeInSec = initialTimeInSecVal;
                }
            }

            if ((initialTimeInSec == 0) && (overrideInitialTimeInSec > -1))
            {
                initialTimeInSec = overrideInitialTimeInSec;
            }

            double idleTimeInSec = 0;
            string? idleTimeInSecTxt = Environment.GetEnvironmentVariable("IDLE_TIME_IN_SEC");
            if (string.IsNullOrEmpty(idleTimeInSecTxt) == false)
            {
                if (double.TryParse(idleTimeInSecTxt, out double idleTimeInSecVal) == true)
                {
                    idleTimeInSec = idleTimeInSecVal;
                }
            }

            if ((idleTimeInSec == 0) && (overrideIdleTimeInSec > -1))
            {
                idleTimeInSec = overrideIdleTimeInSec;
            }

            if ((initialTimeInSec > 0) && (initialTimeInSec < MinInitialTimeInSec))
            {
                initialTimeInSec = MinInitialTimeInSec;
            }
            if ((idleTimeInSec > 0) && (idleTimeInSec < MinIdleTimeInSec))
            {
                idleTimeInSec = MinIdleTimeInSec;
            }

            #endregion

            #region Use Idle Time Shutdown

            if (idleTimeInSec > 0)
            {
                if (initialTimeInSec == 0) initialTimeInSec = idleTimeInSec;
                app.UseArshuIdleTimeShutdown(initialTimeInSec, idleTimeInSec, App.AppBase.WSApiEndPoint + "," + App.AppBase.MetricsEndPoint);

                //ArshuIdleTimeShutdownMiddleware.OnAppStopping += (obj, eventArgs) =>
                //{
                //    appLifetime.StopApplication();
                //};

                AppLogger.WriteLog("Configuring Idle Time Shutdown for Initial Time in Sec [" + initialTimeInSec + "] and Idle Time in Sec [" + idleTimeInSec + "]");               
            }

            #endregion

            AppLogger.WriteLog("After Idle Shutdown Configure");
        }

        public static void RegisterBasicMiddlewares(this IApplicationBuilder app, IHostApplicationLifetime appLifetime, int overrideMetricsPort = -1)
        {
            #region Initialize

            string? envAppRegion = Environment.GetEnvironmentVariable("FLY_REGION");

            string versionInfo = GetVersionInfo();

            string appName = GetAppName();

            #endregion

            #region Build Validaty Date

            DateTime buildDate = App.AppBase.GetBuildDate();
            int validityPeriodInMonths = App.AppBase.GetBuildValidityInMonths();
            DateTime buildValidityDate = buildDate.AddMonths(validityPeriodInMonths);

            #endregion

            #region Set Env Metrics Config

            int metricsPort = 9091;
            string? metricsPortTxt = Environment.GetEnvironmentVariable("METRICS_PORT");
            if (string.IsNullOrEmpty(metricsPortTxt) == false)
            {
                if (int.TryParse(metricsPortTxt, out int metricsPortVal) == true)
                {
                    metricsPort = metricsPortVal;
                }
            }

            if (overrideMetricsPort > -1)
            {
                metricsPort = overrideMetricsPort;
            }

            #endregion

            #region Hook App Startup Handler

            List<string> addressList = new List<string>();

            bool applicationStarted = false;

            if (appLifetime != null)
            {
                appLifetime.ApplicationStarted.Register(async () =>
                {
                    await Task.Delay(0).ConfigureAwait(false);

                    double appStartupTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;

                    var originalForeColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    AppLogger.WriteLog("Started after " + appStartupTimeInMs + " milliseconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                    Console.ForegroundColor = originalForeColor;

                    try
                    {
                        string firstAddress = "";

                        #region Security Configuration

                        //var keyManager = app.ApplicationServices.GetService<IKeyManager>();
                        //if (keyManager != null)
                        //{
                        //    keyManager.CreateNewKey(
                        //        activationDate: DateTimeOffset.Now,
                        //        expirationDate: DateTimeOffset.Now.AddDays(7));
                        //}

                        #endregion

                        #region Get Server First Address

                        var server = app.ApplicationServices.GetService<IServer>();
                        //var server = app.ServerFeatures.Get<IServer>();
                        if (server != null)
                        {
                            var addressFeature = server.Features.Get<IServerAddressesFeature>();
                            if (addressFeature != null)
                            {
                                foreach (var address in addressFeature.Addresses)
                                {
                                    AppLogger.WriteLog("Hosting Url [" + address + "]");
                                    if (string.IsNullOrEmpty(firstAddress) == true) firstAddress = address;

                                    addressList.Add(address);

                                    if ((address.Contains(":5000") == true)
                                    //&& (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true)
                                    )
                                    {
                                        OpenUrl(address);
                                        break;
                                    }
                                }
                            }
                        }

                        #endregion

                        await App.AppBase.RaiseAppStartupEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(AppLogger.FirstAddress) == true) AppLogger.FirstAddress = firstAddress;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WriteLog("Program_GetUrl Error:" + ex.Message);
                    }

                    if (App.AppBase.HaveAppStoppingEvent() == false)
                    {
                        ActionHandler redirectAppStoppingHandler = async (source, actionName, actionArgs) =>
                        {
                            return await RaiseAppShutdownEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                        };
                        App.AppBase.OnAppStopping -= redirectAppStoppingHandler;
                        App.AppBase.OnAppStopping += redirectAppStoppingHandler;
                    }

                    applicationStarted = true;
                });
            }

            #endregion

            #region Hook App Shutdown Handler

            if (appLifetime != null)
            {
                ActionHandler appShutdownHandler = async (source, actionName, actionArgs) =>
                {
                    await Task.Run(async () =>
                    {
                        //Max 10 Seconds Check
                        int delayCount = 0;
                        int maxDelayCount = 400;
                        while (applicationStarted == false)
                        {
                            await Task.Delay(25).ConfigureAwait(false);
                            delayCount++;
                            if (delayCount >= maxDelayCount) break;
                        }

                        double beforeFunctionRunningInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;
                        await App.AppBase.IsFunctionRunning(App.AppBase.FunctionStartup).ConfigureAwait(false);
                        double afterFunctionRunningInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;
                        double functionRuningLapsedTime = afterFunctionRunningInMs - beforeFunctionRunningInMs;
                        if (functionRuningLapsedTime > 10)
                        {
                            AppLogger.WriteLog("Waiting for FunctionRunning for " + functionRuningLapsedTime + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                        }

                        await App.AppBase.IsFunctionRunning(App.AppBase.FunctionStopping).ConfigureAwait(false);
                        double afterFunctionStoppingInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;
                        double functionStoppingLapsedTime = afterFunctionStoppingInMs - afterFunctionRunningInMs;
                        if (functionStoppingLapsedTime > 10)
                        {
                            AppLogger.WriteLog("Waiting for FunctionStopping for " + functionStoppingLapsedTime + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                        }

                        await App.AppBase.IsFunctionRunning(App.AppBase.FunctionShutdown).ConfigureAwait(false);
                        double afterFunctionShutdownInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;
                        double functionShutdownLapsedTime = afterFunctionShutdownInMs - afterFunctionStoppingInMs;
                        if (functionShutdownLapsedTime > 10)
                        {
                            AppLogger.WriteLog("Waiting for FunctionShutdown for " + functionShutdownLapsedTime + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                        }

                        double appRunTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;
                        AppLogger.WriteLog("Stopping Application called after " + appRunTimeInMs + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");

#if !DEBUG
                        appLifetime.StopApplication();
#endif
                    }).ConfigureAwait(false);

                    return true;
                };
                OnAppShutdown -= appShutdownHandler;
                OnAppShutdown += appShutdownHandler;

                appLifetime.ApplicationStopped.Register(() =>
                {
                    OnAppShutdown -= appShutdownHandler;

                    double appRunTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;

                    var originalForeColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    AppLogger.WriteLog("Stopped called after " + appRunTimeInMs + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                    Console.ForegroundColor = originalForeColor;
                });

                var sigintReceived = false;
                PosixSignalRegistration.Create(PosixSignal.SIGINT, async (context) =>
                {
                    context.Cancel = true; //Stop Shutdown
                    sigintReceived = true;

                    double appRunTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;

                    AppLogger.WriteLog("Received PosixSignal SIGINT after " + appRunTimeInMs + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                    bool haveStoppingHandler = await App.AppBase.RaiseAppStoppingEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                    if (haveStoppingHandler == false)
                    {
                        await RaiseAppShutdownEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                    }
                });

                PosixSignalRegistration.Create(PosixSignal.SIGTERM, async (context) =>
                {
                    if (!sigintReceived)
                    {
                        context.Cancel = true; //Stop Shutdown
                        sigintReceived = true;

                        double appRunTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;

                        AppLogger.WriteLog("Received PosixSignal SIGTERM after " + appRunTimeInMs + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                        bool haveStoppingHandler = await App.AppBase.RaiseAppStoppingEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                        if (haveStoppingHandler == false)
                        {
                            await RaiseAppShutdownEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        AppLogger.WriteLog("Received PosixSignal SIGTERM, ignoring it because already processed SIGINT");
                    }
                });

                Console.CancelKeyPress += async (_, ea) =>
                {
                    ea.Cancel = true;
                    sigintReceived = true;

                    double appRunTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;

                    AppLogger.WriteLog("Received Cancel Key Press after " + appRunTimeInMs + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                    bool haveStoppingHandler = await App.AppBase.RaiseAppStoppingEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                    if (haveStoppingHandler == false)
                    {
                        await RaiseAppShutdownEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                    }
                };

                AppDomain.CurrentDomain.ProcessExit += async (_, _) =>
                {
                    if (!sigintReceived)
                    {
                        double appRunTimeInMs = Stopwatch.GetElapsedTime(AppLogger.AppStartTime).TotalMilliseconds;

                        AppLogger.WriteLog("Received ProcessExit SIGTERM after " + appRunTimeInMs + " MilliSeconds [" + appName + "][" + envAppRegion + "][" + versionInfo + "." + buildValidityDate.ToString("yyyyMMdd") + "]");
                        bool haveStoppingHandler = await App.AppBase.RaiseAppStoppingEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                        if (haveStoppingHandler == false)
                        {
                            await RaiseAppShutdownEvent(typeof(ArshuBaseConfiguration)).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        AppLogger.WriteLog("Received ProcessExit SIGTERM, ignoring it because already processed SIGINT");
                    }
                };
            }

            #endregion

            AppLogger.WriteLog("After Application Event Configure");

            #region Use Compression

            app.UseResponseCompression();

            #endregion

            #region Use Forwarded Headers

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            #endregion

            #region Use Arshu Replay Header

            app.UseArshuReplayHeader("");

            #endregion

            #region Use Arshu Echo Info

            app.UseArshuEchoInfo();

            #endregion

            AppLogger.WriteLog("After Basic Use Services");

            #region Use Metrics

            if (metricsPort >= 0)
            {
                if (metricsPort == 0)
                {
                    //app.MapPrometheusScrapingEndpoint();
                    app.UseOpenTelemetryPrometheusScrapingEndpoint(
                        context => context.Request.Path == "/metrics");
                }
                else
                {
                    //app.MapPrometheusScrapingEndpoint().RequireHost("*:" + metricsPort);
                    app.UseOpenTelemetryPrometheusScrapingEndpoint(
                        context => context.Request.Path == "/metrics"
                            && context.Connection.LocalPort == metricsPort); //8080/9091
                }
            }

            #endregion

            AppLogger.WriteLog("After Use Metrics");
        }

        #endregion

        #region Open Url

        [System.Security.SecuritySafeCriticalAttribute]
        private static Tuple<bool, string> OpenUrl(string url)
        {
            bool ret = false;
            string message = "";

            try
            {
                var proc = new Process();
                var si = new ProcessStartInfo();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    si.FileName = url;
                    si.UseShellExecute = true;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    si.FileName = "xdg-open";
                    si.ArgumentList.Add(url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    si.FileName = "open";
                    si.ArgumentList.Add(url);
                }
                else
                {
                    AppLogger.WriteLog("Don't know how to open url on this OS platform");
                }

                proc.StartInfo = si;
                proc.Start();
            }
            catch (Exception exc1)
            {
                message = exc1.Message;
                // System.ComponentModel.Win32Exception is a known exception that occurs when Firefox is default browser.  
                // It actually opens the browser but STILL throws this exception so we can just ignore it.  If not this exception,
                // then attempt to open the URL in IE instead.
                if (exc1.GetType().ToString() != "System.ComponentModel.Win32Exception")
                {
                    // sometimes throws exception so we have to just ignore
                    // this is a common .NET bug that no one online really has a great reason for so now we just need to try to open
                    // the URL using IE if we can.
                    try
                    {
                        System.Diagnostics.ProcessStartInfo? startInfo = new System.Diagnostics.ProcessStartInfo("IExplore.exe", url);
                        System.Diagnostics.Process.Start(startInfo);
                        startInfo = null;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WriteLog("ActionManager-OpenUrl Error:" + ex.Message);
                    }
                }
            }

            return new Tuple<bool, string>(ret, message);
        }

        //[System.Security.SecuritySafeCriticalAttribute]
        //private static Tuple<bool, string> OpenUrl(string sUrl)
        //{
        //    bool ret = false;
        //    string message = "";

        //    try
        //    {
        //        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        //        proc.StartInfo.UseShellExecute = true;
        //        proc.StartInfo.FileName = sUrl;
        //        proc.Start();
        //        ret = true;
        //    }
        //    catch (Exception exc1)
        //    {
        //        message = exc1.Message;
        //        // System.ComponentModel.Win32Exception is a known exception that occurs when Firefox is default browser.  
        //        // It actually opens the browser but STILL throws this exception so we can just ignore it.  If not this exception,
        //        // then attempt to open the URL in IE instead.
        //        if (exc1.GetType().ToString() != "System.ComponentModel.Win32Exception")
        //        {
        //            // sometimes throws exception so we have to just ignore
        //            // this is a common .NET bug that no one online really has a great reason for so now we just need to try to open
        //            // the URL using IE if we can.
        //            try
        //            {
        //                System.Diagnostics.ProcessStartInfo? startInfo = new System.Diagnostics.ProcessStartInfo("IExplore.exe", sUrl);
        //                System.Diagnostics.Process.Start(startInfo);
        //                startInfo = null;
        //            }
        //            catch (Exception ex)
        //            {
        //                LogManager.WriteLog(LogType.Error, "ActionManager-OpenUrl", ex.Message, ex, typeof(Program));
        //            }
        //        }
        //    }

        //    return new Tuple<bool, string>(ret, message);
        //}

        #endregion
    }
}
