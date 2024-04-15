using Markdig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arshu.App
{
    public class MarkdownUtil
    {
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

                _htmlFilesList.Add(dirPath, htmlFiles);
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

                _cssFilesList.Add(dirPath, cssFiles);
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

    }
}
