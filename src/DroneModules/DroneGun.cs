using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src.DroneModules
{
	// CannonBarrel
    public class DroneGun : DroneSubModule, IDroneBarrel
    {
		public float Range
        {
			get
            {
				return Mathf.Infinity;
            }
        }

		#region DroneBarrel Interface
		public void Setup(FireData firingData, ModuleWeapon weapon)
		{
			this.m_Weapon = weapon;
			this.m_FiringData = firingData;
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
			if (this.m_FiringData.m_BulletPrefab)
			{
				WeaponRound weaponRound = this.m_FiringData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, this.projectileSpawnPoint.position, base.transform.rotation);
				weaponRound.SetVariationParameters(projectileSpawnPoint_forward, spin);
				weaponRound.Fire(Vector3.zero, this.m_FiringData, this.m_Weapon, this.drone.TankSelf, seeking, true);
				TechWeapon.RegisterWeaponRound(weaponRound, projectileUID);
			}
			return this.ProcessFire();
		}

		public bool PrepareFiring(bool prepareFiring)
		{
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
			bool needsToSendNetMessage = false;
			d.Assert(this.drone.TankSelf != null);
			NetTech netTech = this.drone.TankSelf.netTech;
			if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && netTech.IsNotNull())
			{
				if (netTech.NetPlayer.IsNotNull())
				{
					needsToSendNetMessage = netTech.NetPlayer.IsActuallyLocalPlayer;
				}
				else if (Singleton.Manager<ManNetwork>.inst.IsServer)
				{
					needsToSendNetMessage = true;
				}
			}
			if (this.recoiling && !this.recoilAnim.isPlaying)
			{
				this.recoiling = false;
			}
			if (this.recoiling || !this.drone.TankSelf)
			{
				return false;
			}
			if (this.m_FiringData.m_BulletPrefab)
			{
				Vector3 position = this.projectileSpawnPoint.position;
				Vector3 forward = this.projectileSpawnPoint.forward;
				WeaponRound weaponRound = this.m_FiringData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, position, this.transform.rotation);
				weaponRound.Fire(forward, this.m_FiringData, this.m_Weapon, this.drone.TankSelf, seeking, false);
				TechWeapon.RegisterWeaponRound(weaponRound, int.MinValue);
				Vector3 force = -forward * this.m_FiringData.m_KickbackStrength;
				this.drone.rbody.AddForceAtPosition(force, position, ForceMode.Impulse);
				if (needsToSendNetMessage)
				{
					Vector3 fireDirection;
					Vector3 fireSpin;
					weaponRound.GetVariationParameters(out fireDirection, out fireSpin);
					this.drone.TankSelf.Weapons.QueueProjectileLaunch(null, weaponRound.ShortlivedUID, fireDirection, fireSpin, this.m_Weapon, seeking);
				}
			}
			return this.ProcessFire();	
		}

		private bool ProcessFire()
		{
			if (this.muzzleFlash)
			{
				this.muzzleFlash.Fire(false);
			}
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
			else
			{
				this.EjectCasing();
			}
			return true;
		}

		// Token: 0x06000353 RID: 851 RVA: 0x0001618C File Offset: 0x0001438C
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

		#region Recoil/Casing
		private void EjectCasing()
		{
			QualitySettingsExtended.CasingSpawnMode shellCasingSpawnMode = QualitySettingsExtended.ShellCasingSpawnMode;
			if (shellCasingSpawnMode != QualitySettingsExtended.CasingSpawnMode.None && this.m_FiringData.m_BulletCasingPrefab && this.drone.TankSelf != null)
			{
				bool flag = shellCasingSpawnMode == QualitySettingsExtended.CasingSpawnMode.Throttled;
				bool flag2 = true;
				if (flag)
				{
					int frameCount = Time.frameCount;
					if (DroneGun.s_LastCasingFrame == frameCount)
					{
						if (DroneGun.s_CasingCount >= DroneGun.kMaxCasingPerFrame)
						{
							flag2 = false;
						}
					}
					else
					{
						DroneGun.s_LastCasingFrame = frameCount;
						DroneGun.s_CasingCount = 0;
					}
				}
				if (flag2)
				{
					Vector3 position = this.casingEjectTransform.position;
					if (flag)
					{
						Vector3 vector = Singleton.camera.WorldToViewportPoint(position);
						flag2 = (vector.x >= 0f && vector.y >= 0f && vector.x <= 1f && vector.y <= 1f && vector.z > 0f && vector.z < DroneGun.kCasingMaxZ);
					}
					if (flag2)
					{
						this.m_FiringData.m_BulletCasingPrefab.Spawn(Singleton.dynamicContainer, position).Eject(this.casingEjectTransform.forward, this.m_FiringData, this.drone.TankSelf);
						DroneGun.s_CasingCount++;
					}
				}
			}
		}
		private void OnRecoilMax()
		{
			if (this.recoiling)
			{
				this.EjectCasing();
			}
		}
		private void OnRecoilReturn()
		{
			this.recoiling = false;
		}
        #endregion Recoil

        #region Component Pool
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
							this.OnRecoilMax();
							return;
						}
						this.OnRecoilReturn();
					});
				}
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
		}
		#endregion

		[SerializeField]
		public ParticleSystem[] particles;
		[SerializeField]
		public MuzzleFlash muzzleFlash;

		[SerializeField]
		public Transform casingEjectTransform;
		[SerializeField]
		public Transform projectileSpawnPoint;
		[SerializeField]
		public Transform recoiler;
		
		[SerializeField]
		public Spinner m_FireSpinner;
		[SerializeField]
		public bool m_ShowParticlesOnAllQualitySettings;
		
		private bool recoiling;
		
		private AnimationState animState;
		private Animation recoilAnim;
		
		private FireData m_FiringData;
		private ModuleWeapon m_Weapon;
		
		private bool m_CachedHasClearLineOfFire;
		private int m_CachedClearLineOfFireFrameIndex = -1;
		
		private const float kCasingMaxZ = 10f;
		private const int kMaxCasingPerFrame = 2;

		private static int s_LastCasingFrame;
		private static int s_CasingCount;
    }
}
