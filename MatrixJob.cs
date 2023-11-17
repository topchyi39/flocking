using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Flock.Scripts.Flock
{   
    [BurstCompile]
    public struct MatrixJob : IJobParallelFor
    {
        public NativeArray<Vector3> Positions;
        public NativeArray<Quaternion> Rotations;
        public NativeArray<Vector3> Directions;
        public NativeArray<Vector3> Accelerations;
        public NativeArray<Matrix4x4> Matrices;
        

        public float DeltaTime;
        public Vector2 Speed;
        public Vector3 Scale;

        public void Execute(int index)
        {
            var direction = Accelerations[index];
            direction = direction.normalized * Mathf.Clamp(direction.magnitude, Speed.x, Speed.y);

            var prevRotation = Rotations[index];
            
            var rotation = Quaternion.Slerp(prevRotation, Quaternion.LookRotation(direction), DeltaTime).normalized;
            var velocity = rotation * Vector3.forward;
            
            Positions[index] += velocity * direction.magnitude * DeltaTime;
            Rotations[index] = rotation;
            Directions[index] = velocity;
            Matrices[index] = Matrix4x4.TRS(Positions[index], rotation, Scale);
        }
    }
}