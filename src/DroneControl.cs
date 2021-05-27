using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DroneMod.src.DroneModules;


namespace DroneMod.src
{
    public enum MovementMode : byte
    {
        Circle,
        Bomb,
        Intercept
    }

    public class DroneControl : DroneModule
    {
        #region Movement/AI params
        [SerializeField]
        public bool droneGravity = false;

        [SerializeField]
        public int maxSpeed = 100;
        [SerializeField]
        public float acceleration = 10.0f;
        [SerializeField]
        public float turnSpeed = 20.0f;

        [SerializeField]
        public MovementMode movementMode = MovementMode.Circle;

        // circle/broadside parameters
        [SerializeField]
        public float turnOutRadius = 50.0f; // At what distance do you start turning out again
        [SerializeField]
        public float turnInRadius = 100.0f; // At what distance do you start turning in again

        // intercept parameters
        [SerializeField]
        public bool intercept = false;
        [SerializeField]
        public float muzzleVelocity = 100.0f;
        [SerializeField]
        public bool useGravity = false;

        // Attack parameters (non-intercept)
        [SerializeField]
        public Vector3 targetOffset = Vector3.zero;

        // Generic movement parameters
        [SerializeField]
        public float targetHeight = 30.0f;
        [SerializeField]
        public bool alwaysMove = true;
        [SerializeField]
        public float maxClimbAngle = 25.0f;
        [SerializeField]
        public float approximateRadius = 1.0f;
        [SerializeField]
        public float collisionAvoidanceTime = 2.0f;
        [SerializeField]
        public float maxBankAngle = 25.0f;

        private int movementLayer = 0;
        private int movementLayers = 1;
        private float MaxDriftDist = 10.0f;
        #endregion

        public Rigidbody rbody { get; private set; }

        private bool debug = true;
        private void DebugPrint(string message)
        {
            if (this.debug)
            {
                Console.WriteLine("[DM] " + message);
            }
        }

        internal static float ClampAngle360(float angle)
        {
            float modAngle = angle % 360;
            if (modAngle < 0)
            {
                modAngle += 360.0f;
            }
            return modAngle;
        }

