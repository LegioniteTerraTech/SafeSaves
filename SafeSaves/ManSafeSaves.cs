using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;
using Ionic.Zlib;
using Newtonsoft.Json;
using HarmonyLib;

namespace SafeSaves
{
    /// <summary>
    ///  MAKES a totally safe save seperate from TerraTech's main saving system for mods.
    ///  This is so that when the mod is removed, the game won't break.
    ///  <para>
    ///  - This only saves in relation to Save Files.  Use ModConfigHelper at 
    ///  https://github.com/Aceba1/TTQMM-ModConfigHelper
    ///  for matters that should remain constant between saving and loading.
    ///  
    /// </para>
    ///  This project extensively reuses code from SubMissions.
    ///  The .SSAV file format is a GZIP-ed SafeSave.
    /// </summary>
    public class ManSafeSaves : MonoBehaviour
    {
        internal static bool UseCompressor = true;

        internal static string DLLDirectory;
        internal static string SavesDirectory;
        internal static string compressFileName = ".SSAV";
        internal static char up = '\\';
        internal static SafeSave currentSave = new SafeSave();

        internal static JsonSerializerSettings JSONSaver = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            MaxDepth = 30,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        internal static Event<bool> onSaving = new Event<bool>();
        internal static Event<bool> onLoading = new Event<bool>();
        internal static List<int> RegisteredSaveDLLs = new List<int>();
        internal static List<Type> RegisteredManagers = new List<Type>();
        internal static List<Type> RegisteredModules = new List<Type>();


        private static ManSafeSaves inst;
        private static Harmony harmonyInst;
        internal static bool needsToFetch = false;
        internal static bool ignoreSaving = true;
        internal static bool IgnoreSaving { get { return ignoreSaving && RegisteredSaveDLLs.Count > 0; } }

