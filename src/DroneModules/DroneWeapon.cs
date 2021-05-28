using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;

namespace DroneMod.src.DroneModules
{
    // A combination of ModuleWeapon and IModuleWeapon functionalities
	[RequireComponent(typeof(FireData))]
	[RequireComponent(typeof(TargetAimer))]
    public class DroneWeapon : DroneSubModule, TechAudio.IModuleAudioProvider
    {
		#region Audio
		public TechAudio.SFXType SFXType
		{
			get
			{
				return this.m_FireSFXType;
			}
		}

		public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;
		#endregion

		[SerializeField]
		public bool m_IsMainArmament = false;

		#region ModuleWeapon equivalent
		[SerializeField]
		public float m_ChangeTargetInterval = 1.0f;

        [SerializeField]
        public ModuleWeapon.AimType m_AimType = ModuleWeapon.AimType.AutoAim;

        [SerializeField]
        public float m_RotateSpeed = 90f;

        [SerializeField]
        public bool m_AutoFire = true;

        [SerializeField]
        public bool m_PreventShootingTowardsFloor = false;

        [SerializeField]
        public bool m_DeployOnHasTarget = true;

        [SerializeField]
        public float m_LimitedShootAngle = 90f;

        [SerializeField]
        public bool m_DontFireIfNotAimingAtTarget = false;

        [SerializeField]
        public float m_ShotCooldown = 1f;

		[SerializeField]
		public TechAudio.SFXType m_FireSFXType = TechAudio.SFXType.CoilLaserSmall;

		[SerializeField]
		public IModuleWeapon m_WeaponComponent;
		#endregion

		[SerializeField]
		[HideInInspector]
		public TargetAimer m_TargetAimer;

		private Vector3 m_TargetAimDirectionLocal;
		private Vector3 m_TargetPosition;
		private WarningHolder m_Warning;
		private ManTimedEvents.ManagedEvent m_CheckDismissWarningEvent = new ManTimedEvents.ManagedEvent();
		private int m_RemoteShotFiredPending;
		private bool m_HasTargetInFiringCone;
		private bool launched = false;

		#region Component LifeCycle
		private void PrePool()
        {
			this.m_TargetAimer = base.GetComponent<TargetAimer>();
			d.Assert(this.m_TargetAimer != null, "DroneWeapon needs an TargetAimer.");
		}
        private void OnDepool()
        {

        }
		private void OnPool()
		{
			this.launched = false;
			this.m_WeaponComponent = base.GetComponent<IModuleWeapon>();
			d.Assert(this.m_WeaponComponent != null, "DroneWeapon needs an IModuleWeaponComponent.");
		}

		private void OnSpawn()
		{
			this.ResetAim();
			this.AimControl = 0;
			this.DisableTargetAimer = false;
			this.m_HasTargetInFiringCone = false;
			this.m_TargetPosition = Vector3.zero;
			this.m_RemoteShotFiredPending = 0;
			
		}
		private void OnRecycle()
		{
			this.m_CheckDismissWarningEvent.Clear();
			this.launched = false;
		}
		#endregion

