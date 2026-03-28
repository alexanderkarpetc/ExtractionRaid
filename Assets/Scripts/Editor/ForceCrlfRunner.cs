using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Editor
{
    public static class ForceCrlfRunner
    {
        private static readonly string[] Extensions =
        {
            ".asset", ".meta", ".prefab", ".unity", ".mat", ".anim", ".controller", ".asmdef", ".cs"
        };

        [MenuItem("Tools/Fix CRLF in Changed Files")]
        public static void FixChangedFiles()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var files = GetChangedFiles(projectRoot);

            if (files.Length == 0)
            {
                Debug.LogWarning("CRLF: No changed files found.");
                EditorUtility.DisplayDialog("CRLF", "No changed files found.", "OK");
                return;
            }

            int fixedCount = 0;

            foreach (var relativePath in files)
            {
                var fullPath = Path.Combine(projectRoot, relativePath);

                if (!File.Exists(fullPath))
                    continue;

                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!Extensions.Contains(ext))
                    continue;

                if (IsBinary(fullPath))
                {
                    Debug.Log($"CRLF: Skipped binary file {relativePath}");
                    continue;
                }

                if (ConvertFileToCrlf(fullPath))
                {
                    fixedCount++;
                    Debug.Log($"CRLF fixed: {relativePath}");
                }
            }

            AssetDatabase.Refresh();

            Debug.Log($"CRLF: Done. Fixed {fixedCount} file(s).");
            EditorUtility.DisplayDialog("CRLF", $"Done. Fixed {fixedCount} file(s).", "OK");
        }

        private static string[] GetChangedFiles(string workingDirectory)
        {
            var output = RunProcess(
                "git",
                "status --porcelain=v1",
                workingDirectory);

            if (string.IsNullOrWhiteSpace(output))
                return Array.Empty<string>();

            var lines = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var result = new List<string>();

            foreach (var line in lines)
            {
                if (line.Length < 4)
                    continue;

                // Format:
                // XY path
                // ?? path
                // R  old -> new
                var pathPart = line.Substring(3).Trim();

                // For renames, take new path
                var renameIndex = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
                if (renameIndex >= 0)
                    pathPart = pathPart.Substring(renameIndex + 4).Trim();

                if (!string.IsNullOrWhiteSpace(pathPart))
                    result.Add(pathPart);
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string RunProcess(string fileName, string arguments, string workingDirectory)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stderr))
                Debug.LogWarning($"{fileName} stderr: {stderr}");

            return stdout;
        }

        private static bool ConvertFileToCrlf(string path)
        {
            var original = File.ReadAllText(path, Encoding.UTF8);
            var normalized = original.Replace("\r\n", "\n").Replace("\r", "\n");
            var converted = normalized.Replace("\n", "\r\n");

            if (original == converted)
                return false;

            File.WriteAllText(path, converted, new UTF8Encoding(false));
            return true;
        }

        private static bool IsBinary(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return bytes.Take(8000).Any(b => b == 0);
        }
    }
}