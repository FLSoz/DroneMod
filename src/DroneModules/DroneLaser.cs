using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src.DroneModules
{
    // Combine CannonBarrel and BeamWeapon
    public class DroneLaser : DroneSubModule, IDroneBarrel
    {
        public float Range
        {
            get
            {
                return this.m_Range;
            }
        }

        public void SetActive(MuzzleFlash flashEffect)
        {
            if (this.m_BeamParticles && !this.m_BeamParticles.isPlaying)
            {
                this.m_BeamParticles.Play();
            }
            if (this.m_BeamLine)
            {
                this.m_BeamLine.enabled = true;
            }
            this.m_FlashEffect = flashEffect;
            if (this.m_FlashEffect)
            {
                this.m_FlashEffect.Hold(true);
            }
            this.m_FadeTimer = this.m_FadeOutTime;
        }

        #region DroneBarrel Interface
        public void Setup(FireData firingData, ModuleWeapon weapon)
        {
            return;
        }
        public bool HasClearLineOfFire()
        {
            int frameCount = Time.frameCount;
            if (this.m_CachedClearLineOfFireFrameIndex == frameCount)
            {
                return this.m_CachedHasClearLineOfFire;
            }

            bool clearLineOfFire = true;
            float boundsSize = 1.0f;
            Vector3 position = this.projectileSpawnPoint.position;
            RaycastHit raycastHit;
            // Check if raycast hits
            if (Physics.Raycast(position, this.projectileSpawnPoint.forward, out raycastHit, boundsSize, Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) && raycastHit.rigidbody == this.drone.rbody)
            {
                clearLineOfFire = false;
            }
            // check if originates within something
            if (clearLineOfFire)
            {
                // relies on ColliderSwapper for blocks
            }
            this.m_CachedClearLineOfFireFrameIndex = frameCount;
            this.m_CachedHasClearLineOfFire = clearLineOfFire;
            return clearLineOfFire;
        }
        public bool OnClientFire(Vector3 projectileSpawnPoint_forward, Vector3 spin, bool seeking, int projectileUID)
        {
            return this.Fire(false);
        }
        public bool PrepareFiring(bool prepareFiring)
        {
            // Console.WriteLine($"LASER PREPARE FIRING");
            bool result;
            if (this.m_FireSpinner != null)
            {
                this.m_FireSpinner.SetAutoSpin(prepareFiring);
                result = this.m_FireSpinner.AtFullSpeed;
            }
            else
            {
                result = prepareFiring;
            }
            return result;
        }
        public bool Fire(bool seeking)
        {
            // Console.WriteLine($"FIRE LASER");

            this.SetActive(this.muzzleFlash);

            if ((!QualitySettingsExtended.DisableWeaponFireParticles || this.m_ShowParticlesOnAllQualitySettings) && this.particles != null)
            {
                for (int i = 0; i < this.particles.Length; i++)
                {
                    this.particles[i].Play();
                }
            }
            if (this.recoilAnim)
            {
                this.recoiling = true;
                if (this.recoilAnim.isPlaying)
                {
                    this.recoilAnim.Rewind();
                }
                else
                {
                    this.recoilAnim.Play();
                }
            }
            return true;
        }
        public float GetFireRateFraction()
        {
            float result = 1f;
            if (this.m_FireSpinner != null)
            {
                result = this.m_FireSpinner.SpeedFraction;
            }
            return result;
        }
        #endregion

        #region Recoil
        private void OnRecoilReturn()
        {
            this.recoiling = false;
        }
        #endregion Recoil

        #region Component Pool
        private void PrePool()
        {
            // laser
            DroneLaser.k_RaycastLayerMask = (Globals.inst.layerScenery.mask | Globals.inst.layerWater.mask | Globals.inst.layerLandmark.mask | Globals.inst.layerTerrain.mask | Globals.inst.layerPickup.mask | Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask | Globals.inst.layerContainer.mask | Globals.inst.layerShieldBulletsFilter.mask);
            this.m_BeamLine = base.GetComponent<LineRenderer>();
            if (this.m_BeamLine)
            {
                this.m_BeamLine.positionCount = 2;
                this.m_BeamLine.SetPosition(0, Vector3.zero);
                this.m_BeamLine.SetPosition(1, new Vector3(0f, 0f, this.m_Range));
            }
        }
        private void OnPool()
        {
            this.recoiling = false;
            if (this.recoiler)
            {
                this.recoilAnim = this.recoiler.GetComponentsInChildren<Animation>(true).FirstOrDefault<Animation>();
                if (this.recoilAnim)
                {
                    foreach (object obj in this.recoilAnim)
                    {
                        AnimationState animationState = (AnimationState)obj;
                        if (this.animState != null)
                        {
                            d.LogError(string.Format("{0} (base anim {1}) contains additional animation {2}", this.animState.name, animationState.name));
                        }
                        else
                        {
                            this.animState = animationState;
                        }
                    }
                }
                AnimEvent animEvent = this.recoiler.GetComponentsInChildren<AnimEvent>(true).FirstOrDefault<AnimEvent>();
                if (animEvent)
                {
                    animEvent.HandleEvent.Subscribe(delegate (int i)
                    {
                        if (i == 1)
                        {
                            return;
                        }
                        this.OnRecoilReturn();
                    });
                }
            }
            // laser
            if (this.m_BeamParticlesPrefab)
            {
                this.m_BeamParticles = UnityEngine.Object.Instantiate<ParticleSystem>(this.m_BeamParticlesPrefab, this.transform.position, this.transform.rotation);
                this.m_BeamParticles.transform.parent = this.transform;
            }
        }
        private void OnSpawn()
        {
            if (this.m_BeamParticles)
            {
                this.m_BeamParticles.Stop();
            }
            if (this.m_BeamLine)
            {
                this.m_BeamLine.enabled = false;
            }
        }
        private void OnRecycle()
        {
            if (this.recoilAnim != null && this.animState != null && this.recoilAnim.isPlaying)
            {
                this.animState.enabled = true;
                this.animState.normalizedTime = 1f;
                this.recoilAnim.Sample();
                this.animState.enabled = false;
                this.recoilAnim.Stop();
                this.recoiling = false;
            }

            // Laser code
            if (this.m_BeamParticles)
            {
                this.m_BeamParticles.Stop();
            }
            if (this.m_BeamLine)
            {
                this.m_BeamLine.enabled = false;
            }
            if (this.m_FlashEffect)
            {
                this.m_FlashEffect.Hold(false);
                this.m_FlashEffect = null;
            }
            if (this.m_HitParticles)
            {
                this.m_HitParticles.Stop();
                this.m_HitParticles.Recycle(true);
                this.m_HitParticles = null;
            }
        }
        #endregion

        private void Update()
        {
            if (this.m_FadeTimer < 0.0f)
            {
                return;
            }

            float beamLength = this.m_Range * (this.m_ToFadeOut ? this.m_FadeTimer / this.m_FadeOutTime : 1.0f);
            Tank parentTank = this.drone.TankSelf;
            int numHits = Physics.RaycastNonAlloc(this.transform.position + this.transform.forward * this.m_BeamStartDistance, this.transform.forward, DroneLaser.s_Hits, beamLength, DroneLaser.k_RaycastLayerMask, QueryTriggerInteraction.Collide);
            int hitInd = -1;
            for (int i = 0; i < numHits; i++)
            {
                RaycastHit raycastHit = DroneLaser.s_Hits[i];
                if (raycastHit.distance != 0f && raycastHit.distance <= beamLength)
                {
                    // handle odd friendly fire?
                    if (raycastHit.collider.gameObject.layer == Globals.inst.layerShieldBulletsFilter && parentTank.IsNotNull())
                    {
                        Visible visible = Singleton.Manager<ManVisible>.inst.FindVisible(raycastHit.collider);
                        if (visible.IsNotNull() && visible.block.tank.IsNotNull() && !visible.block.tank.IsEnemy(parentTank.Team))
                        {
                            break;
                        }
                    }
                    hitInd = i;
                    beamLength = raycastHit.distance;
                }
            }
            if (hitInd >= 0)
            {
                RaycastHit raycastHit2 = DroneLaser.s_Hits[hitInd];
                float damage = (float)this.m_DamagePerSecond * Mathf.Min(Time.deltaTime, this.m_FadeTimer);
                if (damage != 0f)
                {
                    Damageable targetDamageable = raycastHit2.collider.GetComponentInParents<Damageable>(false);
                    if (targetDamageable)
                    {
                        Singleton.Manager<ManDamage>.inst.DealDamage(targetDamageable, damage, this.m_DamageType, this, parentTank, raycastHit2.point, this.transform.forward, 0f, 0f);
                    }
                }
            }
            if (this.m_BeamLine)
            {
                this.m_BeamLine.SetPosition(1, new Vector3(0f, 0f, beamLength));
            }
            this.m_FadeTimer -= Time.deltaTime;

            if (this.m_FadeTimer < 0f)
            {
                if (this.m_BeamParticles)
                {
                    this.m_BeamParticles.Stop();
                }
                if (this.m_BeamLine)
                {
                    this.m_BeamLine.enabled = false;
                }
                hitInd = -1;
                if (this.m_FlashEffect)
                {
                    this.m_FlashEffect.Hold(false);
                    this.m_FlashEffect = null;
                }
            }
            if (this.m_HitParticlesPrefab)
            {
                if (hitInd >= 0)
                {
                    RaycastHit raycastHit3 = DroneLaser.s_Hits[hitInd];
                    Quaternion quaternion = Quaternion.LookRotation(raycastHit3.normal);
                    if (!this.m_HitParticles)
                    {
                        this.m_HitParticles = this.m_HitParticlesPrefab.Spawn(this.transform, raycastHit3.point, quaternion);
                        this.m_HitParticles.Play();
                    }
                    this.m_HitParticles.transform.SetPositionIfChanged(raycastHit3.point, null, 0.01f);
                    this.m_HitParticles.transform.SetRotationIfChanged(quaternion, null, 0.001f);
                    return;
                }
                if (this.m_HitParticles)
                {
                    this.m_HitParticles.Stop();
                    this.m_HitParticles.Recycle(true);
                    this.m_HitParticles = null;
                }
            }
        }

        #region Firing Management
        [SerializeField]
        public ParticleSystem[] particles;
        [SerializeField]
        public MuzzleFlash muzzleFlash;

        [SerializeField]
        public Transform projectileSpawnPoint;
        [SerializeField]
        public Transform recoiler;

        [SerializeField]
        public Spinner m_FireSpinner;
        [SerializeField]
        public bool m_ShowParticlesOnAllQualitySettings;

        private bool m_CachedHasClearLineOfFire;
        private int m_CachedClearLineOfFireFrameIndex = -1;

        private bool recoiling;
        private AnimationState animState;
        private Animation recoilAnim;
        #endregion

        #region Laser management
        [SerializeField]
        public bool m_ToFadeOut = false;

        [SerializeField]
        public float m_Range = 5f;

        [SerializeField]
        public int m_DamagePerSecond = 20;

        [SerializeField]
        public ManDamage.DamageType m_DamageType = ManDamage.DamageType.Energy;

        [SerializeField]
        public float m_FadeOutTime = 0.2f;

        [SerializeField]
        public ParticleSystem m_BeamParticlesPrefab;

        [SerializeField]
        public ParticleSystem m_HitParticlesPrefab;

        [SerializeField]
        public float m_BeamStartDistance;

        private float m_FadeTimer;

        private ParticleSystem m_BeamParticles;

        private ParticleSystem m_HitParticles;

        [HideInInspector]
        [SerializeField]
        private LineRenderer m_BeamLine;

        private MuzzleFlash m_FlashEffect;

        private static int k_RaycastLayerMask;

        private static RaycastHit[] s_Hits = new RaycastHit[32];
        #endregion
    }
}
