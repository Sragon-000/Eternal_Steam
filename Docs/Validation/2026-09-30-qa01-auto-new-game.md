# QA-01 — Editor Play 자동 새 게임 재검증

2026-09-30, 이미 열려 있던 Unity Editor의 `StartRegionSandbox`에서 확인했다. 작업 트리의 실제 소스에는 과거 검증 문서가 언급한 Play 시작 처리 함수가 없어서, `OpenWorldSandbox`의 `SubsystemRegistration`에서 Play 세션 플래그를 켜고 첫 저장 초기화에서만 소비하도록 복구했다. `SingleMapPersistence.Initialize(true)`는 기존 저장을 `JsonSaveStore.Archive`로 보관한 뒤 고정 메인을 설치한다. 보관 실패 시 복원·새 저장을 차단한다. 게임 내 씬 재로드는 플래그를 다시 켜지 않는다. 빌드의 `Initialize()` 기본 경로는 기존 저장 복원으로 남겼다.

검증 저장 경로는 Editor 프로세스에만 설정한 `/tmp/eternal-qa01-auto-start-20260930`이었다. 제품 저장 경로를 사용하지 않았다. 첫 Play에서 메인 1개·`Blocked=false`·새 저장을 확인했다. 카메라 줌 56을 안전 저장한 뒤 게임 내 `ContinueSaved()`로 재로드하자 runId `de217cf29aab4edb9a26c87940518741`, 메인 ID `ee12aeba71ae4310b083047b8a1129f8`, 줌 56이 모두 유지됐다. 이 제어된 왕복에서는 과거의 줌 56→70 불일치가 재현되지 않았다.

| Enter Play Mode 설정 | 관찰한 새 runId | 메인 수 | 이전 저장 |
|---|---|---:|---|
| 기본 설정, 두 번째 Play | `484612a279e54d4fb036302687a1a959` | 1 | 직전 runId 보관 |
| Domain Reload 비활성 | `fe0657dfa98a4b3ba796c684361fe3a1` | 1 | 직전 runId 보관 |
| Domain·Scene Reload 비활성 | `628221a4ba62458f9ef5a7b580532019` | 1 | 직전 runId 보관 |
| Domain·Scene Reload 비활성, 반복 Play | `9953850bfd134aa0a8756395608fd2a7` | 1 | 직전 runId 보관 |
| Scene Reload 비활성 | `f8181b7ce320469db1ba8505443c5eae` | 1 | 직전 runId 보관 |

각 전환에서 `archive/*/current.json`의 runId가 직전 활성 저장과 같고, 새 `current.json`의 runId가 달랐다. Unity EditMode 198/198 통과. 종료 시 Edit Mode, Enter Play Mode 옵션 `False/None`, 임시 저장 환경변수 해제, 씬 dirty=false를 확인했다. 독립 빌드에서의 이어하기·강제 종료 복원은 QA-07이며, 실제 포인터 조작 중 카메라 줌 왕복은 QA-06에서 확인한다.

과거 [자동 새 게임 기록](2026-09-29-auto-new-game-on-play.md)은 당시 관찰로 보존한다. 이후 실제 소스와 불일치가 발견되어 이번 변경·재검증이 현행 상태다.
