using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TargetAimer))]
    [RequireComponent(typeof(ModuleWeapon))]
    public class ModuleHangar : Module, IModuleWeapon
    {
        [SerializeField]
        public Drone m_DronePrefab;
        [SerializeField]
        public Transform m_FireTransform;
        [SerializeField]
        public int m_MaxDrones = 5;
        [SerializeField]
        public float m_MaxRange = 500.0f;
        [SerializeField]
        public float m_LaunchVelocity = 100.0f;
        [SerializeField]
        public float m_ShotCooldown = 1.0f;

        private float m_ShotTimer;
        private float m_DetachTimeStamp;

        [SerializeField]
        [HideInInspector]
        public TargetAimer aimer;

        [SerializeField]
        [HideInInspector]
        public ModuleWeapon m_Weapon;

        private bool debug = false;

        private bool m_CachedHasClearLineOfFire;
        private int m_CachedClearLineOfFireFrameIndex = -1;

        private List<Drone> activeDrones;
        private List<int> emptySlots;

        private void DebugPrint(string message)
        {
            if (this.debug)
            {
                Console.WriteLine("[DM] " + message);
            }
        }

        // module finding
        private void PrePool()
        {
            this.DebugPrint("PrePool");
            this.aimer = this.GetComponent<TargetAimer>();
            this.m_Weapon = this.GetComponent<ModuleWeapon>();

            // if fireTransform is not set, set it to base transform
            if (!this.m_FireTransform)
            {
                this.m_FireTransform = this.transform;
            }
        }

        // event subscriptions
        private void OnPool()
        {
            this.DebugPrint("OnPool");
            base.block.AttachEvent.Subscribe(new Action(this.OnAttach));
            base.block.DetachEvent.Subscribe(new Action(this.OnDetach));
        }

        // spawn preparation
        private void OnSpawn()
        {
            this.DebugPrint("OnSpawn");
            this.activeDrones = new List<Drone>(this.m_MaxDrones);

        }

        // reset to factory
        private void OnRecycle()
        {
            this.DebugPrint("OnRecycle");
            foreach (Drone drone in this.activeDrones)
            {
                drone.Shooter = null;
                drone.OnLifetimeEnd();
            }
            this.activeDrones.Clear();
            this.activeDrones = null;
        }

        private void OnAttach()
        {
            float num = Singleton.Manager<ManGameMode>.inst.GetCurrentModeRunningTime() - this.m_DetachTimeStamp;
            this.m_ShotTimer = Math.Max(Mathf.Epsilon, this.m_ShotTimer - num);
            this.m_DetachTimeStamp = 0f;
        }

        private void OnDetach()
        {
            this.m_DetachTimeStamp = Singleton.Manager<ManGameMode>.inst.GetCurrentModeRunningTime();
            foreach (Drone drone in this.activeDrones)
            {
                drone.Shooter = null;
                drone.OnLifetimeEnd();
            }
            this.activeDrones.Clear();
        }

        public void RegisterDroneDestruction(Drone drone)
        {
            int slot = drone.slot;
            this.activeDrones.Remove(drone);
        }

        public bool AimWithTrajectory()
        {
            return false;
        }

        public bool Deploy(bool deploy)
        {
            return true;
        }

        public bool FiringObstructed()
        {
            int frameCount = Time.frameCount;
            if (this.m_CachedClearLineOfFireFrameIndex == frameCount)
            {
                if (this.m_CachedHasClearLineOfFire)
                {
                    this.DebugPrint($"Firing Obstructed - Cached");
                }
                return this.m_CachedHasClearLineOfFire;
            }
            if (this.block.tank == null)
            {
                this.DebugPrint($"Firing Obstructed - null tank");
                return false;
            }
            bool raycastHasHit = false;
            float num = Mathf.Max(this.block.tank.blockBounds.size.magnitude, 1f);
            Vector3 position = this.m_FireTransform.position;
            RaycastHit raycastHit;
            if (Physics.Raycast(position, this.m_FireTransform.forward, out raycastHit, num, Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) && raycastHit.rigidbody == this.block.tank.rbody)
            {
                this.DebugPrint($"RAYCAST HIT TANK");
                raycastHasHit = true;
            }
            if (raycastHasHit)
            {
                Vector3 v = this.block.tank.trans.InverseTransformPoint(position);
                TankBlock blockAtPosition = this.block.tank.blockman.GetBlockAtPosition(v);
                if (blockAtPosition && blockAtPosition != this.block)
                {
                    foreach (ColliderSwapper.ColliderSwapperEntry colliderSwapperEntry in blockAtPosition.visible.ColliderSwapper.AllColliders)
                    {
                        if (colliderSwapperEntry.collisionWhenAttached)
                        {
                            Collider collider = colliderSwapperEntry.collider;
                            if (collider.bounds.Contains(position))
                            {
                                Ray ray = new Ray(position - this.transform.up * num, this.transform.up);
                                if (collider.Raycast(ray, out raycastHit, num))
                                {
                                    this.DebugPrint($"RAYCAST ENTRY WITHIN COLLIDER");
                                    raycastHasHit = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            this.m_CachedClearLineOfFireFrameIndex = frameCount;
            this.m_CachedHasClearLineOfFire = raycastHasHit;
            if (raycastHasHit)
            {
                this.DebugPrint($"Firing Obstructed - raycast hit");
            }
            return raycastHasHit;
        }

        public float GetFireRateFraction()
        {
            return 1.0f;
        }

        public Transform GetFireTransform()
        {
            return this.m_FireTransform;
        }

        public float GetRange()
        {
            return this.m_MaxRange;
        }

        public float GetVelocity()
        {
            return this.m_LaunchVelocity;
        }

        public bool IsAimingAtFloor(float limitedAngle)
        {
            return false;
        }

        public bool PrepareFiring(bool prepareFiring)
        {
            this.DebugPrint($"[DM] PrepareFiring {prepareFiring}");
            return prepareFiring;
        }

        public int ProcessFiring(bool firing)
        {
            this.DebugPrint($"[DM] ProcessFiring {firing}");
            if (this.m_ShotTimer > 0f && this.activeDrones.Count < this.m_MaxDrones)
            {
                this.m_ShotTimer -= Time.deltaTime;
            }

            if (firing)
            {
                this.DebugPrint("LAUNCHING DRONE");
                bool needSendLaunchMessage = false;
                d.Assert(this.block.tank != null);
                NetTech netTech = this.block.tank.netTech;
                if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && netTech.IsNotNull())
                {
                    if (netTech.NetPlayer.IsNotNull())
                    {
                        needSendLaunchMessage = netTech.NetPlayer.IsActuallyLocalPlayer;
                    }
                    else if (Singleton.Manager<ManNetwork>.inst.IsServer)
                    {
                        needSendLaunchMessage = true;
                    }
                }

                Vector3 position = this.m_FireTransform.position;
                Vector3 forward = this.m_FireTransform.forward;
                Drone drone = this.m_DronePrefab.Spawn(null, position, this.transform.rotation);
                drone.Launch(0, this, forward, false);
                this.activeDrones.Add(drone);
                this.m_ShotTimer = this.m_ShotCooldown;
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public bool ReadyToFire()
        {
            this.DebugPrint("ReadyToFire");
            return this.m_ShotTimer <= Mathf.Epsilon && this.activeDrones.Count < this.m_MaxDrones;
        }
    }
}
