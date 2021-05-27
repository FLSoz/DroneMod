using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DroneMod.src.DroneModules;
using Nuterra.BlockInjector;
using UnityEngine;
using System.Reflection;


namespace DroneMod.src
{
    internal class BlockCreation
    {
        public GameObject holder = new GameObject("Holder");

        private static readonly FieldInfo m_StrippedTankTypesList = typeof(ManSpawn).GetField("m_StrippedTankTypesList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_TankPrefab = typeof(ManSpawn).GetField("m_TankPrefab", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_RuntimePrefabsContainer = typeof(ManSpawn).GetField("m_RuntimePrefabsContainer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_Explosion = typeof(Projectile).GetField("m_Explosion", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo CreateStrippedTypesSet = typeof(ManSpawn).GetMethod("CreateStrippedTypesSet", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        #region BeamWeapon Fields
        private static readonly FieldInfo m_Range = typeof(BeamWeapon).GetField("m_Range", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_DamagePerSecond = typeof(BeamWeapon).GetField("m_DamagePerSecond", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_DamageType = typeof(BeamWeapon).GetField("m_DamageType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_FadeOutTime = typeof(BeamWeapon).GetField("m_FadeOutTime", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_BeamParticlesPrefab = typeof(BeamWeapon).GetField("m_BeamParticlesPrefab", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_HitParticlesPrefab = typeof(BeamWeapon).GetField("m_HitParticlesPrefab", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_BeamStartDistance = typeof(BeamWeapon).GetField("m_BeamStartDistance", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        #endregion

        #region CannonBarrel Fields
        private static readonly FieldInfo particles = typeof(CannonBarrel).GetField("particles", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo muzzleFlash = typeof(CannonBarrel).GetField("muzzleFlash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo casingEjectTransform = typeof(CannonBarrel).GetField("casingEjectTransform", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo projectileSpawnPoint = typeof(CannonBarrel).GetField("projectileSpawnPoint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo recoiler = typeof(CannonBarrel).GetField("recoiler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_FireSpinner = typeof(CannonBarrel).GetField("m_FireSpinner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo m_ShowParticlesOnAllQualitySettings = typeof(CannonBarrel).GetField("m_ShowParticlesOnAllQualitySettings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        #endregion

        internal static Mesh MeshFromBytes(string meshData)
        {
            return GameObjectJSON.MeshFromData(meshData);
        }

        internal static void AddMeshToObject(GameObject obj, Material mat, string meshData)
        {
            obj.AddComponent<MeshFilter>().sharedMesh = MeshFromBytes(meshData);
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        internal static void AddDroneToObject(GameObject obj, DroneModelParameters parameters)
        {
            GameObject drone = new GameObject("Drone Mesh") { };
            AddMeshToObject(drone, parameters.mat, parameters.meshData);

            drone.transform.parent = obj.transform;
            TransformParameters transformParameters = parameters.transformParameters;
            SetupTransform(drone.transform, transformParameters);
        }
        private static void StripComponents(GameObject item, HashSet<Type> strippedTypes)
        {
            Component[] components = item.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (strippedTypes.Contains(component.GetType()))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        public struct TransformParameters
        {
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;

            public static TransformParameters Default()
            {
                return new TransformParameters()
                {
                    localPosition = Vector3.zero,
                    localEulerAngles = Vector3.zero,
                    localScale = Vector3.one
                };
            }
        }
        public struct DroneParameters
        {
            public string droneName;
            public bool m_ExplodeAfterLifetime;
            public bool m_ExplodeOnDeath;
            public float m_LifeTime;
            public bool m_ExplodeOnTerrain;
            public bool m_ExplodeOnTank;
            public float mass;
            public float m_LaunchTime;
            public float maxHealth;
            public float armorHealth;
            public float shieldHealth;
            public Vector3 shieldEllipse;

            public static DroneParameters Default()
            {
                return new DroneParameters()
                {
                    droneName = "Drone",
                    m_ExplodeAfterLifetime = true,
                    m_ExplodeOnDeath = true,
                    m_LifeTime = 20.0f,
                    m_ExplodeOnTerrain = true,
                    m_ExplodeOnTank = true,
                    mass = 1.0f,
                    m_LaunchTime = 1.0f,
                    maxHealth = 100.0f,
                    armorHealth = 100.0f,
                    shieldHealth = 100.0f,
                    shieldEllipse = Vector3.zero
                };
            }
        }
        public struct DroneControllerParameters
        {
            public bool droneGravity;
            public int maxSpeed;
            public float acceleration;
            public float turnSpeed;
            public MovementMode movementMode;

            // circle/broadside parameters
            public float turnOutRadius; // At what distance do you start turning out again
            public float turnInRadius; // At what distance do you start turning in again

            // intercept parameters
            public bool intercept;
            public float muzzleVelocity;
            public bool useGravity;
            public Vector3 targetOffset;
            public float targetHeight;
            public bool alwaysMove;
            public float maxClimbAngle;

            public static DroneControllerParameters Default()
            {
                return new DroneControllerParameters()
                {
                    droneGravity = false,
                    maxSpeed = 100,
                    acceleration = 10.0f,
                    turnSpeed = 20.0f,
                    movementMode = MovementMode.Circle,
                    turnOutRadius = 50.0f,
                    turnInRadius = 100.0f,
                    intercept = false,
                    muzzleVelocity = 100.0f,
                    useGravity = false,
                    targetOffset = Vector3.zero,
                    targetHeight = 30.0f,
                    alwaysMove = true,
                    maxClimbAngle = 25.0f
                };
            }
        }
        public struct DroneModelParameters
        {
            public Material mat;
            public TransformParameters transformParameters;
            public string meshData;

            public static DroneModelParameters Default()
            {
                return new DroneModelParameters(null, "");
            }

            public DroneModelParameters(Material mat, string meshData)
            {
                this.transformParameters = TransformParameters.Default();
                this.mat = mat;
                this.meshData = meshData;
            }

            public DroneModelParameters(Material mat, string meshData, Vector3 scale)
            {
                this.transformParameters = TransformParameters.Default();
                this.transformParameters.localScale = scale;
                this.mat = mat;
                this.meshData = meshData;
            }
        }
        public struct DroneHangarParameters
        {
            public TransformParameters fireTransform;

            public int m_MaxDrones;
            public float m_MaxRange;
            public float m_LaunchVelocity;
            public float m_ShotCooldown;

            public static DroneHangarParameters Default()
            {
                return new DroneHangarParameters()
                {
                    fireTransform = TransformParameters.Default(),
                    m_MaxDrones = 5,
                    m_MaxRange = 500.0f,
                    m_LaunchVelocity = 100.0f,
                    m_ShotCooldown = 1.0f
                };
            }
        }

        internal static void SetupTransform(Transform transform, TransformParameters parameters)
        {
            transform.localPosition = parameters.localPosition;
            transform.localEulerAngles = parameters.localEulerAngles;
            transform.localScale = parameters.localScale;
        }
        internal static void SetupDroneParameters(Drone drone, DroneParameters parameters)
        {
            drone.name = drone.droneName;
            drone.droneName = parameters.droneName;
            drone.m_ExplodeAfterLifetime = parameters.m_ExplodeAfterLifetime;
            drone.m_ExplodeOnDeath = parameters.m_ExplodeOnDeath;
            drone.m_LifeTime = parameters.m_LifeTime;
            drone.m_ExplodeOnTerrain = parameters.m_ExplodeOnTerrain;
            drone.m_ExplodeOnTank = parameters.m_ExplodeOnTank;
            drone.mass = parameters.mass;
            drone.m_LaunchTime = parameters.m_LaunchTime;
            drone.maxHealth = parameters.maxHealth;
            drone.armorHealth = parameters.armorHealth;
            drone.shieldHealth = parameters.shieldHealth;
            drone.shieldEllipse = parameters.shieldEllipse;
        }
        internal static void SetupDroneControllerParameters(DroneControl controller, DroneControllerParameters parameters)
        {
            controller.droneGravity = parameters.droneGravity;
            controller.maxSpeed = parameters.maxSpeed;
            controller.acceleration = parameters.acceleration;
            controller.turnSpeed = parameters.turnSpeed;
            controller.movementMode = parameters.movementMode;
            controller.turnOutRadius = parameters.turnOutRadius;
            controller.turnInRadius = parameters.turnInRadius;
            controller.intercept = parameters.intercept;
            controller.muzzleVelocity = parameters.muzzleVelocity;
            controller.useGravity = parameters.useGravity;
            controller.targetOffset = parameters.targetOffset;
            controller.targetHeight = parameters.targetHeight;
            controller.alwaysMove = parameters.alwaysMove;
            controller.maxClimbAngle = parameters.maxClimbAngle;
        }
        internal static void SetupDroneHangarParameters(ModuleHangar hangar, DroneHangarParameters parameters)
        {
            hangar.m_MaxDrones = parameters.m_MaxDrones;
            hangar.m_MaxRange = parameters.m_MaxRange;
            hangar.m_LaunchVelocity = parameters.m_LaunchVelocity;
            hangar.m_ShotCooldown = parameters.m_ShotCooldown;

            // setup fire transform
            GameObject fireTransformObject = new GameObject("_launchLocation");
            Transform fireTransform = fireTransformObject.transform;
            fireTransform.parent = hangar.transform;
            SetupTransform(fireTransform, parameters.fireTransform);
            hangar.m_FireTransform = fireTransform;
        }

        public static Drone SetupDrone(DroneModelParameters modelParameters, DroneParameters droneParameters, DroneControllerParameters controllerParameters)
        {
            Transform originalTankPrefab = (Transform)m_TankPrefab.GetValue(Singleton.Manager<ManSpawn>.inst);
            List<string> typesList = (List<string>)m_StrippedTankTypesList.GetValue(Singleton.Manager<ManSpawn>.inst);
            HashSet<Type> strippedTypes = (HashSet<Type>)CreateStrippedTypesSet.Invoke(null, new object[] { typesList });

            // Drone prefab
            Transform transform = UnityEngine.Object.Instantiate<Transform>(originalTankPrefab);
            StripComponents(transform.gameObject, strippedTypes);
            transform.name = droneParameters.droneName;
            Transform container = (Transform)m_RuntimePrefabsContainer.GetValue(Singleton.Manager<ManSpawn>.inst);
            transform.SetParent(container);
            transform.gameObject.layer = Globals.inst.layerTank;

            Tank tank = transform.GetComponent<Tank>();
            tank.SetName(droneParameters.droneName);

            GameObject dronePrefab = transform.gameObject;
            AddDroneToObject(dronePrefab, modelParameters);

            Damageable damageable = dronePrefab.AddComponent<Damageable>();
            damageable.DamageableType = ManDamage.DamageableType.Standard;
            damageable.SetInvulnerable(false, true);

            DroneControl controller = dronePrefab.AddComponent<DroneControl>();
            SetupDroneControllerParameters(controller, controllerParameters);

            DroneWeaponsController weaponsController = dronePrefab.AddComponent<DroneWeaponsController>();

            Drone drone = dronePrefab.AddComponent<Drone>();
            SetupDroneParameters(drone, droneParameters);

            SetupTransform(dronePrefab.transform, TransformParameters.Default());

            dronePrefab.layer = Globals.inst.layerContainer;
            dronePrefab.AddComponent<Rigidbody>();
            dronePrefab.SetActive(false);
            return drone;
        }

        public static void SetupHangar(GameObject prefab, Drone drone, DroneHangarParameters hangarParameters)
        {
            TargetAimer aimer = prefab.AddComponent<TargetAimer>();
            ModuleWeapon weapon = prefab.AddComponent<ModuleWeapon>();

            ModuleHangar hangar = prefab.AddComponent<ModuleHangar>();
            hangar.m_DronePrefab = drone;
            SetupDroneHangarParameters(hangar, hangarParameters);
        }

        private static void CopyBeamWeapon(BeamWeapon source, DroneLaser target)
        {
            target.m_Range = (float)m_Range.GetValue(source);
            target.m_DamagePerSecond = (int)m_DamagePerSecond.GetValue(source);
            target.m_DamageType = (ManDamage.DamageType)m_DamageType.GetValue(source);
            target.m_FadeOutTime = (float)m_FadeOutTime.GetValue(source);
            target.m_BeamParticlesPrefab = (ParticleSystem)m_BeamParticlesPrefab.GetValue(source);
            target.m_HitParticlesPrefab = (ParticleSystem)m_HitParticlesPrefab.GetValue(source);
            target.m_BeamStartDistance = (float)m_BeamStartDistance.GetValue(source);
        }
        internal static void CopyCannonBarrel(CannonBarrel source, DroneGun target)
        {
            target.particles = (ParticleSystem[])particles.GetValue(source);
            target.muzzleFlash = (MuzzleFlash)muzzleFlash.GetValue(source);
            target.casingEjectTransform = (Transform)casingEjectTransform.GetValue(source);
            target.projectileSpawnPoint = (Transform)projectileSpawnPoint.GetValue(source);
            target.recoiler = (Transform)recoiler.GetValue(source);
            target.m_FireSpinner = (Spinner)m_FireSpinner.GetValue(source);
            target.m_ShowParticlesOnAllQualitySettings = (bool)m_ShowParticlesOnAllQualitySettings.GetValue(source);
        }
        internal static void CopyCannonBarrel(CannonBarrel source, DroneLaser target)
        {
            target.particles = (ParticleSystem[])particles.GetValue(source);
            target.muzzleFlash = (MuzzleFlash)muzzleFlash.GetValue(source);
            target.projectileSpawnPoint = (Transform)projectileSpawnPoint.GetValue(source);
            target.recoiler = (Transform)recoiler.GetValue(source);
            target.m_FireSpinner = (Spinner)m_FireSpinner.GetValue(source);
            target.m_ShowParticlesOnAllQualitySettings = (bool)m_ShowParticlesOnAllQualitySettings.GetValue(source);

            BeamWeapon beam = source.beamWeapon;
            if (beam)
            {
                CopyBeamWeapon(beam, target);
            }
        }

        internal static void AddColliderToDrone(Drone drone)
        {
            GameObject dronePrefab = drone.gameObject;

            GameObject colliderObj = new GameObject("Collider");
            colliderObj.transform.parent = dronePrefab.transform;
            colliderObj.transform.localPosition = Vector3.zero;
            colliderObj.transform.localEulerAngles = Vector3.zero;
            colliderObj.transform.localScale = Vector3.one;

            SphereCollider collider = colliderObj.AddComponent<SphereCollider>();
            collider.radius = 0.75f;

            colliderObj.layer = Globals.inst.layerContainer;
        }
        internal static void AddExplosionToDrone(Drone drone, int blockType, bool blockExplosion)
        {
            GameObject blockPrefab;
            BlockPrefabBuilder.GameBlocksByID(blockType, out blockPrefab);

            Explosion explosion = null;
            if (blockExplosion)
            {
                ModuleDamage batteryDamage = blockPrefab.GetComponent<ModuleDamage>();
                explosion = batteryDamage.deathExplosion.GetComponent<Explosion>();
            }
            else
            {
                FireData fireData = blockPrefab.GetComponentInChildren<FireData>();
                if (fireData && fireData.m_BulletPrefab)
                {
                    Projectile projectile = fireData.m_BulletPrefab.GetComponentInChildren<Projectile>();
                    if (projectile)
                    {
                        explosion = (Explosion)m_Explosion.GetValue(projectile);
                    }
                }
            }
            drone.m_Explosion = explosion;
        }

        public struct DroneWeaponParameters
        {
            public float m_ChangeTargetInterval;
            public ModuleWeapon.AimType m_AimType;
            public float m_RotateSpeed;
            public bool m_AutoFire;
            public bool m_PreventShootingTowardsFloor;
            public bool m_DeployOnHasTarget;
            public float m_LimitedShootAngle;
            public bool m_DontFireIfNotAimingAtTarget;
            public float m_ShotCooldown;
            public TechAudio.SFXType m_FireSFXType;

            public static DroneWeaponParameters Default()
            {
                return new DroneWeaponParameters()
                {
                    m_ChangeTargetInterval = 1.0f,
                    m_AimType = ModuleWeapon.AimType.AutoAim,
                    m_RotateSpeed = 90f,
                    m_AutoFire = true,
                    m_PreventShootingTowardsFloor = false,
                    m_DeployOnHasTarget = true,
                    m_LimitedShootAngle = 90f,
                    m_DontFireIfNotAimingAtTarget = false,
                    m_ShotCooldown = 1f,
                    m_FireSFXType = TechAudio.SFXType.CoilLaserSmall
                };
            }
        }

        private static void ChangeColor(ParticleSystem system, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.startColor = color;

            ParticleSystem.ColorOverLifetimeModule colorModule = system.colorOverLifetime;
            ParticleSystem.MinMaxGradient gradient = colorModule.color;
            gradient.mode = ParticleSystemGradientMode.Color;
            gradient.color = color;

            ParticleSystemRenderer renderer = system.gameObject.GetComponent<ParticleSystemRenderer>();
            if (renderer)
            {
                renderer.material.color = color;
            }
        }
        internal static void ChangeLaserColor(DroneLaser laser, Color colorOverride)
        {
            LineRenderer renderer = laser.GetComponent<LineRenderer>();

            ParticleSystem hitParticles = laser.m_HitParticlesPrefab;
            if (hitParticles) {
                ParticleSystem copySystem = UnityEngine.Object.Instantiate(hitParticles);
                copySystem.gameObject.SetActive(false);
                ChangeColor(copySystem, colorOverride);

                ParticleSystem.SubEmittersModule subEmitters = copySystem.subEmitters;
                int subEmittersCount = subEmitters.subEmittersCount;

                for (int i = 0; i < subEmittersCount; i++)
                {
                    ParticleSystem originalSubSystem = subEmitters.GetSubEmitterSystem(i);
                    ParticleSystem copySubSystem = UnityEngine.Object.Instantiate(originalSubSystem);
                    ChangeColor(copySubSystem, colorOverride);
                    subEmitters.SetSubEmitterSystem(i, copySubSystem);
                }

                laser.m_HitParticlesPrefab = copySystem;
            }

            ParticleSystem beamParticles = laser.m_BeamParticlesPrefab;
            if (beamParticles)
            {
                ParticleSystem copySystem = UnityEngine.Object.Instantiate(beamParticles);
                copySystem.gameObject.SetActive(false);
                ChangeColor(copySystem, colorOverride);
                ParticleSystem.SubEmittersModule subEmitters = copySystem.subEmitters;
                int subEmittersCount = subEmitters.subEmittersCount;

                for (int i = 0; i < subEmittersCount; i++)
                {
                    ParticleSystem originalSubSystem = subEmitters.GetSubEmitterSystem(i);
                    ParticleSystem copySubSystem = UnityEngine.Object.Instantiate(originalSubSystem);
                    ChangeColor(copySubSystem, colorOverride);
                    subEmitters.SetSubEmitterSystem(i, copySubSystem);
                }

                laser.m_BeamParticlesPrefab = copySystem;
            }

            renderer.startColor = colorOverride;
            renderer.endColor = colorOverride;
        }

        // Note - cannot rotate gimbal angles in any way relative to drone transform.
        internal static void AddCoilLaser(Drone drone)
        {
            GameObject laser = new GameObject("_laser");
            laser.transform.SetParent(drone.transform);
            SetupTransform(laser.transform, TransformParameters.Default());

            BlockPrefabBuilder.GameBlocksByID((int)BlockTypes.GSOLaserFixed_111, out GameObject blockPrefab);
            GameObject laserPrefab = GameObject.Instantiate(blockPrefab);

            GameObject gimbalBase = GameObject.Instantiate(laserPrefab.FindChildGameObject("_gimbalBase"));
            gimbalBase.transform.SetParent(laser.transform);
            SetupTransform(gimbalBase.transform, new TransformParameters() {
                localPosition = new Vector3(0, -0.25f, 0),
                localScale = Vector3.one / 2,
                localEulerAngles = Vector3.zero
            });

            GameObject gimbalElev = gimbalBase.FindChildGameObject("_gimbalElev");
            
            /* GameObject modelObj = gimbalElev.FindChildGameObject("m_GSO_Laser_Fixed_111_Body");
            modelObj.transform.localEulerAngles = new Vector3(270, 0, 180); */

            GameObject modelObj2 = gimbalBase.FindChildGameObject("m_GSO_Laser_Fixed_111_Mount");
            modelObj2.transform.localEulerAngles = new Vector3(90, 0, 180);
            GameObject dishObj = gimbalElev.FindChildGameObject("m_GSO_Laser_Fixed_111_Gun");
            dishObj.transform.localScale = Vector3.one / 2;

            // Setup weapon bits
            FireData new_FireData = laser.AddComponent<FireData>();
            FireData old_FireData = laserPrefab.GetComponent<FireData>();
            GameObjectJSON.ShallowCopy(typeof(FireData), new_FireData, old_FireData, true);

            TargetAimer new_TargetAimer = laser.AddComponent<TargetAimer>();
            TargetAimer old_TargetAimer = laserPrefab.GetComponent<TargetAimer>();
            GameObjectJSON.ShallowCopy(typeof(TargetAimer), new_TargetAimer, old_TargetAimer, true);

            DroneWeapon droneWeapon = laser.AddComponent<DroneWeapon>();
            DroneWeaponGun droneWeaponGun = laser.AddComponent<DroneWeaponGun>();

            CannonBarrel existingBarrel = gimbalElev.GetComponentInChildren<CannonBarrel>();
            DroneLaser laserBarrel = existingBarrel.gameObject.AddComponent<DroneLaser>();
            CopyCannonBarrel(existingBarrel, laserBarrel);
            UnityEngine.Object.Destroy(existingBarrel);
            laserBarrel.transform.localScale = Vector3.one * 2;

            BlockPrefabBuilder.GameBlocksByID(1032, out GameObject laserTemplate);
            BeamWeapon laserBeam = laserTemplate.GetComponentInChildren<BeamWeapon>();
            CopyBeamWeapon(laserBeam, laserBarrel);

            LineRenderer existingTemplate = laserBeam.GetComponent<LineRenderer>();
            LineRenderer newRenderer = laserBarrel.gameObject.AddComponent<LineRenderer>();
            GameObjectJSON.ShallowCopy(typeof(LineRenderer), existingTemplate, newRenderer, false);
            // newRenderer.widthMultiplier *= 2;

            newRenderer.widthMultiplier *= 0.6f;
            ChangeLaserColor(laserBarrel, new Color(127.0f / 255.0f, 64.0f / 255.0f, 1.0f, 1.0f));

            // do weapon balancing
            droneWeapon.m_RotateSpeed = 700.0f;

            droneWeaponGun.m_ShotCooldown = 1.0f;
            
            laserBarrel.m_FadeOutTime = 0.2f;
            laserBarrel.m_ToFadeOut = false;
            laserBarrel.m_Range = 150.0f;
            laserBarrel.m_DamagePerSecond = 40;

            // Change aiming
            GimbalAimer[] gimbalAimers = gimbalBase.GetComponentsInChildren<GimbalAimer>();
            foreach (GimbalAimer gimbalAimer in gimbalAimers)
            {
                if (gimbalAimer.rotationAxis == GimbalAimer.AxisConstraint.X)
                {
                    gimbalAimer.rotationLimits = new float[2] { -20, 90 }; // -90, 20
                }
                else if (gimbalAimer.rotationAxis == GimbalAimer.AxisConstraint.Y)
                {
                    gimbalAimer.rotationLimits = new float[2] { 0, 0 };
                }
            }

            UnityEngine.Object.Destroy(laserPrefab);
        }

        public static void CreateBlocks()
        {
            var mat = GameObjectJSON.GetObjectFromGameResources<Material>("HE_Main");

            GameObject block;
            BlockPrefabBuilder.GameBlocksByID((int)BlockTypes.GSOBlock_111, out block);
            MeshFilter meshFilter = block.GetComponentInChildren<MeshFilter>();

            // Hangar prefab
            BlockPrefabBuilder DroneLauncher = new BlockPrefabBuilder(BlockTypes.GSOBlock_111)
                            .SetName("Drone Launcher")
                            .SetDescription("Launches drones")
                            .SetBlockID(10085)
                            .SetFaction(FactionSubTypes.HE)
                            .SetCategory(BlockCategories.Control)
                            .SetModel(meshFilter.sharedMesh, false, mat)
                            .SetGrade(2)
                            .SetPrice(4527)
                            .SetHP(1000)
                            .SetMass(2.5f);

            GameObject launcherPrefab = DroneLauncher.TankBlock.gameObject;
            BoxCollider blockCollider = launcherPrefab.AddComponent<BoxCollider>();
            blockCollider.size = Vector3.one;

            DroneLauncher.SetSizeManual(
                new IntVector3[] {
                    IntVector3.zero,
                    IntVector3.forward
                },
                new Vector3[]{
                    Vector3.down * 0.5f,
                    Vector3.left * 0.5f,
                    Vector3.right * 0.5f,
                    Vector3.back * 0.5f
                })
                .SetRecipe(ChunkTypes.FuelInjector, ChunkTypes.SensoryTransmitter, ChunkTypes.PlubonicAlloy)
                .RegisterLater();

            DroneControllerParameters controllerParameters = DroneControllerParameters.Default();
            controllerParameters.alwaysMove = true;
            controllerParameters.turnInRadius = 60.0f;
            controllerParameters.turnOutRadius = 30.0f;
            controllerParameters.turnSpeed = 160.0f;
            controllerParameters.targetOffset = Vector3.zero;
            controllerParameters.maxSpeed = 80;
            controllerParameters.acceleration = 15.0f;
            controllerParameters.targetHeight = 20.0f;

            DroneParameters droneParameters = DroneParameters.Default();
            droneParameters.droneName = "Drone";
            droneParameters.m_ExplodeAfterLifetime = true;
            droneParameters.m_LifeTime = 0.0f;
            droneParameters.m_LaunchTime = 0.1f;

            DroneModelParameters modelParameters = new DroneModelParameters(mat, Properties.Resources.Drone, Vector3.one / 4);

            Drone drone = SetupDrone(modelParameters, droneParameters, controllerParameters);
            AddCoilLaser(drone);

            DroneHangarParameters hangarParameters = DroneHangarParameters.Default();
            hangarParameters.fireTransform.localPosition = new Vector3(0, 0, 2);
            hangarParameters.m_MaxDrones = 10;
            hangarParameters.m_ShotCooldown = 0.5f;
            hangarParameters.m_MaxRange = 1000.0f;
            hangarParameters.m_LaunchVelocity = 200.0f;

            SetupHangar(launcherPrefab, drone, hangarParameters);

            AddColliderToDrone(drone);
            AddExplosionToDrone(drone, (int)BlockTypes.GSOBattery_111, true);
        }
    }
}
