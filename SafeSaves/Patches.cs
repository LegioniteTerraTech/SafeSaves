using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            return ManSafeSaves.SaveBlockToSave(inst.GetComponent<TankBlock>(), inst);
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
            return ManSafeSaves.SaveBlockComplexFieldToSave(inst.GetComponent<TankBlock>(), inst, Field);
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
            return ManSafeSaves.LoadBlockFromSave(inst.GetComponent<TankBlock>(), inst);
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
            return ManSafeSaves.LoadBlockComplexFieldFromSave(inst.GetComponent<TankBlock>(), inst, ref Field);
        }
    }

#if STEAM
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
            Debug.Log("KickStartSafeSaves - INITEarly");
            if (oInst == null)
            {
                oInst = this;
                ManSafeSaves.Init();
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
        [HarmonyPatch(typeof(ModeAttract))]
        [HarmonyPatch("SetupTechs")]// Setup main menu techs
        internal static class Subscribe
        {
            private static void Postfix()
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
                Debug.Log("SafeSaves: Saving!");
                ManSafeSaves.SaveData(saveName, ManGameMode.inst.GetCurrentGameMode());
            }
        }
    }
}
