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

        [Tooltip("차량 속도 범위 (최소~최대) - 빠르게 시원하게 지나가는 버스 속도")]
        [SerializeField] private float _minSpeed = 7.5f;
        [SerializeField] private float _maxSpeed = 10.5f;

        [Tooltip("차량 통과 후 주어지는 충분한 안전 통과 시간(Clear Window) 범위(초)")]
        [SerializeField] private float _minClearWindow = 4.2f;
        [SerializeField] private float _maxClearWindow = 6.8f;

        [Tooltip("스폰 시작 X축 좌표 (도로 폭 20m 기준 ±13m)")]
        [SerializeField] private float _spawnBoundaryX = 13.0f;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 4.2f;
        private Coroutine _spawnRoutine;

        // 차량 오브젝트 풀
        private IObjectPool<MovingVehicle> _vehiclePool;
        private readonly List<MovingVehicle> _activeVehicles = new List<MovingVehicle>();

        // [핵심!] RoadLane의 Transform 스케일 (20, 0.2, 1) 왜곡을 차량이 상속받지 않도록 독립 컨테이너 사용
        private static Transform _vehicleRootContainer;

        private static Transform GetOrCreateContainer()
        {
            if (_vehicleRootContainer == null)
            {
                GameObject container = GameObject.Find("[Vehicle_Pool_Container]");
                if (container == null)
                {
                    container = new GameObject("[Vehicle_Pool_Container]");
                    container.transform.position = Vector3.zero;
                    container.transform.rotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                }
                _vehicleRootContainer = container.transform;
            }
            return _vehicleRootContainer;
        }

        private void Awake()
        {
            _isSafeLane = false;
            InitializePool();
        }

        private void InitializePool()
        {
            if (_vehiclePrefab == null) return;

            Transform container = GetOrCreateContainer();

            _vehiclePool = new ObjectPool<MovingVehicle>(
                createFunc: () =>
                {
                    // RoadLane(스케일 20, 0.2, 1)의 자식이 아닌 스케일 (1,1,1)인 독립 컨테이너 아래 생성
                    MovingVehicle vehicle = Instantiate(_vehiclePrefab, container);
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
                defaultCapacity: 3,
                maxSize: 6
            );
        }

        /// <summary>
        /// 레인 매니저에서 방향을 번갈아(교차) 지정할 때 호출할 수 있는 설정 메서드입니다.
        /// </summary>
        public void SetDesiredDirection(float direction)
        {
            _fixedDirection = Mathf.Sign(direction);
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

            // 풀이 아직 생성되지 않았다면 안전하게 초기화
            if (_vehiclePool == null)
            {
                InitializePool();
            }

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
            // 1. 레인 생성 직후 초기 시차 분산:
            // 인접한 도로끼리 동시에 차가 튀어나와 통행을 막지 않도록 Z 인덱스 기반 시차 부여
            float initialPhaseDelay = ((Mathf.Abs(LaneZIndex) * 2.3f) % 3.5f) + Random.Range(0.6f, 2.0f);
            yield return new WaitForSeconds(initialPhaseDelay);

            while (true)
            {
                // [단독 객체 반복 스폰] 각각의 차량 1대가 정해진 궤적을 지나감
                SpawnVehicle();

                // [확실한 안전 통과 시간(Clear Window)]
                // 차량 1대가 통과한 후, 다음 차량이 스폰되기까지 5.0초 ~ 7.5초 동안 도로가 텅 비어 있어 안심하고 건널 수 있음
                float clearWindow = Random.Range(_minClearWindow, _maxClearWindow);
                yield return new WaitForSeconds(clearWindow);
            }
        }

        private void SpawnVehicle()
        {
            if (_vehiclePrefab == null || _vehiclePool == null) return;

            MovingVehicle vehicle = _vehiclePool.Get();

            // 스폰 위치 계산 (진행 방향의 반대쪽 끝 X=±13m 에서 출발, 도로 위 Y=0.1f 착지)
            float startX = (_currentDirection > 0) ? -_spawnBoundaryX : _spawnBoundaryX;
            vehicle.transform.position = new Vector3(startX, 0.1f, LaneZIndex);
            vehicle.transform.localScale = Vector3.one;

            // 차량 초기화 (반대편 끝 도착 시 풀 반환 콜백 연결)
            vehicle.Initialize(_currentDirection, _currentSpeed, (v) =>
            {
                if (_vehiclePool != null)
                {
                    _vehiclePool.Release(v);
                }
            });
        }
    }
}
