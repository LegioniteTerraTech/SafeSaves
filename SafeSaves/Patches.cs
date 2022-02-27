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
        /// <summary>
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
        public static bool SerializeToSafe<T>(this T inst) where T : Module
        {
            return ManSafeSaves.SaveBlockToSave(inst.GetComponent<TankBlock>(), inst);
        }

        /// <summary>
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
        public static bool DeserializeFromSafe<T>(this T inst) where T : Module
        {
            return ManSafeSaves.LoadBlockFromSave(inst.GetComponent<TankBlock>(), inst);
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
