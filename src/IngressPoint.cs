using System;
using System.Collections.Generic;
using System.Reflection;

using Harmony;
using UnityEngine;


namespace DroneMod.src
{
    public class IngressPoint
    {
        public static void Main()
        {
            Console.WriteLine("DRONE MOD INITIALIZE");
            Physics.IgnoreLayerCollision(Globals.inst.layerContainer, Globals.inst.layerContainer, true);
            Physics.IgnoreLayerCollision(Globals.inst.layerContainer, Globals.inst.layerLandmark, false);

            BlockCreation.CreateBlocks();

            HarmonyInstance.Create("flsoz.ttmm.dronemod.mod").PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