        public static void Init()
        {
            if (inst)
                return;
            harmonyInst = new Harmony("legionite.safesaves");
            harmonyInst.PatchAll(Assembly.GetExecutingAssembly());
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX)
            {
                up = '/';
            }
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            DLLDirectory = di.Parent.ToString();
            DirectoryInfo game = new DirectoryInfo(Application.dataPath);
            game = game.Parent;
            SavesDirectory = game.ToString() + up + "SafeSaves";
            ValidateDirectory(SavesDirectory);
            inst = new GameObject("ManSafeSaves").AddComponent<ManSafeSaves>();
            ignoreSaving = false;
            DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Init");
        }
        private static bool isSubscribed = false;
        internal static void Subscribe()
        {
            if (isSubscribed)
                return;
            ManGameMode.inst.ModeSwitchEvent.Subscribe(ModeSwitch);
            ManGameMode.inst.ModeSetupEvent.Subscribe(ModeLoad);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(ModeFinished);
            ManTechs.inst.TankDestroyedEvent.Subscribe(TankDestroyed);
            DebugSafeSaves.Log("SafeSaves: Core module hooks launched");
            isSubscribed = true;
        }
        private static void ModeSwitch()
        {
            currentSave = new SafeSave();
        }
        private static void ModeLoad(Mode mode)
        {
            if (mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCampaign)
            {
                //Debug.Log("SafeSaves: ManSafeSaves Loading from save " + Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false) + "!");
                //LoadDataAutomatic();
                // Delay launch for one frame - this allows the Techs to load in properly.
                inst.Invoke("ModeLoadDelayed", 0.01f);
            }
        }
        public void ModeLoadDelayed()
        {
            DebugSafeSaves.Log("SafeSaves: ManSafeSaves Loading from save " + Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false) + "!");
            LoadDataAutomatic();
        }
        private static void ModeFinished(Mode mode)
        {
            if (mode is ModeMain || mode is ModeCoOpCampaign)
            {
                var saver = Singleton.Manager<ManSaveGame>.inst;
                if (saver.IsSaveNameAutoSave(saver.GetCurrentSaveName(false)))
                {
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves Saving!");
                    SaveDataAutomatic();
                }
            }
        }
        private static void TankDestroyed(Tank tech, ManDamage.DamageInfo DI)
        {
            if (tech)
                LoadSerialToTank(tech, null);
        }

        public static Type[] FORCE_GET_TYPES(Assembly AEM)
        {
            try
            {
                return AEM.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
        }

        /// <summary>
            /// Invoke this once to register it to the saving system.
            /// <para>
            /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
            /// </para>
            /// </summary>
        public static void RegisterSaveSystem(Assembly AEM)
        {
            try
            {
                int nameHash = AEM.GetName().Name.GetHashCode();
                if (!RegisteredSaveDLLs.Contains(nameHash))
                {
                    Type[] types = FORCE_GET_TYPES(AEM);
                    for (int step = 0; step < types.Length; step++)
                    {
                        Type typeCase = types[step];
                        if (typeCase == null)
                            continue;
                        foreach (var item in typeCase.GetCustomAttributes())
                        {
                            if (item is AutoSaveManagerAttribute)
                            {
                                RegisteredManagers.Add(typeCase);
                            }
                            else if (item is AutoSaveComponentAttribute)
                            {
                                RegisteredModules.Add(typeCase);
                            }
                        }
                    }
                    RegisteredSaveDLLs.Add(nameHash);
                    DebugSafeSaves.Log("SafeSaves: Registered " + AEM.FullName + " with ManSafeSaves.");
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.Log("SafeSaves: Could not register " + AEM.FullName + ", will try again." + e);
                inst.queued.Add(new AssemblyQueue(AEM, null, null));
            }
        }
        /// <summary>
        /// Invoke this once to register it to the saving system.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <param name="OnSave">The method to invoke before this acts. true when starting and false when done.</param>
        /// <param name="OnLoad">The method to invoke after this acts. true when starting and false when done.</param>
        public static void RegisterSaveSystem(Assembly AEM, Action<bool> OnSave, Action<bool> OnLoad)
        {
            try
            {
                int nameHash = AEM.GetName().Name.GetHashCode();
                if (!RegisteredSaveDLLs.Contains(nameHash))
                {
                    onSaving.Subscribe(OnSave);
                    onLoading.Subscribe(OnLoad);
                    Type[] types = FORCE_GET_TYPES(AEM);
                    for (int step = 0; step < types.Length; step++)
                    {
                        Type typeCase = types[step];
                        if (typeCase == null)
                            continue;
                        foreach (var item in typeCase.GetCustomAttributes())
                        {
                            if (item is AutoSaveManagerAttribute)
                            {
                                RegisteredManagers.Add(typeCase);
                            }
                            else if (item is AutoSaveComponentAttribute)
                            {
                                RegisteredModules.Add(typeCase);
                            }
                        }
                    }
                    RegisteredSaveDLLs.Add(nameHash);
                    DebugSafeSaves.Log("SafeSaves: Registered " + AEM.FullName + " with ManSafeSaves.");
                }
            }
            catch
            {
                DebugSafeSaves.Log("SafeSaves: Could not register " + AEM.FullName + ", will try again.");
                inst.queued.Add(new AssemblyQueue(AEM, OnSave, OnLoad));
            }
        }

        /// <summary>
        /// Unregister from saving.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        public static void UnregisterSaveSystem(Assembly AEM)
        {
            int nameHash = AEM.GetName().Name.GetHashCode();
            if (RegisteredSaveDLLs.Contains(nameHash))
            {
                Type[] types = FORCE_GET_TYPES(AEM);
                for (int step = 0; step < types.Length; step++)
                {
                    Type typeCase = types[step];
                    if (typeCase == null)
                        continue;
                    foreach (var item in typeCase.GetCustomAttributes())
                    {
                        if (item is AutoSaveManagerAttribute)
                        {
                            RegisteredManagers.Remove(typeCase);
                        }
                        else if (item is AutoSaveComponentAttribute)
                        {
                            RegisteredModules.Remove(typeCase);
                        }
                    }
                }
                RegisteredSaveDLLs.Remove(nameHash);
                DebugSafeSaves.Log("SafeSaves: Un-Registered " + AEM.FullName + " from ManSafeSaves.");
            }
        }
        /// <summary>
        /// Unregister from saving.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <param name="OnSave">The method to invoke before this acts. true when starting and false when done.</param>
        /// <param name="OnLoad">The method to invoke after this acts. true when starting and false when done.</param>
        public static void UnregisterSaveSystem(Assembly AEM, Action<bool> OnSave, Action<bool> OnLoad)
        {
            int nameHash = AEM.GetName().Name.GetHashCode();
            if (RegisteredSaveDLLs.Contains(nameHash))
            {
                Type[] types = FORCE_GET_TYPES(AEM);
                for (int step = 0; step < types.Length; step++)
                {
                    Type typeCase = types[step];
                    if (typeCase == null)
                        continue;
                    foreach (var item in typeCase.GetCustomAttributes())
                    {
                        if (item is AutoSaveManagerAttribute)
                        {
                            RegisteredManagers.Remove(typeCase);
                        }
                        else if (item is AutoSaveComponentAttribute)
                        {
                            RegisteredModules.Remove(typeCase);
                        }
                    }
                }
                onSaving.Unsubscribe(OnSave);
                onLoading.Unsubscribe(OnLoad);
                RegisteredSaveDLLs.Remove(nameHash);
                DebugSafeSaves.Log("SafeSaves: Un-Registered " + AEM.FullName + " from ManSafeSaves.");
            }
        }

        private List<AssemblyQueue> queued = new List<AssemblyQueue>();
        private void Update()
        {
            if (queued.Count == 0)
                return;
            try
            {
                foreach (var item in queued)
                {
                    item.TryAdd();
                }
                queued.Clear();
            }
            catch { }
        }



        /// <summary>
        /// Gets the serialization of a Tech from the current SafeSave
        /// </summary>
        /// <param name="inst">The non-null Tank instance</param>
        /// <returns>the string serial</returns>
        public static string GetSerialOfTank(Tank inst)
        {
            try
            {
                string serial = currentSave.GetSaveStateTank(inst);
                DebugSafeSaves.Assert(serial == null, "SafeSaves: ManSafeSaves - GetSerialOfTank: FAILIURE IN OPERATION!  Output was null!");
                return serial;
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - GetSerialOfTank: FAILIURE IN OPERATION! " + e);
            }
            return null;
        }

        /// <summary>
        /// Loads the serialization of a Tech to the Tech
        /// </summary>
        /// <param name="inst">The non-null Tank instance</param>
        /// <param name="serial">The serial to load to the Tank</param>
        public static void LoadSerialToTank(Tank inst, string serial)
        {
            try
            {
                currentSave.LoadStateTankExternal(inst, serial);
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - LoadSerialToTank: FAILIURE IN OPERATION! " + e);
            }
        }

        /// <summary>
        /// ONLY SAVES INTEGERS
        /// Call this whenever you want to save a block's data - will not automatically save on it's own!
        /// </summary>
        /// <typeparam name="T">The block's Module</typeparam>
        /// <param name="block">The TankBlock that holds it</param>
        /// <param name="component">The Module to save</param>
        /// <returns>true if it saved</returns>
        internal static bool SaveBlockToSave<T>(TankBlock block, T component)
        {
            try
            {
                return currentSave.SaveModuleState(block.visible, component);
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: FAILIURE IN OPERATION! " + e);
            }
            return false;
        }

        /// <summary>
        /// USE THIS FOR EACH CASE
        /// Call this whenever you want to save a block's data - will not automatically save on it's own!
        /// </summary>
        /// <typeparam name="T">The block's Module</typeparam>
        /// <param name="block">The TankBlock that holds it</param>
        /// <param name="component">The Module to save</param>
        /// <returns>true if it saved</returns>
        internal static bool SaveBlockComplexFieldToSave<T, C>(TankBlock block, T component, C Field)
        {
            try
            {
                return currentSave.SaveModuleStateField(block.visible, component, Field);
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: FAILIURE IN OPERATION! " + e);
            }
            return false;
        }

        /// <summary>
        /// ONLY SAVES INTEGERS
        /// Call this whenever you want to load a block's data for use in a Mod
        /// </summary>
        /// <typeparam name="T">The block's Module</typeparam>
        /// <param name="block">The TankBlock that holds it</param>
        /// <param name="component">The Module to save</param>
        /// <returns>true if it loaded</returns>
        internal static bool LoadBlockFromSave<T>(TankBlock block, T component)
        {
            try
            {
                return currentSave.LoadModuleState(block.visible, component);
            }
            catch
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: FAILIURE IN OPERATION!");
            }
            return false;
        }
        /// <summary>
        /// USE THIS FOR EACH CASE
        /// Call this whenever you want to load a block's data (Non-Int) for use in a Mod
        /// </summary>
        /// <typeparam name="T">The block's Module</typeparam>
        /// <param name="block">The TankBlock that holds it</param>
        /// <param name="component">The Module to save</param>
        /// <returns>true if it loaded</returns>
        internal static bool LoadBlockComplexFieldFromSave<T, C>(TankBlock block, T component, ref C Field)
        {
            try
            {
                return currentSave.LoadModuleStateField(block.visible, component, ref Field);
            }
            catch
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: FAILIURE IN OPERATION!");
            }
            return false;
        }


        private static string SerializeFromManager(bool defaultState = false)
        {
            return JsonConvert.SerializeObject(SaveToFileFormatting(defaultState), UseCompressor ? Formatting.None : Formatting.Indented, JSONSaver);
        }
        private static void DeserializeToManager(string SafeSaveIn)
        {
            LoadFromFileFormatting(JsonConvert.DeserializeObject<SafeSave>(SafeSaveIn, JSONSaver));
        }

        private static SafeSave SaveToFileFormatting(bool defaultState)
        {
            onSaving.Send(true);
            if (defaultState)
            {
                DebugSafeSaves.Log("SafeSaves: Resetting SafeSave for new save instance...");
                currentSave = new SafeSave();
            }
            currentSave.SaveStateALL();
            onSaving.Send(false);
            return currentSave;
        }
        private static void LoadFromFileFormatting(SafeSave save)
        {
            onLoading.Send(true);
            if (save == null)
            {
                DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Save is corrupted!");
                currentSave = new SafeSave();
                return;
            }
            currentSave = save;
            currentSave.LoadStateALL();
            onLoading.Send(false);
        }



        internal static void LoadDataAutomatic()
        {
            try
            {
                string saveName = Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false);
                LoadData(saveName, ManGameMode.inst.GetCurrentGameMode());
                //Debug.Log("SafeSaves: SaveManSubMissions - LoadDataAutomatic: Loaded save " + saveName + " successfully");
            }
            catch
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - LoadDataAutomatic: FAILIURE IN MAJOR OPERATION!");
            }
        }
        internal static void SaveDataAutomatic()
        {
            try
            {
                string saveName = Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false);
                SaveData(saveName, ManGameMode.inst.GetCurrentGameMode());
                //Debug.Log("SafeSaves: SaveManSubMissions - SaveDataAutomatic: Saved save " + saveName + " successfully");
            }
            catch
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - SaveDataAutomatic: FAILIURE IN MAJOR OPERATION!");
            }
        }


        internal static void LoadData(string saveName, string altDirectory)
        {
            if (IgnoreSaving)
                return;
            string destination = SavesDirectory + up + altDirectory + up + saveName;
            ValidateDirectory(SavesDirectory);
            ValidateDirectory(SavesDirectory + up + altDirectory);
            try
            {
                try
                {
                    if (UseCompressor)
                    {
                        if (File.Exists(destination + compressFileName))
                        {
                            using (FileStream FS = File.Open(destination + compressFileName, FileMode.Open, FileAccess.Read))
                            {
                                using (GZipStream GZS = new GZipStream(FS, CompressionMode.Decompress))
                                {
                                    using (StreamReader SR = new StreamReader(GZS))
                                    {
                                        DeserializeToManager(SR.ReadToEnd());
                                    }
                                }
                            }
                            DebugSafeSaves.Log("SafeSaves: Loaded " + compressFileName + " for " + saveName + " successfully.");
                        }
                        else if (File.Exists(destination + ".json"))
                        {
                            string output = "";
                            output = File.ReadAllText(destination + ".json");

                            DeserializeToManager(output);
                            DebugSafeSaves.Log("SafeSaves: Loaded SafeSave.json for " + saveName + " successfully.");
                        }
                    }
                    else
                    {
                        if (File.Exists(destination + ".json"))
                        {
                            string output = "";
                            output = File.ReadAllText(destination + ".json");

                            DeserializeToManager(output);
                            DebugSafeSaves.Log("SafeSaves: Loaded SafeSave.json for " + saveName + " successfully.");
                        }
                        else if (File.Exists(destination + compressFileName))
                        {
                            using (FileStream FS = File.Open(destination + compressFileName, FileMode.Open, FileAccess.Read))
                            {
                                using (GZipStream GZS = new GZipStream(FS, CompressionMode.Decompress))
                                {
                                    using (StreamReader SR = new StreamReader(GZS))
                                    {
                                        DeserializeToManager(SR.ReadToEnd());
                                    }
                                }
                            }
                            DebugSafeSaves.Log("SafeSaves: Loaded " + compressFileName + " for " + saveName + " successfully.");
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugSafeSaves.LogError("SafeSaves: Could not load contents of MissionSave.json/.SMSAV for " + saveName + "!");
                    DebugSafeSaves.Log(e);
                    return;
                }
                return;
            }
            catch
            {
                try
                {
                    File.WriteAllText(destination + ".json", SerializeFromManager(true));
                    DebugSafeSaves.Log("SafeSaves: Created new SafeSave.json for " + saveName + " successfully.");
                    return;
                }
                catch
                {
                    DebugSafeSaves.Log("SafeSaves: Could not read SafeSave.json for " + saveName + ".  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }
            }
        }

        internal static void SaveData(string saveName, string altDirectory)
        {
            if (IgnoreSaving)
                return;
            DebugSafeSaves.Log("SafeSaves: Setting up template reference...");
            string destination = SavesDirectory + up + altDirectory + up + saveName;
            ValidateDirectory(SavesDirectory);
            ValidateDirectory(SavesDirectory + up + altDirectory);
            try
            {

                if (UseCompressor)
                {
                    using (FileStream FS = File.Create(destination + compressFileName))
                    {
                        using (GZipStream GZS = new GZipStream(FS, CompressionMode.Compress))
                        {
                            using (StreamWriter SW = new StreamWriter(GZS))
                            {
                                SW.WriteLine(SerializeFromManager());
                                SW.Flush();
                            }
                        }
                    }
                    CleanUpCache();
                    DebugSafeSaves.Log("SafeSaves: Saved " + compressFileName + " for " + saveName + " successfully.");
                }
                else
                {
                    File.WriteAllText(destination + ".json", SerializeFromManager());
                    CleanUpCache();
                    DebugSafeSaves.Log("SafeSaves: Saved SafeSave.json for " + saveName + " successfully.");
                }
            }
            catch
            {
                DebugSafeSaves.LogError("SafeSaves: Could not save SafeSave.json/" + compressFileName + " for " + saveName + ".  \n   This could be due to a bug with this mod or file permissions.");
                return;
            }
        }


        private static void CleanUpCache()
        {
            if (currentSave != null)
                currentSave.AllManagerEntries.Clear();
        }

        private static bool GetName(string FolderDirectory, out string output, bool doJSON = false)
        {
            StringBuilder final = new StringBuilder();
            foreach (char ch in FolderDirectory)
            {
                if (ch == up)
                {
                    final.Clear();
                }
                else
                    final.Append(ch);
            }
            if (doJSON)
            {
                if (!final.ToString().Contains(".json"))
                {
                    output = "error";
                    return false;
                }
                final.Remove(final.Length - 5, 5);// remove ".json"
            }
            output = final.ToString();
            //Debug.Log("SafeSaves: Cleaning Name " + output);
            return true;
        }
        private static void ValidateDirectory(string DirectoryIn)
        {
            if (!GetName(DirectoryIn, out string name))
                return;// error
            if (!Directory.Exists(DirectoryIn))
            {
                DebugSafeSaves.Log("SafeSaves: Generating " + name + " folder.");
                try
                {
                    Directory.CreateDirectory(DirectoryIn);
                    DebugSafeSaves.Log("SafeSaves: Made new " + name + " folder successfully.");
                }
                catch
                {
                    DebugSafeSaves.LogError("SafeSaves: Could not create new " + name + " folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }
            }
        }


        internal class AssemblyQueue
        {
            public Assembly toDo;
            public Action<bool> OnSave;
            public Action<bool> OnLoad;

            public AssemblyQueue(Assembly AEY, Action<bool> save, Action<bool> load)
            {
                toDo = AEY;
                OnSave = save;
                OnLoad = load;
            }
            public void TryAdd()
            {
                if (OnSave != null && OnLoad != null)
                    RegisterSaveSystem(toDo, OnSave, OnLoad);
                else
                    RegisterSaveSystem(toDo);
            }
        }
    }
}