        // turn towards direction
        private void TurnTowards(Vector3 direction)
        {
            // DebugPrint($"[DM] - Turn towards {direction} at position {this.transform.position}");
            Vector3 normalized = Vector3.Cross(this.transform.forward, direction).normalized;
            float b = Vector3.SignedAngle(this.transform.forward, direction, normalized);

            // Vector3 changedYaw = Quaternion.AngleAxis(yawAngle, Vector3.up) * this.transform.forward;
            // Vector3 newForward = Quaternion.AngleAxis(yawAngle, Vector3.up) * changedYaw;

            float oldPitch = ClampAngle360(Mathf.Atan2(this.transform.forward.y, this.transform.forward.SetY(0).magnitude) * Mathf.Rad2Deg);
            float oldYaw = ClampAngle360(Mathf.Atan2(this.transform.forward.x, this.transform.forward.z) * Mathf.Rad2Deg);

            float targetPitch = ClampAngle360(Mathf.Atan2(direction.y, direction.SetY(0).magnitude) * Mathf.Rad2Deg);
            float targetYaw = ClampAngle360(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);

            // float newPitch = Mathf.MoveTowardsAngle(oldPitch, targetPitch, this.turnSpeed * Time.fixedDeltaTime / 2);
            // float newYaw = Mathf.MoveTowardsAngle (oldYaw, targetYaw, this.turnSpeed * Time.fixedDeltaTime / 2);

            /* float remainingMovement = (this.turnSpeed * Time.fixedDeltaTime) - (Mathf.DeltaAngle(newYaw, oldYaw) + Mathf.DeltaAngle(newPitch, oldPitch));
            float remainingPitch = Mathf.DeltaAngle(targetPitch, newPitch);
            float remainingYaw = Mathf.DeltaAngle(targetYaw, newYaw);
            float remainingTargetMovement = remainingPitch + remainingYaw;

            newPitch = Mathf.MoveTowardsAngle(newPitch, targetPitch, remainingPitch / remainingTargetMovement * remainingMovement);
            newYaw = Mathf.MoveTowardsAngle(newYaw, targetYaw, remainingYaw / remainingTargetMovement * remainingMovement);

            Vector3 newForward = new Vector3(Mathf.Tan(newYaw * Mathf.Deg2Rad), 0, 1.0f);
            newForward = newForward.SetY(newForward.magnitude * Mathf.Tan(newPitch * Mathf.Deg2Rad)).normalized; */

            Quaternion quaternion = Quaternion.AngleAxis(Mathf.Min(this.turnSpeed * Time.fixedDeltaTime, Mathf.Abs(b)) * Mathf.Sign(b), normalized);
            // Quaternion quaternion = Quaternion.LookRotation(newForward);

            // clamp bank/Z angle
            Vector3 newForward = quaternion * this.transform.forward;
            Vector3 newUp = Vector3.up;

            if (this.maxBankAngle > 0.0f)
            {
                float maxBankAngleDelta = 1.0f;
                Vector3 newRight = Vector3.Cross(newForward, Vector3.up);

                float turnAngle = -Vector3.SignedAngle(this.transform.forward, direction, Vector3.up);
                /* Vector3 newUpIdeal = Vector3.MoveTowards(Vector3.up, newRight * Mathf.Sign(turnAngle), Mathf.Min(Mathf.Abs(turnAngle), this.maxBankAngle));
                if (newUpIdeal.y < 0.0f)
                {
                    newUpIdeal = -newUpIdeal;
                } */
                // newUp = Vector3.MoveTowards(this.transform.up, newUpIdeal, maxBankAngleDelta);
                float currentAngle = Vector3.SignedAngle(Vector3.up, this.transform.up, this.transform.forward);
                float targetAngle = Mathf.Sign(turnAngle) * Mathf.Min(Mathf.Abs(turnAngle), this.maxBankAngle);
                float newAngle = currentAngle >= -this.maxBankAngle * 1.5f && currentAngle <= this.maxBankAngle * 1.5f ? Mathf.MoveTowards(currentAngle, targetAngle, maxBankAngleDelta) : targetAngle;
                DebugPrint($"[DM]   - We want a bank angle of {targetAngle}. Clamp means we go {currentAngle} ==[{maxBankAngleDelta}]=> {newAngle}");
                Vector3 properDirection = newRight * Mathf.Sign(newAngle);
                newUp = properDirection.SetY(properDirection.magnitude / Mathf.Abs(Mathf.Tan(Mathf.Deg2Rad * newAngle)));
                // newUp = Vector3.MoveTowards(Vector3.up, newRight * Mathf.Sign(newAngle), Mathf.Abs(newAngle));
            }
            Quaternion clampedRot = Quaternion.LookRotation(newForward, newUp);

            // need protection for horizontal?
            this.rbody.velocity = quaternion * this.transform.forward * this.rbody.velocity.magnitude;
            this.rbody.MoveRotation(clampedRot);

            float newPitch = Mathf.Atan2(this.rbody.velocity.y, this.rbody.velocity.SetY(0).magnitude) * Mathf.Rad2Deg;
            float newYaw = Mathf.Atan2(this.rbody.velocity.x, this.rbody.velocity.z) * Mathf.Rad2Deg;
            DebugPrint($"[DM] - Pitch goes {oldPitch} ==[{Mathf.Abs(newPitch - oldPitch)}]=> {newPitch} (target {targetPitch})");
            DebugPrint($"[DM] - Yaw goes {oldYaw} ==[{Mathf.Abs(newYaw - oldYaw)}]=> {newYaw} (target {targetYaw})");
        }

        private float GetDriftAngle(float height)
        {
            return 0.0f;
        }

