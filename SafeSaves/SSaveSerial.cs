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
    /// Is not a usable class. Only derive from this.
    /// </summary>
    public class SSaveSerial
    {
        private static JsonSerializerSettings JSONSaver = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            MaxDepth = 10,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        internal bool corrupted = false;
        internal Type type;
        /// <summary>
        /// Values can overlap with hashcodes but it's extermely unlikely
        /// </summary>
        protected Dictionary<int, string> serialized;

        protected int GetEncodedID(string saveFieldID)
        {
            try
            {
                if (saveFieldID.NullOrEmpty())
                    saveFieldID = "UNSET_ERROR";
                return (Assembly.GetCallingAssembly().GetName() + "|" + saveFieldID).GetHashCode();
            }
            catch { }
            Debug.LogError("SafeSaves: Could not get encoded ID?!?");
            return 0;
        }

        /// <summary>
        /// Saves the state of a Object to an external save.
        /// Note: BlockSerial will return false if it cannot save.
        /// </summary>
        /// <typeparam name="T">Type of Object to save</typeparam>
        /// <param name="saveFieldID">The name/ID to save this as</param>
        /// <param name="objToSave">The object/value to save</param>
        /// <returns>true if the operation completed successfully</returns>
        protected bool SaveState<T>(string saveFieldID, T objToSave)
        {
            try
            {
                if (corrupted)
                    return false;
                return SaveStateInternal(GetEncodedID(saveFieldID), objToSave, out _);
            }
            catch { }
            return false;
        }
        /// <summary>
        /// Saves the state of a Object to an external save.
        /// Note: BlockSerial will out a string of NULL if it cannot save.
        /// </summary>
        /// <typeparam name="T">Type of Object to save</typeparam>
        /// <param name="saveFieldID">The name/ID to save this as</param>
        /// <param name="objToSave">The object/value to save</param>
        /// <returns>true if the operation completed successfully</returns>
        protected bool SaveState<T>(string saveFieldID, T objToSave, out string serial)
        {
            serial = null;
            try
            {
                if (corrupted)
                    return false;
                return SaveStateInternal(GetEncodedID(saveFieldID), objToSave, out serial);
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Loads the state of a Object from an external save.
        /// Note: BlockSerial will return false if it cannot load the data.
        /// </summary>
        /// <typeparam name="T">Type of Object to load</typeparam>
        /// <param name="saveFieldID">The name/ID to save this as</param>
        /// <param name="objToApplyTo">The object to load from the save</param>
        /// <returns>true if the operation completed successfully</returns>
        protected bool LoadState<T>(string saveFieldID, ref T objToApplyTo)
        {
            try
            {
                if (corrupted)
                    return false;
                if (serialized.TryGetValue(GetEncodedID(saveFieldID), out string serial))
                {
                    if (objToApplyTo is MonoBehaviour)
                    {   // uh-oh, we can't directly touch this
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(serial);
                        if (data != null)
                        {
                            return TryLoad<T>(ref objToApplyTo, data);
                        }
                        else
                            return false;
                    }
                    else
                        objToApplyTo = JsonConvert.DeserializeObject<T>(serial, JSONSaver);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Loads the state of a Object from an external save.
        /// Note: BlockSerial will return false if it cannot load the data.
        /// </summary>
        /// <typeparam name="T">Type of Object to load</typeparam>
        /// <param name="saveFieldID">The name/ID to save this as</param>
        /// <param name="objToApplyTo">The object to load from the save</param>
        /// <returns>true if the operation completed successfully</returns>
        protected bool LoadState(Type type, string saveFieldID, ref object objToApplyTo)
        {
            try
            {
                if (corrupted)
                    return false;
                if (serialized.TryGetValue(GetEncodedID(saveFieldID), out string serial))
                {
                    if (objToApplyTo is MonoBehaviour)
                    {   // uh-oh, we can't directly touch this
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(serial);
                        if (data != null)
                        {
                            return TryLoad(type, ref objToApplyTo, data);
                        }
                        else
                            return false;
                    }
                    else
                        objToApplyTo = Convert.ChangeType(JsonConvert.DeserializeObject(serial, JSONSaver), type);
                    return true;
                }
            }
            catch { }
            return false;
        }


        /// <summary>
        /// Loads the state of a Object from an external save.
        /// USE THIS ONE FOR NON-CLONING OBJECTS ONLY
        /// Note: BlockSerial will return false if it cannot load the data.
        /// </summary>
        /// <typeparam name="T">Type of Object to load</typeparam>
        /// <param name="saveFieldID">The name/ID to save this as</param>
        /// <param name="objToApplyTo">The object to load from the save</param>
        /// <returns>true if the operation completed successfully</returns>
        protected bool LoadState<T>(string saveFieldID, T objToApplyTo)
        {
            try
            {
                if (corrupted)
                    return false;
                if (serialized.TryGetValue(GetEncodedID(saveFieldID), out string serial))
                {
                    if (objToApplyTo is MonoBehaviour)
                    {   // uh-oh, we can't directly touch this
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(serial);
                        if (data != null)
                        {
                            return TryLoad<T>(ref objToApplyTo, data);
                        }
                        else
                            return false;
                    }
                    else
                        objToApplyTo = JsonConvert.DeserializeObject<T>(serial, JSONSaver);
                    return true;
                }
            }
            catch { }
            return false;
        }


        /// <summary>
        /// Saves the state of a Object to this instance
        /// </summary>
        protected bool SaveStateInternal<T>(int ID, T objToSave, out string serial)
        {
            serial = null;
            try
            {
                if (objToSave is MonoBehaviour)
                {   // uh-oh, we can't directly touch this
                    serial = JsonConvert.SerializeObject(MakeCompat(objToSave), JSONSaver);
                }
                else
                    serial = JsonConvert.SerializeObject(objToSave, JSONSaver);
                serialized.Add(ID, serial);
                return true;
            }
            catch { }
            return false;
        }

        private static Dictionary<string, object> MakeCompat<T>(T convert)
        {
            List<PropertyInfo> PI = convert.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
            Debug.Log("SafeSaves: MakeCompat - Compiling " + convert.GetType() + " which has " + PI.Count() + " properties");
            Dictionary<string, object> converted = new Dictionary<string, object>();
            foreach (PropertyInfo PIC in PI)
            {
                //if (FI.IsPublic)
                converted.Add(PIC.Name, PIC.GetValue(convert));
            }
            return converted;
        }

        private static bool TryLoad<T>(ref T toLoadFor, Dictionary<string, object> saveState)
        {
            try
            {
                if (!(toLoadFor is MonoBehaviour MB))
                    return false;
                foreach (KeyValuePair<string, object> entry in saveState)
                {
                    try
                    {
                        Type type = Type.GetType(entry.Key);
                        if (type == null)
                            continue;
                        if (!type.IsClass)
                            continue;
                        var comp = MB.GetComponent(type.GetType());
                        if (!comp)
                            comp = MB.gameObject.AddComponent(type.GetType());

                        foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)entry.Value)
                        {
                            PropertyInfo PI = type.GetType().GetProperty(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (PI != null)
                            {
                                PI.SetValue(comp, pair.Value);
                            }
                        }
                    }
                    catch
                    {   // report missing component 
                        Debug.LogError("SafeSaves: Error in " + toLoadFor.GetType() + " save format.\n No such variable exists: " + (entry.Key.NullOrEmpty() ? entry.Key : "ENTRY IS NULL OR EMPTY"));
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Error in " + toLoadFor.GetType() + " save format.\n GameObject case: " + e);
            }
            return false;
        }

        private static bool TryLoad(Type type, ref object toLoadFor, Dictionary<string, object> saveState)
        {
            try
            {
                if (!(toLoadFor is MonoBehaviour MB))
                    return false;
                foreach (KeyValuePair<string, object> entry in saveState)
                {
                    try
                    {
                        Type type2 = Type.GetType(entry.Key);
                        if (type2 == null)
                            continue;
                        if (!type2.IsClass)
                            continue;
                        var comp = MB.GetComponent(type2.GetType());
                        if (!comp)
                            comp = MB.gameObject.AddComponent(type2.GetType());

                        foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)entry.Value)
                        {
                            PropertyInfo PI = type2.GetType().GetProperty(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (PI != null)
                            {
                                PI.SetValue(comp, pair.Value);
                            }
                        }
                    }
                    catch
                    {   // report missing component 
                        Debug.LogError("SafeSaves: Error in " + type + " save format.\n No such variable exists: " + (entry.Key.NullOrEmpty() ? entry.Key : "ENTRY IS NULL OR EMPTY"));
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Error in " + type + " save format.\n GameObject case: " + e);
            }
            return false;
        }
    }
}
