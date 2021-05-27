using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine;


namespace DroneMod.src
{
    public class Patches
    {
        /* [HarmonyPatch(typeof(ManVisible), "Start")]
        public static class PatchExplosionHits
        {
            public static readonly FieldInfo VisiblePickerMaskNoTechs = typeof(ManVisible).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(field =>
                        field.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)) &&
                        (field.DeclaringType == typeof(ManVisible).GetProperty("VisiblePickerMaskNoTechs").DeclaringType) &&
                        field.FieldType.IsAssignableFrom(typeof(ManVisible).GetProperty("VisiblePickerMaskNoTechs").PropertyType) &&
                        field.Name.StartsWith("<" + typeof(ManVisible).GetProperty("VisiblePickerMaskNoTechs").Name + ">")
                    );

            public static void Postfix(ref ManVisible __instance)
            {
                VisiblePickerMaskNoTechs.SetValue(__instance, __instance.VisiblePickerMaskNoTechs | Globals.inst.layerContainer.mask);
            }
        } */
        /* [HarmonyPatch(typeof(ManDamage), "DealDamage", new Type[] { typeof(Damageable), typeof(float), typeof(ManDamage.DamageType), typeof(Component), typeof(Tank), typeof(Vector3), typeof(Vector3), typeof(float), typeof(float)})]
        public static class PatchLog
        {
            public static void Postfix(ref Damageable damageTarget, ref float damage)
            {
                Console.WriteLine($"ATTEMPTING TO DEAL {damage} DAMAGE TO {damageTarget.name}");
            }
        } */

