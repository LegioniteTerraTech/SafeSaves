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

        // Techs

        /// <summary>
        /// Serialises the tank.  Invoke BEFORE the tank is removed from the world!
        /// </summary>
        /// <param name="tank">Tech to fetch the serial of</param>
        /// <returns>The serialization</returns>
        internal string GetSaveStateTank(Tank tank)
        {
            if (tank == null)
                return null;
            List<SSaveVisibleSerial> strings = new List<SSaveVisibleSerial>();
            int searchID = tank.visible.ID;
            if (AllVisibleEntries.TryGetValue(searchID, out List<SSaveVisibleSerial> list))
            {
                foreach (SSaveVisibleSerial SSVS in list)
                {
                    strings.Add(SSVS);
                }
            }
            else
            {
                Debug.Log("SafeSaves: SafeSave - GetSaveStateTank: There's no saved entry for " + searchID + ".");
            }
            return JsonConvert.SerializeObject(strings);
        }

        /// <summary>
        /// Serializes the tank.  Invoke BEFORE the tank is removed from the world!
        /// </summary>
        /// <param name="tank"></param>
        internal void LoadStateTankExternal(Tank tank, string serial)
        {
            if (tank == null)
                return;
            List<SSaveVisibleSerial> SSVSS;
            int searchID = tank.visible.ID;
            if (serial != null)
            {
                SSVSS = JsonConvert.DeserializeObject<List<SSaveVisibleSerial>>(serial);
                if (SSVSS == null)
                {
                    SSVSS = new List<SSaveVisibleSerial>();
                }
            }
            else
            {
                AllVisibleEntries.Remove(searchID);
                return; // No need to save Techs that are removed
            }

            if (AllVisibleEntries.TryGetValue(searchID, out _))
            {
                AllVisibleEntries.Remove(searchID);
            }
            AllVisibleEntries.Add(searchID, SSVSS);
            foreach (SSaveVisibleSerial SSVS in SSVSS)
            {
                SSVS.LoadModuleOnVisible(tank.visible);
            }
        }


        // Blocks
        /// <summary>
        /// Blocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vis"></param>
        /// <param name="Component"></param>
        /// <returns></returns>
        internal bool SaveModuleState<T>(Visible vis, T Component)
        {
            if (!vis)
                return false;
            SSaveVisibleSerial SSVS = null;
            int searchID = vis.ID;
            int searchPos = -1;
            bool isBlock = true;
            if (isBlock)
            {
                if (vis.trans.root.GetComponent<Tank>() == null)
                    return false; //INVALID
                searchID = vis.trans.root.GetComponent<Tank>().visible.ID;
                // GETS the block at position
                int pos = 0;
                foreach (var TB in vis.trans.root.GetComponent<Tank>().blockman.IterateBlocks())
                {
                    if (TB.visible == vis)
                        break;
                    pos++;
                }
                searchPos = pos;
            }
            if (AllVisibleEntries.TryGetValue(searchID, out List<SSaveVisibleSerial> list))
            {
                if (isBlock)
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    { return cand.blockOrder == searchPos && cand.type == typeof(T); });
                }
                else
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    {
                        return cand.type == typeof(T);
                    });
                }
                if (SSVS != null)
                {
                    if (!SSVS.SaveModuleOnVisible(vis))
                    {
                        Debug.LogError("SafeSaves: SafeSave - SaveModuleState: Could NOT save to present in " + searchID);
                        return false;
                    }
                    Debug.Info("SafeSaves: SafeSave - SaveModuleState: Saved to present in " + searchID);
                }
                else
                {
                    SSVS = new SSaveVisibleSerial(vis, typeof(T));
                    if (SSVS != null)
                    {
                        list.Add(SSVS);
                        Debug.Info("SafeSaves: SafeSave - SaveModuleState: Saved " + list.Count + " in " + searchID);
                    }
                    else
                        Debug.LogError("SafeSaves: SafeSave - SaveModuleState: Tried to save " + list.Count + " in " + searchID + " but the GC GOT IT FIRST");
                }
            }
            else
            {
                SSVS = new SSaveVisibleSerial(vis, typeof(T));
                if (SSVS != null)
                {
                    AllVisibleEntries.Add(searchID, new List<SSaveVisibleSerial> { SSVS });
                    Debug.Info("SafeSaves: SafeSave - SaveModuleState: Saved in " + searchID);
                }
                else
                    Debug.LogError("SafeSaves: SafeSave - SaveModuleState(NewList): Tried to save " + list.Count + " in " + searchID + " but the GC GOT IT FIRST");
            }
            if (SSVS == null)
            {
                Debug.LogError("SafeSaves: SafeSave - SaveModuleState(END): Tried to save " + list.Count + " in " + searchID + " but the GC GOT IT FIRST");
                return false;
            }
            if (SSVS.corrupted)
                Debug.LogError("SafeSaves: SafeSave - SaveModuleState(END): Tried to save " + list.Count + " in " + searchID + " but it got corrupted down the line!?");
            return !SSVS.corrupted;
        }
        /// <summary>
        /// Blocks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <param name="vis"></param>
        /// <param name="Component"></param>
        /// <param name="Field"></param>
        /// <returns></returns>
        internal bool SaveModuleStateField<T,C>(Visible vis, T Component, C Field)
        {
            if (!vis)
                return false;
            SSaveVisibleSerial SSVS = null;
            int searchID = vis.ID;
            int searchPos = -1;
            bool isBlock = true;
            if (isBlock)
            {
                if (vis.trans.root.GetComponent<Tank>() == null)
                    return false; //INVALID
                searchID = vis.trans.root.GetComponent<Tank>().visible.ID;

                // GETS the block at position
                int pos = 0;
                foreach (var TB in vis.trans.root.GetComponent<Tank>().blockman.IterateBlocks())
                {
                    if (TB.visible == vis)
                        break;
                    pos++;
                }
                searchPos = pos;
            }
            if (AllVisibleEntries.TryGetValue(searchID, out List<SSaveVisibleSerial> list))
            {
                if (isBlock)
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    { return cand.blockOrder == searchPos && cand.type == typeof(T); });
                }
                else
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    {
                        return cand.type == typeof(T);
                    });
                }
                if (SSVS != null)
                {
                    if (!SSVS.SaveModuleFieldOnVisible(vis, Field))
                    {
                        Debug.LogError("SafeSaves: SafeSave - SaveModuleState: Could NOT save to present in " + searchID);
                        return false;
                    }
                    Debug.Info("SafeSaves: SafeSave - SaveModuleState: Saved to present in " + searchID);
                }
                else
                {
                    SSVS = new SSaveVisibleSerial(vis, typeof(T));
                    if (SSVS != null)
                    {
                        list.Add(SSVS);
                        Debug.Info("SafeSaves: SafeSave - SaveModuleState: Saved " + list.Count + " in " + searchID);
                    }
                    else
                        Debug.LogError("SafeSaves: SafeSave - SaveModuleState: Tried to save " + list.Count + " in " + searchID + " but the GC GOT IT FIRST");
                }
            }
            else
            {
                SSVS = new SSaveVisibleSerial(vis, typeof(T));
                if (SSVS != null)
                {
                    AllVisibleEntries.Add(searchID, new List<SSaveVisibleSerial> { SSVS });
                    Debug.Info("SafeSaves: SafeSave - SaveModuleState: Saved in " + searchID);
                }
                else
                    Debug.LogError("SafeSaves: SafeSave - SaveModuleState(NewList): Tried to save " + list.Count + " in " + searchID + " but the GC GOT IT FIRST");
            }
            if (SSVS == null)
            {
                Debug.LogError("SafeSaves: SafeSave - SaveModuleState(END): Tried to save " + list.Count + " in " + searchID + " but the GC GOT IT FIRST");
                return false;
            }
            if (SSVS.corrupted)
                Debug.LogError("SafeSaves: SafeSave - SaveModuleState(END): Tried to save " + list.Count + " in " + searchID + " but it got corrupted down the line!?");
            return !SSVS.corrupted;
        }
        internal bool LoadModuleState<T>(Visible vis, T Component)
        {
            if (!vis)
                return false;
            SSaveVisibleSerial SSVS = null;
            int searchID = vis.ID;
            int searchPos = -1;
            bool isBlock = true;
            if (isBlock)
            {
                if (vis.trans.root.GetComponent<Tank>() == null)
                    return false; //INVALID
                searchID = vis.trans.root.GetComponent<Tank>().visible.ID;
                // GETS the block at position
                int pos = 0;
                foreach (var TB in vis.trans.root.GetComponent<Tank>().blockman.IterateBlocks())
                {
                    if (TB.visible == vis)
                        break;
                    pos++;
                }
                searchPos = pos;
            }
            if (AllVisibleEntries.TryGetValue(searchID, out List<SSaveVisibleSerial> list))
            {
                if (isBlock)
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    { return cand.blockOrder == searchPos && cand.type == typeof(T); });
                }
                else
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    { 
                        return cand.type == typeof(T); 
                    });
                }

                if (SSVS != null)
                {
                    if (!SSVS.LoadModuleOnVisible(vis))
                    {
                        Debug.LogError("SafeSaves: SafeSave - LoadModuleState: Could NOT load to present in " + searchID);
                        return false;
                    }
                    Debug.Info("SafeSaves: SafeSave - LoadModuleState: Loaded to present in " + searchID);
                    return true;
                }
                else
                {
                    Debug.Info("SafeSaves: SafeSave - LoadModuleState: There's no saved entry for " + searchID + " of component " + typeof(T).ToString() + ".");
                    //SSVS = new SSaveVisibleSerial(vis, typeof(T));
                    //if (SSVS != null)
                    //    list.Add(SSVS);
                }
            }
            else
            {
                Debug.Info("SafeSaves: SafeSave - LoadModuleState: There's no saved entry for " + searchID + ".");
                //SSVS = new SSaveVisibleSerial(vis, typeof(T));
                //if (SSVS != null)
                //    AllVisibleEntries.Add(searchID, new List<SSaveVisibleSerial> { SSVS });
            }
            //if (SSVS == null)
            //    return false;
            return true;
        }
        internal bool LoadModuleStateField<T,C>(Visible vis, T Component, ref C Field)
        {
            if (!vis)
                return false;
            SSaveVisibleSerial SSVS = null;
            int searchID = vis.ID;
            int searchPos = -1;
            bool isBlock = true;
            if (isBlock)
            {
                if (vis.trans.root.GetComponent<Tank>() == null)
                    return false; //INVALID
                searchID = vis.trans.root.GetComponent<Tank>().visible.ID;
                // GETS the block at position
                int pos = 0;
                foreach (var TB in vis.trans.root.GetComponent<Tank>().blockman.IterateBlocks())
                {
                    if (TB.visible == vis)
                        break;
                    pos++;
                }
                searchPos = pos;
            }
            if (AllVisibleEntries.TryGetValue(searchID, out List<SSaveVisibleSerial> list))
            {
                if (isBlock)
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    { return cand.blockOrder == searchPos && cand.type == typeof(T); });
                }
                else
                {
                    SSVS = list.Find(delegate (SSaveVisibleSerial cand)
                    {
                        return cand.type == typeof(T);
                    });
                }

                if (SSVS != null)
                {
                    if (!SSVS.LoadModuleFieldOnVisible(vis, ref Field))
                    {
                        Debug.LogError("SafeSaves: SafeSave - LoadModuleState: Could NOT load to present in " + searchID);
                        return false;
                    }
                    Debug.Info("SafeSaves: SafeSave - LoadModuleState: Loaded to present in " + searchID);
                    return true;
                }
                else
                {
                    Debug.Info("SafeSaves: SafeSave - LoadModuleState: There's no saved entry for " + searchID + " of component " + typeof(T).ToString() + ".");
                    //SSVS = new SSaveVisibleSerial(vis, typeof(T));
                    //if (SSVS != null)
                    //    list.Add(SSVS);
                }
            }
            else
            {
                Debug.Info("SafeSaves: SafeSave - LoadModuleState: There's no saved entry for " + searchID + ".");
                //SSVS = new SSaveVisibleSerial(vis, typeof(T));
                //if (SSVS != null)
                //    AllVisibleEntries.Add(searchID, new List<SSaveVisibleSerial> { SSVS });
            }
            //if (SSVS == null)
            //    return false;
            return true;
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

    }
}
