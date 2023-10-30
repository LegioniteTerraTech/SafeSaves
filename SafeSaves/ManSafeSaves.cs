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
    internal class ManagedDLL
    {
        internal readonly Assembly DLL;
        internal readonly Action<bool> onSaving;
        internal readonly Action<bool> onLoading;
        internal readonly HashSet<Type> RegisteredManagers;
        internal readonly HashSet<Type> RegisteredModules;

        public ManagedDLL(Assembly DLL, Action<bool> Saving, Action<bool> Loading)
        {
            this.DLL = DLL;
            onSaving = Saving;
            onLoading = Loading;
            RegisteredManagers = new HashSet<Type>();
            RegisteredModules = new HashSet<Type>();
        }
    }

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
        public static bool UseCompressor = false;

        internal static string DLLDirectory;
        internal static string SavesDirectory;
        internal static string compressFileName = ".SSAV";
        internal static SafeSave currentSave
        {
            get => _currentSave;
            set 
            {
                if (_currentSave != value)
                {
                    if (_currentSave != null)
                    {
                        ModeAttract.inst.UnsubscribeFromEvents(_currentSave);
                        ModeMain.inst.UnsubscribeFromEvents(_currentSave);
                        ModeMisc.inst.UnsubscribeFromEvents(_currentSave);
                        ModeCoOpCampaign.inst.UnsubscribeFromEvents(_currentSave);
                        ModeCoOpCreative.inst.UnsubscribeFromEvents(_currentSave);
                        DebugSafeSaves.Log("SafeSaves: Saving System hooked to vanilla");
                    }
                    else
                        DebugSafeSaves.Log("SafeSaves: Saving System changed files");
                    _currentSave = value;
                    ModeAttract.inst.SubscribeToEvents(_currentSave);
                    ModeMain.inst.SubscribeToEvents(_currentSave);
                    ModeMisc.inst.SubscribeToEvents(_currentSave);
                    ModeCoOpCampaign.inst.SubscribeToEvents(_currentSave);
                    ModeCoOpCreative.inst.SubscribeToEvents(_currentSave);
                }
            }
        }
        private static SafeSave _currentSave = null;

        internal static ManSaveGame.SaveDataJSONType safeSaveJsonType = (ManSaveGame.SaveDataJSONType)int.MinValue;

        internal static JsonSerializerSettings JSONSaver = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            MaxDepth = 30,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        internal static readonly Dictionary<int, ManagedDLL> RegisteredSaveDLLs = new Dictionary<int, ManagedDLL>();


        private static ManSafeSaves inst;
        private static Harmony harmonyInst;
        internal static bool needsToFetch = false;

        public static bool DisableExternalBackupSaving = false;
        internal static bool ignoreSaving = true;
        internal static bool IgnoreSaving { get { return ignoreSaving && RegisteredSaveDLLs.Count > 0; } }


        public static void Init()
        {
            if (inst)
                return;
#if LONE
            DebugSafeSaves.Log("SafeSaves: MAIN (Steam Workshop Version LONE) startup");
#elif STEAM
            DebugSafeSaves.Log("SafeSaves: MAIN (Steam Workshop Version2) startup");
#else
            DebugSafeSaves.Log("SafeSaves: MAIN (TTMM Version) startup");
#endif
            harmonyInst = new Harmony("legionite.safesaves");
            harmonyInst.PatchAll(Assembly.GetExecutingAssembly());
            DirectoryInfo di = new DirectoryInfo(Assembly.GetExecutingAssembly().Location);
            DLLDirectory = di.Parent.ToString();
            DirectoryInfo game = new DirectoryInfo(Application.dataPath);
            game = game.Parent;
            SavesDirectory = Path.Combine(game.ToString(), "SafeSaves");
            ValidateDirectory(SavesDirectory);
            inst = new GameObject("ManSafeSaves").AddComponent<ManSafeSaves>();
            ignoreSaving = false;
            DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Inited");
        }
        private static bool isSubscribed = false;
        internal static void Subscribe()
        {
            if (isSubscribed || !ManGameMode.inst || !ManTechs.inst)
                return;
            ManGameMode.inst.ModeSwitchEvent.Subscribe(ModeSwitch);
            ManGameMode.inst.ModeSetupEvent.Subscribe(ModeLoad);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(ModeFinished);
            ManTechs.inst.TankDestroyedEvent.Subscribe(TankDestroyed);
            DebugSafeSaves.Log("SafeSaves: Core module hooks launched");

            currentSave = new SafeSave();

            isSubscribed = true;
        }
        private static void ModeSwitch()
        {
            currentSave.ClearSave();
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
            LoadData(null, ManGameMode.inst.GetCurrentGameMode());
        }
        private static void ModeFinished(Mode mode)
        {
            if (mode is ModeMain || mode is ModeCoOpCampaign)
            {
                var saver = Singleton.Manager<ManSaveGame>.inst;
                if (saver.IsSaveNameAutoSave(saver.GetCurrentSaveName(false)))
                {
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves Saving!");
                    SaveDataExtBackup(null, ManGameMode.inst.GetCurrentGameMode());
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
                Init();
                Subscribe();
                int nameHash = AEM.GetName().Name.GetHashCode();
                if (!RegisteredSaveDLLs.TryGetValue(nameHash, out _))
                {
                    ManagedDLL newDLL = new ManagedDLL(AEM, null, null);
                    Type[] types = FORCE_GET_TYPES(AEM);
                    int manCount = 0;
                    int modCount = 0;
                    for (int step = 0; step < types.Length; step++)
                    {
                        Type typeCase = types[step];
                        if (typeCase == null)
                            continue;
                        foreach (var item in typeCase.GetCustomAttributes())
                        {
                            if (item is AutoSaveManagerAttribute)
                            {
                                newDLL.RegisteredManagers.Add(typeCase);
                                manCount++;
                            }
                            else if (item is AutoSaveComponentAttribute)
                            {
                                newDLL.RegisteredModules.Add(typeCase);
                                modCount++;
                            }
                        }
                    }
                    RegisteredSaveDLLs.Add(nameHash, newDLL);
                    DebugSafeSaves.Log("SafeSaves: Registered " + AEM.FullName + " with ManSafeSaves.");
                    DebugSafeSaves.Log("SafeSaves: Managers: " + manCount + ",  Modules: " + modCount + ".");
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
                Init();
                Subscribe();
                int nameHash = AEM.GetName().Name.GetHashCode();
                if (!RegisteredSaveDLLs.TryGetValue(nameHash, out _))
                {
                    ManagedDLL newDLL = new ManagedDLL(AEM, OnSave, OnLoad);
                    Type[] types = FORCE_GET_TYPES(AEM);
                    int manCount = 0;
                    int modCount = 0;
                    for (int step = 0; step < types.Length; step++)
                    {
                        Type typeCase = types[step];
                        if (typeCase == null)
                            continue;
                        foreach (var item in typeCase.GetCustomAttributes())
                        {
                            if (item is AutoSaveManagerAttribute)
                            {
                                newDLL.RegisteredManagers.Add(typeCase);
                                manCount++;
                            }
                            else if (item is AutoSaveComponentAttribute)
                            {
                                newDLL.RegisteredModules.Add(typeCase);
                                modCount++;
                            }
                        }
                    }
                    RegisteredSaveDLLs.Add(nameHash, newDLL);
                    DebugSafeSaves.Log("SafeSaves: Registered " + AEM.FullName + " with ManSafeSaves.");
                    DebugSafeSaves.Log("SafeSaves: Managers: " + manCount + ",  Modules: " + modCount + ".");
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
            if (RegisteredSaveDLLs.TryGetValue(nameHash, out _))
            {
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
            if (RegisteredSaveDLLs.TryGetValue(nameHash, out _))
            {
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

        internal static HashSet<Type> GetRegisteredModules()
        {
            HashSet<Type> allModules = new HashSet<Type>();
            foreach (var item in RegisteredSaveDLLs)
            {
                foreach (var item2 in item.Value.RegisteredModules)
                {
                    allModules.Add(item2);
                }
            }
            return allModules;
        }
        internal static HashSet<Type> GetRegisteredManagers()
        {
            HashSet<Type> allManagers = new HashSet<Type>();
            foreach (var item in RegisteredSaveDLLs)
            {
                foreach (var item2 in item.Value.RegisteredManagers)
                {
                    allManagers.Add(item2);
                }
            }
            return allManagers;
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

        private static void OnSaving(bool Before)
        {
            foreach (var item in RegisteredSaveDLLs)
            {
                try
                {
                    if (item.Value.onSaving != null)
                        item.Value.onSaving.Invoke(Before);
                }
                catch (Exception e)
                {
                    try
                    {
                        DebugSafeSaves.CacheException(item.Value, "SafeSaves: " + item.Value.DLL.FullName + " encountered a error on calling " +
                            item.Value.onSaving.GetMethodInfo().Name + " " + (Before ? "BEFORE" : "AFTER") + " the call to SAVE the save\n" + e);
                    }
                    catch
                    {
                        DebugSafeSaves.CacheException(item.Value, "SafeSaves: " + item.Value.DLL.FullName + " encountered a error on calling NULL METHOD " +
                            (Before ? "BEFORE" : "AFTER") + " the call to SAVE the save\n" + e);
                    }
                }
            }
        }
        private static void OnLoading(bool Before)
        {
            foreach (var item in RegisteredSaveDLLs)
            {
                try
                {
                    if (item.Value.onLoading != null)
                        item.Value.onLoading.Invoke(Before);
                }
                catch (Exception e)
                {
                    try
                    {
                        DebugSafeSaves.CacheException(item.Value, "SafeSaves: " + item.Value.DLL.FullName + " encountered a error on calling " +
                            item.Value.onLoading.GetMethodInfo().Name + " " + (Before ? "BEFORE" : "AFTER") + " the call to LOAD the save\n" + e);
                    }
                    catch
                    {
                        DebugSafeSaves.CacheException(item.Value, "SafeSaves: " + item.Value.DLL.FullName + " encountered a error on calling NULL METHOD " +
                            (Before ? "BEFORE" : "AFTER") + " the call to LOAD the save\n" + e);
                    }
                }
            }
        }

        private static SafeSave SaveToFileFormatting(bool defaultState)
        {
            OnSaving(true);
            if (defaultState)
            {
                DebugSafeSaves.Log("SafeSaves: Resetting SafeSave for new save instance...");
                currentSave.ClearSave();
            }
            currentSave.SaveStateALL();
            OnSaving(false);
            if (DebugSafeSaves.GetSubExceptions(out string errors, "while saving"))
                DebugSafeSaves.Log(errors);
            return currentSave;
        }
        private static void LoadFromFileFormatting(SafeSave save)
        {
            OnLoading(true);
            if (save == null)
            {
                DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Save is corrupted!");
                currentSave.ClearSave();
                return;
            }
            currentSave.LoadStateALL();
            OnLoading(false);
            if (DebugSafeSaves.GetSubExceptions(out string errors, "while loading"))
                DebugSafeSaves.Log(errors);
        }


        internal static void LoadData(string saveName, string altDirectory)
        {
            if (IgnoreSaving)
                return;
            if (saveName == null)
                saveName = Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false);
            if (saveName.NullOrEmpty())
                return;
            if (ManSaveGame.inst.CurrentState.GetSaveData(safeSaveJsonType, out SafeSave data))
            {
                DebugSafeSaves.Log("Loading from save file...");
                currentSave.SetSave(data);
                DebugSafeSaves.Log("Loaded from save file successfully.");
            }
            else
            {
                DebugSafeSaves.Log("Was not able to load from save file directly.");
                DebugSafeSaves.Log("Loading from external backup save file...");
                LoadDataExtBackup(saveName, altDirectory);
            }
        }
        internal static void LoadDataExtBackup(string saveName, string altDirectory)
        {
            string destination = Path.Combine(SavesDirectory, altDirectory, saveName);
            ValidateDirectory(SavesDirectory);
            ValidateDirectory(Path.Combine(SavesDirectory, altDirectory));
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
                    DebugSafeSaves.Log("SafeSaves: Could not load contents of MissionSave.json/.SMSAV for " + saveName + "!");
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
                catch (Exception e)
                {
                    DebugSafeSaves.Log("SafeSaves: Could not read SafeSave.json for " + saveName + ".  \n   This could be due to a bug with this mod or file permissions.");
                    DebugSafeSaves.Log(e);
                    return;
                }
            }
        }

        internal static void SaveDataExtBackup(string saveName, string altDirectory)
        {
            if (IgnoreSaving || DisableExternalBackupSaving)
                return;
            DebugSafeSaves.Log("Saving to external backup save file...");
            if (saveName == null)
            {
                DebugSafeSaves.Log("SafeSaves: Setting up template reference...");
                saveName = Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false);
            }
            string destination = Path.Combine(SavesDirectory, altDirectory, saveName);
            ValidateDirectory(SavesDirectory);
            ValidateDirectory(Path.Combine(SavesDirectory, altDirectory));
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
            catch (Exception e)
            {
                DebugSafeSaves.Log("SafeSaves: Could not save SafeSave.json/" + compressFileName + " for " + saveName + ".  \n   This could be due to a bug with this mod or file permissions.");
                DebugSafeSaves.Log(e);
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
                if (ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar)
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
