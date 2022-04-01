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
    [Serializable]
    public class SSaveVisibleSerial : SSaveSerial
    {
        public int visID = 0;       // Host Tank
        public int blockOrder = 0;  // position of the block in save files
        public ObjectTypes objType = ObjectTypes.Null;

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
                    if (!toSave.trans.root.GetComponent<Tank>())
                    {
                        corrupted = true;
                        Debug.LogError("SafeSaves: Was given a Block not attached to any Tech on save! \n This DOES NOT WORK because unattached block IDs do not save or load with the save!!! \n" + StackTraceUtility.ExtractStackTrace());
                        return;
                    }
                    visID = toSave.transform.root.GetComponent<Visible>().ID;
                    blockOrder = 0;
                    bool found = false;
                    foreach (TankBlock TB in toSave.trans.root.GetComponent<Tank>().blockman.IterateBlocks())
                    {
                        if (toSave.block == TB)
                        {
                            found = true;
                            break;
                        }
                        blockOrder++;
                    }
                    if (!found)
                    {
                        corrupted = true;
                        Debug.LogError("SafeSaves: Tried to save a block not linked properly to the Tech \n" + StackTraceUtility.ExtractStackTrace());
                        return;
                    }
                    objType = toSave.type;
                    type = Module;
                    Debug.Log("SafeSaves: Saved a Block with Tank ID " + visID + ", index " + blockOrder);
                    break;
                case ObjectTypes.Vehicle:
                    if (!toSave.trans.root.GetComponent<Tank>())
                    {
                        corrupted = true;
                        Debug.LogError("SafeSaves: Was given a null Vehicle to save! " + StackTraceUtility.ExtractStackTrace());
                        return;
                    }
                    visID = toSave.ID;
                    blockOrder = -1;
                    objType = toSave.type;
                    type = Module;
                    Debug.Log("SafeSaves: Saved a Vehicle with ID " + visID);
                    break;
                default:
                    throw new ArgumentException("SafeSaves.SSaveVisibleSerial - Was given an illegal Visible type of " + toSave.type + " which is not supported. ");
            }
            corrupted = !SaveModuleOnVisible(toSave);
        }

        private static bool ContinueLoadingAnyways = true;
        internal bool SaveModuleOnVisible(Visible currentInst)
        {
            try
            {
                if (currentInst == null)
                {
                    Debug.Log("SafeSaves: Could not find the SerializedVisible " + visID + " for the component " + type.ToString() + "!");
                    //return false;
                }
                foreach (var item in currentInst.gameObject.GetComponents(type))
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponentAttribute)) != null)
                    {
                        bool worked = true;
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                if (!SaveState(saveable.Name, saveable.GetValue(item)))
                                {
                                    worked = false;
                                    Debug.Log("SafeSaves: Could not save item " + saveable.Name + " of " + type.ToString());
                                }
                                //else
                                //    Debug.Log("SafeSaves: Saved item " + saveable.Name + " of " + type.ToString() + " in state " + saveable.GetValue(item));
                            }
                        }
                        if (!worked)
                            Debug.Log("SafeSaves: Could not save items in " + type.ToString() + "!");
                        return worked;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Crash on operation " + e);
            }
            Debug.LogError("SafeSaves: Could not get encoded ID?!?");
            return false;
        }
        internal bool SaveModuleFieldOnVisible<T>(Visible currentInst, T field)
        {
            try
            {
                if (currentInst == null)
                {
                    Debug.Log("SafeSaves: Could not find the SerializedVisible " + visID + " for the component " + type.ToString() + "!");
                    //return false;
                }
                foreach (var item in currentInst.gameObject.GetComponents(type))
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponentAttribute)) != null)
                    {
                        bool worked = true;
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.FieldType == typeof(T))
                            {
                                if (!SaveState(saveable.Name, field))
                                {
                                    worked = false;
                                    Debug.Log("SafeSaves: Could not save item " + saveable.Name + " of " + type.ToString());
                                }
                                //else
                                //    Debug.Log("SafeSaves: Saved item " + saveable.Name + " of " + type.ToString() + " in state " + saveable.GetValue(item));
                                return true;
                            }
                        }
                        Debug.Log("SafeSaves: Could not save " + typeof(T).ToString() + " in " + type.ToString() + "!");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Crash on operation " + e);
            }
            Debug.LogError("SafeSaves: Could not get encoded ID?!?");
            return false;
        }
        internal bool LoadModuleOnVisible(Visible currentInst)
        {
            try
            {
                if (currentInst == null)
                {
                    Debug.Log("SafeSaves: Could not find the SerializedVisible " + visID + " for the component " + type.ToString() + "!");
                    //return false;
                }
                if (!CanLoad())
                    return false;

                foreach (var item in currentInst.gameObject.GetComponents(type))
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponentAttribute)) != null)
                    {
                        bool worked = true;
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.GetCustomAttribute(typeof(SSaveFieldAttribute)) != null)
                            {
                                var val = saveable.GetValue(item);
                                if (!Autocast(val, saveable, item))
                                    worked = false;
                            }
                        }
                        if (!worked)
                            Debug.Log("SafeSaves: Could not load items in " + type.ToString() + "!");
                        if (ContinueLoadingAnyways)
                            return true;
                        return worked;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Crash on operation " + e);
            }
            Debug.LogError("SafeSaves: Could not set encoded ID?!?");
            return false;
        }
        internal bool LoadModuleFieldOnVisible<T>(Visible currentInst, ref T field)
        {
            try
            {
                if (currentInst == null)
                {
                    Debug.Log("SafeSaves: Could not find the SerializedVisible " + visID + " for the component " + type.ToString() + "!");
                    //return false;
                }
                if (!CanLoad())
                    return false;

                foreach (var item in currentInst.gameObject.GetComponents(type))
                {
                    if (item.GetType().GetCustomAttribute(typeof(AutoSaveComponentAttribute)) != null)
                    {
                        bool worked = true;
                        foreach (FieldInfo saveable in item.GetType().GetFields())
                        {
                            if (saveable.FieldType == typeof(T))
                            {
                                if (!LoadState(saveable.Name, ref field))
                                    worked = false;
                            }
                        }
                        if (!worked)
                            Debug.Log("SafeSaves: Could not load items in " + type.ToString() + "!");
                        if (ContinueLoadingAnyways)
                            return true;
                        return worked;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Crash on operation " + e);
            }
            Debug.LogError("SafeSaves: Could not set encoded ID?!?");
            return false;
        }
      
        internal bool Autocast<T,C>(T val, FieldInfo saveable, C baseInst)
        {
            var valIn = (object)val;
            if (!LoadState<T>(saveable.Name, valIn, out object valOut))
            {
                Debug.Log("SafeSaves: Autocast - Could not load item " + saveable.Name + " of " + type.ToString());
            }
            else
            {
                if (valOut == null)
                {
                    //Debug.Log("SafeSaves: Autocast - Loaded item " + saveable.Name + " of " + type.ToString() + " to the Component instance");
                }
                else
                {
                    try
                    {
                        try
                        {
                            try
                            {
                                try
                                {
                                    saveable.SetValue(baseInst, JsonConvert.DeserializeObject(valOut.ToString(), saveable.FieldType));
                                    //Debug.Log("SafeSaves: Autocast - Loaded item " + saveable.Name + " of " + type.ToString() + " which is " + (T)valOut);
                                    return true;
                                }
                                catch
                                {
                                    saveable.SetValue(baseInst, Convert.ChangeType(valOut, saveable.FieldType));
                                    //Debug.Log("SafeSaves: Autocast - Loaded item " + saveable.Name + " of " + type.ToString() + " which is " + (T)valOut);
                                    return true;
                                }
                            }
                            catch
                            {
                                saveable.SetValue(baseInst, Convert.ChangeType(Convert.ToInt64(valOut), saveable.FieldType));
                                //Debug.Log("SafeSaves: Autocast - Loaded item " + saveable.Name + " of " + type.ToString() + " which is " + (T)valOut);
                                return true;
                            }
                        }
                        catch
                        {
                            saveable.SetValue(baseInst, Enum.Parse(saveable.FieldType, Convert.ToInt64(valOut).ToString()));
                            //Debug.Log("SafeSaves: Autocast - Loaded item " + saveable.Name + " of " + type.ToString() + " which is " + (T)valOut);
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("SafeSaves: Autocast - Failed trying to deal with item " + saveable.Name + " of " + type.ToString() + " which is " + saveable.FieldType.ToString() + ", valOut: " + (T)valOut);
                        Debug.LogError("SafeSaves: Autocast - Error on operation in " + saveable.Name + " of " + type.ToString() + " | " + e);
                    }
                }
            }
            return false;
        }

    }
}
