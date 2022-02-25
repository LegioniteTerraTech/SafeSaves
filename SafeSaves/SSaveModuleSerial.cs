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
    /// Only automatically saves fields with [AutoSave] over it.
    /// </summary>
    public class SSaveModuleSerial : SSaveSerial
    {

        internal int visID = 0;
        internal ObjectTypes objType = ObjectTypes.Null;

        /// <summary>
        /// ONLY USED FOR NEWTONSOFT
        /// </summary>
        public SSaveModuleSerial() { }

        /// <summary>
        /// Save it automatically
        /// </summary>
        public SSaveModuleSerial(Component toSave)
        {
            if (!toSave)
            {
                corrupted = true;
                Debug.LogError("SafeSaves: Was given a null Visible to save! " + StackTraceUtility.ExtractStackTrace());
                return;
            }
            SaveState
        }

        public bool LoadToModule()
        {
            try
            {
                Visible vis = ManSaveGame.inst.LookupSerializedVisible(visID);
                if (vis == null)
                    return false;
                foreach (Component item in vis.gameObject.GetComponents<Component>())
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponent)) != null)
                    {
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveField)) != null)
                            {
                                SaveState(saveable.Name, item.GetValue(managerInst));
                            }
                        }
                        return true;
                    }
                }
            }
            catch { }
            Debug.LogError("SafeSaves: Could not get encoded ID?!?");
            return false;
        }


    }
}
