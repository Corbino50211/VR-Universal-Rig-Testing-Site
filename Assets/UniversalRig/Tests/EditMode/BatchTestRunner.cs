using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UniversalRig.Tests
{
    // Editor-only helper so a headless trigger file can run Test Runner without a dialog.
    // Delete this with the test run; it does not ship (UNITY_INCLUDE_TESTS, Editor platform).
    [InitializeOnLoad]
    public static class BatchTestRunner
    {
        static string Trigger => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/run-rig-tests.txt"));
        static string Result => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/rig-test-result.txt"));
        static bool running;

        static double startedAt;

        static BatchTestRunner() { EditorApplication.update += Poll; }

        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (running && EditorApplication.timeSinceStartup - startedAt < 3d) return;
            if (!File.Exists(Trigger)) return;
            string mode;
            using (var stream = new FileStream(Trigger, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                mode = reader.ReadToEnd().Trim();
            File.Delete(Trigger);
            Run(mode);
        }

        static void Run(string mode)
        {
            running = true;
            startedAt = EditorApplication.timeSinceStartup;
            File.WriteAllText(Result, "running " + mode + "\n");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callback());
            bool play = mode != "edit";
            var filter = new Filter
            {
                testMode = play ? TestMode.PlayMode : TestMode.EditMode,
                assemblyNames = new[] { play ? "UniversalRig.PlayModeTests" : "UniversalRig.EditModeTests" }
            };
            if (mode == "guns") filter.groupNames = new[] { "^UniversalRig\\.Tests\\.FirearmPlayModeTests" };
            if (mode == "rig") filter.groupNames = new[] { "^UniversalRig\\.Tests\\.RigPlayModeTests" };
            api.Execute(new ExecutionSettings(filter));
        }

        sealed class Callback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren || result.TestStatus != TestStatus.Failed) return;
                File.AppendAllText(Result, "FAIL " + result.FullName + "\n" + result.Message + "\n" + result.StackTrace + "\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                File.AppendAllText(Result, "DONE pass=" + result.PassCount + " fail=" + result.FailCount +
                    " skip=" + result.SkipCount + "\n");
                running = false;
            }
        }
    }
}