        // Get the target angle for us
        private float GetTargetHeightAngle(float targetHeight)
        {
            /* float currentAngle = Vector3.SignedAngle(Vector3.forward, this.transform.forward, Vector3.Cross(this.transform.forward, Vector3.up));
            if (currentAngle > 90.0f)
            {
                currentAngle = 180.0f - currentAngle;
            }
            else if (currentAngle < -90.0f)
            {
                currentAngle = -180.0f - currentAngle;
            } */
            float heightDifference = Mathf.Abs(targetHeight - this.transform.position.y);
            /* if (heightDifference >= this.MaxDriftDist)
            {
                return 80.0f;
            }

            float allocatableTurnSpeed = this.turnSpeed * Time.fixedDeltaTime / 2 * Mathf.Deg2Rad;
            float constant = heightDifference * allocatableTurnSpeed * allocatableTurnSpeed / (this.maxSpeed * Time.fixedDeltaTime);
            DebugPrint($"[DM]   - Height difference {heightDifference} has Constant {constant}");
            if (constant <= 2 * Mathf.PI)
            {
                // equation is Constant approximately equal to x^2 / 3 + x, where x = angle
                // solve quadratic equation (1/3)x^2 + x - C = 0 for first positive root
                float det = Mathf.Sqrt(1 + (4 * constant / 3));
                float angle = 3 * (det - 1) / 2;
                if (angle > 0 && angle < Mathf.PI)
                {
                    if (angle > Mathf.PI / 2)
                    {
                        angle = Mathf.PI - angle;
                    }
                    float convertedAngle = Mathf.Abs(angle) * Mathf.Rad2Deg;
                    DebugPrint($"[DM]   - Initial phase approximation {angle} => {convertedAngle}");
                    return Mathf.Min(80.0f, convertedAngle);
                }
                else
                {
                    return 10.0f;
                }
            }
            else
            {
                int lastPi = Mathf.FloorToInt(constant / (2 * Mathf.PI));
                if (lastPi % 2 == 1)
                {
                    lastPi++;
                }
                // we have a line from 0 at lastPi*PI to 2*(lastPi+1)*PI at (lastPi+1)*PI
                // should probably be 2*(lastPi+1)*PI*sin(x/2)
                float numPi = (lastPi + 1) * Mathf.PI;
                float angle = Mathf.Asin((constant - numPi) / numPi) + (Mathf.PI / 2);

                // should never happen - is here just in case
                if (float.IsNaN(angle))
                {
                    return 25.0f;
                }

                if (angle > Mathf.PI / 2)
                {
                    angle = Mathf.PI - angle;
                }

                // float frac = Mathf.InverseLerp(0, 2 * (lastPi + 1) * Mathf.PI, constant);
                // float angle = frac * Mathf.PI;
                float convertedAngle = Mathf.Abs(angle) * Mathf.Rad2Deg;
                DebugPrint($"[DM]   - Later approximation {angle} => {convertedAngle} with LAST PI {lastPi}");
                return Mathf.Min(80.0f, convertedAngle);
            } */

            if (heightDifference >= this.MaxDriftDist)
            {
                return 80.0f;
            }
            else if (heightDifference <= Mathf.Epsilon)
            {
                return 0.0f;
            }
            else
            {
                float allocatableTurnSpeed = this.turnSpeed / 4 * Time.fixedDeltaTime;
                float dist = 0.0f;
                int maxJoints = Mathf.CeilToInt(90.0f / allocatableTurnSpeed);
                float angle = 0.0f;

                float speed = Mathf.Max(this.rbody.velocity.magnitude, this.maxSpeed);

                for (int i = 1; i < maxJoints; i++)
                {
                    float distSegment = Mathf.Abs(Mathf.Sin((angle + allocatableTurnSpeed) * Mathf.Deg2Rad) * Time.fixedDeltaTime * speed);
                    dist += distSegment;
                    if (dist >= heightDifference)
                    {
                        return angle;
                    }
                    angle += allocatableTurnSpeed;
                }
                DebugPrint($"[DM]   - We have calculated a travelled distance of {dist}, angle [{allocatableTurnSpeed} x n] = {angle}");
                return Mathf.Min(80.0f, Mathf.Max(angle, allocatableTurnSpeed));
                
                /* if (Time.fixedDeltaTime == 0.0f)
                {
                    throw new Exception("FIXEDDELTATIME IS 0");
                }
                return Mathf.Min(80.0f, Mathf.Abs(Mathf.Acos(heightDifference / (this.maxSpeed * Time.fixedDeltaTime)) * Mathf.Rad2Deg)); */
            }
        }

