using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace SafeSaves
{
    public static class SaveDataExts
    {
        // TECHS (handled automatically)
        /// <summary>
        /// This is only to extract SafeSave stored modules & Tank data for storage elsewhere.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <param name="inst">Called from the tank you want to fetch the serial for</param>
        /// <returns>The serial in a string of ALL saved blocks as well as the tank changes</returns>
        public static string GetSerialization(this Tank inst)
        {
            return ManSafeSaves.GetSerialOfTank(inst);
        }

        /// <summary>
        /// This is only to set the Tank and it's blocks on non-world save conditions.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <param name="inst">Called from the tank you want to fetch the serial for</param>
        /// <param name="serialToLoad">The serial in a string of ALL saved blocks as well as the tank changes.
        /// Set to null to purge the serialization.</param>
        public static void SetSerialization(this Tank inst, string serialToLoad)
        {
            try
            {
                ManSafeSaves.LoadSerialToTank(inst, serialToLoad);
            }
            catch { }
        }

        // BLOCKS
        /// <summary>
        /// ONLY HANDLES INTS
        /// Save the module to the save file. 
        /// Use within Module's OnSerialize.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <typeparam name="T">Any Valid Block Module</typeparam>
        /// <param name="inst">called from the module</param>
        /// <param name="module">The module you are using</param>
        /// <returns>true if it saved correctly</returns>
        public static bool SerializeToSafe<T>(this T inst) where T : MonoBehaviour
        {
            try
            {
                if (ManSafeSaves.GetRegisteredModules().Contains(typeof(T)))
                    return ManSafeSaves.SaveBlockToSave(inst.GetComponent<TankBlock>(), inst);
                else
                {
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - SerializeToSafe: Please register your Assembly(.dll) with class "
                        + typeof(T) + " in SafeSaves.ManSafeSaves.RegisterSaveSystem() first before calling this. "
                        + StackTraceUtility.ExtractStackTrace().ToString());

                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Trying to auto-register...");
                    ManSafeSaves.RegisterSaveSystem(Assembly.GetCallingAssembly());
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Auto-register successful...");
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - SerializeToSafe: FAILIURE IN OPERATION! " + e);
            }
            return false;
        }
        /// <summary>
        /// USE FOR EACH NON-INT FIELD
        /// Save the module to the save file. 
        /// Use within Module's OnSerialize.
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <typeparam name="T">Any Valid Block Module</typeparam>
        /// <param name="inst">called from the module</param>
        /// <param name="module">The module you are using</param>
        /// <returns>true if it saved correctly</returns>
        public static bool SerializeToSafeObject<T,C>(this T inst, C Field) where T : MonoBehaviour
        {
            try
            {
                if (ManSafeSaves.GetRegisteredModules().Contains(typeof(T)))
                {
                    return ManSafeSaves.SaveBlockComplexFieldToSave(inst.GetComponent<TankBlock>(), inst, Field);
                }
                else
                {
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - SerializeToSafeObject: Please register your Assembly(.dll) with class "
                        + typeof(T) + " in SafeSaves.ManSafeSaves.RegisterSaveSystem() first before calling this. "
                        + StackTraceUtility.ExtractStackTrace().ToString());

                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Trying to auto-register...");
                    ManSafeSaves.RegisterSaveSystem(Assembly.GetCallingAssembly());
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Auto-register successful...");
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - SerializeToSafeObject: FAILIURE IN OPERATION! \n" + e);
            }
            return false;
        }

        /// <summary>
        /// ONLY HANDLES INTS
        /// Load the module from the save file. 
        /// Use within Module's OnSerialize. 
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <typeparam name="T">Any Valid Block Module</typeparam>
        /// <param name="inst">called from the module</param>
        /// <param name="module">The module you are using</param>
        /// <returns>true if it loaded correctly</returns>
        public static bool DeserializeFromSafe<T>(this T inst) where T : MonoBehaviour
        {
            try
            {
                if (ManSafeSaves.GetRegisteredModules().Contains(typeof(T)))
                    return ManSafeSaves.LoadBlockFromSave(inst.GetComponent<TankBlock>(), inst);
                else
                {
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - DeserializeFromSafe: Please register your Assembly(.dll) with class "
                        + typeof(T) + " in SafeSaves.ManSafeSaves.RegisterSaveSystem() first before calling this. "
                        + StackTraceUtility.ExtractStackTrace().ToString());

                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Trying to auto-register...");
                    ManSafeSaves.RegisterSaveSystem(Assembly.GetCallingAssembly());
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Auto-register successful...");
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - DeserializeFromSafe: FAILIURE IN OPERATION! \n" + e);
            }
            return false;
        }

        /// <summary>
        /// USE FOR EACH NON-INT FIELD
        /// Load the module from the save file. 
        /// Use within Module's OnSerialize. 
        /// <para>
        /// MAKE SURE TO PUT THIS IN A TRY-CATCH BLOCK FOR MAXIMUM SAFETY
        /// </para>
        /// </summary>
        /// <typeparam name="T">Any Valid Block Module</typeparam>
        /// <param name="inst">called from the module</param>
        /// <param name="module">The module you are using</param>
        /// <returns>true if it loaded correctly</returns>
        public static bool DeserializeFromSafeObject<T,C>(this T inst, ref C Field) where T : MonoBehaviour
        {
            try
            {
                if (ManSafeSaves.GetRegisteredModules().Contains(typeof(T)))
                    return ManSafeSaves.LoadBlockComplexFieldFromSave(inst.GetComponent<TankBlock>(), inst, ref Field);
                else
                {
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - DeserializeFromSafeObject: Please register your Assembly(.dll) with class "
                        + typeof(T) + " in SafeSaves.ManSafeSaves.RegisterSaveSystem() first before calling this. "
                        + StackTraceUtility.ExtractStackTrace().ToString());

                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Trying to auto-register...");
                    ManSafeSaves.RegisterSaveSystem(Assembly.GetCallingAssembly());
                    DebugSafeSaves.Log("SafeSaves: ManSafeSaves - Auto-register successful...");
                }
            }
            catch (Exception e)
            {
                DebugSafeSaves.LogError("SafeSaves: ManSafeSaves - DeserializeFromSafeObject: FAILIURE IN OPERATION! " + e);
            }
            return false;
        }
    }

#if LONE
    public class KickStartSafeSaves : ModBase
    {

        internal static KickStartSafeSaves oInst;

        bool isInit = false;
        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void EarlyInit()
        {
            DebugSafeSaves.Log("KickStartSafeSaves - INITEarly");
            if (oInst == null)
            {
                oInst = this;
            }
        }
        public override void Init()
        {
            if (isInit)
                return;
            isInit = true;
            ManSafeSaves.ignoreSaving = false;
        }
        public override void DeInit()
        {
            if (!isInit)
                return;
            isInit = false;
            ManSafeSaves.ignoreSaving = true;
        }
    }
#endif

    public class Patches
    {
        [HarmonyPatch(typeof(Mode))]
        [HarmonyPatch("UpdateMode")]// Setup main menu techs
        internal static class Subscribe
        {
            private static void Prefix()
            {
                ManSafeSaves.Subscribe();
            }
        }
        [HarmonyPatch(typeof(ManSaveGame))]
        [HarmonyPatch("Save")]// SAAAAAVVE
        private static class SaveTheSaves
        {
            private static void Prefix(ref ManGameMode.GameType gameType, ref string saveName)
            {
                DebugSafeSaves.Log("SafeSaves: Saving!");
                ManSafeSaves.SaveData(saveName, ManGameMode.inst.GetCurrentGameMode());
            }
        }
    }
}