		public void Launch(ModuleHangar hangar)
		{
			Console.WriteLine("Pre Subscribe");
			this.Subscribe(hangar);
			// Console.WriteLine("Subscribe");
			if (this.m_TargetAimer)
			{
				// Console.WriteLine("Pre delegate");
				Func<Vector3, Vector3> aimDelegate = null;
				if (this != null && this.m_WeaponComponent.AimWithTrajectory())
				{
					aimDelegate = new Func<Vector3, Vector3>(this.AimPointWithTrajectory);
				}
				// Console.WriteLine("Post delegate");
				this.m_TargetAimer.Init(hangar.block, this.m_ChangeTargetInterval, aimDelegate);
				// Console.WriteLine("Post init");
			}
			else
			{
				d.LogWarning($"DroneWeapon.Launch: No Target Aimer on drone {base.drone.name} => {base.name}");
			}
			// Console.WriteLine("Pre WeaponComponent");
			try
			{
				MethodInfo Launch = this.m_WeaponComponent.GetType().GetMethod("Launch", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				Launch.Invoke(this.m_WeaponComponent, new object[] { hangar });
			}
			catch (Exception exc)
            {
				// pass - we don't care if it errors out
				d.LogWarning($"{this.m_WeaponComponent.GetType()} {((Component) this.m_WeaponComponent).name} on Drone {this.drone.droneName} failed to invoke Launch(hangar)!\n" + exc.ToString());
            }
			// Console.WriteLine("Post WeaponComponent reflection");
			this.launched = true;
		}
		public void Subscribe(ModuleHangar hangar)
		{
			hangar.block.tank.TechAudio.AddModule<DroneWeapon>(this);
			hangar.block.tank.control.manualAimFireEvent.Subscribe(new Action<int, bool>(this.ControlInputManual));
			hangar.block.tank.control.targetedAimFireEvent.Subscribe(new Action<Vector3, float>(this.ControlInputTargeted));
		}
		public void UnSubscribe()
		{
			if (base.drone.hangar)
			{
				base.drone.hangar.block.tank.TechAudio.RemoveModule<DroneWeapon>(this);
				base.drone.hangar.block.tank.control.manualAimFireEvent.Unsubscribe(new Action<int, bool>(this.ControlInputManual));
				base.drone.hangar.block.tank.control.targetedAimFireEvent.Unsubscribe(new Action<Vector3, float>(this.ControlInputTargeted));
			}
		}

		#region ModuleWeapon replication
		public int AimControl { get; set; }

		public bool DisableTargetAimer { get; set; }

		public int Process()
		{
			int num = 0;
			if (this.launched)
			{
				this.UpdateAim();
				bool isDeployed = this.m_WeaponComponent.Deploy(this.m_HasTargetInFiringCone || (this.m_DeployOnHasTarget && this.m_TargetAimer.HasTarget));
				bool canFire = this.m_TargetAimer.HasTarget && isDeployed && !this.m_WeaponComponent.FiringObstructed();
				bool readyToFire = this.m_WeaponComponent.PrepareFiring(canFire) && this.CanShoot();
				num = this.m_WeaponComponent.ProcessFiring(readyToFire);
				if (readyToFire && Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && Singleton.Manager<ManNetwork>.inst.IsServer && Singleton.Manager<ManNetwork>.inst.ServerSpawnBank != null && Singleton.Manager<ManNetwork>.inst.NetController.GameModeType == MultiplayerModeType.Deathmatch && base.drone.TankSelf.netTech != null && base.drone.TankSelf.netTech.SpawnShieldCount > 0)
				{
					Singleton.Manager<ManNetwork>.inst.ServerSpawnBank.DisableShieldAfterDelay(base.drone.TankSelf.netTech.InitialSpawnShieldID);
				}
				if (this.m_RemoteShotFiredPending > 0)
				{
					num += this.m_RemoteShotFiredPending;
					this.m_RemoteShotFiredPending = 0;
				}
				if (this.OnAudioTickUpdate != null)
				{
					float fireRateFraction = this.m_WeaponComponent.GetFireRateFraction();
					TechAudio.AudioTickData value = new TechAudio.AudioTickData
					{
						module = base.drone.hangar,
						provider = this,
						sfxType = this.m_FireSFXType,
						numTriggered = num,
						triggerCooldown = this.m_ShotCooldown,
						isNoteOn = canFire,
						adsrTime01 = fireRateFraction
					};
					this.OnAudioTickUpdate.Send(value, null);
				}
			}
			return num;
		}

		private void UpdateAim()
		{
			this.m_HasTargetInFiringCone = false;
			ModuleWeapon.AimType aimType = this.m_AimType;
			if (aimType == ModuleWeapon.AimType.AutoAim)
			{
				this.UpdateAutoAimBehaviour();
				return;
			}
			if (aimType != ModuleWeapon.AimType.Default)
			{
				d.LogError("ModuleWeapon.UpdateAim - Unsupported Aim Type");
				return;
			}
			this.UpdateDefaultBehaviour();
		}

		private void ResetAim()
		{
			this.m_TargetAimDirectionLocal = Vector3.forward;
			this.m_TargetPosition = Vector3.zero;
			this.m_TargetAimer.Reset();
		}

		private void SetAimTarget(Vector3 targetWorld)
		{
			this.m_TargetAimDirectionLocal = base.drone.transform.InverseTransformPoint(targetWorld).normalized;
			this.m_TargetPosition = targetWorld;
		}

		private void UpdateAutoAimBehaviour()
		{
			Transform fireTransform = this.m_WeaponComponent.GetFireTransform();
			Vector3 position = fireTransform.position;

			if (this.DisableTargetAimer)
			{
				this.m_HasTargetInFiringCone = (this.m_TargetAimer.UpdateAndCanAimAtTarget() && this.m_TargetAimer.HasTarget);
				this.m_TargetAimer.AimAtWorldPos(position + 10f * base.drone.transform.forward, this.m_RotateSpeed);
				return;
			}
			this.m_HasTargetInFiringCone = (this.m_TargetAimer.UpdateAndAimAtTarget(this.m_RotateSpeed) && this.m_TargetAimer.HasTarget);
			if (this.m_TargetAimer.HasTarget)
			{
				this.m_TargetPosition = this.m_TargetAimer.Target.GetAimPoint(base.transform.position);
			}
		}

		private void UpdateDefaultBehaviour()
		{
			if (this.AimControl != 0)
			{
				Vector3 axis = base.transform.InverseTransformDirection(base.drone.transform.up);
				Quaternion rotation = Quaternion.AngleAxis((float)this.AimControl * this.m_RotateSpeed * Time.deltaTime, axis);
				this.m_TargetAimDirectionLocal = rotation * this.m_TargetAimDirectionLocal;
			}
			Transform fireTransform = this.m_WeaponComponent.GetFireTransform();
			this.m_TargetAimer.AimAtWorldPos(fireTransform.position + base.transform.TransformDirection(this.m_TargetAimDirectionLocal), this.m_RotateSpeed);
		}

		private bool CanShoot()
		{
			bool result = false;
			if (base.drone.TankSelf != null)
			{
				result = this.m_WeaponComponent.ReadyToFire();
			}
			if (this.m_PreventShootingTowardsFloor && this.m_WeaponComponent.IsAimingAtFloor(this.m_LimitedShootAngle))
			{
				result = false;
			}
			return result;
		}

		private Vector3 AimPointWithTrajectory(Vector3 aimPoint)
		{
			// Do target prediction
			Vector3 adjustedTarget = aimPoint;

			// Do Gravity targeting
			float velocity = this.m_WeaponComponent.GetVelocity();
			Vector3 relDistance = adjustedTarget - this.m_WeaponComponent.GetFireTransform().position;
			float magnitude = Physics.gravity.magnitude;
			float sqrMagnitude = relDistance.SetY(0f).sqrMagnitude;
			float sqrVelocity = velocity * velocity;
			float num2 = sqrVelocity * sqrVelocity - magnitude * (magnitude * sqrMagnitude + (relDistance.y + relDistance.y) * sqrVelocity);
			num2 = ((num2 < 0f) ? 0f : Mathf.Sqrt(num2));
			Vector3 result = adjustedTarget;
			result.y += (sqrVelocity - num2) / magnitude - relDistance.y;
			return result;
		}

		private void ControlInputTargeted(Vector3 targetPositionWorld, float targetRadiusWorld)
		{
			this.SetAimTarget(targetPositionWorld);
			Vector3 lhs = targetPositionWorld - base.transform.position;
			lhs.y = 0f;
			Vector3 forward = this.m_WeaponComponent.GetFireTransform().forward;
			Vector3 normalized = new Vector3(forward.z, 0f, -forward.x).normalized;
			if (Mathf.Abs(Vector3.Dot(lhs, normalized)) < targetRadiusWorld && Vector3.Dot(lhs, forward) > 0f)
			{
				return;
			}
		}

		private void ControlInputManual(int aim, bool fire)
		{
			this.AimControl = aim;
			this.m_TargetPosition = Vector3.zero;
		}
		#endregion

		public void AddRemoteShotFired()
		{
			this.m_RemoteShotFiredPending++;
		}
	}
}