        private void Circle(Visible target, bool isEnemy)
        {
            DebugPrint($"[DM] Circle around {target} AT {target.transform.position}, We are at {this.transform.position}");
            Vector3 targetCenterPosition = target.transform.position;
            float targetRadius = 0.0f;
            if (target.tank)
            {
                Bounds bounds = target.tank.blockBounds;
                targetCenterPosition += bounds.center;
                targetRadius = bounds.extents.magnitude;
            }
            Vector3 targetVector = targetCenterPosition + this.targetOffset - this.transform.position;

            float distance = targetVector.magnitude - targetRadius;
            // targetVector.y = 0.0f;
            float speed = this.rbody.velocity.magnitude;
            // DebugPrint($"[DM] Target is at {targetCenterPosition}, we are {distance} distance");

            // Determine wanted direction
            Vector3 targetPosition;
            if (distance < this.turnOutRadius)
            {
                // DebugPrint("[DM] - Turn out");
                // sharp turn out
                targetPosition = (-targetVector.normalized * Time.fixedDeltaTime * speed) + this.transform.position;
            }
            else if (distance > this.turnInRadius)
            {
                // DebugPrint("[DM] - Turn In");
                // sharp turn in
                targetPosition = (targetVector.normalized * Time.fixedDeltaTime * speed) + this.transform.position;
            }
            else
            {
                // let line between target and you have equation z = mx
                // Perpendicular/tangent line has equation z = -x/m
                Vector3 tangentSlope = new Vector3(1.0f, 0.0f, -targetVector.x / targetVector.z).normalized;
                Vector3 target1 = this.transform.position + (tangentSlope * Time.fixedDeltaTime * speed);
                Vector3 target2 = this.transform.position - (tangentSlope * Time.fixedDeltaTime * speed);

                float error1 = Vector3.Angle(tangentSlope, this.transform.forward);
                float error2 = Vector3.Angle(-tangentSlope, this.transform.forward);
                if (error1 > error2)
                {
                    targetPosition = target2;
                }
                else
                {
                    targetPosition = target1;
                }

                // randomize target radius
                // DebugPrint("[DM] - Circle as is");
            }
            targetPosition.y = targetCenterPosition.y + this.targetOffset.y;

            float offset = this.movementLayer * this.approximateRadius * 2;
            float tentativeHeight = target.transform.position.y + this.targetOffset.y;
            float minPossibleHeight = this.targetOffset.y > 0 ? targetPosition.y : Mathf.NegativeInfinity;
            float maxPossibleHeight = this.targetOffset.y < 0 ? targetPosition.y : Mathf.Infinity;

            bool avoidance = false;
            // Collision avoidance

            // Terrain avoidance
            float height;
            if (this.drone.m_ExplodeOnTerrain)
            {
                if (!Singleton.Manager<ManWorld>.inst.GetTerrainHeight(targetPosition, out height))
                {
                    DebugPrint("Getting terrain height failed");
                    height = 0.0f;
                }
                height += this.targetHeight;

                if (tentativeHeight < height)
                {
                    // adjust to go up sharp
                    tentativeHeight = height;
                }
                minPossibleHeight = height;

                avoidance = height - this.transform.position.y >= this.MaxDriftDist;
            }

            // randomize movement layer on height
            float centerLayer = this.movementLayers / 2.0f;
            if (minPossibleHeight != Mathf.NegativeInfinity)
            {
                if (maxPossibleHeight != Mathf.Infinity)
                {
                    // limited min/max range
                    float topLeeway = (maxPossibleHeight - tentativeHeight) / (this.approximateRadius * 2);
                    float bottomLeeway = (tentativeHeight - minPossibleHeight) / (this.approximateRadius * 2);

                    if (topLeeway + bottomLeeway <= 2 * this.approximateRadius * (this.movementLayers - 1))
                    {
                        // stack all within
                        tentativeHeight = (this.movementLayer * (maxPossibleHeight - minPossibleHeight) / this.movementLayers) + minPossibleHeight;
                    }
                    else
                    {
                        // center it
                        float center = topLeeway - ((topLeeway + bottomLeeway) / 2.0f);
                        tentativeHeight += center + (centerLayer - this.movementLayer) * this.approximateRadius * 2;
                    }
                }
                else
                {
                    // only min
                    float bottomLeeway = (tentativeHeight - minPossibleHeight) / (this.approximateRadius * 2);
                    if (bottomLeeway <= this.approximateRadius * (this.movementLayers - 1))
                    {
                        // center it
                        float center = (this.approximateRadius * (this.movementLayers - 1)) - bottomLeeway;
                        tentativeHeight += center + (centerLayer - this.movementLayer) * this.approximateRadius * 2;
                    }
                    else
                    {
                        tentativeHeight += (centerLayer - this.movementLayer) * this.approximateRadius * 2;
                    }

                    DebugPrint($"[DM]   - We have target height of {target.transform.position.y + this.targetOffset.y}, min height of {minPossibleHeight}, [Layer {this.movementLayer}] ==[{this.approximateRadius} x 2]==> {tentativeHeight}");
                }
            }
            else if (maxPossibleHeight != Mathf.Infinity)
            {
                // only max
                float topLeeway = (maxPossibleHeight - tentativeHeight) / (this.approximateRadius * 2);
                if (topLeeway <= this.approximateRadius * (this.movementLayers - 1))
                {
                    // center it
                    float center = topLeeway - (this.approximateRadius * (this.movementLayers - 1));
                    tentativeHeight += center + (centerLayer - this.movementLayer) * this.approximateRadius * 2;
                }
                else
                {
                    tentativeHeight += (centerLayer - this.movementLayer) * this.approximateRadius * 2;
                }
            }
            else
            {
                // neither
                tentativeHeight += (centerLayer - this.movementLayer) * this.approximateRadius * 2;
            }

            /* if ((maxPossibleHeight - minPossibleHeight) < (2 * this.approximateRadius * this.movementLayers))
            {
                tentativeHeight = (this.movementLayer * (maxPossibleHeight - minPossibleHeight) / this.movementLayers) + minPossibleHeight;
            }
            else if (minPossibleHeight != Mathf.NegativeInfinity)
            {
                tentativeHeight = (this.movementLayer * this.approximateRadius * 2) + minPossibleHeight;
            }
            else if (maxPossibleHeight != Mathf.Infinity)
            {
                tentativeHeight = maxPossibleHeight - (this.movementLayer * this.approximateRadius * 2);
            } */
            targetPosition.y = tentativeHeight;
            DebugPrint($"[DM]   - We are targeting a position of {targetPosition}");

            // Adjust vertical angle to make corrections less extreme
            Vector3 targetDirection = targetPosition - this.transform.position;
            if (!avoidance)
            {
                float groundDistance = Mathf.Sqrt(targetDirection.x * targetDirection.x + targetDirection.z * targetDirection.z);

                // x angle is height
                float xAngle = Mathf.Abs(Mathf.Atan2(this.transform.forward.y, this.transform.forward.SetY(0).magnitude) * Mathf.Rad2Deg);
                // y angle is yaw
                // float yAngle = Vector3.SignedAngle(targetDirection, this.transform.forward, Vector3.up);
                // z angle is roll - ignore
                float clampAngle = this.GetTargetHeightAngle(tentativeHeight);

                // want to eventually finish and level out 
                float newY = Mathf.Abs(Mathf.Tan(Mathf.Min(xAngle, clampAngle) * Mathf.Deg2Rad)) * groundDistance * Mathf.Sign(targetDirection.y);
                DebugPrint($"[DM]   - We have a vertical angle of {xAngle}, clamped by {clampAngle} for relative height {Mathf.Abs(tentativeHeight - this.transform.position.y)}");
                targetDirection.y = newY;
            }

            DebugPrint($"[DM]   - We are targeting {targetDirection}");
            this.TurnTowards(targetDirection);
            float currSpeed = this.rbody.velocity.magnitude;
            if (this.alwaysMove)
            {
                // always max speed
                this.rbody.velocity = this.rbody.velocity.normalized * Mathf.MoveTowards(currSpeed, this.maxSpeed, this.acceleration);
            }
            else
            {
                // slow down
                this.rbody.velocity = this.rbody.velocity.normalized * Mathf.MoveTowards(currSpeed, 0, this.acceleration);
            }
            DebugPrint($"[DM]   - Current velocity {currSpeed} ==[{this.acceleration}]=> {this.rbody.velocity.magnitude} (should be {this.maxSpeed})");
        }

