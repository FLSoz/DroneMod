using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src.DroneModules
{
    public class DroneSubModule : MonoBehaviour
    {
		[HideInInspector]
		[SerializeField]
		private Drone _drone;

		public Drone drone
		{
			get
			{
				return this._drone;
			}
		}

		private void PrePool()
		{
			this._drone = this.GetComponentInParents<Drone>(true);
		}
	}
}
