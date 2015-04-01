﻿//-----------------------------------------------------------------------
// <copyright file="ProjectLoader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public static class ProjectLoader
    {
        public static List<Project> LoadFrom(string dumpFolderPath)
        {
            List<Project> result = new List<Project>();

            foreach (string projectFolderPath in Directory.GetDirectories(dumpFolderPath))
            {
                var projectInfo = TryGetProjectInfo(projectFolderPath);
                if (projectInfo == null)
                {
                    continue;
                }

                List<String> files = new List<string>();
                var compiledFilesPath = TryGetAnalysisFileLocation(projectInfo, AnalysisType.ManagedCompilerInputs);
                if (compiledFilesPath != null)
                {
                    files.AddRange(File.ReadAllLines(compiledFilesPath, Encoding.UTF8));
                }
                var contentFilesPath = TryGetAnalysisFileLocation(projectInfo, AnalysisType.ContentFiles);
                if (contentFilesPath != null)
                {
                    files.AddRange(File.ReadAllLines(contentFilesPath, Encoding.UTF8));
                }
                if (!files.Any())
                {
                    // Skip projects without any source file
                    continue;
                }

                bool isTest = projectInfo.ProjectType == ProjectType.Test;
                string fxCopReport = TryGetAnalysisFileLocation(projectInfo, AnalysisType.FxCop);
                string visualStudioCodeCoverageReport = TryGetAnalysisFileLocation(projectInfo, AnalysisType.VisualStudioCodeCoverage);

                result.Add(new Project(projectInfo.ProjectName, projectInfo.ProjectGuid, projectInfo.FullPath, isTest, files, fxCopReport, visualStudioCodeCoverageReport));
            }

            return result;
        }

        private static ProjectInfo TryGetProjectInfo(string projectFolderPath)
        {
            ProjectInfo projectInfo = null;

            string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

            if (File.Exists(projectInfoPath))
            {
                projectInfo = ProjectInfo.Load(projectInfoPath);
            }

            return projectInfo;
        }

        /// <summary>
        /// Attempts to return the file location for the specified type of analysis result.
        /// Returns null if the there is not a result for the specified type, or if the
        /// file does not exist.
        /// </summary>
        private static string TryGetAnalysisFileLocation(ProjectInfo projectInfo, AnalysisType analysisType)
        {
            string location = null;

            AnalysisResult result = null;
            if (projectInfo.TryGetAnalyzerResult(analysisType, out result))
            {
                if (File.Exists(result.Location))
                {
                    location = result.Location;
                }
            }
            return location;
        }
    }
}