using System;
using System.Collections.Generic;
using UnityEngine;
using Vectrosity;

namespace StudioItemIk
{
    public class FabrikIK : MonoBehaviour
    {
        /// <summary>
        /// List of VectorLines used for Gizmos
        /// </summary>
        public List<VectorLine> lines = new List<VectorLine>();

        /// <summary>
        /// if is initalized
        /// </summary>
        public bool isInit = false;

        /// <summary>
        /// string used to create the gizmolines
        /// </summary>
        public string hashString;

        /// <summary>
        /// Chain length of bones
        /// Can be changed during Runtime
        /// </summary>
        public int ChainLength = 2;

        /// <summary>
        /// Target the chain should go towards
        /// </summary>
        public Transform Target;

        /// <summary>
        /// Poles the chain should bent to
        /// </summary>
        public List<Transform> Poles = new List<Transform>();

        /// <summary>
        /// Solver iterations per update
        /// </summary>
        [Header("Solver Parameters")]
        public int Iterations = 10;

        /// <summary>
        /// Distance from leafbone to target at which the solver stops
        /// </summary>
        public float Delta = 0.001f;

        protected float[] BonesLength;
        protected float CompleteLength;
        protected Transform[] Bones;
        protected Vector3[] Positions;
        // rotation
        protected Vector3[] StartDirectionSucc;
        protected Quaternion[] StartRotationBone;
        protected Quaternion StartRotationTarget;

