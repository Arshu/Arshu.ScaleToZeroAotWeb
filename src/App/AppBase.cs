using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable 

namespace Arshu.App
{
    public enum SearchBy
    {
        ExactMatch,
        ByContains,
        ByStartWith,
        ByEndWith,
    }

    public delegate Task<bool> ActionHandler(Type source, string actionName, object? actionArg);

    public class AppBase
    {
        #region General Configuration

        public static string SiteDateFormat = "dd.MMM.yyyy HH:mm:ss";
        public static string AppDataFolderName = "App_Data";
        public static string CertFolderName = "Cert";

        #endregion

        #region Loopback IP

        public const string IPv6LocalAddress = "[0000:0000:0000:0000:0000:0000:0000:0001]";
        public const string IPv4LocalAddress = "127.0.0.1";

        #endregion

        #region WebSocket Configuration

        public static string WSApiEndPoint = "ws";

        #endregion

        #region Metrics Configuration

        public static string MetricsEndPoint = "metrics";

        #endregion



        #region App Running Check

        public const string FunctionStartup = "Startup";
        public const string FunctionStopping = "Stopping";
        public const string FunctionShutdown = "Shutdown";
        public const int MaxFunctionCount = 10;

        private static readonly Dictionary<string, SemaphoreSlim> RunningSemaphoreList = new Dictionary<string, SemaphoreSlim>();
        private static int MaxWaitTimeInMs = 120000;
        public static async Task<(bool RunStatus, int RunCount, List<string> FunctionList)> IsFunctionRunning(string functionInfo, bool release = false, int initialCount = 1, int maxCount = 1)
        {
            bool runStatus = false;
            int runCount = 0;
            List<string> functionList = new List<string>();

            try
            {
                if (release == false)
                {
                    SemaphoreSlim runningSemaphore = new SemaphoreSlim(initialCount, maxCount);
                    if (RunningSemaphoreList.ContainsKey(functionInfo) == false)
                    {
                        RunningSemaphoreList.Add(functionInfo, runningSemaphore);
                    }
                    else
                    {
                        runningSemaphore = RunningSemaphoreList[functionInfo];
                    }
                    runStatus = await runningSemaphore.WaitAsync(MaxWaitTimeInMs).ConfigureAwait(false);
                    runCount = runningSemaphore.CurrentCount;
                }
                else
                {
                    if (RunningSemaphoreList.ContainsKey(functionInfo) == true)
                    {
                        SemaphoreSlim runningSemaphore = RunningSemaphoreList[functionInfo];
                        runCount = runningSemaphore.Release();
                    }
                }
                functionList.AddRange(RunningSemaphoreList.Keys);
            }
            catch (Exception ex)
            {
                AppLogger.WriteLog("IsFunctionRunning -" + ex.Message);
            }

            return (runStatus, runCount, functionList);
        }

        #endregion

        #region App Startup EventHook

        public static event ActionHandler? OnAppStartup;
        public static bool IsAppStarted = false;
        public static async Task<bool> RaiseAppStartupEvent(Type sourceType, object? actionArg = null)
        {
            bool ret = false;
            IsAppStarted = true;
            if (OnAppStartup != null)
            {
                ret = await OnAppStartup(sourceType, "Startup", actionArg).ConfigureAwait(false);
            }
            return ret;
        }
        

        #endregion

        #region App Stopping EventHook

        public static event ActionHandler? OnAppStopping;

        public static bool HaveAppStoppingEvent()
        {
            bool ret = false;
            if (OnAppStopping != null)
            {
                ret = true;
            }
            return ret;
        }

        public static async Task<bool> RaiseAppStoppingEvent(Type sourceType, object? actionArg = null)
        {
            bool ret = false;
            int delayCount = 0;
            int maxDelayCount = 400;
            while (IsAppStarted == false)
            {
                await Task.Delay(25).ConfigureAwait(false);
                delayCount++;
                if (delayCount >= maxDelayCount) break;
            }
            if (OnAppStopping != null)
            {
                ret = await OnAppStopping(sourceType, "Stopping", actionArg).ConfigureAwait(false);
            }
            return ret;
        }

        #endregion
       
        #region Build Info

        public static DateTime GetBuildDate()
        {
            DateTime buildDate = new DateTime(2023, 1, 1);

            Assembly resourceAssembly = typeof(AppBase).Assembly;
            string[] resourceNameList = resourceAssembly.GetManifestResourceNames();
            foreach (var itemResource in resourceNameList)
            {
                if (itemResource.Contains("BuildDate") == true)
                {
                    using (Stream? stm = resourceAssembly.GetManifestResourceStream(itemResource))
                    {
                        if (stm != null)
                        {
                            byte[] ba = new byte[(int)stm.Length];
                            stm.Read(ba, 0, (int)stm.Length);
                            string dateString = UTF8Encoding.UTF8.GetString(ba);
                            if (DateTime.TryParse(dateString, out DateTime resourceBuildDate) == true)
                            {
                                buildDate = resourceBuildDate;
                                break;
                            }
                            else
                            {
                                buildDate = new DateTime(2021, 10, 1);
                            }
                        }
                    }
                }
            }
            //if (DateTime.TryParse(Arshu.Base.Build.Timestamp, out DateTime resourceBuildDate) == true)
            //{
            //    buildDate = resourceBuildDate;
            //}

            return buildDate;
        }

