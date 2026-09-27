using System.IO;
using System.Text;
using UnityEngine;

namespace Tobii.Research.Unity
{
    /// <summary>Single authority for writable study output locations.</summary>
    public static class StudyDataPaths
    {
        public static string RootDirectory
        {
            get { return EnsureDirectory(Path.Combine(Application.persistentDataPath, "StudyData")); }
        }

        public static string SessionDirectory
        {
            get
            {
                string sessionName = string.IsNullOrEmpty(GameManager.identifierName)
                    ? "_Unassigned"
                    : SanitizePathSegment(GameManager.identifierName.TrimEnd('_'));
                return EnsureDirectory(Path.Combine(RootDirectory, sessionName));
            }
        }

        public static string EyeTrackingDirectory
        {
            get { return EnsureDirectory(Path.Combine(SessionDirectory, "EyeTracking")); }
        }

        public static string GetSessionFile(string suffix)
        {
            string prefix = string.IsNullOrEmpty(GameManager.identifierName)
                ? string.Empty
                : SanitizePathSegment(GameManager.identifierName);
            return Path.Combine(SessionDirectory, prefix + suffix);
        }

        public static string GetEyeTrackingFile(string fileName)
        {
            return Path.Combine(EyeTrackingDirectory, SanitizePathSegment(fileName));
        }

        private static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrEmpty(value)) return "_Unassigned";
            char[] invalid = Path.GetInvalidFileNameChars();
            var result = new StringBuilder(value.Length);
            foreach (char character in value)
                result.Append(System.Array.IndexOf(invalid, character) >= 0 ? '_' : character);
            return result.ToString();
        }
    }
}
