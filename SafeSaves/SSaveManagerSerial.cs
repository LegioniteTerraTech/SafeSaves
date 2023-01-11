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
        private static bool ContinueLoadingOnErrorAnyways = false;

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
                DebugSafeSaves.LogError("SafeSaves: Was given a null Manager to save!");
                return;
            }
            this.type = type;
            SaveManager();
        }

        internal bool SaveManager()
        {
            try
            {
                DebugSafeSaves.Log("SafeSaves: SaveManager() - Trying to save from " + type.Name);
                FieldInfo[] FI = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                foreach (FieldInfo item in FI)
                {
                    if (item.GetCustomAttribute(typeof(SSManagerInstAttribute)) != null)
                    {
                        DebugSafeSaves.Log("SafeSaves: Saving " + item.Name + " of " + type.ToString());
                        bool worked = true;
                        object managerInst = item.GetValue(null);

                        foreach (FieldInfo saveable in FI)
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                if (!SaveState(saveable.Name, saveable.GetValue(managerInst)))
                                {
                                    worked = false;
                                    DebugSafeSaves.Log("SafeSaves: Could not save item " + saveable.Name + " of " + type.ToString());
                                }
                            }
                        }
                        if (!worked)
                            DebugSafeSaves.Log("SafeSaves: Could not save items in " + type.ToString() + "!");
                        return true;
                    }
                }
                DebugSafeSaves.Log("SafeSaves: SaveManager() - Could not find manager for " + type.Name + " to save to!");
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: SaveManager() - Could not save values for " + type.FullName + " - " + e);
            }
            return false;
        }
        internal bool LoadToManager()
        {
            try
            {
                if (!CanLoad())
                    return false;
                DebugSafeSaves.Log("SafeSaves: LoadToManager() - Trying to load to " + type.Name);
                FieldInfo[] FI = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                foreach (FieldInfo item in FI)
                {
                    if (item.GetCustomAttribute(typeof(SSManagerInstAttribute)) != null)
                    {
                        object managerInst = item.GetValue(null);

                        bool worked = true;
                        foreach (FieldInfo saveable in FI)
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                var val = saveable.GetValue(managerInst);
                                if (!Autocast(val, saveable, managerInst))
                                {
                                    worked = false;
                                    DebugSafeSaves.Log("SafeSaves: Could not load item " + saveable.Name + "!");
                                }
                            }
                        }
                        if (!worked)
                            DebugSafeSaves.Log("SafeSaves: Could not load items in " + type.ToString() + "!");
                        if (ContinueLoadingOnErrorAnyways)
                            return true;
                        return worked;
                    }
                }
                DebugSafeSaves.Log("SafeSaves: LoadToManager() - Could not find manager for " + type.Name + " to load to!");
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: LoadToManager() - Could not set values for " + type.FullName + " - " + e);
            }
            return false;
        }


    }
}
