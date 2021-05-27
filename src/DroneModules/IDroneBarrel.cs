using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src.DroneModules
{
    public interface IDroneBarrel
    {
		float Range { get; }

		void Setup(FireData firingData, ModuleWeapon weapon);

		bool HasClearLineOfFire();

		bool OnClientFire(Vector3 projectileSpawnPoint_forward, Vector3 spin, bool seeking, int projectileUID);

		bool PrepareFiring(bool prepareFiring);

		bool Fire(bool seeking);

		float GetFireRateFraction();
	}
}