        public static int GetBuildValidityInMonths()
        {
            int validityPeriodInMonths = 120;
            return validityPeriodInMonths;
        }

        public static DateTime GetBuildExpiryDate()
        {
            DateTime checkDate = AppBase.GetBuildDate();
            int validityPeriodInMonths = AppBase.GetBuildValidityInMonths();
            DateTime expiryDate = checkDate.AddMonths(validityPeriodInMonths);

            return expiryDate;
        }

        #endregion

        #region App Version

        public static string Version
        {
            get
            {
                string versionInfo = "";
                if (string.IsNullOrEmpty(versionInfo) == true)
                {
                    Version? assemblyVersion = typeof(AppBase).Assembly.GetName().Version;
                    if (assemblyVersion != null)
                    {
                        versionInfo = assemblyVersion.ToString(4);
                    }
                }

                int validityPeriodInMonths = AppBase.GetBuildValidityInMonths();

                DateTime buildDate = AppBase.GetBuildDate();
                DateTime buildValidityDate = buildDate.AddMonths(validityPeriodInMonths);

                string fullversion = versionInfo + "." + buildValidityDate.ToString("yyyyMMdd");
                return fullversion;
            }
        }

        public static int ResourceVersion = 0;
        public static string GetResourceVersion()
        {
            string resourceVersion = ResourceVersion.ToString(CultureInfo.InvariantCulture);
            if (ResourceVersion == 0)
            {
                string version = AppBase.Version;
                int idxOfLastDot = version.LastIndexOf(".");
                if (idxOfLastDot > -1)
                {
                    string versionWithoutDate = version.Substring(0, idxOfLastDot);
                    int idxOfNextLastDot = versionWithoutDate.LastIndexOf(".");
                    if (idxOfNextLastDot > -1)
                    {
                        resourceVersion = versionWithoutDate.Substring(idxOfNextLastDot + 1);
                    }
                }
            }
            return resourceVersion;
        }

        #endregion

        #region Hash Utility

        private static string HashSecure = "fiyfyfyf o giufjytdfuyrstst";
        public static string GetHashValue(string txt)
        {
            byte[] txtHashBytes = { };
            using (HashAlgorithm algorithm = SHA256.Create())
            {
                byte[] txtBytes = Encoding.UTF8.GetBytes(txt + HashSecure);
                txtHashBytes = algorithm.ComputeHash(txtBytes);
            }
            //string txtHash = BitConverter.ToString(txtHashBytes).Replace("-", String.Empty);

            StringBuilder txtHash = new StringBuilder();
            foreach (byte b in txtHashBytes)
                txtHash.Append(b.ToString("X2"));

            return txtHash.ToString();
        }

        #endregion

        #region Is Localhost

        public static bool IsLocalHost(string rootDomain)
        {
            return rootDomain.ToUpper().Contains("LOCALHOST")
                || rootDomain.Contains("127.0.0.1")
                || (rootDomain == "0000:0000:0000:0000:0000:0000:0000:0001")
                || (rootDomain == "0:0:0:0:0:0:0:1")
                || (rootDomain == "::1");
        }

        #endregion

        #region Json Utility

        public static string GetJson(Dictionary<string, string> jsonDict, bool prettyFormat = true)
        {
            string newLine = Environment.NewLine;
            if (prettyFormat == false) newLine = "";

            List<string> entries = new List<string>();
            foreach (var item in jsonDict)
            {
                entries.Add(string.Format("\"{0}\": \"{1}\"", item.Key, item.Value) + newLine);
            }
            var jsonString = "{" + newLine + string.Join(",", entries) + newLine + "}";

            return jsonString;
        }

        #endregion

        #region Search Methods

        public static bool InListPattern(string patternSplitString, char stringSeparator, bool capitalizeString, string searchString, SearchBy searchBy, bool patternInSearchString)
        {
            bool ret = false;
            List<string> list = GetList(patternSplitString, stringSeparator, capitalizeString);
            if (capitalizeString == true)
            {
                searchString = searchString.ToUpper();
            }

            if (searchBy != SearchBy.ExactMatch)
            {
                foreach (string item in list)
                {
                    switch (searchBy)
                    {
                        case SearchBy.ByContains:
                            if (patternInSearchString == false)
                            {
                                ret = item.Contains(searchString);
                            }
                            else
                            {
                                ret = searchString.Contains(item);
                            }
                            break;
                        case SearchBy.ByStartWith:
                            if (patternInSearchString == false)
                            {
                                ret = item.StartsWith(searchString);
                            }
                            else
                            {
                                ret = searchString.StartsWith(item);
                            }
                            break;
                        case SearchBy.ByEndWith:
                            if (patternInSearchString == false)
                            {
                                ret = item.EndsWith(searchString);
                            }
                            else
                            {
                                ret = searchString.EndsWith(item);
                            }
                            break;
                        default:
                            if (patternInSearchString == false)
                            {
                                ret = item.Contains(searchString);
                            }
                            else
                            {
                                ret = searchString.Contains(item);
                            }
                            break;
                    }
                    if (ret == true)
                        break;
                }
            }
            else
            {
                if (list.Contains(searchString) == true)
                {
                    ret = true;
                }
            }

            return ret;
        }

        public static List<string> GetList(string splitString, char stringSeparator, bool capitalizeString, string appendSuffix = "", string appendPrefix = "")
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
}

#nullable disable