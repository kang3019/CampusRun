# 🏃‍♂️ Campus Run - 프로젝트 셋업 및 협업 가이드 (`setup.md`)

> **Campus Run** 4인 개발팀을 위한 초기 프로젝트 환경 설정, 디렉토리 구조, 아키텍처 패턴, Git 협업 및 코드 컨벤션 온보딩 문서입니다.

---

## 📌 목차
1. [프로젝트 개요 및 개발 환경](#1-프로젝트-개요-및-개발-환경)
2. [5분 초고속 로컬 셋업 가이드](#2-5분-초고속-로컬-셋업-가이드-온보딩)
3. [프로젝트 아키텍처 패턴](#3-프로젝트-아키텍처-패턴)
4. [디렉토리(폴더) 구조 규칙](#4-디렉토리폴더-구조-규칙)
5. [씬(Scene) 충돌 방지 워크플로우 (★필독)](#5-씬scene-충돌-방지-워크플로우-필독)
6. [Git 협업 및 브랜치 전략](#6-git-협업-및-브랜치-전략)
7. [코드 및 에셋 네이밍 컨벤션](#7-코드-및-에셋-네이밍-컨벤션)
8. [빌드 및 최종 배포 안내](#8-빌드-및-최종-배포-안내)

---

## 1. 프로젝트 개요 및 개발 환경

| 항목 | 내용 |
| :--- | :--- |
| **게임명** | Campus Run (캠퍼스 배경 3D 아케이드 런너 게임) |
| **타겟 플랫폼** | PC Standalone (Windows x64) |
| **엔진 버전** | **Unity 2022 LTS (2022.3.x 이상)** |
| **렌더 파이프라인** | Universal Render Pipeline (URP) |
| **입력 시스템** | Unity New Input System (`UnityEngine.InputSystem`) |
| **버전 관리** | Git / GitHub (LFS 활성화 권장) |

---

## 2. 5분 초고속 로컬 셋업 가이드 (온보딩)

새로운 팀원은 아래 5단계를 거쳐 5분 안에 개발 환경을 세팅할 수 있습니다.

### Step 1. 저장소 클론 (Clone)
터미널을 열고 작업할 로컬 경로에서 클론합니다.
```bash
git clone <Repository-URL>
cd CampusRun_Git
```

### Step 2. Unity Hub에 프로젝트 등록
1. **Unity Hub** 실행
2. 우측 상단 **[열기]** 또는 **[Add]** 클릭
3. 클론받은 프로젝트 루트 폴더(`CampusRun_Git`) 선택
4. 에디터 버전이 올바르게 매칭되어 있는지 확인 후 클릭하여 실행

### Step 3. 초기 패키지 로딩 대기
- 첫 실행 시 패키지 다운로드 및 셰이더 컴파일로 1~3분 정도 소요됩니다.
- 로드 후 Console 뷰에 컴파일 에러가 없는지 확인합니다.

### Step 4. 작업 브랜치 생성
`main` 브랜치에서는 직접 작업하지 않습니다. 본인의 작업 목적에 맞는 브랜치를 생성합니다.
```bash
# 예시: 플레이어 점프 구현 브랜치 생성
git checkout -b feature/player-jump
```

### Step 5. 개인 샌드박스 씬 생성
- `Assets/_Scenes/Sandboxes/` 경로로 이동합니다.
- `Sandbox_Template.unity` 복사 또는 새 씬을 생성하여 `Sandbox_<본인이름>.unity`로 저장하고 작업을 시작합니다.

---

## 3. 프로젝트 아키텍처 패턴

본 프로젝트는 **Manager-Singleton 패턴**과 **Unity 컴포넌트 기반 패턴**을 혼합 적용합니다.

```
┌──────────────────────────────────────────────────────────┐
│                   Game Lifecycle Core                    │
│   [GameManager]      [UIManager]       [SoundManager]    │
│  (게임상태/점수/루프)     (HUD/팝업/UI)      (BGM/SFX 관리)   │
└───────────────┬──────────────────────────┬───────────────┘
                ▼                          ▼
┌───────────────────────────────┐ ┌────────────────────────┐
│      Runner System Domain     │ │      UI & Feedback     │
│ [TrackSpawner] (트랙 스폰/풀링) │ │ [ScoreView]            │
│ [Obstacle]     (장애물 로직)   │ │ [PauseMenu]            │
│ [Coin]         (수집 아이템)   │ │ [GameOverPanel]        │
└───────────────┬───────────────┘ └────────────────────────┘
                ▼
┌───────────────────────────────┐
│         Player Domain         │
│ [PlayerController] (이동/점프) │
│ [PlayerAnimation]  (애니메이션) │
│ [PlayerCollision]  (충돌 판정) │
└───────────────────────────────┘
```

### 3.1 Manager-Singleton (글로벌 핵심 로직)
- **대상:** `GameManager`, `UIManager`, `SoundManager`, `ScoreManager` 등 게임 전체에서 단 하나만 존재하며 유지되어야 하는 시스템.
- **원칙:**
  - `DontDestroyOnLoad` 처리하여 씬 전환 시에도 파괴되지 않도록 유지.
  - 중복 인스턴스 발생 시 스스로 파괴(`Destroy(gameObject)`)하여 유일성 보장.
  - 아래의 제네릭 싱글톤 베이스 클래스를 상속받아 구현합니다.

```csharp
// Assets/_Scripts/Core/MonoSingleton.cs
using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();

    public static T Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject(typeof(T).Name);
                        _instance = singletonObject.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}
```

### 3.2 컴포넌트 기반 패턴 (게임플레이 객체)
- **대상:** `PlayerController`, `TrackSegment`, `Obstacle`, `CoinPickup` 등
- **원칙:**
  - 독립적인 기능 단위로 컴포넌트를 분리(단일 책임 원칙).
  - 다른 객체/매니저와의 직접 결합을 지양하고, 이벤트(UnityEvent 또는 C# `event/Action`) 기반 통신 사용.
  - 성능을 위해 오브젝트 풀링(`UnityEngine.Pool.ObjectPool<T>`) 적용.

---

## 4. 디렉토리(폴더) 구조 규칙

외부 에셋(Asset Store 다운로드 에셋)과 **팀 자체 제작 작업물**을 완벽하게 분리하기 위해, 팀이 생성하는 모든 폴더명 앞에는 **언더바(`_`)**를 붙여 Project View 최상단에 정렬되도록 관리합니다.

```text
Assets/
├── _Animations/          # 팀 제작 애니메이션 클립, 컨트롤러 (Animator)
├── _Audio/               # 팀 BGM 및 SFX 사운드 파일
│   ├── BGM/
│   └── SFX/
├── _Materials/           # 팀 머티리얼, 물리 머티리얼(Physic Material)
├── _Prefabs/             # 팀 프리팹 (★모든 오브젝트는 프리팹화)
│   ├── Characters/       # 플레이어 및 NPC 캐릭터
│   ├── Environment/      # 캠퍼스 건물, 도로, 프롭
│   ├── Obstacles/        # 런너 장애물
│   └── UI/               # 재사용 UI 프리팹 (버튼, 팝업 등)
├── _Scenes/              # 프로젝트 씬
│   ├── MainScene.unity   # ★최종 배포용 메인 씬 (직접 편집 절대 금지)
│   ├── TitleScene.unity  # 타이틀/시작 씬
│   └── Sandboxes/        # 개인 테스트용 샌드박스 씬
│       ├── Sandbox_Player.unity
│       ├── Sandbox_Track.unity
│       └── Sandbox_UI.unity
├── _Scripts/             # 팀 C# 소스 코드
│   ├── Core/             # 싱글톤 베이스, 게임 매니저, 게임 상태 머신
│   ├── Player/           # 플레이어 이동, 애니메이션, 충돌
│   ├── Track/            # 트랙 생성기, 타일 스폰, 무한 런너 루프
│   ├── Obstacles/        # 장애물 이동 및 상호작용
│   ├── UI/               # HUD, 스코어보드, 메뉴 컨트롤러
│   └── Utils/            # 확장 메서드, 수학 유틸, 오브젝트 풀러
├── _Textures/            # 팀 UI 스프라이트, 커스텀 텍스처
├── Settings/             # URP 설정 및 Input System Action 파일
└── ThirdParty/           # 에셋스토어 외부 다운로드 패키지 보관 (언더바 없음)
```

---

## 5. 씬(Scene) 충돌 방지 워크플로우 (★필독)

Unity 협업에서 가장 빈번하고 해결하기 힘든 충돌은 **`.unity` 씬 파일 병합 충돌**입니다. 이를 원천 차단하기 위해 아래의 3대 씬 룰을 엄격히 준수합니다.

### 🚫 Rule 1: `MainScene.unity` 직접 수정 절대 금지
- `_Scenes/MainScene.unity`는 배포용 마스터 씬입니다.
- 개별 기능 개발자가 이 씬을 열어서 오브젝트를 추가하거나 수정 후 커밋하는 행위를 절대 금지합니다.

### 🧪 Rule 2: 개인 샌드박스(Sandbox) 씬에서 개발
- 각 팀원은 `Assets/_Scenes/Sandboxes/` 아래에 본인 전용 테스트 씬을 만들어 작업합니다.
- 예: `Sandbox_Kim.unity`, `Sandbox_TrackTest.unity`
- 샌드박스 씬은 자유롭게 커밋해도 다른 팀원의 작업에 영향을 주지 않습니다.

### 📦 Rule 3: 작업물은 100% 프리팹(Prefab)으로 모듈화
- 샌드박스에서 테스트가 완료된 기능(플레이어, 장애물, UI 패널 등)은 **반드시 `Assets/_Prefabs/`에 프리팹으로 저장**합니다.
- 메인 씬에 반영할 때는:
  1. 기능 프리팹이 완성되어 PR이 머지된 후,
  2. **지정된 1인의 씬 통합 담당자(Scene Master)**가 `MainScene`을 열고 해당 프리팹을 배치하거나,
  3. 런타임에 동적으로 프리팹을 스폰하는 구조로 연결합니다.

---

## 6. Git 협업 및 브랜치 전략

우리 팀은 심플하고 신속한 **GitHub Flow** 브랜치 전략을 사용합니다.

```
(main) ─────────●───────────────────────────● (Merged)
                 \                         /
(feature/...)     ●───────●───────●───────● (Pull Request & Review)
                작업1   작업2   작업3
```

### 6.1 브랜치 전략
- **`main`**: 상시 빌드 및 실행 가능한 안정화 브랜치 (직접 커밋/푸시 금지, PR 필수).
- 작업 브랜치 네이밍 규칙:
  - `feature/<기능명>`: 새로운 게임플레이 기능 추가 (예: `feature/player-slide`)
  - `design/<작업명>`: 3D 모델링, 레벨 디자인, 아트 리소스 추가 (예: `design/campus-building`)
  - `ui/<작업명>`: UI 레이아웃 및 사운드 연결 (예: `ui/gameover-popup`)
  - `fix/<버그명>`: 버그 수정 (예: `fix/jump-height-bug`)
  - `docs/<문서명>`: 문서 추가 및 수정 (예: `docs/readme-update`)

### 6.2 커밋 메시지 컨벤션
모든 커밋 메시지는 대괄호 말머리로 시작하며, 명확한 작업 목적을 기술합니다.

| 말머리 | 용도 | 커밋 메시지 예시 |
| :--- | :--- | :--- |
| **`[Feat]`** | 새로운 기능 추가 | `[Feat] 플레이어 2단 점프 로직 구현` |
| **`[Fix]`** | 버그 및 오류 수정 | `[Fix] 장애물 충돌 시 게임오버가 두 번 호출되는 버그 수정` |
| **`[UI]`** | UI 및 레이아웃 작업 | `[UI] 인게임 스코어 HUD 텍스트 및 일시정지 버튼 추가` |
| **`[Asset]`** | 3D 모델, 사운드, 텍스처 추가/수정 | `[Asset] 캠퍼스 본관 3D 모델링 프리팹 추가` |
| **`[Refactor]`**| 코드 구조 개선 (기능 변화 없음) | `[Refactor] TrackSpawner 오브젝트 풀링 구조 리팩토링` |
| **`[Docs]`** | 문서 작성 및 수정 | `[Docs] setup.md 씬 워크플로우 가이드 갱신` |
| **`[Chore]`** | 빌드 세팅, 패키지 설정, 잡무 | `[Chore] TextMeshPro 필수 리소스 임포트` |

### 6.3 Pull Request 및 코드 리뷰
1. 작업 완료 후 `main` 브랜치를 향해 PR(Pull Request) 생성.
2. 최소 **1명 이상의 팀원 승인(Approve)**을 받아야 머지 가능.
3. 머지 시 **Squash and Merge** 또는 **Rebase and Merge** 권장 (커밋 히스토리 깔끔 유지).

---

## 7. 코드 및 에셋 네이밍 컨벤션

### 7.1 C# 스크립트 네이밍 규칙
| 항목 | 표기법 | 예시 |
| :--- | :--- | :--- |
| **클래스 (Class)** | `PascalCase` | `PlayerController`, `ScoreManager` |
| **인터페이스 (Interface)** | `I` + `PascalCase` | `IDamageable`, `IInteractable` |
| **공개 메서드 / 프로퍼티** | `PascalCase` | `TakeDamage()`, `CurrentScore` |
| **비공개 필드 (Private)** | `_camelCase` | `_moveSpeed`, `_playerRigidbody` |
| **직렬화 필드 (Inspector)** | `[SerializeField] private` + `_camelCase` | `[SerializeField] private float _jumpForce;` |
| **매개변수 / 로컬 변수** | `camelCase` | `damageAmount`, `hitCollider` |
| **상수 (Const / Readonly)** | `UPPER_SNAKE_CASE` 또는 `PascalCase` | `MAX_HEALTH`, `DefaultSpawnInterval` |

### 7.2 Unity 에셋 네이밍 접두사 규칙
| 에셋 종류 | 접두사 / 포맷 | 예시 |
| :--- | :--- | :--- |
| **프리팹 (Prefab)** | `PF_<이름>` 또는 명확한 명사 | `PF_Player`, `PF_Obstacle_Bench` |
| **머티리얼 (Material)** | `M_<이름>` | `M_CampusRoad`, `M_PlayerSkin` |
| **텍스처 (Texture)** | `T_<이름>` | `T_Road_Diffuse`, `T_Brick_Normal` |
| **스프라이트 (UI Sprite)** | `SP_<이름>` | `SP_Btn_Play`, `SP_Icon_Coin` |
| **애니메이션 컨트롤러** | `AC_<이름>` | `AC_Player`, `AC_CoinSpin` |
| **오디오 클립 (Audio)** | `BGM_<이름>` / `SFX_<이름>` | `BGM_MainTheme`, `SFX_PlayerJump` |

---

## 8. 빌드 및 최종 배포 안내

> ⚠️ **주의**: 최종 배포 및 빌드를 진행하기 전에는 반드시 팀 내부 **"프로젝트 패키징"** 문서를 참고하여 세팅을 검증해야 합니다.

### 빌드 전 필수 체크리스트
1. **Scene In Build 목록 확인**:
   - `File > Build Settings`에서 `_Scenes/TitleScene` (Index 0), `_Scenes/MainScene` (Index 1)만 등록되어 있는지 확인.
   - 개인 샌드박스 씬(`Sandbox_*.unity`)이 빌드 목록에 포함되지 않도록 주의.
2. **URP Graphics Quality 세팅**:
   - `Project Settings > Quality`에서 PC Standalone 타겟 퀄리티가 올바르게 설정되었는지 확인.
3. **New Input System 바인딩 점검**:
   - 키보드/마우스 컨트롤 바인딩이 누락 없이 매핑되어 있는지 확인.
4. **패키징 가이드 문서 열람**:
   - 배포 바이너리 생성(Zip 압축, 실행 권한, 해상도 고정 옵션 등)은 **"프로젝트 패키징"** 문서의 세부 절차를 정확히 따라 진행해 주세요.

---
*문서 관련 문의나 추가 제안은 팀 슬랙/디스코드 채널 또는 PR 코멘트로 남겨주세요.*
