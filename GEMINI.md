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
