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
    /// Stores general information for various blocks, Techs, even the world.
    /// </summary>
    public class SafeSave
    {
        public List<SSaveManagerSerial> AllManagerEntries = new List<SSaveManagerSerial>();
        public Dictionary<int, List<SSaveVisibleSerial>> AllVisibleEntries = new Dictionary<int, List<SSaveVisibleSerial>>();
        

        internal void SaveStateALL()
        {
            SaveManagers();
            //SaveModules();
        }
        internal void LoadStateALL()
        {
            LoadManagers();
            //LoadModules();
        }

        internal bool SaveModuleState<T>(Visible vis, T Component)
        {
            if (!vis)
                return false;
            SSaveVisibleSerial SSVS = null;
            if (AllVisibleEntries.TryGetValue(vis.ID, out List<SSaveVisibleSerial> list))
            {
                SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                { return cand.type == Component.GetType(); });
                if (SSVS != null)
                {
                    SSVS.SaveModuleOnVisible();
                }
                else
                {
                    SSVS = new SSaveVisibleSerial(vis, Component.GetType());
                    if (SSVS != null)
                        list.Add(SSVS);
                }
            }
            else
            {
                SSVS = new SSaveVisibleSerial(vis, Component.GetType());
                if (SSVS != null)
                    AllVisibleEntries.Add(vis.ID, new List<SSaveVisibleSerial> { SSVS });
            }
            if (SSVS == null)
                return false;
            return !SSVS.corrupted;
        }
        internal bool SetModuleStateFromSave<T>(Visible vis, T Component)
        {
            if (!vis)
                return false;
            SSaveVisibleSerial SSVS = null;
            if (AllVisibleEntries.TryGetValue(vis.ID, out List<SSaveVisibleSerial> list))
            {
                SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                { return cand.type == Component.GetType(); });
                if (SSVS != null)
                {
                    SSVS.LoadModuleOnVisible();
                    return true;
                }
                else
                {
                    SSVS = new SSaveVisibleSerial(vis, Component.GetType());
                    if (SSVS != null)
                        list.Add(SSVS);
                }
            }
            else
            {
                SSVS = new SSaveVisibleSerial(vis, Component.GetType());
                if (SSVS != null)
                    AllVisibleEntries.Add(vis.ID, new List<SSaveVisibleSerial> { SSVS });
            }
            if (SSVS == null)
                return false;
            return false;
        }

        internal void SaveManagers()
        {
            AllManagerEntries.Clear();
            foreach (var type in ManSafeSaves.RegisteredManagers)
            {
                foreach (var field in type.GetFields())
                {
                    if (field.GetCustomAttributes(true).Contains(typeof(SSManagerInstAttribute)))
                    {   // Save the manager
                        AllManagerEntries.Add(new SSaveManagerSerial(type));
                    }
                }
            }
        }
        internal void SaveModules()
        {
            foreach (var type in ManSafeSaves.RegisteredModules)
            {
                //ManSaveGame.inst.LookupSerializedVisible(type.);
                foreach (var field in type.GetFields())
                {
                    if (field.GetCustomAttributes(true).Contains(typeof(SSaveFieldAttribute)))
                    {   // Save the Modules

                    }
                }
            }
        }

        internal void LoadManagers()
        {
            foreach (var saveMan in AllManagerEntries)
            {
                try
                {
                    saveMan.LoadToManager();
                }
                catch { }
            }
        }
        internal void LoadModules()
        {
            foreach (var saveModule in AllVisibleEntries)
            {
                try
                {
                    foreach (var saveModuleCase in saveModule.Value)
                    {
                        try
                        {
                            SSaveVisibleSerial SSVS = saveModuleCase;
                            if (!SSVS.corrupted)
                            {
                                SSVS.LoadModuleOnVisible();
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

    }
}
