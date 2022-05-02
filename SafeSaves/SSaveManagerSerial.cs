using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;
using Ionic.Zlib;
using Newtonsoft.Json;

namespace SafeSaves
{
    /// <summary>
    /// Saves Manager information (public, must be static non-array)
    /// Only automatically saves fields with [AutoSave] over it.
    /// </summary>
    public class SSaveManagerSerial : SSaveSerial
    {
        /// <summary>
        /// ONLY USED FOR NEWTONSOFT
        /// </summary>
        public SSaveManagerSerial() { }

        /// <summary>
        /// Create and save it automatically
        /// </summary>
        internal SSaveManagerSerial(Type type)
        {
            if (type == null)
            {
                corrupted = true;
                DebugSafeSaves.LogError("SafeSaves: Was given a null Manager to save! " + StackTraceUtility.ExtractStackTrace());
                return;
            }
            this.type = type;
            SaveManager();
        }

        internal bool SaveManager()
        {
            try
            {
                FieldInfo[] FI = type.GetFields();
                foreach (FieldInfo item in FI)
                {
                    if (item.GetCustomAttribute(typeof(SSManagerInstAttribute)) != null)
                    {
                        object managerInst = item.GetValue(null);

                        foreach (FieldInfo saveable in FI)
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                SaveState(saveable.Name, saveable.GetValue(managerInst));
                            }
                        }
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: Could not set values - " + e);
            }
            return false;
        }
        internal bool LoadToManager()
        {
            try
            {
                if (!CanLoad())
                    return false;
                FieldInfo[] FI = type.GetFields();
                foreach (FieldInfo item in FI)
                {
                    if (item.GetCustomAttribute(typeof(SSManagerInstAttribute)) != null)
                    {
                        object managerInst = item.GetValue(null);

                        foreach (FieldInfo saveable in FI)
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                LoadState(type, saveable.Name, managerInst, out _);
                            }
                        }
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: Could not set values - " + e);
            }
            return false;
        }


    }
}
