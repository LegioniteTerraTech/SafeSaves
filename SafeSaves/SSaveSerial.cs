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
    [Serializable]
    public class SSaveSerial
    {
        [JsonIgnore]
        private static JsonSerializerSettings JSONSaver = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            MaxDepth = 10,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            //Converters = new List<JsonConverter> { new converter }
            TypeNameHandling = TypeNameHandling.Auto,
        };

        public bool corrupted = false;
        [JsonIgnore]
        public bool typeNotLoaded = false;
        [JsonIgnore]
        public Type type
        {
            get
            {
                if (typeSav == null)
                {
                    try
                    {
                        if (!typeString.NullOrEmpty())
                            typeSav = JsonConvert.DeserializeObject<Type>(typeString);
                        else
                            typeNotLoaded = true;
                    }
                    catch
                    {
                        // the mod is no longer installed! 
                    }
                }
                return typeSav;
            }
            set
            {
                typeSav = value;
                typeString = JsonConvert.SerializeObject(value);
                typeNotLoaded = false;
            }
        }
        [JsonIgnore]
        public Type typeSav;
        public string typeString;
        /// <summary>
        /// The fields in a saveable format. 
        /// Values can overlap with hashcodes but it's extermely unlikely
        /// </summary>
        public Dictionary<int, string> serialized = new Dictionary<int, string>();

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
        public bool CanLoad()
        {
            try
            {
                if (type == null)
                {
                    if (typeString != null)
                        Debug.Log("SafeSaves: CanLoad - Could not load type of " + typeString + " because it's respective assembly is not accessable.");
                    else
                        Debug.Log("SafeSaves: CanLoad - Could not load because there is no saved type that this entry references.");
                    return false;
                }
                if (corrupted)
                {
                    Debug.Log("SafeSaves: CanLoad - Could not load because the saved entry was corrupted.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.Log("SafeSaves: CanLoad - Could not load because of technical error " + e);
            }
            return true;
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
            catch (Exception e)
            {
                Debug.Log("SafeSaves: SaveState - error " + e);
            }
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



        protected bool LoadState(Type toGetType, string saveFieldID, object objCurrentInst, out object objectLoaded)
        {
            objectLoaded = null;
            try
            {
                if (corrupted)
                {
                    Debug.Log("SafeSaves: Could not load item!  DATA CORRUPTED");
                    return false;
                }
                if (serialized.TryGetValue(GetEncodedID(saveFieldID), out string serial))
                {
                    if (toGetType.IsAssignableFrom(typeof(MonoBehaviour)))
                    {   // uh-oh, we can't directly touch this
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(serial);
                        if (data != null)
                        {
                            return TryLoad(toGetType, ref objCurrentInst, data);
                        }
                        else
                        {
                            Debug.Log("SafeSaves: Could not load item!  Object was modified, but then lost!");
                            return false;
                        }
                    }
                    else
                        objectLoaded = JsonConvert.DeserializeObject<object>(serial, JSONSaver);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.Log("SafeSaves: LoadState - Could not load item ! " + e);
            }
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
        protected bool LoadState<T>(string saveFieldID, object objCurrentInst, out object objectLoaded)
        {
            objectLoaded = null;
            try
            {
                if (corrupted)
                {
                    Debug.Log("SafeSaves: Could not load item!  DATA CORRUPTED");
                    return false;
                }
                if (serialized.TryGetValue(GetEncodedID(saveFieldID), out string serial))
                {
                    if (objCurrentInst is MonoBehaviour)
                    {   // uh-oh, we can't directly touch this
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(serial);
                        if (data != null)
                        {
                            return TryLoad(objCurrentInst.GetType(), ref objCurrentInst, data);
                        }
                        else
                        {
                            Debug.Log("SafeSaves: Could not load item!  Object was modified, but then lost!");
                            return false;
                        }
                    }
                    else
                        objectLoaded = JsonConvert.DeserializeObject<T>(serial, JSONSaver);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.Log("SafeSaves: LoadState - Could not load item ! " + e);
            }
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
        protected bool LoadState<T>(string saveFieldID, ref T objToApplyTo)
        {
            try
            {
                if (corrupted)
                {
                    Debug.Log("SafeSaves: Could not load item!  DATA CORRUPTED");
                    return false;
                }
                if (serialized.TryGetValue(GetEncodedID(saveFieldID), out string serial))
                {
                    try
                    {
                        if (objToApplyTo is MonoBehaviour)
                        {   // uh-oh, we can't directly touch this
                            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(serial);
                            if (data != null)
                            {
                                return TryLoad<T>(ref objToApplyTo, data);
                            }
                            else
                            {
                                Debug.Log("SafeSaves: Could not load item!  Object was modified, but then lost!");
                                return false;
                            }
                        }
                        else
                            objToApplyTo = JsonConvert.DeserializeObject<T>(serial, JSONSaver);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("SafeSaves: LoadState(FALLBACK) - Could not load item! " + e);
                        objToApplyTo = (T)JsonConvert.DeserializeObject(serial, JSONSaver);
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.Log("SafeSaves: LoadState - Could not load item ! " + e);
            }
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
                    Debug.Info("SafeSaves: SaveStateInternal - Saved MonoBehavior " + ID);
                }
                else
                {
                    serial = JsonConvert.SerializeObject(objToSave, JSONSaver);
                    Debug.Info("SafeSaves: SaveStateInternal - Saved Normal Field " + ID);
                }
                if (serialized.TryGetValue(ID, out _))
                {
                    serialized.Remove(ID);
                }
                serialized.Add(ID, serial);
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("SafeSaves: SaveStateInternal - error " + e);
            }
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

        internal List<string> GetSerials()
        {
            List<string> serials = new List<string>();
            try
            {
                foreach (KeyValuePair<int, string> entry in serialized.ToList())
                {
                    try
                    {
                        serials.Add(JsonConvert.SerializeObject(entry));
                    }
                    catch
                    {   // report missing component 
                        Debug.LogError("SafeSaves: Error in save format.\n Could not load serial of ID " + entry.Key);
                    }
                }
                return serials;
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Error in save format.\n GameObject case: " + e);
            }
            return serials;
        }
        internal void ApplySerials(List<string> serials)
        {
            try
            {
                serialized.Clear();
                foreach (string serial in serials)
                {
                    try
                    {
                        KeyValuePair<int, string> IS = (KeyValuePair<int, string>)JsonConvert.DeserializeObject(serial);
                        serialized.Add(IS.Key, IS.Value);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("SafeSaves: Error in Loading from serial." + e);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: Error in Loading from serial." + e);
            }
        }
    }
    internal class ErrorTypeUnset
    {
    }
}
