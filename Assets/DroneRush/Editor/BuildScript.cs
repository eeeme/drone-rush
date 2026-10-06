using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneRush.EditorTools
{
    /// <summary>
    /// ビルド用スクリプト。GitHub Actions（GameCI）からも、エディタのメニューからも呼べる。
    ///   メニュー: Drone Rush > Build WebGL / Build Android (AAB)
    /// アプリ名・パッケージ名・画面の向きなどの設定もここでまとめて適用する。
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/DroneRush/Scenes/Main.unity";
        const string CompanyName = "ME IS ME";
        const string ProductName = "Drone Rush";
        const string PackageName = "com.meisme.dronerush";
        const string DefaultVersion = "0.1.0";

        [MenuItem("Drone Rush/Build WebGL")]
        public static void BuildWebGL()
        {
            ApplyCommonSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // GitHub Pages は Brotli 配信に非対応のため
            PlayerSettings.WebGL.dataCaching = true;
            Build(BuildTarget.WebGL, Arg("customBuildPath") ?? "build/WebGL/drone-rush");
        }

        [MenuItem("Drone Rush/Build Android (AAB)")]
        public static void BuildAndroid()
        {
            ApplyCommonSettings();

            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            EditorUserBuildSettings.buildAppBundle = true;

            string code = Arg("androidVersionCode");
            if (int.TryParse(code, out int versionCode) && versionCode > 0)
                PlayerSettings.Android.bundleVersionCode = versionCode;

            // 署名（GameCI がキーストアをプロジェクト直下に展開し、引数で名前とパスワードを渡す）
            string keystore = Arg("androidKeystoreName");
            if (!string.IsNullOrEmpty(keystore) && File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = Arg("androidKeystorePass");
                PlayerSettings.Android.keyaliasName = Arg("androidKeyaliasName");
                PlayerSettings.Android.keyaliasPass = Arg("androidKeyaliasPass");
            }
            else
            {
                Debug.LogWarning("[DroneRush] キーストア未設定のためデバッグ署名でビルドします（Google Play には提出できません）");
            }

            string path = Arg("customBuildPath") ?? "build/Android/drone-rush.aab";
            if (!path.EndsWith(".aab", StringComparison.OrdinalIgnoreCase)) path += ".aab";
            Build(BuildTarget.Android, path);
        }

        static void ApplyCommonSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageName);
            PlayerSettings.bundleVersion = Arg("buildVersion") is string v && v.Length > 0 && char.IsDigit(v[0]) ? v : DefaultVersion;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            EnsureScene();
        }

        /// <summary>シーンが無ければ作る（中身は GameManager が実行時に自動生成するので空でよい）</summary>
        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void Build(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[DroneRush] Build {s.result}: {path} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors)");

            if (Application.isBatchMode)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>コマンドライン引数 -name value を読む</summary>
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-" + name) return args[i + 1];
            return null;
        }
    }
}
