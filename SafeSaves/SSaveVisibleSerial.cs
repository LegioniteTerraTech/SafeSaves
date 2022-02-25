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
    /// Saves a Component's save information 
    /// Only automatically saves fields with [AutoSave] over it.
    /// </summary>
    public class SSaveVisibleSerial : SSaveSerial
    {
        internal int visID = 0;
        internal ObjectTypes objType = ObjectTypes.Null;

        /// <summary>
        /// ONLY USED FOR NEWTONSOFT
        /// </summary>
        public SSaveVisibleSerial() { }

        /// <summary>
        /// Create and Save it automatically
        /// </summary>
        internal SSaveVisibleSerial(Visible toSave, Type Module)
        {
            if (!toSave)
            {
                corrupted = true;
                Debug.LogError("SafeSaves: Was given a null Visible to save! " + StackTraceUtility.ExtractStackTrace());
                return;
            }
            switch (toSave.type)
            {
                case ObjectTypes.Block:
                    if (!toSave.block)
                    {
                        corrupted = true;
                        Debug.LogError("SafeSaves: Was given a null Block to save! " + StackTraceUtility.ExtractStackTrace());
                        return;
                    }
                    break;
                case ObjectTypes.Vehicle:
                    if (!toSave.tank)
                    {
                        corrupted = true;
                        Debug.LogError("SafeSaves: Was given a null Vehicle to save! " + StackTraceUtility.ExtractStackTrace());
                        return;
                    }
                    break;
            }
            visID = toSave.ID;
            objType = toSave.type;
            type = Module;
            SaveModuleOnVisible();
        }

        internal bool SaveModuleOnVisible()
        {
            try
            {
                Visible vis = ManSaveGame.inst.LookupSerializedVisible(visID);
                if (vis == null)
                    return false;
                foreach (var item in vis.gameObject.GetComponents(type.GetType()))
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponentAttribute)) != null)
                    {
                        bool worked = true;
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                if (SaveState(saveable.Name, saveable.GetValue(item)))
                                    worked = false;
                            }
                        }
                        return worked;
                    }
                }
            }
            catch { }
            Debug.LogError("SafeSaves: Could not get encoded ID?!?");
            return false;
        }
        internal bool LoadModuleOnVisible()
        {
            try
            {
                Visible vis = ManSaveGame.inst.LookupSerializedVisible(visID);
                if (vis == null)
                    return false;
                foreach (var item in vis.gameObject.GetComponents(type.GetType()))
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponentAttribute)) != null)
                    {
                        bool worked = true;
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                if (LoadState(saveable.Name, item))
                                    worked = false;
                            }
                        }
                        return worked;
                    }
                }
            }
            catch { }
            Debug.LogError("SafeSaves: Could not set encoded ID?!?");
            return false;
        }


    }
}
