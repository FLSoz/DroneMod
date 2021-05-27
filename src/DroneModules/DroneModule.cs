using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DroneMod.src.DroneModules
{
    [RequireComponent(typeof(Drone))]
    public class DroneModule : MonoBehaviour
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
			this._drone = base.GetComponent<Drone>();
		}
	}
}