        // bomb an enemy - go directly over them with target heigh offset. 0 will kamikaze
        private void Bomb(Vector3 target)
        {

        }

        // Intercept an enemy
        private void Intercept(Visible enemy)
        {

        }

        // Escort a friendly - assumes no always move - so do formation
        private void Escort(Visible mothership)
        {

        }

        public void Control(Visible target, bool isEnemy)
        {
            Vector3 targetPosition = target.transform.position;
            if (!isEnemy)
            {
                if (this.alwaysMove)
                {
                    this.Circle(target, isEnemy);
                }
                else
                {
                    this.Escort(target);
                }
            }
            else
            {
                switch (this.movementMode)
                {
                    case MovementMode.Circle:
                        this.Circle(target, isEnemy);
                        break;
                    case MovementMode.Bomb:
                        this.Bomb(targetPosition);
                        break;
                    case MovementMode.Intercept:
                        this.Intercept(target);
                        break;
                }
            }
        }

        public void HandleLaunch()
        {
            float currSpeed = this.rbody.velocity.magnitude;
            this.rbody.velocity = this.rbody.velocity.normalized * Mathf.MoveTowards(currSpeed, this.maxSpeed, this.acceleration);
        }

        public void Launch(ModuleHangar hangar)
        {
            this.movementLayers = Mathf.RoundToInt(this.drone.hangar.m_MaxDrones / 2.0f);
            this.movementLayer = UnityEngine.Random.Range(0, this.movementLayers - 1);
        }