        /* [HarmonyPatch(typeof(ManDamage), "DealDamage", new Type[] { typeof(ManDamage.DamageInfo), typeof(Damageable) })]
        public static class PatchDamage
        {
            private static readonly FieldInfo m_PendingDamage = typeof(ManDamage).GetField("m_PendingDamage", BindingFlags.Instance | BindingFlags.NonPublic);

            public static void Postfix(ref ManDamage.DamageInfo damageInfo, ref Damageable damageableTarget)
            {
                if (IsDrone(damageableTarget))
                {
                    List<object> pendingDamageEvents = (List<object>) m_PendingDamage.GetValue(Singleton.Manager<ManDamage>.inst);
                }
            }
        } */
        [HarmonyPatch(typeof(TargetAimer), "UpdateTarget")]
        public static class PatchTargeting
        {
            public static readonly FieldInfo m_TargetPosition = typeof(TargetAimer).GetField("m_TargetPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo Target = typeof(TargetAimer).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(field =>
                            field.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)) &&
                            (field.DeclaringType == typeof(TargetAimer).GetProperty("Target").DeclaringType) &&
                            field.FieldType.IsAssignableFrom(typeof(TargetAimer).GetProperty("Target").PropertyType) &&
                            field.Name.StartsWith("<" + typeof(TargetAimer).GetProperty("Target").Name + ">")
                        );
            public static readonly FieldInfo m_Block = typeof(TargetAimer).GetField("m_Block", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_ChangeTargetTimeout = typeof(TargetAimer).GetField("m_ChangeTargetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_ChangeTargetInteval = typeof(TargetAimer).GetField("m_ChangeTargetInteval", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            public static bool Prefix(ref TargetAimer __instance)
            {
                Drone drone = __instance.GetComponentInParents<Drone>(true);
                if (drone)
                {
                    Visible target = drone.hangar ? drone.hangar.aimer.Target : null;
                    Target.SetValue(__instance, target);

                    if (target.IsNotNull())
                    {
                        m_TargetPosition.SetValue(__instance, target.GetAimPoint(__instance.transform.position));
                    }

                    return false;
                }
                return true;
            }
        }

        /* [HarmonyPatch(typeof(Damageable), "TryToDamage")]
        public static class Logger
        {
            public static void Postfix(ref Damageable __instance, ref ManDamage.DamageInfo info, ref bool actuallyDealDamage)
            {
                if (IsDrone(__instance))
                {
                    Console.WriteLine($"ATTEMPTING TO DEAL {info.Damage} DAMAGE TO {__instance.gameObject.name}, TEST? {actuallyDealDamage} - CURRENT HEALTH: {__instance.Health}");
                }
            }
        } */

        [HarmonyPatch(typeof(ManPurchases), "StoreTechToInventory")]
        public static class PatchSCUTech
        {
            public static bool Prefix(ref TrackedVisible tv)
            {
                if (tv.visible.IsNotNull() && tv.visible.tank.IsNotNull())
                {
                    Tank tank = tv.visible.tank;
                    Drone drone = tank.GetComponent<Drone>();
                    if (drone)
                    {
                        drone.DeregisterDrone();
                        drone.OnLifetimeEnd();
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TechData), "SaveTech")]
        public static class PatchSnapshot
        {
            public static bool Prefix(ref Tank tech)
            {
                Drone drone = tech.GetComponent<Drone>();
                if (drone)
                {
                    Console.WriteLine("PATCH SAVED US");
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Visible), "GetAimPoint")]
        public static class PatchAimPoint
        {
            public static bool Prefix(ref Visible __instance, ref Vector3 __result)
            {
                if (IsDrone(__instance))
                {
                    __result = __instance.tank.transform.position;
                    return false;
                }
                return true;
            }
        }

        public static bool IsDrone(Visible visible)
        {
            if (visible != null && visible.m_ItemType != null && visible.m_ItemType.ObjectType == ObjectTypes.Vehicle)
            {
                Tank tank = visible.tank;
                if (tank)
                {
                    Drone drone = tank.GetComponent<Drone>();
                    if (drone)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsDrone(Damageable damageable)
        {
            Drone drone = damageable.GetComponent<Drone>();
            if (drone)
            {
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(ManWorld), "Update")]
        public static class PatchDroneStaticUpdate
        {
            public static void Postfix()
            {
                Drone.StaticUpdate();
            }
        }

        [HarmonyPatch(typeof(TankControl), "GetWeaponTargetLocation")]
        public static class PatchTargeting2
        {
            public static bool Prefix(ref TankControl __instance, ref Vector3 __result)
            {
                Drone drone = __instance.Tech.GetComponent<Drone>();
                if (drone)
                {
                    __result = __instance.Tech.rbody.transform.position;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ManTechs), "CheckSleepRange")]
        public static class PatchSleep
        {
            private static readonly FieldInfo m_SleepingTechs = typeof(ManTechs).GetField("m_SleepingTechs", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly MethodInfo SleepingTechRecycled = typeof(ManTechs).GetMethod("SleepingTechRecycled", BindingFlags.Instance | BindingFlags.NonPublic);

            public static void Postfix(ref ManTechs __instance, ref Tank tech)
            {
                if (tech.IsSleeping)
                {
                    Drone drone = tech.GetComponent<Drone>();
                    if (drone)
                    {
                        List<Tank> tanks = (List<Tank>)m_SleepingTechs.GetValue(__instance);
                        tanks.Remove(tech);
                        tech.visible.RecycledEvent.Unsubscribe((Action<Visible>)Delegate.CreateDelegate(typeof(Action<Visible>), __instance, SleepingTechRecycled));
                        tech.SetSleeping(false);
                        drone.Destroy();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Tank), "FixedUpdate")]
        public static class PatchDronePhysics
        {
            public static bool Prefix(ref Tank __instance)
            {
                Drone drone = __instance.GetComponent<Drone>();
                if (drone)
                {
                    drone.rbody.drag = 0.0f;
                    drone.rbody.angularDrag = 0.0f;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(WorldTile), "StoreLoadedVisible")]
        public static class PreventStoreDrone
        {
            public static bool Prefix(ref WorldTile __instance, ref Visible visible)
            {
                if (visible.m_ItemType.ObjectType == ObjectTypes.Vehicle)
                {
                    Tank tank = visible.tank;
                    Drone drone = tank.GetComponent<Drone>();
                    if (drone)
                    {
                        drone.DeregisterDrone();
                        drone.OnLifetimeEnd();
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ManSaveGame.StoredTile), "StoreVisibles")]
        public static class PreventStoreDrone2
        {
            public static bool Prefix(ref ManSaveGame.StoredTile __instance, ref Dictionary<int, Visible>[] visiblesOnTile, ref List<ManSaveGame.StoredVisible> storedVisiblesOnTile)
            {
                foreach (Dictionary<int, Visible> visibleDict in visiblesOnTile)
                {
                    if (visibleDict != null)
                    {
                        List<int> toRemove = visibleDict
                            .Where(pair => pair.Value == null || IsDrone(pair.Value))
                            .Select(pair => pair.Key)
                            .ToList();
                        foreach (int key in toRemove)
                        {
                            visibleDict.Remove(key);
                        }
                    }
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ManSaveGame), "CreateStoredVisible")]
        public static class PreventStoreDrone3
        {
            public static bool Prefix(ref Visible visible, ref ManSaveGame.StoredVisible __result)
            {
                if (visible.m_ItemType.ObjectType == ObjectTypes.Vehicle)
                {
                    Tank tank = visible.tank;
                    Drone drone = tank.GetComponent<Drone>();
                    if (drone)
                    {
                        drone.DeregisterDrone();
                        drone.OnLifetimeEnd();
                        __result = null;
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
