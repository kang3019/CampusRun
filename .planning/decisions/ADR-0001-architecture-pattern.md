# ADR-0001: 길건너 친구들 스타일 레인 기반 이벤트 주도 아키텍처 (Lane-Based Event-Driven Architecture)

- **상태:** Accepted
- **날짜:** 2026-09-15
- **결정자:** CampusRun 개발팀 (4인)

---

## 1. 배경 (Context)

CampusRun은 **'길건너 친구들(Crossy Road)' 스타일의 3D 하이퍼캐주얼 탭 아케이드 게임**입니다.
이 장르는 기존의 3레인 연속 러너(서브웨이 서퍼 등)와는 완전히 다른 시스템적 특성을 요구합니다:

1. **이산적 그리드 홉(Discrete Grid Hop):** 연속적인 물리 속도 이동 대신 1x1 격자 단위로 상/하/좌/우 포물선 점프 이동을 수행합니다.
2. **행(Row) 단위 레인 시스템:** 맵 전체가 '도로(셔틀버스)', '강의실(출석 레이저)', '도서관(프리라이더)', '안전지대(보도블록)'라는 **독립적인 레인(Lane) 객체들의 연속적인 집합**으로 구성됩니다.
3. **4인 협업 및 씬 충돌 방지:** 개발자(조작/스폰), 디자이너(UI/랭킹), 3D 아티스트(모델/VFX), 사운드 매니저가 개별 샌드박스 씬에서 독립적으로 일할 수 있는 완벽한 결합도 분리가 요구됩니다.
4. **축제 부스 현장 안정성 (10/16 완공):** 태블릿/노트북에서 1분 내외의 회전율을 보장하고, 60fps 무할당(GC-Free) 성능을 달성해야 합니다.

---

## 2. 고려한 대안 (Alternatives)

### 대안 A: 전통적 모놀리식 싱글톤 구조 (Monolithic Singletons)
- 모든 로직(`GameManager`, `LaneManager`, `UIManager`)이 static Instance로 서로의 메서드를 직접 호출.
- **문제점:** 플레이어 조작을 테스트하려 해도 UI와 사운드 매니저가 씬에 없으면 NRE 오류 발생. 4인 동시 개발 시 심각한 병목.

### 대안 B: 레인 기반 이벤트 주도 아키텍처 (Lane-Based Event-Driven Architecture) [선택]
- 핵심 라이프사이클 관리(`GameManager`, `SoundManager`)만 싱글톤으로 유지.
- 게임플레이의 핵심 객체들은 **계층별 도메인**으로 엄격히 분리:
  - `Player Domain`: `GridPlayerController`, `HopMovement` (그리드 이동 및 포물선 트윈)
  - `World Domain`: `LaneManager` (행 단위 스폰 및 풀링), `BaseLane` 상속 구조 (`RoadLane`, `ClassroomLane` 등)
  - `UI Domain`: `InGameHUD`, `LeaderboardModal` (랭킹 조회 팝업)
- 도메인 간의 모든 소통은 **경량 C# 이벤트(`event Action<T>`)**로 수행.
- **장점:** 샌드박스 씬에서 플레이어나 특정 레인만 단독으로 올려놓고 테스트 가능. 씬 충돌 원천 차단.

### 대안 C: ScriptableObject 이벤트 아키텍처
- SO 에셋 기반 이벤트 버스.
- **문제점:** 에셋 파일 생성량이 급증하여 4인 Git 협업 시 에셋 메타 파일 충돌 위험이 크고 축제 일정(~10/16) 내에 과도한 관리 비용 초래.

---

## 3. 결정 (Decision)

**대안 B: 레인 기반 이벤트 주도 아키텍처 (Lane-Based Event-Driven Architecture)**를 공식 프로젝트 아키텍처로 확정한다.

---

## 4. 세부 도메인 구조 및 통신 흐름

```
[ New Input System / Touch ]
         │  (Tap / Arrow Input)
         ▼
┌─────────────────────────────────────────────────────────────┐
│                       Player Domain                         │
│  [GridPlayerController]  ───▶  [HopTween / Animator]        │
│  (1x1 그리드 좌표 계산)          (포물선 점프 & 바운스 연출)    │
│  (충돌체크 & 타임아웃 감지)                                   │
└──────────────────────────────┬──────────────────────────────┘
                               │  발행 (Publish Event)
               ┌───────────────┴───────────────┐
               ▼                               ▼
    GameEvents.OnPlayerHopped        GameEvents.OnPlayerDied
    (플레이어 1칸 전진 시)           (장애물 충돌 / 시간초과 시)
               │                               │
       구독    │                       구독    │
    ┌──────────┴──────────┐            ┌───────┴──────────┐
    ▼                     ▼            ▼                  ▼
┌──────────────┐   ┌────────────┐   ┌────────────┐   ┌────────────┐
│ World Domain │   │ UI Domain  │   │ UI Domain  │   │Audio Domain│
│ [LaneManager]│   │ [HUDView]  │   │[ResultModal│   │[SoundMgr]  │
│ 새로운 레인  │   │ 실시간 거리│   │학점(A+~F)  │   │피격/사망음 │
│ 스폰 & 풀링  │   │ 미터 증가  │   │사유 팝업   │   │효과음 재생 │
└──────────────┘   └────────────┘   └────────────┘   └────────────┘
```

---

## 5. 선택 이유 (Rationale)

1. **'길건너 친구들' 메커니즘에 완벽 부합:**
   - 맵 전체를 하나의 거대한 씬으로 만들지 않고, 1개 행(Row) 단위의 `Lane` 프리팹을 조립식으로 생성하므로 메모리 효율이 극대화됩니다.
2. **역할별 샌드박스 씬 독립 테스트:**
   - 3D 아티스트는 `Sandbox_Assets`에서 셔틀버스 프리팹만 테스트할 수 있습니다.
   - UI 디자이너는 게임플레이 없이도 `Sandbox_UI`에서 랭킹 모달만 띄워볼 수 있습니다.
3. **10/16 완공 일정 준수:**
   - 복잡한 프레임워크 없이 순수 C# 이벤트와 Unity 컴포넌트만으로 구성되어 구현 난이도가 낮고 버그 추적이 용이합니다.

---

## 6. 후속 작업

- [x] 전역 싱글톤 베이스 `MonoSingleton<T>` 구현 완료
- [ ] 글로벌 이벤트 정적 클래스 `Assets/_Scripts/Core/GameEvents.cs` 작성
- [ ] 그리드 플레이어 이동기 `Assets/_Scripts/Player/GridPlayerController.cs` 작성
- [ ] 기본 레인 베이스 `Assets/_Scripts/Track/BaseLane.cs` 작성
