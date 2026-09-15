using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;

namespace CampusRun.Track
{
    /// <summary>
    /// 1단계 도로 레인입니다. 셔틀버스나 승용차가 일정한 방향과 속도로 횡이동하며 지나갑니다.
    /// 차량 스폰 시 ObjectPool을 사용하여 런타임 메모리 할당을 방지합니다.
    /// </summary>
    public class RoadLane : BaseLane
    {
        [Header("--- 차량 스폰 설정 ---")]
        [Tooltip("스폰할 차량 프리팹 (MovingVehicle 컴포넌트 필수)")]
        [SerializeField] private MovingVehicle _vehiclePrefab;

        [Tooltip("차량 이동 방향 (+1: 왼쪽->오른쪽, -1: 오른쪽->왼쪽, 0: 랜덤)")]
        [SerializeField] private float _fixedDirection = 0f;

        [Tooltip("차량 속도 범위 (최소~최대)")]
        [SerializeField] private float _minSpeed = 4f;
        [SerializeField] private float _maxSpeed = 8f;

        [Tooltip("차량 스폰 간격(초) 범위")]
        [SerializeField] private float _minSpawnInterval = 1.5f;
        [SerializeField] private float _maxSpawnInterval = 3.5f;

        [Tooltip("스폰 시작 X축 좌표")]
        [SerializeField] private float _spawnBoundaryX = 14f;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 5f;
        private Coroutine _spawnRoutine;

        // 차량 오브젝트 풀
        private IObjectPool<MovingVehicle> _vehiclePool;
        private readonly List<MovingVehicle> _activeVehicles = new List<MovingVehicle>();

        private void Awake()
        {
            _isSafeLane = false;
            InitializePool();
        }

        private void InitializePool()
        {
            if (_vehiclePrefab == null) return;

            _vehiclePool = new ObjectPool<MovingVehicle>(
                createFunc: () =>
                {
                    MovingVehicle vehicle = Instantiate(_vehiclePrefab, transform);
                    vehicle.gameObject.SetActive(false);
                    return vehicle;
                },
                actionOnGet: (vehicle) =>
                {
                    _activeVehicles.Add(vehicle);
                },
                actionOnRelease: (vehicle) =>
                {
                    _activeVehicles.Remove(vehicle);
                    vehicle.gameObject.SetActive(false);
                },
                actionOnDestroy: (vehicle) =>
                {
                    if (vehicle != null) Destroy(vehicle.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: 5,
                maxSize: 15
            );
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();

            // 레인마다 방향과 속도를 랜덤 또는 고정 설정
            if (_fixedDirection == 0f)
            {
                _currentDirection = (Random.value > 0.5f) ? 1f : -1f;
            }
            else
            {
                _currentDirection = Mathf.Sign(_fixedDirection);
            }

            _currentSpeed = Random.Range(_minSpeed, _maxSpeed);

            // 차량 스폰 루틴 시작
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(VehicleSpawnRoutine());
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            // 활성화되어 있던 모든 차량 회수
            for (int i = _activeVehicles.Count - 1; i >= 0; i--)
            {
                if (_activeVehicles[i] != null && _vehiclePool != null)
                {
                    _vehiclePool.Release(_activeVehicles[i]);
                }
            }
            _activeVehicles.Clear();
        }

        private IEnumerator VehicleSpawnRoutine()
        {
            // 첫 진입 시 즉시 1대 스폰 후 주기 루프 진입
            SpawnVehicle();

            while (true)
            {
                float waitTime = Random.Range(_minSpawnInterval, _maxSpawnInterval);
                yield return new WaitForSeconds(waitTime);

                SpawnVehicle();
            }
        }

        private void SpawnVehicle()
        {
            if (_vehiclePrefab == null || _vehiclePool == null) return;

            MovingVehicle vehicle = _vehiclePool.Get();

            // 스폰 위치 계산 (진행 방향의 반대쪽 끝에서 출발)
            float startX = (_currentDirection > 0) ? -_spawnBoundaryX : _spawnBoundaryX;
            vehicle.transform.position = new Vector3(startX, 0f, LaneZIndex);

            // 차량 초기화 (도착 시 풀 반환 콜백 연결)
            vehicle.Initialize(_currentDirection, _currentSpeed, (v) =>
            {
                if (gameObject.activeInHierarchy && _vehiclePool != null)
                {
                    _vehiclePool.Release(v);
                }
            });
        }
    }
}
