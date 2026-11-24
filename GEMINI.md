# Workspace Rules for CampusRun

> This workspace rule mirrors [agy.md](file:///C:/UnityProjects/CampusRun_Git/agy.md).

## Project Environment
- **Project:** CampusRun (Endless / Stage Runner)
- **Engine:** Unity 6 (`6000.6.0f1`) + URP 17.6.0
- **Input:** Unity New Input System (`UnityEngine.InputSystem`)
- **Git Root:** `CampusRun_Git/` (Project Root)

## Core Rules for AI Assistance
1. **Language & Communication:** 
   - 항상 **한국어**로 응답할 것.
   - 작업을 진행할 때는 어떤 작업을 수행하는지, 왜 하는지 단계별로 명확히 설명할 것.
2. **Serialization:** Prefer `[SerializeField] private` over public fields.
3. **Performance:** Do not allocate heap memory (`new`, LINQ, string concatenation) or call `GetComponent<T>()` in `Update()` / `FixedUpdate()`. Cache references in `Awake()`.
4. **Object Pooling:** Use `UnityEngine.Pool.ObjectPool<T>` for obstacles, path segments, and coins.
5. **Input:** Always use the New Input System ([InputSystem_Actions.inputactions](file:///C:/UnityProjects/CampusRun_Git/Assets/InputSystem_Actions.inputactions)). Avoid legacy `Input.Get*`.
6. **Assets & Meta:** Never delete or rename assets without keeping Unity `.meta` files consistent.
7. **Script Location:** Put new scripts under `Assets/_Scripts/<FeatureName>/`.

## Planned Tasks for Next Session (박성엽 브랜치)
사용자가 접속 후 "오늘 뭐 해야 돼?" 또는 "다음 주 작업 진행하자"라고 질문하면 아래 순서로 안내 및 진행할 것:
1. **Git 동기화:** `origin/main`의 팀원 작업(설정창 보강, 업적, 캐릭터 수집/선택)을 `git fetch` 확인 후 충돌 없이 병합.
2. **회원가입 / 로그인 기능 (`Assets/_Scripts/Auth/`):**
   - 게임 시작 시 회원가입/로그인 모달(`AuthUI`, `AuthManager`, `UserAccountData`) 표시 및 Firebase Realtime Database 연동.
   - 회원가입 후 첫 로그인 시 **최초 1회 닉네임 입력 모달** 표시 (중복 검사 포함, 계정에 영구 귀속).
3. **전체 유저 무제한 랭킹 확장 (`LeaderboardManager`, `LeaderboardUI`):**
   - 기존 TOP 5 제한(`MaxEntries = 5`)을 해제하여 모든 유저의 최고 점수가 등록되도록 변경.
   - 랭킹 모달에 스크롤 뷰(`ScrollRect`) 및 내 순위 표시 적용, 게임오버 시 로그인된 유저의 닉네임으로 자동 점수 갱신.
4. **팀원 충돌 방지:** 씬 파일(`Sandbox_PlayTest.unity`) 및 `InGameUIManager.cs` 수정을 최소화하고 독립 폴더(`Assets/_Scripts/Auth/`) 기반으로 구현.
