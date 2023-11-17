using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Flock.Scripts.Flock
{
    public class FlockController : MonoBehaviour
    {
        [SerializeField] private int entitiesCount = 100;
        [SerializeField] private Mesh entityMesh;
        [SerializeField] private Material entityMaterial;
        [SerializeField] private Vector3 entityScale = Vector3.one;
        
        [SerializeField] private bool useRaycasts = true;
        
        [Header("Bounds")]
        [SerializeField] private Vector3 bounds;
        [SerializeField, Range(0, 1)] private float boundsThreshold = 0.15f; 
        [Header("Group Settings")]
        [SerializeField, Range(1, 100)] private int groupCount = 10;
        [SerializeField, Range(0.1f, 5f)] private float avoidGroupStrength = 0.2f;
        [Header("Entity Settings")]
        [SerializeField, Range(0.1f, 10f)] private float minSpeed = 0.2f;
        [SerializeField, Range(0.1f, 10f)] private float maxSpeed = 1f;
        [SerializeField, Range(0.1f, 5f)] private float visionRadius = 2f;
        [SerializeField, Range(0.1f, 5f)] private float collisionRadius = 0.5f;
        [Header("Entity Weights")]
        [SerializeField, Range(0.1f, 5f)] private float cohesionStrength = 1f;
        [SerializeField, Range(0.1f, 5f)] private float avoidStrength = 1f;
        [SerializeField, Range(0.1f, 5f)] private float alignmentStrength = 1f;
        [SerializeField, Range(0.1f, 5f)] private float targetStrength = 2.33f;
        
        private NativeArray<int> _groups;
        private NativeArray<Vector3> _positions;
        private NativeArray<Quaternion> _rotations;
        private NativeArray<Vector3> _direction;
        private NativeArray<Vector3> _accelerations;
        private NativeArray<Vector3> _raycastHits;
        private NativeArray<Vector3> _targetPositions;

        private NativeArray<Matrix4x4> _matrices;

        private RenderParams _renderParams;
        
        private void Start()
        {

            if (entityMaterial == null)
            {
                entityMaterial = new Material(Shader.Find("Diffuse"));
                entityMaterial.enableInstancing = true;
            }
            
            _renderParams = new RenderParams(entityMaterial)
            {
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.On
            };
            
            InitArrays();
            
            for (var i = 0; i < groupCount; i++)
            {
                UpdateTargetPosition(i);
            }
            
            SpawnEntities();
        }
        
        private void InitArrays()
        {
            _groups = new NativeArray<int>(entitiesCount, Allocator.Persistent);
            _positions = new NativeArray<Vector3>(entitiesCount, Allocator.Persistent);
            _rotations = new NativeArray<Quaternion>(entitiesCount, Allocator.Persistent);
            _direction = new NativeArray<Vector3>(entitiesCount, Allocator.Persistent);
            _accelerations = new NativeArray<Vector3>(entitiesCount, Allocator.Persistent);
            _raycastHits = new NativeArray<Vector3>(entitiesCount, Allocator.Persistent);
            _targetPositions = new NativeArray<Vector3>(groupCount, Allocator.Persistent);
            _matrices = new NativeArray<Matrix4x4>(entitiesCount, Allocator.Persistent);
        }

        private void SpawnEntities()
        {
            for (var i = 0; i < entitiesCount; i++)
            {
                _groups[i] = i % groupCount;
                _positions[i] = GetRandomPositionInBounds();
                _rotations[i] = Quaternion.identity;
                _direction[i] = Random.insideUnitSphere;
            }
        }

        private void Update()
        {
            Move();
            DrawMeshes();
        }

        private void Move()
        {
            if (useRaycasts)
            {
                for (var i = 0; i < entitiesCount; i++)
                {
                    var forward = _direction[i];
                    var origin = _positions[i] - _direction[i] * visionRadius;

                    //Check world collision and write inverse ray direction
                    if (Physics.SphereCast(origin, collisionRadius, forward, out var hit, visionRadius * 2))
                    {
                        _raycastHits[i] = _positions[i] - hit.point;
                    }
                    else
                    {
                        _raycastHits[i] = Vector3.zero;
                    }
                }
            }

            var deltaTime = Time.deltaTime; 
            
            // Update Targets for groups
            for (var i = 0; i < groupCount; i++)
            {
                var weight = Random.Range(1, 1000);

                if (weight <= 1)
                {
                    UpdateTargetPosition(i);
                }
            }


            var accelerationJob = new AccelerationJob
            {
                Positions = _positions,
                Directions = _direction,
                RaycastHits = _raycastHits,
                Groups = _groups,
                Accelerations = _accelerations,
                TargetPosition = _targetPositions,
                Extends = bounds * (1f - boundsThreshold),
                Center = transform.position,
                TargetWeight = targetStrength,
                Weights = new Vector4(cohesionStrength, avoidStrength, alignmentStrength, avoidGroupStrength),
                VisionRadius = visionRadius,
                CollisionRadius = collisionRadius
            };
            
            var matrixJob = new MatrixJob()
            {
                Positions = _positions,
                Rotations = _rotations,
                Directions = _direction,
                Accelerations = _accelerations,
                Matrices = _matrices,
                Speed = new Vector2(minSpeed, maxSpeed),
                Scale = entityScale,
                DeltaTime = deltaTime
            };

            var accelerationHandle = accelerationJob.Schedule(entitiesCount, 0);
            var matrixHandle = matrixJob.Schedule(entitiesCount, 0, accelerationHandle);

            matrixHandle.Complete();
        }

        private void DrawMeshes()
        {
            var renderParams = new RenderParams();
            renderParams.material = entityMaterial;
            Graphics.RenderMeshInstanced(_renderParams, entityMesh, 0, _matrices, entitiesCount);
        }

        private void UpdateTargetPosition(int index)
        {
            _targetPositions[index] = GetRandomPositionInBounds();
        }

        private Vector3 GetRandomPositionInBounds()
        {
            var halfBounds = bounds * ((1f - boundsThreshold) * 0.5f);
            
            return new Vector3(Random.Range(-halfBounds.x, halfBounds.x),
                Random.Range(-halfBounds.y, halfBounds.y),
                Random.Range(-halfBounds.z, halfBounds.z)) + transform.position;
        }

        private void OnDestroy()
        {
            _groups.Dispose();
            _positions.Dispose();
            _rotations.Dispose();
            _direction.Dispose();
            _accelerations.Dispose();
            _raycastHits.Dispose();
            _targetPositions.Dispose();

            _matrices.Dispose();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, bounds);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, bounds * (1f - boundsThreshold));
        }
    }
}