        void Awake()
        {
            //Init();
        }
        /// <summary>
        /// Has to be called once, after Target and hashString and optionally poles have been set.
        /// </summary>
        public void Init()
        {
            Bones = new Transform[ChainLength + 1];
            Positions = new Vector3[ChainLength + 1];
            BonesLength = new float[ChainLength];
            //rotation
            StartDirectionSucc = new Vector3[ChainLength + 1];
            StartRotationBone = new Quaternion[ChainLength + 1];

            if (Target == null)
            {
                StudioItemIk.Logger.LogError("No Target");
                return;
            }
            if (hashString == null)
            {
                StudioItemIk.Logger.LogError("Missing Hashstring");
                return;
            }
            if (Poles.Count > Bones.Length)
            {
                StudioItemIk.Logger.LogError("More poles than bones to map them to");
                return;
            }

            StartRotationTarget = Target.rotation; // set start rotation of the target

            CompleteLength = 0;
            var current = this.transform;
            for (var i = Bones.Length - 1; i >= 0; i--)
            {
                Bones[i] = current;
                current = current.parent;
            }
            current = this.transform;
            for (var i = Bones.Length - 1; i >= 0; i--)
            {
                if (i == Bones.Length - 1)
                {
                    StartDirectionSucc[i] = Target.position - current.position; //set Successor to Leaf-Target vector
                }
                else
                {
                    StartDirectionSucc[i] = Bones[i + 1].position - current.position; // set Sec
                    BonesLength[i] = (Bones[i + 1].position - current.position).magnitude;
                    CompleteLength += BonesLength[i];
                }
                StartRotationBone[i] = current.rotation;

                current = current.parent;
            }
            isInit = true;
            setGizmo();
        }
        /// <summary>
        /// Should be called whenever the Targets or Poles are moved
        /// Automatically calls setGizmos()
        /// </summary>
        public void ResolveIK()
        {
            if (Target == null || !isInit || (Poles.Count > Bones.Length))
                return;

            if (BonesLength.Length != ChainLength)
                Init();

            //get position
            for (int i = 0; i < Bones.Length; i++)
            {
                Positions[i] = Bones[i].position;
            }
            Quaternion RootRot = (Bones[0].parent != null) ? Bones[0].parent.rotation : Quaternion.identity; //get rotation of root bone
            Quaternion RootRotDiff = RootRot * Quaternion.Inverse(StartRotationBone[0]);

            //calc target
            if ((Target.position - Bones[0].position).sqrMagnitude >= CompleteLength * CompleteLength)
            {
                var direction = (Target.position - Positions[0]).normalized;
                for (int i = 1; i < Positions.Length; i++)
                {
                    Positions[i] = Positions[i - 1] + direction * BonesLength[i - 1];
                }
            }
            else
            {
                for (int j = 0; j < Iterations; j++)
                {
                    if ((Positions[Positions.Length - 1] - Target.position).sqrMagnitude < Delta * Delta)
                        break;

                    //FABRIC algorithm
                    //backwarts
                    for (int i = Positions.Length - 1; i > 0; i--)
                    {
                        if (i == Positions.Length - 1)
                            Positions[i] = Target.position; // set leaf bone to target
                        else
                        {
                            Positions[i] = Positions[i + 1] + (BonesLength[i] * (Positions[i] - Positions[i + 1]).normalized);
                        }
                    }
                    //forwards
                    for (int i = 1; i < Bones.Length; i++)
                    {
                        Positions[i] = Positions[i - 1] + (BonesLength[i - 1] * (Positions[i] - Positions[i - 1]).normalized);
                    }
                }
            }
            //calc poles
            if (Poles != null && Poles.Count > 0)
            {
                //StudioItemIk.Logger.LogInfo("Pole shittery");
                if (Poles.Count == 1)
                {
                    for (int i = 1; i < Positions.Length - 1; i++)
                    {
                        Transform Pole = Poles[0];
                        Plane plane = new Plane(Positions[i + 1] - Positions[i - 1], Positions[i - 1]); //create projectionplane perpendicular to the rotationaxis [i-1]-[i+1]
                        Vector3 projectedPole = ClosestPointOnPlane(plane, Pole.position);   // pole shadow
                        Vector3 projectedBone = ClosestPointOnPlane(plane, Positions[i]);    // bone shadow
                        float angle = SignedAngle(projectedBone - Positions[i - 1], projectedPole - Positions[i - 1], plane.normal); // get angle between Vector Center-boneshadow and Vector Center-poleshadow, where center is [i-1]
                        Positions[i] = Quaternion.AngleAxis(angle, plane.normal) * (Positions[i] - Positions[i - 1]) + Positions[i - 1]; // get vector from [i-1] to [i], place it onto [i-1] and rotate it with the above angle => now position
                    }
                }
                else
                {
                    for (int i = 1; i < Positions.Length - 1; i++)
                    {
                        int poleIndex = (int)(((float)Poles.Count / (float)(Positions.Length - 2)) * (float)(i - 1));
                        Transform Pole = Poles[poleIndex];
                        Plane plane = new Plane(Positions[i + 1] - Positions[i - 1], Positions[i - 1]); //create projectionplane perpendicular to the rotationaxis [i-1]-[i+1]
                        Vector3 projectedPole = ClosestPointOnPlane(plane, Pole.position);   // pole shadow
                        Vector3 projectedBone = ClosestPointOnPlane(plane, Positions[i]);    // bone shadow
                        float angle = SignedAngle(projectedBone - Positions[i - 1], projectedPole - Positions[i - 1], plane.normal); // get angle between Vector Center-boneshadow and Vector Center-poleshadow, where center is [i-1]
                        Positions[i] = Quaternion.AngleAxis(angle, plane.normal) * (Positions[i] - Positions[i - 1]) + Positions[i - 1]; // get vector from [i-1] to [i], place it onto [i-1] and rotate it with the above angle => now position
                    }
                }
            }

            //set position & rotation
            for (int i = 0; i < Bones.Length; i++)
            {
                Bones[i].position = Positions[i];
                if (i == Positions.Length - 1) // is leaf bone?
                {
                    Bones[i].rotation = Target.rotation * Quaternion.Inverse(StartRotationTarget) * StartRotationBone[i];
                }
                else
                {
                    Bones[i].rotation = Quaternion.FromToRotation(StartDirectionSucc[i], Positions[i + 1] - Positions[i]) * StartRotationBone[i];
                }
            }

            setGizmo();
        }
        /// <summary>
        /// Draws Gizmo onto screen, has to be recalled if the bonechain moved on screen
        /// </summary>
        public void drawGizmo()
        {
            if (!StudioItemIk.draw || !isInit)
                return;
            foreach (VectorLine line in lines)
                line.Draw();
        }
        /// <summary>
        /// Destroys Gizmolines and empties gizmolist
        /// </summary>
        public void DestroyGizmo()
        {
            VectorLine.Destroy(lines);
            lines.Clear();
        }
        /// <summary>
        /// Creates Vectorlines for the bonechain and poles
        /// </summary>
        public void setGizmo()
        {
            if (!isInit)
                return;
            VectorLine.Destroy(lines);
            lines.Clear();
            List<Vector3> point = new List<Vector3>();
            foreach (Transform bone in Bones)
            {
                point.Add(bone.position);
                point.Add(bone.position + new Vector3(0, 0.01f, 0));
                point.Add(bone.position);
            }
            VectorLine line = new VectorLine($"{hashString}_IKChainLine", point, 2.0f, LineType.Continuous);
            line.color = Color.green;
            lines.Add(line);

            if (Poles != null && Poles.Count > 0)
            {
                List<Vector3> poleChainPoints = new List<Vector3>();
                if (Poles.Count == 1)
                {
                    for (int i = 1; i < Bones.Length - 1; i++)
                    {
                        Transform bone = Bones[i];
                        poleChainPoints.Add(bone.position);
                        poleChainPoints.Add(Poles[0].position);
                    }
                }
                else
                {
                    for (int i = 1; i < Bones.Length - 1; i++)
                    {
                        int poleIndex = (int)(((float)Poles.Count / (float)(Positions.Length - 2)) * (float)(i - 1));
                        Transform bone = Bones[i];
                        poleChainPoints.Add(bone.position);
                        poleChainPoints.Add(Poles[poleIndex].position);
                    }
                }
                VectorLine poleLine = new VectorLine($"{hashString}_PoleLine", poleChainPoints, 2.0f, LineType.Discrete);
                poleLine.color = Color.yellow;
                lines.Add(poleLine);
            }
            drawGizmo();
        }
        void OnDestroy()
        {
            VectorLine.Destroy(lines);
            StudioItemIk.activeIKs.Remove(this);
        }

        // just swooped these two of the fokin unity github...
        public Vector3 ClosestPointOnPlane(Plane plane, Vector3 point)
        {
            var pointToPlaneDistance = Vector3.Dot(plane.normal, point) + plane.distance;
            return point - (plane.normal * pointToPlaneDistance);
        }
        private float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float unsignedAngle = Vector3.Angle(from, to);

            float cross_x = from.y * to.z - from.z * to.y;
            float cross_y = from.z * to.x - from.x * to.z;
            float cross_z = from.x * to.y - from.y * to.x;
            float sign = Mathf.Sign(axis.x * cross_x + axis.y * cross_y + axis.z * cross_z);
            return unsignedAngle * sign;
        }
    }
}
