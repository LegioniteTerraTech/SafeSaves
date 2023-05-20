using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SafeSaves
{
    public static class DebugSafeSaves
    {
        public static bool LogAll = false;
        internal static bool ShouldLog = true;

        internal static void Info(string message)
        {
            if (!ShouldLog || !LogAll)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void Log(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(message);
        }
        internal static void Log(Exception e)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(e);
        }
        internal static void Assert(bool shouldAssert, string message)
        {
            if (!ShouldLog || !shouldAssert)
                return;
            UnityEngine.Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        private static List<KeyValuePair<ManagedDLL, string>> subExceptions = new List<KeyValuePair<ManagedDLL, string>>();
        internal static void CacheException(ManagedDLL cause, string message)
        {
            string subEx = message + "\n" + StackTraceUtility.ExtractStackTrace().ToString();
            UnityEngine.Debug.Log(subEx);
            subExceptions.Add(new KeyValuePair<ManagedDLL, string>(cause, subEx));
        }
        internal static bool GetSubExceptions(out string ex, string onAction)
        {
            if (subExceptions.Count == 0)
            {
                ex = null;
                return false;
            }
            StringBuilder SB = new StringBuilder();
            SB.Append("\n---------------------------------------------------------\n");
            SB.Append("---------------------------------------------------------\n");
            SB.Append("SafeSaves: The following mods had errors " + onAction + ":\n");
            foreach (var item in subExceptions)
            {
                SB.Append(item.Key.DLL.FullName + ", ");
            }
            SB.Append("\nTechnical Details:\n");
            foreach (var item in subExceptions)
            {
                SB.Append(item.Value + "\n");
            }
            SB.Append("\n---------------------------------------------------------\n");
            SB.Append("---------------------------------------------------------\n");
            subExceptions.Clear();
            ex = SB.ToString();
            return true;
        }
        internal static void LogError(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log(message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void FatalError()
        {
            UnityEngine.Debug.Log("SafeSaves: ENCOUNTERED CRITICAL ERROR");
        }
        internal static void FatalError(string e)
        {
            try
            {
                ManUI.inst.ShowErrorPopup("SafeSaves: ENCOUNTERED CRITICAL ERROR: " + e);
            }
            catch { }
            UnityEngine.Debug.Log("SafeSaves: ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log("SafeSaves: MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
    }
}
