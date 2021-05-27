using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using UnityEngine;
using Harmony;
using DroneMod.src.DroneModules;


namespace DroneMod.src
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(Visible))]
    [RequireComponent(typeof(Tank))]
    [RequireComponent(typeof(DroneControl))]
    [RequireComponent(typeof(DroneWeaponsController))]
    public class Drone : MonoBehaviour, IWorldTreadmill
    {
        [SerializeField]
        public string droneName = "Drone";

        #region Explosion/lifetime params
        [SerializeField]
        public Explosion m_Explosion;
        [SerializeField]
        public bool m_ExplodeAfterLifetime = true;
        [SerializeField]
        public bool m_ExplodeOnDeath = true;
        [SerializeField]
        public float m_LifeTime = 20.0f;
        [SerializeField]
        public bool m_ExplodeOnTerrain = true;
        [SerializeField]
        public bool m_ExplodeOnTank = true;
        [SerializeField]
        public float mass = 1.0f;
        [SerializeField]
        public float m_LaunchTime = 1.0f;
        private float m_LaunchTimeout;

        // managed by module
        private float m_DestroyTimeout;
        private bool m_NeedsReturningToPool;
        #endregion

        #region Health Params
        [SerializeField]
        public float maxHealth = 100.0f;
        [SerializeField]
        public float armorHealth = 100.0f;
        [SerializeField]
        public float shieldHealth = 100.0f;
        [SerializeField]
        public Vector3 shieldEllipse = Vector3.zero;
        #endregion

        #region Detected params - module managed
        [SerializeField]
        [HideInInspector]
        private TrailRenderer m_Trail;
        [SerializeField]
        [HideInInspector]
        private SmokeTrail m_Smoke;

        public ModuleHangar hangar { get; private set; }

        [SerializeField]
        [HideInInspector]
        private Damageable damageable;
        [SerializeField]
        [HideInInspector]
        private Visible visible;

        [SerializeField]
        [HideInInspector]
        protected MeshRenderer m_MeshRenderer;

        [SerializeField]
        [HideInInspector]
        private DroneControl controller;

        [SerializeField]
        [HideInInspector]
        private DroneWeaponsController weaponsController;
        #endregion

        [SerializeField]
        public Tank TankSelf;
        public Tank Shooter { get; internal set; }
        public Transform trans { get; private set; }
        public Rigidbody rbody { get; private set; }
        public int slot { get; private set; }

        private bool debug = false;
        private void DebugPrint(string message)
        {
            if (this.debug)
            {
                Console.WriteLine("[DM] " + message);
            }
        }

        // replay rounds is for when client needs to replay something the server already did - in which case we do no variation
        public virtual void Launch(int slot, ModuleHangar hangar, Vector3 fireDirection, bool ReplayLaunch = false)
        {
            this.hangar = hangar;
            this.slot = slot;
            if (hangar && hangar.block)
            {
                this.Shooter = hangar.block.tank;
                this.TankSelf.SetTeam(this.Shooter.Team);
            }

            Vector3 vector = fireDirection.normalized * this.hangar.GetVelocity();
            if (!ReplayLaunch)
            {
                
            }
            else
            {
                Console.WriteLine("NONONONONO");
                vector = fireDirection;
            }

            if (this.Shooter != null)
            {
                vector += this.Shooter.rbody.velocity;
            }
            Console.WriteLine($"[DM] - Launching drone with velocity {vector}");
            this.rbody.velocity = vector;
            this.rbody.rotation = Quaternion.LookRotation(vector);

            this.rbody.useGravity = this.controller.droneGravity;
            this.TankSelf.EnableGravity = this.controller.droneGravity;

            if (this.m_LifeTime > 0f)
            {
                this.SetDroneForDelayedDestruction(this.m_LifeTime);
            }
            this.m_LaunchTimeout = this.m_LaunchTime;

            if (this.weaponsController)
            {
                this.weaponsController.Launch(hangar);
            }

            this.controller.Launch(hangar);
        }

        // Rotation
        private void OnUpdateRotation()
        {
            if (this.rbody.velocity != Vector3.zero)
            {
                this.trans.rotation = Quaternion.LookRotation(this.rbody.velocity);
            }
        }

        #region Damage handlers
        private bool OnRejectDamage(ManDamage.DamageInfo info, bool actuallyDealDamage)
        {
            if (this.Shooter && !Singleton.Manager<ManGameMode>.inst.IsFriendlyFireEnabled() && info.SourceTank && !info.SourceTank.IsEnemy(this.Shooter.Team))
            {
                // Console.WriteLine("REJECT DAMAGE!");
                return true;
            }
            return false;
        }
        private void OnFatalDamage(Damageable damageable, ManDamage.DamageInfo info)
        {
            if (this.m_Explosion)
            {
                this.SpawnExplosion(this.transform.position, damageable);
            }
            this.DeregisterDrone();
            this.Recycle(false);
        }
        private void SpawnExplosion(Vector3 explodePos, Damageable directHitTarget = null)
        {
            if (this.m_Explosion)
            {
                Explosion component = this.m_Explosion.Spawn(Singleton.dynamicContainer, explodePos).GetComponent<Explosion>();
                if (component != null)
                {
                    component.SetDamageSource(this.Shooter);
                    component.SetDirectHitTarget(directHitTarget);
                }
            }
        }
        private void SetDroneForDelayedDestruction(float timeout)
        {
            if (this.m_DestroyTimeout < 0f)
            {
                Drone.s_AliveList.Add(this);
            }
            this.m_DestroyTimeout = timeout;
        }
        public virtual void OnLifetimeEnd()
        {
            d.Assert(base.gameObject.activeInHierarchy);
            if (this.m_ExplodeAfterLifetime && this.m_Explosion)
            {
                this.SpawnExplosion(this.trans.position, null);
            }
            this.Destroy();
        }
        private void OnDamaged(ManDamage.DamageInfo info)
        {
            // Console.WriteLine($"[DM] Drone Damaged: DMG Dealt: {info.Damage}, HP Remaining: {this.damageable.Health}");
        }
        #endregion

        #region Component Pool handlers
        // module finding
        private void PrePool()
        {
            this.controller = base.GetComponent<DroneControl>();
            this.weaponsController = base.GetComponent<DroneWeaponsController>();

            this.TankSelf = base.GetComponent<Tank>();
            this.DebugPrint("Rbody initialization set");

            this.visible = this.GetComponentInChildren<Visible>();
            this.damageable = this.GetComponentInChildren<Damageable>();

            this.m_MeshRenderer = base.GetComponentInChildren<MeshRenderer>();
            this.m_Trail = base.GetComponent<TrailRenderer>();
            this.m_Smoke = base.GetComponent<SmokeTrail>();

            this.gameObject.layer = Globals.inst.layerContainer;
        }

        // event subscriptions
        private void OnPool()
        {
            this.DebugPrint("ModuleDrone OnPool");
            this.m_NeedsReturningToPool = false;
            this.trans = base.transform;

            this.rbody = this.TankSelf.rbody;
            if (!this.rbody)
            {
                this.rbody = this.gameObject.AddComponent<Rigidbody>();
            }
            this.DebugPrint("Rbody setup");

            this.rbody.mass = this.mass;
            this.DebugPrint("Rbody mass set");

            this.DebugPrint("RotationUpdater set");

            this.damageable.SetMaxHealth((float)this.maxHealth);
            this.damageable.destroyOnDeath = false;
            this.damageable.deathEvent.Subscribe(new Action<Damageable, ManDamage.DamageInfo>(this.OnFatalDamage));
            this.damageable.damageEvent.Subscribe(new Action<ManDamage.DamageInfo>(this.OnDamaged)); // this primarily handles netcode. No netcode
            this.damageable.SetRejectDamageHandler(new Func<ManDamage.DamageInfo, bool, bool>(this.OnRejectDamage));
            // this.damageable.HealEvent.Subscribe(new Action<float>(this.OnHealed)); // no healing
            this.DebugPrint("Damageable stuff set");

            this.DebugPrint("Targeting stuff set");
        }

        private static readonly MethodInfo CreateTrackedVisibleForVehicle = typeof(ManSpawn).GetMethod("CreateTrackedVisibleForVehicle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // spawn preparation
        private void OnSpawn()
        {
            this.TankSelf.SetName(this.droneName);
            this.damageable.SetMaxHealth(this.maxHealth);
            this.damageable.InitHealth(this.maxHealth);

            if (this.m_Trail)
            {
                Singleton.Manager<ManWorldTreadmill>.inst.AddListener(this);
            }

            this.m_DestroyTimeout = -1f;

            if (this.m_Smoke)
            {
                this.m_Smoke.enabled = true;
            }
            this.rbody.useGravity = false;
            this.m_NeedsReturningToPool = true;

            this.m_MeshRenderer.enabled = true;
            this.gameObject.SetActive(true);

            SphereCollider collider = this.GetComponentInChildren<SphereCollider>();
            Visible visible = Visible.FindVisibleUpwards(collider);
            d.Assert(visible == this.visible, "Not able to find visible properly");
            d.Assert(this.visible.damageable == this.damageable, "Not able to find damageable properly from visible");

            Damageable damageable = collider.GetComponentInParents<Damageable>(false);
            d.Assert(damageable == this.damageable, "Not able to find damageable properly from collider");
            
            // Make available to radar
            TrackedVisible trackedVisible = (TrackedVisible) CreateTrackedVisibleForVehicle.Invoke(Singleton.Manager<ManSpawn>.inst , new object[] { this.TankSelf.visible.ID, this.visible, this.transform.position, RadarTypes.Vehicle });
            Singleton.Manager<ManVisible>.inst.TrackVisible(trackedVisible, false);
            
            // setup base block
            //  this.self.blockman.AddBlockToTech(new TankBlock(), IntVector3.zero);
        }

        public void Destroy()
        {
            this.Recycle(true);
        }

        private void OnApplicationQuit()
        {
            this.m_NeedsReturningToPool = false;
        }

        private void OnDisable()
        {
            if (this.m_NeedsReturningToPool)
            {
                d.LogError("Drone " + base.gameObject.name + " being disabled before recycle! This shouldn't happen. Cleaning it up safely.");
                this.DeregisterDrone();
                this.Recycle(true);
            }
        }

        // reset to factory
        private void OnRecycle()
        {
            this.Shooter = null;
            this.m_NeedsReturningToPool = false;
            Singleton.Manager<ManVisible>.inst.StopTrackingVisible(this.visible.ID);
            // Singleton.Manager<ManBlockLimiter>.inst.RemoveTechByID(this.visible.ID);

            if (this.m_Trail)
            {
                Singleton.Manager<ManWorldTreadmill>.inst.RemoveListener(this);
                this.m_Trail.Clear();
            }
            if (this.m_DestroyTimeout > 0f)
            {
                Drone.s_AliveList.Remove(this);
            }
        }
        #endregion

        #region Collision handling
        // handle collisions
        private void HandleCollision(Vector3 hitPoint, Collider otherCollider, bool forceDestroy = false)
        {
            LayerMask layer = otherCollider.gameObject.layer;
            if (layer != Globals.inst.layerBullet && layer != Globals.inst.layerShieldPiercingBullet)
            {
                bool toDestroy = false;
                if (this.m_ExplodeOnTank && layer == Globals.inst.layerTank)
                {
                    toDestroy = true;
                    this.SpawnExplosion(hitPoint, null);
                }
                else if (this.m_ExplodeOnTerrain &&
                    (
                        layer == Globals.inst.layerTerrain ||
                        layer == Globals.inst.layerTerrainOnly ||
                        layer == Globals.inst.layerLandmark ||
                        layer == Globals.inst.layerScenery ||
                        layer == Globals.inst.layerSceneryCoarse ||
                        layer == Globals.inst.layerSceneryFader
                        )
                    )
                {
                    toDestroy = true;
                    this.SpawnExplosion(hitPoint, null);
                }

                if (toDestroy)
                {
                    this.DeregisterDrone();
                    this.Destroy();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ContactPoint[] contacts = collision.contacts;
            if (contacts.Length == 0)
            {
                return;
            }
            ContactPoint contactPoint = contacts[0];
            this.HandleCollision(contactPoint.point, collision.collider, false);
        }
        #endregion

        private void FixedUpdate()
        {
            if (this.m_LaunchTimeout <= 0.0f)
            {
                if (this.rbody.useGravity)
                {
                    Console.WriteLine("GRAVITY IS ENABLED");
                }

                Visible target;
                if (this.hangar.aimer.HasTarget)
                {
                    target = this.hangar.aimer.Target;
                }
                else
                {
                    target = this.hangar.block.tank.visible;
                }


                // Tank will update tile cache for us
                // Singleton.Manager<ManWorld>.inst.TileManager.UpdateTileCache(this.visible, false);
                this.controller.Control(target, this.hangar.aimer.HasTarget);
            }
            else
            {
                this.controller.HandleLaunch();
            }
        }

        private void Update()
        {
            if (this.Shooter)
            {
                if (this.m_LaunchTimeout > 0)
                {
                    this.m_LaunchTimeout -= Time.deltaTime;
                }

                if (!this.hangar || (this.Shooter.transform.position - this.transform.position).magnitude > this.hangar.m_MaxRange)
                {
                    this.DeregisterDrone();
                    this.OnLifetimeEnd();
                }
            }
            else
            {
                this.DeregisterDrone();
                this.OnLifetimeEnd();
            }
        }

        public void OnMoveWorldOrigin(IntVector3 amountToMove)
        {
            if (this.m_Trail && this.m_Trail.enabled && this.m_Trail.positionCount > 0)
            {
                Vector3 b = new Vector3((float)amountToMove.x, (float)amountToMove.y, (float)amountToMove.z);
                Vector3[] array = new Vector3[this.m_Trail.positionCount];
                this.m_Trail.GetPositions(array);
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] += b;
                }
                this.m_Trail.SetPositions(array);
            }
        }

        // Handle delayed destruction
        private bool UpdateInternal(float deltaTime)
        {
            if (this.m_DestroyTimeout > 0f)
            {
                this.m_DestroyTimeout -= deltaTime;
                if (this.m_DestroyTimeout < 0f)
                {
                    this.DeregisterDrone();
                    this.OnLifetimeEnd();
                    return true;
                }
            }
            return false;
        }
        public static void StaticUpdate()
        {
            int count = Drone.s_AliveList.Count;
            if (count > 0)
            {
                float deltaTime = Time.deltaTime;
                for (int i = count - 1; i >= 0; i--)
                {
                    if (Drone.s_AliveList[i].UpdateInternal(deltaTime))
                    {
                        Drone.s_AliveList.RemoveAt(i);
                    }
                }
            }
        }

        public void DeregisterDrone()
        {
            DroneWeapon[] weapons = this.GetComponentsInChildren<DroneWeapon>();
            foreach (DroneWeapon weapon in weapons)
            {
                weapon.UnSubscribe();
            }

            if (this.hangar)
            {
                this.hangar.RegisterDroneDestruction(this);
            }
        }

        private static List<Drone> s_AliveList = new List<Drone>(256);
    }
}
