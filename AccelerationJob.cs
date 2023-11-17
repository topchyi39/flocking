using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Flock.Scripts.Flock
{
    [BurstCompile]
    public struct AccelerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> Positions;
        [ReadOnly] public NativeArray<Vector3> Directions;
        [ReadOnly] public NativeArray<Vector3> RaycastHits;
        [ReadOnly] public NativeArray<int> Groups;
        
        [WriteOnly] public NativeArray<Vector3> Accelerations;
        [ReadOnly] public NativeArray<Vector3> TargetPosition;
        
        public float TargetWeight;
        public Vector4 Weights;
        public Vector3 Extends;
        public Vector3 Center;
        public float VisionRadius;
        public float CollisionRadius;

        public void Execute(int index)
        {
            var bounds = new Bounds(Center, Extends);
            
            var entityPosition = Positions[index];

            var inBounds = bounds.Contains(entityPosition);

            if (!inBounds)
            {
                var inBoundsDirection = Center - entityPosition;
                
                // Check raycast and move to inverse hii direction
                if (RaycastHits[index].magnitude > 0)
                {
                    inBoundsDirection += RaycastHits[index] * 100f;
                }

                Accelerations[index] = inBoundsDirection;
                return;
            }
            
            // Check raycast and move to inverse hii direction
            if (RaycastHits[index].magnitude > 0)
            {
                Accelerations[index] = RaycastHits[index] * 100f;
                return;
            }

            var cohesion = Vector3.zero;
            var separation = Vector3.zero;
            var alignment = Vector3.zero;

            var groupSize = 0;
            var alignmentSize = 0;
            
            var arrayLength = Positions.Length;
            
            for (var i = 0; i < arrayLength; i++)
            {
                if (i == index) continue;

                var neighbourPosition = Positions[i];
                var neighbourDistance = Vector3.Distance(entityPosition, neighbourPosition);

                if (neighbourDistance > VisionRadius)
                {
                    if (Groups[i] == Groups[index])
                    {
                        cohesion += neighbourPosition;
                        groupSize++;
                    }
                    
                    continue;
                }

                groupSize++;
                cohesion += neighbourPosition;
                
                // Add separation vector for another group
                if (Groups[i] != Groups[index])
                {
                    var vectorAway = (entityPosition - neighbourPosition).normalized * Weights.w;
                    separation += vectorAway;
                }
                else // Add alignment vector for group
                {
                    alignment += Directions[i];
                    alignmentSize++;
                }
                
                // Add separation vector if neighbour in collision radius
                if (neighbourDistance < CollisionRadius)
                {
                    var vectorAway = entityPosition - neighbourPosition;
                    separation += vectorAway;
                }

            }

            if (groupSize <= 0) return;
            
            cohesion /= groupSize;
            if (alignmentSize > 0) alignment /= alignmentSize;
            
            var targetDirection = (TargetPosition[Groups[index]] - entityPosition);
                
            var direction = (cohesion * Weights.x + separation * Weights.y + alignment * Weights.z) 
                            - entityPosition  
                            + targetDirection * TargetWeight;
            if (direction.magnitude > 0)
            {
                Accelerations[index] = direction;
            }
        }
    }
}