        private void PrePool()
        {
        }

        private void OnSpawn()
        {
            this.rbody = this.drone.rbody;
            if (!this.rbody)
            {
                this.rbody = this.gameObject.AddComponent<Rigidbody>();
            }

            float allocatableTurnSpeed = this.turnSpeed * Time.fixedDeltaTime / 2;
            float dist = 0.0f;
            int maxJoints = Mathf.CeilToInt(90.0f / allocatableTurnSpeed);
            for (int i = 0; i < maxJoints; i++)
            {
                float angle = allocatableTurnSpeed * i;
                if (angle < 90.0f)
                {
                    dist += Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad) * Time.fixedDeltaTime * this.maxSpeed);
                }
            }
            float remainder = 90.0f - (allocatableTurnSpeed * (maxJoints - 1));
            if (remainder > 0.0f)
            {
                dist += Mathf.Abs(Mathf.Sin(remainder * Mathf.Deg2Rad) * Time.fixedDeltaTime * this.maxSpeed);
            }
            // this.MaxDriftDist = this.maxSpeed / (allocatableTurnSpeed * allocatableTurnSpeed);
            this.MaxDriftDist = Mathf.Max(20.0f, dist);
            DebugPrint($"[DM] MAX DRIFT DIST = {this.MaxDriftDist}");
        }
    }
}
