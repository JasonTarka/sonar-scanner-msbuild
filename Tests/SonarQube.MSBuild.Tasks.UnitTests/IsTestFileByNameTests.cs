﻿/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Diagnostics;
using System.IO;
using TestUtilities;

namespace SonarQube.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class IsTestFileByNameTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_NoRegex()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1. Check file names
            CheckFilePathIsNotTest(testFolder, "test"); // file name alone, no extension
            CheckFilePathIsNotTest(testFolder, "test.csproj"); // file name alone
            CheckFilePathIsNotTest(testFolder, "proj.test"); // ".test" extension
            CheckFilePathIsNotTest(testFolder, "proj.AtestB"); // part of the extension

            CheckFilePathIsNotTest(testFolder, "..\\..\\abc\\test.csproj"); // with relative path
            CheckFilePathIsNotTest(testFolder, "f:\\abc\\test.csproj"); // with absolute path
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\TEST.csproj"); // case-sensitivity
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\Another.test.vbproj"); // training "test"
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\test.foo.proj"); // leading "test"
            CheckFilePathIsNotTest(testFolder, "d:\\abc\\XXXTesTyyy.proj"); // contained "test"

            CheckFilePathIsNotTest(testFolder, "c:\\aFile.csproj"); // doesn't contain "test"
            CheckFilePathIsNotTest(testFolder, "c:\\notATesFile.csproj"); // doesn't contain "test"

            // 2. Check for directory called "test"
            CheckFilePathIsNotTest(testFolder, "c:\\test\\my.csproj");
            CheckFilePathIsNotTest(testFolder, "c:\\aaa\\test\\bbb\\my.csproj"); // embedded in path
            CheckFilePathIsNotTest(testFolder, "..\\test\\bbb\\my.csproj"); // relative
            CheckFilePathIsNotTest(testFolder, ".\\TesT\\bbb\\my.csproj"); // case-sensitivity

            CheckFilePathIsNotTest(testFolder, "..\\Atest\\a.b"); // prefixed
            CheckFilePathIsNotTest(testFolder, "..\\testX\\a.b"); // suffixed
            CheckFilePathIsNotTest(testFolder, "..\\XXXtestYYY\\a.b"); // suffixed

            // 3. Check for directory called "tests"
            CheckFilePathIsNotTest(testFolder, "c:\\tests\\my.csproj");
            CheckFilePathIsNotTest(testFolder, "c:\\aaa\\tests\\bbb\\my.csproj"); // embedded in path
            CheckFilePathIsNotTest(testFolder, "..\\tests\\bbb\\my.csproj"); // relative
            CheckFilePathIsNotTest(testFolder, ".\\TesTs\\bbb\\my.csproj"); // case-sensitivity

            CheckFilePathIsNotTest(testFolder, "..\\Atests\\a.b"); // prefixed
            CheckFilePathIsNotTest(testFolder, "..\\testsX\\a.b"); // suffixed
            CheckFilePathIsNotTest(testFolder, "..\\XXXtestsYYY\\a.b"); // suffixed
        }

        [TestMethod]
        [TestCategory("IsTest")]
        [Description(@"Validate the default regex that determines if a project is test or not if the filename contains the 'test' token (not the file path!)")]
        public void IsTestFile_DefaultRegex()
        {
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            EnsureAnalysisConfig(testFolder, SonarProperties.DefaultTestProjectPattern);

            // filename contains 'test'
            CheckFilePathIsTest(testFolder, "c:\\foo\\mytest.proj");
            CheckFilePathIsTest(testFolder, "c:\\foo\\xtesty.proj");
            CheckFilePathIsTest(testFolder, "c:\\foo\\bar space\\testmy.proj");
            CheckFilePathIsTest(testFolder, "c:\\foo\\test.proj");
            CheckFilePathIsTest(testFolder, "c:\\foo\\bar space\\foo.test");
            CheckFilePathIsTest(testFolder, "c:\\foo\\bar space\\tEsT");
            CheckFilePathIsTest(testFolder, "c:\\foo\\bar space\\xTestyyy.proj");
            CheckFilePathIsTest(testFolder, "c:\\foo\\testtest");
            CheckFilePathIsTest(testFolder, "c:\\foo\\bar\\a.test.proj");
            CheckFilePathIsTest(testFolder, "c:\\foo\\test\\xtesty.proj");
            CheckFilePathIsTest(testFolder, "xTESTy");
            CheckFilePathIsTest(testFolder, "test");
            CheckFilePathIsTest(testFolder, "c:\\foo\\ TEST ");

            CheckFilePathIsNotTest(testFolder, "c:\\foo\\te st.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\foo\\bar\\test\\foo.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\xtestyy\\foo");
            CheckFilePathIsNotTest(testFolder, "c:\\foo\\bar\\myproj.csproj");
            CheckFilePathIsNotTest(testFolder, "test\\foo.proj");
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_InvalidRegexInConfig()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string invalidRegEx = "Invalid regex ((";
            EnsureAnalysisConfig(testFolder, invalidRegEx);

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = "Path";
            task.AnalysisConfigDir = testFolder;

            bool result = task.Execute();

            Assert.IsFalse(result, "Expecting the task to fail");
            dummyEngine.AssertSingleErrorExists(invalidRegEx); // expecting the invalid expression to appear in the error
        }

        [TestMethod]
        [TestCategory("IsTest")] // Regression test for bug http://jira.codehaus.org/browse/SONARMSBRU-11
        public void IsTestFile_TimeoutIfConfigLocked_TaskFails()
        {
            // Arrange
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string configFile = EnsureAnalysisConfig(testFolder, ".XX.");

            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = "XXX.proj";
            task.AnalysisConfigDir = testFolder;

            bool result = true;
            TaskUtilitiesTests.PerformOpOnLockedFile(configFile, () => result = task.Execute(), shouldTimeoutReadingConfig: true);

            Assert.IsFalse(result, "Expecting the task to fail if the config file could not be read");
            dummyEngine.AssertNoWarnings();
            dummyEngine.AssertSingleErrorExists();
        }

        [TestMethod]
        [TestCategory("IsTest")]
        public void IsTestFile_RegExFromConfig()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            // 1a. Check the config setting is used if valid
            EnsureAnalysisConfig(testFolder, ".A.");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsTest(testFolder, "c:\\aProject.proj");

            // 1b. Check another config valid config setting
            EnsureAnalysisConfig(testFolder, ".TEST.");
            CheckFilePathIsTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 2. Check the default is used if the setting is missing
            EnsureAnalysisConfig(testFolder, null);
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 3a. Check the default is used if the setting is empty
            EnsureAnalysisConfig(testFolder, "");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");

            // 3b. Check the default is used if the setting contains only whitespaces
            EnsureAnalysisConfig(testFolder, " ");
            CheckFilePathIsNotTest(testFolder, "c:\\test\\mytest.proj");
            CheckFilePathIsNotTest(testFolder, "c:\\aProject.proj");
        }

        #endregion Tests

        #region Private methods

        /// <summary>
        /// Ensures an analysis config file exists in the specified directory,
        /// replacing one if it already exists.
        /// If the supplied "regExExpression" is not null then the appropriate setting
        /// entry will be created in the file
        /// </summary>
        private static string EnsureAnalysisConfig(string parentDir, string regExExpression)
        {
            AnalysisConfig config = new AnalysisConfig();
            if (regExExpression != null)
            {
                config.LocalSettings = new AnalysisProperties();
                config.LocalSettings.Add(new Property() { Id = IsTestFileByName.TestRegExSettingId, Value = regExExpression });
            }

            string fullPath = Path.Combine(parentDir, FileConstants.ConfigFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            config.Save(fullPath);
            return fullPath;
        }

        #endregion Private methods

        #region Checks

        private static void CheckFilePathIsTest(string analysisDir, string fullFileName)
        {
            bool isTest = ExecuteAndCheckSuccess(analysisDir, fullFileName);
            Assert.IsTrue(isTest, "Expecting the file name to be recognised as a test file. Name: {0}", fullFileName);
        }

        private static void CheckFilePathIsNotTest(string analysisDir, string fullFileName)
        {
            bool isTest = ExecuteAndCheckSuccess(analysisDir, fullFileName);
            Assert.IsFalse(isTest, "Not expecting the file name to be recognised as a test file. Name: {0}", fullFileName);
        }

        private static bool ExecuteAndCheckSuccess(string analysisDir, string fullFileName)
        {
            DummyBuildEngine dummyEngine = new DummyBuildEngine();
            IsTestFileByName task = new IsTestFileByName();
            task.BuildEngine = dummyEngine;
            task.FullFilePath = fullFileName;
            task.AnalysisConfigDir = analysisDir;

            bool taskSucess = task.Execute();
            Assert.IsTrue(taskSucess, "Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();

            return task.IsTest;
        }

        #endregion Checks
    }
}