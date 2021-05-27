using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src.DroneModules
{
	[RequireComponent(typeof(DroneWeapon))]
    public class DroneWeaponGun : DroneSubModule, IModuleWeapon, TechAudio.IModuleAudioProvider
	{
		public TechAudio.SFXType SFXType
		{
			get
			{
				return this.m_DeploySFXType;
			}
		}
		public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;

		#region Replicate ModuleWeaponGun
		[SerializeField]
		public float m_ShotCooldown = 1f;

		[SerializeField]
		public float m_CooldownVariancePct = 0.05f;

		[SerializeField]
		public ModuleWeaponGun.FireControlMode m_FireControlMode;

		[SerializeField]
		public int m_BurstShotCount;

		[SerializeField]
		public float m_BurstCooldown;

		[SerializeField]
		public bool m_ResetBurstOnInterrupt = true;

		[SerializeField]
		public float m_RegisterWarningAfter = 1f;

		[SerializeField]
		public float m_ResetFiringTAfterNotFiredFor = 1f;

		[SerializeField]
		public bool m_HasSpinUpDownAnim;

		[SerializeField]
		public bool m_HasCooldownAnim;

		[SerializeField]
		public bool m_CanInterruptSpinUpAnim;

		[SerializeField]
		public bool m_CanInterruptSpinDownAnim;

		[SerializeField]
		public int m_SpinUpAnimLayerIndex;

		[SerializeField]
		public TechAudio.SFXType m_DeploySFXType;

		[SerializeField]
		public float m_OverheatTime;

		[SerializeField]
		public float m_OverheatPauseWindow;

		[SerializeField]
		public bool m_DisableMainAudioLoop;

		[SerializeField]
		public float m_FireAngleTolerance = 10.0f;

		public float m_AudioLoopDelay;
		private Animator m_Animator;

		private int m_AnimatorSpinUpId = Animator.StringToHash("SpinUp");
		private int m_AnimatorCoolingDownId = Animator.StringToHash("CoolingDown");
		private int m_AnimatorCooldownRemainingId = Animator.StringToHash("CooldownRemaining");
		private int m_AnimatorOverheatedId = Animator.StringToHash("Overheated");
		private int m_AnimatorOverheatId = Animator.StringToHash("Overheat");

		private float m_ShotTimer;
		private int m_NextBarrelToFire;
		private float m_IdleTime;
		private float m_FailedToFireTime;
		private int m_BurstShotsRemaining;
		private bool m_SpinUp;
		private float m_Overheat;
		private float m_OverheatPause;
		#endregion

		[SerializeField]
		[HideInInspector]
		private TargetAimer m_TargetAimer;
		[SerializeField]
		[HideInInspector]
		private IDroneBarrel[] m_Barrels;
		[SerializeField]
		[HideInInspector]
		private FireData FiringData;
		[SerializeField]
		[HideInInspector]
		private bool m_SeekingRounds = false;
		[SerializeField]
		[HideInInspector]
		private int m_NumCannonBarrels;
		[SerializeField]
		[HideInInspector]
		private DroneWeapon m_WeaponModule;

		private void PrePool()
		{
			this.FiringData = base.GetComponent<FireData>();
			this.m_TargetAimer = base.GetComponent<TargetAimer>();

			if (this.FiringData.m_BulletPrefab && this.FiringData.m_BulletPrefab is Projectile projectile)
			{
				if (projectile.GetComponent<SeekingProjectile>())
				{
					this.m_SeekingRounds = true;
				}
			}

			this.m_WeaponModule = base.GetComponent<DroneWeapon>();
		}
		private void OnPool()
		{
			this.m_Barrels = base.GetComponentsInChildren<IDroneBarrel>();
			d.Assert(this.m_Barrels != null && this.m_Barrels.Length > 0, "DroneWeapon needs at least one IDroneBarrel in hierarchy.");
			this.m_NumCannonBarrels = this.m_Barrels.Length;

			this.m_Animator = base.GetComponentInChildren<Animator>();
			this.m_ShotCooldown /= (float)this.m_NumCannonBarrels;
		}

		// Token: 0x06002B58 RID: 11096 RVA: 0x000E08F8 File Offset: 0x000DEAF8
		private void OnSpawn()
		{
			this.m_FailedToFireTime = 0f;
			this.m_IdleTime = 0f;
		}

		public void Launch(ModuleHangar hangar)
        {
			foreach (IDroneBarrel barrel in this.m_Barrels)
			{
				barrel.Setup(this.FiringData, hangar.m_Weapon);
			}
		}

		#region IModuleWeapon implementation
		public bool AimWithTrajectory()
		{
			bool result = false;
			if (this.FiringData.m_BulletPrefab)
			{
				Rigidbody component = this.FiringData.m_BulletPrefab.GetComponent<Rigidbody>();
				result = (component != null && component.useGravity);
			}
			return result;
		}

		public bool Deploy(bool deploy)
		{
			bool isDeployed = true;
			float adsrTime = 1f;
			if (this.m_HasSpinUpDownAnim && this.m_Animator != null)
			{
				AnimatorStateInfo currentAnimatorStateInfo = this.m_Animator.GetCurrentAnimatorStateInfo(this.m_SpinUpAnimLayerIndex);
				bool animatorOverheated = this.m_OverheatTime > 0f && this.m_Overheat == this.m_OverheatTime;
				if (deploy && !animatorOverheated && (this.m_SpinUp || this.m_CanInterruptSpinDownAnim || currentAnimatorStateInfo.IsName("Down")))
				{
					this.m_SpinUp = true;
					this.m_Animator.SetBool(this.m_AnimatorSpinUpId, true);
					if (!currentAnimatorStateInfo.IsName("Up"))
					{
						isDeployed = false;
						adsrTime = 0f;
					}
					else
					{
						this.m_Overheat = Mathf.Min(this.m_OverheatTime, this.m_Overheat + Time.deltaTime);
					}
					this.m_OverheatPause = 0f;
				}
				else if (!this.m_SpinUp || animatorOverheated || this.m_CanInterruptSpinUpAnim || currentAnimatorStateInfo.IsName("Up"))
				{
					this.m_OverheatPause = Mathf.Min(this.m_OverheatPauseWindow, this.m_OverheatPause + Time.deltaTime);
					if (!animatorOverheated && this.m_OverheatPause < this.m_OverheatPauseWindow)
					{
						this.m_Overheat = Mathf.Max(0f, this.m_Overheat - Time.deltaTime);
					}
					else
					{
						this.m_SpinUp = false;
						this.m_Animator.SetBool(this.m_AnimatorSpinUpId, false);
						isDeployed = false;
						if (currentAnimatorStateInfo.IsName("Down"))
						{
							adsrTime = 0f;
							this.m_Overheat = 0f;
							animatorOverheated = false;
						}
						else
						{
							adsrTime = 0.99f;
						}
					}
				}
				if (this.m_OverheatTime > 0f)
				{
					if (this.m_Overheat == this.m_OverheatTime)
					{
						isDeployed = false;
						animatorOverheated = true;
					}
					this.m_Animator.SetBool(this.m_AnimatorOverheatedId, animatorOverheated);
					this.m_Animator.SetFloat(this.m_AnimatorOverheatId, this.m_Overheat / this.m_OverheatTime);
				}
			}

			this.m_WeaponModule.DisableTargetAimer = !isDeployed;

			if (this.OnAudioTickUpdate != null)
			{
				bool flag3 = this.m_SpinUp;
				if (isDeployed)
				{
					flag3 &= !this.m_DisableMainAudioLoop;
				}
				TechAudio.AudioTickData value = new TechAudio.AudioTickData
				{
					module = this.drone.hangar,
					provider = this.m_WeaponModule,
					sfxType = this.m_DeploySFXType,
					numTriggered = 0,
					triggerCooldown = 0f,
					isNoteOn = flag3,
					adsrTime01 = adsrTime
				};
				this.OnAudioTickUpdate.Send(value, null);
			}
			return isDeployed;
		}

		public bool FiringObstructed()
		{
			bool firingObstructed = false;
			foreach (IDroneBarrel barrel in this.m_Barrels)
			{
				if (!barrel.HasClearLineOfFire())
				{
					firingObstructed = true;
					break;
				}
			}
			return firingObstructed;
		}

		public float GetFireRateFraction()
		{
			float num = 0f;
			foreach (IDroneBarrel barrel in this.m_Barrels)
			{
				num += barrel.GetFireRateFraction();
			}
			num /= (float)this.m_Barrels.Length;
			return num;
		}

		public Transform GetFireTransform()
		{
			return ((Component)this.m_Barrels[0]).transform;
		}

		public float GetRange()
		{
			float range = 0.0f;
			foreach (IDroneBarrel barrel in this.m_Barrels)
			{
				range = Mathf.Max(range, barrel.Range);
			}
			return range;
		}

		public float GetVelocity()
		{
			return this.FiringData.m_MuzzleVelocity;
		}

		public bool IsAimingAtFloor(float limitedAngle)
		{
			bool result = false;
			foreach (IDroneBarrel barrel in this.m_Barrels)
			{
				if (Vector3.Angle(((Component)barrel).transform.forward, -Vector3.up) < limitedAngle)
				{
					result = true;
					break;
				}
			}
			return result;
		}

		public bool PrepareFiring(bool prepareFiring)
		{
			// Console.WriteLine($"DroneWeaponGun PrepareFiring {prepareFiring}");
			bool result = true;
			foreach (IDroneBarrel barrel in this.m_Barrels)
			{
				if (!barrel.PrepareFiring(prepareFiring))
				{
					result = false;
				}
			}
			if (prepareFiring)
            {
				if (this.m_WeaponModule.m_TargetAimer && this.m_WeaponModule.m_TargetAimer.HasTarget && this.m_FireAngleTolerance > 0.0f)
                {
					float angle = Vector3.Angle(this.m_WeaponModule.m_TargetAimer.Target.GetAimPoint(base.transform.position) - base.transform.position, this.GetFireTransform().forward);
					return angle <= this.m_FireAngleTolerance;
                }
            }
			return result;
		}

		public int ProcessFiring(bool firing)
		{
			// Console.WriteLine($"DroneWeaponGun ProcessFiring {firing}");
			float num = Time.deltaTime;
			if (this.m_ShotTimer > 0f)
			{
				if (num > 0.04f)
				{
					if (num > 0.0833333358f)
					{
						num = 0f;
					}
					else
					{
						float num2 = Mathf.InverseLerp(0.0833333358f, 0.04f, num);
						num *= num2;
					}
				}
				this.m_ShotTimer -= num;
				if (this.m_HasCooldownAnim && this.m_Animator.IsNotNull())
				{
					this.m_Animator.SetFloat(this.m_AnimatorCooldownRemainingId, this.m_ShotTimer);
				}
			}
			else if (this.m_HasCooldownAnim && this.m_Animator.IsNotNull())
			{
				this.m_Animator.SetBool(this.m_AnimatorCoolingDownId, false);
			}
			int num3 = 0;
			if (firing)
			{
				this.m_IdleTime = 0f;
				bool flag = true;
				if (this.m_FireControlMode == ModuleWeaponGun.FireControlMode.AllAtOnce)
				{
					foreach (IDroneBarrel barrel in this.m_Barrels)
					{
						if (barrel.HasClearLineOfFire())
						{
							if (barrel.Fire(this.m_SeekingRounds))
							{
								num3++;
							}
						}
						else
						{
							flag = false;
						}
					}
				}
				else
				{
					if (this.m_Barrels[this.m_NextBarrelToFire].HasClearLineOfFire())
					{
						if (this.m_Barrels[this.m_NextBarrelToFire].Fire(this.m_SeekingRounds))
						{
							num3++;
						}
					}
					else
					{
						flag = false;
					}
					if (num3 > 0)
					{
						this.m_NextBarrelToFire = ((this.m_NextBarrelToFire == this.m_NumCannonBarrels - 1) ? 0 : (this.m_NextBarrelToFire + 1));
					}
				}
				if (num3 > 0)
				{
					if (this.m_BurstShotCount > 0)
					{
						this.m_BurstShotsRemaining -= num3;
					}
					bool flag2 = this.m_BurstShotCount > 0 && this.m_BurstShotsRemaining <= 0;
					this.m_ShotTimer = (flag2 ? this.m_BurstCooldown : this.m_ShotCooldown.RandomVariance(this.m_CooldownVariancePct));
					if (this.m_HasCooldownAnim && this.m_Animator.IsNotNull())
					{
						this.m_Animator.SetBool(this.m_AnimatorCoolingDownId, true);
					}
					if (flag2)
					{
						this.m_BurstShotsRemaining = this.m_BurstShotCount;
					}
					if (flag)
					{
						this.m_FailedToFireTime = 0f;
					}
				}
				if (this.m_FailedToFireTime > this.m_RegisterWarningAfter)
				{
					this.m_FailedToFireTime = 0f;
				}
				if (!flag)
				{
					this.m_FailedToFireTime += num;
				}
			}
			else
			{
				if (this.m_ResetBurstOnInterrupt && this.m_BurstShotsRemaining != this.m_BurstShotCount)
				{
					this.m_BurstShotsRemaining = this.m_BurstShotCount;
					this.m_ShotTimer = this.m_BurstCooldown;
					if (this.m_HasCooldownAnim && this.m_Animator.IsNotNull())
					{
						this.m_Animator.SetBool(this.m_AnimatorCoolingDownId, true);
					}
				}
				this.m_IdleTime += num;
				if (this.m_IdleTime >= this.m_ResetFiringTAfterNotFiredFor)
				{
					this.m_FailedToFireTime = 0f;
				}
			}
			return num3;
		}

		public bool ReadyToFire()
		{
			return this.m_ShotTimer <= Mathf.Epsilon && this.m_NumCannonBarrels > 0;
		}
		#endregion
	}
}
