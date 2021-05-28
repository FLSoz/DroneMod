using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DroneMod.src.DroneModules;


namespace DroneMod.src
{
	// An equivalent of DroneWeaponsControllers capabilities
	public class DroneWeaponsController : DroneModule
	{
		[SerializeField]
		[HideInInspector]
		public DroneWeapon m_MainArmament;

		public void Launch(ModuleHangar hangar)
		{
			for (int i = 0; i < this.m_WeaponComponents.Length; i++)
			{
				DroneWeapon weaponComponent = this.m_WeaponComponents[i];
				weaponComponent.Launch(hangar);
			}
		}

		#region Component Lifetime
		private void OnPool()
		{
			this.m_WeaponComponents = base.GetComponentsInChildren<DroneWeapon>();
			d.Assert(this.m_WeaponComponents != null, "DroneWeaponsController needs an DroneWeapon.");

			this.m_MainArmament = null;
			foreach (DroneWeapon weapon in this.m_WeaponComponents)
            {
				if (weapon.m_IsMainArmament)
                {
					this.m_MainArmament = weapon;
					break;
                }
            }
			if (this.m_MainArmament == null)
            {
				this.m_MainArmament = this.m_WeaponComponents[0];
			}
		}

		private void OnSpawn()
		{
		}

		private void OnRecycle()
		{
		}

		private void OnDepool()
		{
			this.WeaponsFiredEvent.EnsureNoSubscribers();
		}
		#endregion

		[SerializeField]
		[HideInInspector]
		private DroneWeapon[] m_WeaponComponents;

		#region TechWeapon Replication
		private void Update()
		{
			bool weaponsFired = false;
			foreach (DroneWeapon weapon in this.m_WeaponComponents)
            {
				// Console.WriteLine($"UPDATE Weapon.Process for {weapon.name}");
				if (weapon.Process() > 0)
                {
					weaponsFired = true;
                }
            }
			if (weaponsFired)
			{
				this.WeaponsFiredEvent.Send();
			}
		}

		public EventNoParams WeaponsFiredEvent;
		#endregion DroneWeaponsControllerReplication
	}
}
