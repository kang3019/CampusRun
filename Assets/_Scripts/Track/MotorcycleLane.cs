using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;

namespace CampusRun.Track
{
    /// <summary>
    /// 길건너 친구들의 '철도(기차 레인)'에 해당하는 캠퍼스 배달 오토바이 전용 급습 레인입니다.
    /// 오토바이 돌진 1.0초 전 바닥에 붉은색 경고선(Warning Indicator)이 점멸하여 플레이어에게 사전 회피 기회를 부여합니다.
    /// </summary>
    public class MotorcycleLane : BaseLane
    {
        [Header("--- 오토바이 프리팹 및 풀링 ---")]
        [Tooltip("초고속 돌진 오토바이 프리팹 (FastMotorcycle 컴포넌트 필수)")]
        [SerializeField] private FastMotorcycle _motorcyclePrefab;

        [Header("--- 바닥 붉은색 사전 경고선 ---")]
        [Tooltip("바닥에 깔리는 붉은색 경고선 오브젝트 (Quad, Sprite 또는 Decal)")]
        [SerializeField] private GameObject _warningIndicator;

        [Tooltip("경고선 점멸 시간 (초, 억까 방지를 위해 1.0초 권장)")]
        [SerializeField] private float _warningDuration = 1.0f;

        [Tooltip("경고선 깜빡임 횟수 (1초 동안 깜빡일 횟수)")]
        [SerializeField] private int _blinkCount = 4;

        [Header("--- 돌진 및 스폰 타이밍 ---")]
        [Tooltip("돌진 이동 방향 (+1: 왼쪽->오른쪽, -1: 오른쪽->왼쪽, 0: 랜덤)")]
        [SerializeField] private float _fixedDirection = 0f;

        [Tooltip("오토바이 돌진 속도 (m/s)")]
        [SerializeField] private float _rushSpeed = 30f;

        [Tooltip("다음 오토바이 돌진 대기 시간 (최소~최대 초)")]
        [SerializeField] private float _minInterval = 4.0f;
        [SerializeField] private float _maxInterval = 7.5f;

        [Tooltip("스폰 시작 X축 좌표")]
        [SerializeField] private float _spawnBoundaryX = 16f;

        // 런타임 상태
        private float _currentDirection = 1f;
        private Coroutine _cycleRoutine;
        private IObjectPool<FastMotorcycle> _motorcyclePool;
        private readonly List<FastMotorcycle> _activeMotorcycles = new List<FastMotorcycle>();

        private void Awake()
        {
            _isSafeLane = false;
            InitializePool();

            if (_warningIndicator != null)
            {
                _warningIndicator.SetActive(false);
            }
        }

        private void InitializePool()
        {
            if (_motorcyclePrefab == null) return;

            _motorcyclePool = new ObjectPool<FastMotorcycle>(
                createFunc: () =>
                {
                    FastMotorcycle motorcycle = Instantiate(_motorcyclePrefab, transform);
                    motorcycle.gameObject.SetActive(false);
                    return motorcycle;
                },
                actionOnGet: (motorcycle) =>
                {
                    _activeMotorcycles.Add(motorcycle);
                },
                actionOnRelease: (motorcycle) =>
                {
                    _activeMotorcycles.Remove(motorcycle);
                    motorcycle.gameObject.SetActive(false);
                },
                actionOnDestroy: (motorcycle) =>
                {
                    if (motorcycle != null) Destroy(motorcycle.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: 3,
                maxSize: 8
            );
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();

            // 진행 방향 결정
            if (_fixedDirection == 0f)
            {
                _currentDirection = (Random.value > 0.5f) ? 1f : -1f;
            }
            else
            {
                _currentDirection = Mathf.Sign(_fixedDirection);
            }

            if (_warningIndicator != null)
            {
                _warningIndicator.SetActive(false);
            }

            if (_motorcyclePool == null)
            {
                InitializePool();
            }

            // 돌진 사이클 루틴 시작
            if (_cycleRoutine != null) StopCoroutine(_cycleRoutine);
            _cycleRoutine = StartCoroutine(MotorcycleCycleRoutine());
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();

            if (_cycleRoutine != null)
            {
                StopCoroutine(_cycleRoutine);
                _cycleRoutine = null;
            }

            if (_warningIndicator != null)
            {
                _warningIndicator.SetActive(false);
            }

            // 활성화된 모든 오토바이 회수
            for (int i = _activeMotorcycles.Count - 1; i >= 0; i--)
            {
                if (_activeMotorcycles[i] != null && _motorcyclePool != null)
                {
                    _motorcyclePool.Release(_activeMotorcycles[i]);
                }
            }
            _activeMotorcycles.Clear();
        }

        private IEnumerator MotorcycleCycleRoutine()
        {
            // 플레이어가 레인에 진입하자마자 급습당하지 않도록 초기 여유 시간 부여
            yield return new WaitForSeconds(Random.Range(1.0f, 2.5f));

            while (true)
            {
                // 1. 사전 경고 단계 (붉은색 경고선 깜빡임)
                yield return StartCoroutine(BlinkWarningLineRoutine());

                // 2. 초고속 돌진 단계 (오토바이 발사)
                SpawnAndRushMotorcycle();

                // 3. 다음 돌진까지 대기
                float waitInterval = Random.Range(_minInterval, _maxInterval);
                yield return new WaitForSeconds(waitInterval);
            }
        }

        private IEnumerator BlinkWarningLineRoutine()
        {
            if (_warningIndicator == null)
            {
                yield return new WaitForSeconds(_warningDuration);
                yield break;
            }

            float blinkInterval = _warningDuration / (_blinkCount * 2f);

            for (int i = 0; i < _blinkCount; i++)
            {
                _warningIndicator.SetActive(true);
                yield return new WaitForSeconds(blinkInterval);

                _warningIndicator.SetActive(false);
                yield return new WaitForSeconds(blinkInterval);
            }

            // 돌진 직전 0.2초간 계속 켜져 있어 긴장감 극대화
            _warningIndicator.SetActive(true);
            yield return new WaitForSeconds(0.2f);
            _warningIndicator.SetActive(false);
        }

        private void SpawnAndRushMotorcycle()
        {
            if (_motorcyclePrefab == null || _motorcyclePool == null) return;

            FastMotorcycle motorcycle = _motorcyclePool.Get();

            // 스폰 위치 계산 (레인 Y=0.25f, X는 진행 방향 반대쪽 바깥)
            float startX = (_currentDirection > 0) ? -_spawnBoundaryX : _spawnBoundaryX;
            motorcycle.transform.position = new Vector3(startX, 0.25f, LaneZIndex);

            // 초고속 이동 초기화
            motorcycle.Initialize(_currentDirection, _rushSpeed, (m) =>
            {
                if (gameObject.activeInHierarchy && _motorcyclePool != null)
                {
                    _motorcyclePool.Release(m);
                }
            });
        }
    }
}
