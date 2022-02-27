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
        private static bool UseCompressor = true;

        public static string DLLDirectory;
        public static string SavesDirectory;
        public static string compressFileName = ".SSAV";
        public static char up = '\\';
        public static SafeSave currentSave = new SafeSave();

        private static JsonSerializerSettings JSONSaver = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            MaxDepth = 10,
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
            Debug.Log("SafeSaves: ManSafeSaves - Init");
        }
        private static bool isSubscribed = false;
        public static void Subscribe()
        {
            if (isSubscribed)
                return;
            ManGameMode.inst.ModeStartEvent.Subscribe(ModeLoad);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(ModeFinished);
            Debug.Log("SafeSaves: Core module hooks launched");
            isSubscribed = true;
        }
        private static void ModeLoad(Mode mode)
        {
            if (mode is ModeMain || mode is ModeMisc || mode is ModeCoOpCampaign)
            {
                Debug.Log("SafeSaves: ManSafeSaves Loading from save!");
                LoadDataAutomatic();
            }
        }
        private static void ModeFinished(Mode mode)
        {
            if (mode is ModeMain || mode is ModeCoOpCampaign)
            {
                var saver = Singleton.Manager<ManSaveGame>.inst;
                if (saver.IsSaveNameAutoSave(saver.GetCurrentSaveName(false)))
                {
                    Debug.Log("SafeSaves: ManSafeSaves Saving!");
                    SaveDataAutomatic();
                }
            }
            currentSave = new SafeSave();
        }


        /// <summary>
        /// Invoke this once to register it to the saving system.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        public static void RegisterSaveSystem()
        {
            Assembly AEM = Assembly.GetCallingAssembly();
            int nameHash = AEM.GetName().Name.GetHashCode();
            if (!RegisteredSaveDLLs.Contains(nameHash))
            {
                foreach (var typeCase in AEM.GetTypes())
                {
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
        public static void RegisterSaveSystem(Action<bool> OnSave, Action<bool> OnLoad)
        {
            Assembly AEM = Assembly.GetCallingAssembly();
            int nameHash = AEM.GetName().Name.GetHashCode();
            if (!RegisteredSaveDLLs.Contains(nameHash))
            {
                onSaving.Subscribe(OnSave);
                onLoading.Subscribe(OnLoad);
                foreach (var typeCase in AEM.GetTypes())
                {
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
            }
        }

        /// <summary>
        /// Unregister from saving.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        public static void UnregisterSaveSystem()
        {
            Assembly AEM = Assembly.GetCallingAssembly();
            int nameHash = AEM.GetName().Name.GetHashCode();
            if (RegisteredSaveDLLs.Contains(nameHash))
            {
                foreach (var typeCase in AEM.GetTypes())
                {
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
        public static void UnregisterSaveSystem(Action<bool> OnSave, Action<bool> OnLoad)
        {
            Assembly AEM = Assembly.GetCallingAssembly();
            int nameHash = AEM.GetName().Name.GetHashCode();
            if (RegisteredSaveDLLs.Contains(nameHash))
            {
                foreach (var typeCase in AEM.GetTypes())
                {
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
            }
        }

        /// <summary>
        /// Call this whenever you want to save a block's data - will not automatically save on it's own!
        /// </summary>
        /// <typeparam name="T">The block's Module</typeparam>
        /// <param name="block">The TankBlock that holds it</param>
        /// <param name="component">The Module to save</param>
        /// <returns>true if it saved</returns>
        public static bool SaveBlockToSave<T>(TankBlock block, T component)
        {
            try
            {
                if (RegisteredModules.Contains(component.GetType()))
                    return currentSave.SaveModuleState(block.visible, component);
                else
                    Debug.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: Please register your Assembly(.dll) with class " + component.GetType() + " in RegisterSaveSystem() first before calling this.");
            }
            catch (Exception e)
            {
                Debug.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: FAILIURE IN OPERATION! " + e);
            }
            return false;
        }

        /// <summary>
        /// Call this whenever you want to load a block's data for use in a Mod
        /// </summary>
        /// <typeparam name="T">The block's Module</typeparam>
        /// <param name="block">The TankBlock that holds it</param>
        /// <param name="component">The Module to save</param>
        /// <returns>true if it loaded</returns>
        public static bool LoadBlockFromSave<T>(TankBlock block, T component)
        {
            try
            {
                if (RegisteredModules.Contains(component.GetType()))
                    return currentSave.SetModuleStateFromSave(block.visible, component);
                else
                    Debug.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: Please register your Assembly(.dll) with class " + component.GetType() + " in RegisterSaveSystem() first before calling this.");
            }
            catch
            {
                Debug.LogError("SafeSaves: ManSafeSaves - OnBlockSerialization: FAILIURE IN OPERATION!");
            }
            return false;
        }


        private static string SerializeFromManager(bool defaultState = false)
        {
            return JsonConvert.SerializeObject(SaveToFileFormatting(defaultState), Formatting.None, JSONSaver);
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
                Debug.Log("SafeSaves: Resetting SafeSave for new save instance...");
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
                Debug.Log("SafeSaves: ManSafeSaves - Save is corrupted!");
                currentSave = new SafeSave();
                return;
            }
            currentSave = save;
            currentSave.LoadStateALL();
            onLoading.Send(false);
        }



        public static void LoadDataAutomatic()
        {
            try
            {
                string saveName = Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false);
                LoadData(saveName, ManGameMode.inst.GetCurrentGameMode());
                //Debug.Log("SafeSaves: SaveManSubMissions - LoadDataAutomatic: Loaded save " + saveName + " successfully");
            }
            catch
            {
                Debug.LogError("SafeSaves: ManSafeSaves - LoadDataAutomatic: FAILIURE IN MAJOR OPERATION!");
            }
        }
        public static void SaveDataAutomatic()
        {
            try
            {
                string saveName = Singleton.Manager<ManSaveGame>.inst.GetCurrentSaveName(false);
                SaveData(saveName, ManGameMode.inst.GetCurrentGameMode());
                //Debug.Log("SafeSaves: SaveManSubMissions - SaveDataAutomatic: Saved save " + saveName + " successfully");
            }
            catch
            {
                Debug.LogError("SafeSaves: ManSafeSaves - SaveDataAutomatic: FAILIURE IN MAJOR OPERATION!");
            }
        }


        public static void LoadData(string saveName, string altDirectory)
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
                            Debug.Log("SafeSaves: Loaded " + compressFileName + " for " + saveName + " successfully.");
                        }
                        else if (File.Exists(destination + ".json"))
                        {
                            string output = "";
                            output = File.ReadAllText(destination + ".json");

                            DeserializeToManager(output);
                            Debug.Log("SafeSaves: Loaded SafeSave.json for " + saveName + " successfully.");
                        }
                    }
                    else
                    {
                        if (File.Exists(destination + ".json"))
                        {
                            string output = "";
                            output = File.ReadAllText(destination + ".json");

                            DeserializeToManager(output);
                            Debug.Log("SafeSaves: Loaded SafeSave.json for " + saveName + " successfully.");
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
                            Debug.Log("SafeSaves: Loaded " + compressFileName + " for " + saveName + " successfully.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("SafeSaves: Could not load contents of MissionSave.json/.SMSAV for " + saveName + "!");
                    Debug.Log(e);
                    return;
                }
                return;
            }
            catch
            {
                try
                {
                    File.WriteAllText(destination + ".json", SerializeFromManager(true));
                    Debug.Log("SafeSaves: Created new SafeSave.json for " + saveName + " successfully.");
                    return;
                }
                catch
                {
                    Debug.Log("SafeSaves: Could not read SafeSave.json for " + saveName + ".  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }
            }
        }

        public static void SaveData(string saveName, string altDirectory)
        {
            if (IgnoreSaving)
                return;
            Debug.Log("SafeSaves: Setting up template reference...");
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
                    Debug.Log("SafeSaves: Saved " + compressFileName + " for " + saveName + " successfully.");
                }
                else
                {
                    File.WriteAllText(destination + ".json", SerializeFromManager());
                    CleanUpCache();
                    Debug.Log("SafeSaves: Saved SafeSave.json for " + saveName + " successfully.");
                }
            }
            catch
            {
                Debug.LogError("SafeSaves: Could not save SafeSave.json/" + compressFileName + " for " + saveName + ".  \n   This could be due to a bug with this mod or file permissions.");
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
                Debug.Log("SafeSaves: Generating " + name + " folder.");
                try
                {
                    Directory.CreateDirectory(DirectoryIn);
                    Debug.Log("SafeSaves: Made new " + name + " folder successfully.");
                }
                catch
                {
                    Debug.LogError("SafeSaves: Could not create new " + name + " folder.  \n   This could be due to a bug with this mod or file permissions.");
                    return;
                }
            }
        }

    }
}
