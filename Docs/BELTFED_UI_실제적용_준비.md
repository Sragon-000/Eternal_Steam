# BELTFED HUD 실제 적용 준비

작성: 2026-09-27. 최초 상태: **계획·시안·현재 코드 대조 및 구현 작업 분해 완료.** 이후 코드 적용 상태는 아래 후속 기록을 따른다.

후속 코드 작업: [P1 기반 코드 적용 기록](Validation/2026-09-27-hud-foundation.md). 강화 조회·비용 조회와 기존 HUD의 상태/명령 연결을 추가했다. 아래 조사 내용과 기준 JSON은 변경 전 스냅샷이며, 이 기준 자료 작성 시점에는 전체 시안 재배치/홀로그램 적용 전이었다.

기준은 [적용계획](BELTFED_UI_시안_적용계획.md)과 [시안 설명](References/BELTFED_UI_시안.md), v2·v3·v4 이미지다. 이번 요청의 범위는 실제 적용 **준비**이며 런타임 코드·씬·콘텐츠·저장 파일은 변경하지 않았다. 저장소에 이미 있는 파생 문서를 읽었으므로 새 사용자 첨부로 중복 보관하지 않는다. 기존 계획과 이미지도 변경하지 않았다.

2026-09-28 후속: [P2 HUD 재배치 코드](Validation/2026-09-28-hud-layout.md)를 반영했다. 아래 표는 최초 조사 기준이며, 현재 변경 결과와 미검증 항목은 후속 기록을 따른다.

2026-09-28 후속: [P3 셀 홀로그램·배치 피드백](Validation/2026-09-28-build-area-hologram.md)을 코드로 반영했다. P1~P3 C# 컴파일을 확인했으며 Unity import·테스트 실행·시각/입력/성능 검증은 대기 중이다.

2026-09-28 기준 변경: [Canvas 실제 계층 적용](Validation/2026-09-28-canvas-hierarchy.md)에서 HUD를 Canvas/uGUI로 전환하고 홀로그램·격자·미리보기를 씬 오브젝트로 저장했다. 이전 런타임 생성 방식은 현재 씬의 적용 기준이 아니다.

## 1. 시작 기준과 정적 확인

- 실제 Unity 프로젝트: `/Users/limseth/Projects/Eternal_Steam/Eternal_Steam`. 상위 폴더는 Git 저장소가 아니다.
- 확인한 HEAD: `c7e945bec5dc882cad68bb9de68c97b1e3e5d0ae`. 기존 `Docs/README.md` 수정 및 미추적 시안·계획 문서가 있는 작업 상태에서 확인했다.
- Unity: `6000.3.14f1`. 대상 씬: `Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity`.
- 씬의 UIDocument는 `Shared/UI/OpenWorld/OpenWorldHud.uxml`, PanelSettings는 **`Settings/UI/SamplePanel.asset`**, 카탈로그는 `Content/Buildings/StartLoop/Catalog.asset`을 참조한다. 이하 Assets 경로는 `Assets/EternalSteam/` 기준이다.
- 실제 PanelSettings 기준 해상도는 **1280×800**이다. `HordePanelSettings.asset`을 수정하면 대상 HUD에 적용된다고 가정하지 않는다. 공유 설정 변경 전 다른 사용 씬을 조사하고, 가능하면 기존 설정에서 레이아웃을 먼저 검증한다.
- UXML의 이름 있는 요소 **58개**는 중복이 없다. HUD 및 표시 어댑터 7개의 리터럴 `Q("name")` 참조가 현재 UXML에 존재하고, 씬의 UXML·PanelSettings·카탈로그 GUID가 실제 에셋과 일치한다.
- 파일 해시·요소 목록·조회 목록: [준비 기준 자료](Validation/2026-09-27-hud-preparation-baseline.json). 동적 이름과 클래스 조회는 자동 검사 범위 밖이다. XML 구조 확인은 Unity UXML/USS import 검증을 대체하지 않는다.
- 이번에 Editor 컴파일·Play·입력 회귀·게임 빌드는 실행하지 않았다. 과거 PASS는 이번 작업의 PASS로 계산하지 않는다.

## 2. 구현 전에 해결할 실제 차이

| 항목 | 현재 코드에서 확인한 사실 | 적용할 작업 |
|---|---|---|
| 강화 조회 | `IUpgradeControl`에는 Level/MaximumLevel/TryUpgrade만 있다. HUD는 모듈 존재 여부로 버튼을 켠다. | 상태 변경 없는 가능 여부/사유 조회를 추가한다. 최대 레벨·활성 여부·메인 레벨 제한을 실제 강화 로직과 공유한다. |
| 강화 구현 종류 | `UpgradeModule`, `PerformanceUpgrade`, `MainBaseUpgradeDefinition.Runtime`이 각각 조건을 검사한다. | 세 구현을 모두 갱신한다. HUD에 조건식을 복사하지 않는다. |
| 비용 조회 | `UpgradePurchase`는 결제 경로만 제공한다. `ResourceBank.TryPurchase`가 비용 합산·검사·차감을 함께 맡는다. | 비용 검증/충족 조회를 분리하고 구매에서도 같은 검사를 사용한다. 조회에 TryPurchase 또는 TryUpgrade를 사용하지 않는다. |
| 진행 버튼 | `run`은 Assault 존재 여부에 따라 Clock/Running을 다르게 조작하고 `clock-pause`는 Clock만 반전한다. | 플레이 진행 상태/명령 어댑터를 하나로 만든다. 기존 Assault 유무와 수정/패배/복원 차단 분기를 유지한다. |
| 미니맵 접기 | `OpenWorldMinimap.Refresh`가 `test-body`의 display에 의존한다. | 독립 지도 패널의 실제 표시 상태를 사용한다. 개발자 패널을 접어도 지도는 갱신되어야 한다. |
| 입력 차단 | `OpenWorldHud.Update`가 인벤토리·test·clock·resource 경계를 직접 나열한다. | 새 상단·지도·선택·플레이 메뉴·개발자 패널의 보이는 부분만 검사한다. 루트 전체나 숨은 본문으로 월드를 차단하지 않는다. |
| 체력/상세 | 선택 체력은 표시하지만 메인 기지 체력 전용 HUD는 없다. 상세는 하나의 문자열이다. | `Content.MainBase`의 실제 체력 모듈에서 상단 체력을 읽고 선택 상세는 지원 기능별 행으로 분리한다. |
| 자원 목록 | 생산 모듈의 OutputId와 건물 표시명에서 자원을 수집한다. 주요 자원 우선순위 설정은 없다. | 데이터 기반 우선순위 설정을 추가하되 미설정 기본은 전체 목록 접근 가능. 0 재고도 유지한다. |
| 가동 영역 | `BaseRegistry.Covered`의 제공자 필터와 `HasBuildArea`/`MapCoverage`의 용도가 다르다. | 가동 판정의 제공자 선택과 영역 조회를 재사용 가능한 읽기 경로로 정리한다. 모든 IBuildArea를 무조건 합치지 않는다. |
| 홀로그램 갱신 | BaseRegistry Revision은 등록/제거 및 조건부 일반 기지 레벨 합계 변화를 반영한다. | 특수 제공자·레벨·형상 변화도 캐시 무효화가 보장되는지 점검한다. Revision만으로 모든 변화를 감지한다고 가정하지 않는다. |
| 미리보기 | `OpenWorldInput.Update`는 Validate 결과를 bool로 축약하고 일부 이유를 버린다. | PlacementResult와 Resolve/CheckGround 이유를 읽기 상태로 보존하고 가동 여부는 별도 필드로 제공한다. |
| 개발자 노출 | `clock-dev`만 Editor 또는 Debug 빌드 조건으로 숨긴다. test-panel 전체에 같은 조건은 없다. | 개발자 패널 분리 시 기존 조건을 보존한다. 소환/초기화 전체를 개발 빌드 전용으로 바꾸는 것은 별도 정책 변경이다. |

## 3. UI 요소 이동·명령 연결표

기존 요소 이름은 우선 유지한다. 아래 새 이름은 구현용 제안이며 아직 UXML에 추가하지 않았다. HUD와 어댑터·Tools가 같은 변경에서 함께 전환되어야 한다.

| 새 표시 위치 | 유지할 요소 이름 | 추가/이동 및 연결 |
|---|---|---|
| 좌상단 체력·시계 | `clock-panel`, `clock-body`, `clock-fold`, `day-night-dial` | `main-health` 추가. `Content.MainBase` 체력, 파괴/없음 상태 표시. 기존 시계 재사용. |
| 상단 날짜·진행 | `clock-date`, `clock-remaining`, `mode` | `status-panel` 추가. 진행 막대 중복 없음. `play-pause`는 단일 진행 어댑터에 연결. |
| 상단 전력 | `base-power` | `power-panel` 추가. `BasePowerView`의 연결 기지 우선/선택 기지 fallback과 기지 식별을 유지. 생산·요청·실제 소비 상세 접기. |
| 좌측 자원 | `resource-panel`, `resource-body`, `resource-fold`, `resources` | 자원 행 컨테이너로 바꾸면 Label 조회도 함께 수정. 전체 목록 스크롤과 주요 자원 설정 연결. |
| 우상단 지도 | `minimap-fold`, `minimap-body`, `minimap` | `minimap-panel` 추가. `test-body` 밖으로 이동. 기존 투영·지형 캐시·클릭/드래그·포인터 캡처 재사용. |
| 지도 아래 선택 상세 | `content-stats`, `device-power`, `upgrade` | `selection-panel`, `upgrade-status` 추가. 비선택/파괴/수정 중 숨김. 실제 모듈의 행만 생성. |
| 플레이 메뉴 | `save-controls`, `save-status`, `save`, `load`, `new-game`, `new-game-confirmation`, `new-game-confirm`, `new-game-cancel`, `map-progress`, `map-energy`, `map-stage`, `orb-craft` | `play-menu`/접기 버튼 추가. 저장 가능 이유·마지막 저장·새 게임 확인·오브 제작의 기존 명령 유지. |
| 개발자 패널 | `test-panel`, `test-body`, `test-fold`, `amount`, `spawn-air`, `spawn`, `reset`, `counts`, `clock-dev`, `clock-day`, `clock-night` | 표시 제목 변경. 소환 수량 포커스·입력 종료 유지. `run`/`clock-pause` 정리 시 모든 기존 바인딩과 WorldClockView 참조도 갱신. |
| 하단 카탈로그 | `panel`, `categories`, 네 `category-*`, `inventory-scroll`, `inventory-items`, `inventory-empty`, `inventory-fold`, `inventory-toggle`, `edit`, `confirm`, `pending`, `message` | `cancel` 추가 → `Input.Cancel()`. 일반 상태는 수정, 편집 상태는 확정·취소. 수정 재클릭 취소와 헤더 외부 돌출 영역 입력 차단 유지. |

레이아웃은 상단, 좌측, 우측(지도+상세), 하단을 flex 컨테이너로 구분한다. 우측 높이는 하단 실제 높이를 침범하지 않게 제한하고 상세 본문은 스크롤한다. 선택 패널을 숨기면 빈 영역을 남기지 않는다. 작은 화면에서는 상세/메뉴 본문을 스크롤하고 버튼·하단 탭을 잘라내지 않는다. 접기와 분류 선택, 편집 예약의 수명을 분리한다.

시안의 미지원 배속·미니맵 확대/축소·임의 아이콘·가짜 수치는 만들지 않는다. 폰트는 기존 `Shared/Fonts/NotoSansCJKkr-Regular.otf`를 재사용한다. 새 이미지가 없어도 문자/벡터 기반 1차 구현에 착수할 수 있다.

## 4. 구현 작업 단위와 완료 조건

아래 순서로 각각 컴파일 가능한 단위로 진행한다. 새 파일명은 제안이다.

### P1. 읽기 계약과 HUD 상태

대상: `Code/Contracts/BuildingCapabilities.cs`, `Code/Progression/{UpgradeModuleDefinition,PerformanceUpgradeDefinition,MainBaseUpgradeDefinition}.cs`, `Code/Economy/{UpgradePurchase,ResourceBank}.cs`, `Tests/OpenWorld/Runtime/OpenWorldHud.cs`.

- `IUpgradeControl`에 읽기 조회(예: `CanUpgrade(out reason)`)를 추가하고 세 강화 구현의 명령이 이를 재사용한다. 캠페인 공유 레벨의 최종 변경은 기존 `ICampaignMainLevel.TryUpgrade(expectedLevel)`이 계속 책임진다.
- 구매 상태는 모듈 판정, 비용표 오류/미설정, 중복 자원 비용 합산, 잔액 부족을 조회한다. 비용 정책이 VerificationFreeUpgrade일 때만 `검증용 무료`로 표시한다. 조회 후 클릭 사이 상태가 바뀌어도 명령이 다시 검사한다.
- 편집/패배/복원 등 화면 차단은 Sandbox 쪽 표시 어댑터에서 합성한다. 공통 Runtime/Progression/Economy가 Sandbox나 UI Toolkit을 참조하지 않게 한다.
- `SelectedBuildingView`와 `PlayProgressView`를 OpenWorld Runtime에 두어 구체 모듈의 표시와 명령을 조합한다. 실제 asmdef 이름은 **EternalSteam.OpenWorldSandbox**다.
- 완료 조건: 반복 조회로 레벨·재고·캠페인·저장 요청이 변하지 않으며 성공 클릭 한 번에 강화 및 자동 저장 요청 한 번. 기존 기능 어셈블리 의존성 검사 유지.

### P2. UXML/USS 및 명령·입력 전환

대상: `Shared/UI/OpenWorld/OpenWorldHud.uxml/.uss`, `Tests/OpenWorld/Runtime/{OpenWorldHud,OpenWorldMinimap,ResourceStockView,WorldClockView,BasePowerView,MapProgressView,BuildingInventoryView}.cs`.

- 위 요소 연결표대로 한 번에 이동하고 null 조회가 남지 않게 한다. 선택·플레이 메뉴·개발자 본문의 접기를 각각 관리한다.
- 지도 입력 캡처 도중 숨김/분리/종료에도 캡처와 `Interacting`이 정리되어야 한다. 접힌 지도는 데이터 및 카메라 표시 갱신을 생략한다.
- `SpawnQuantityInput`의 포커스가 WASD를 막는 동작을 유지하며, 개발자 패널을 숨길 때 포커스가 남아 조작을 막는지 검사한다.
- 완료 조건: 새 UI의 모든 패널/탭/지도에서 월드 클릭·회수·휠 전파가 차단되고 숨긴 본문 위치는 조작 가능. 저장/새 게임/오브/강화 명령의 기존 제한 유지.

### P3. 가동 영역과 배치 미리보기

대상: `Code/Building/BuildingOperations.cs`, `Tests/OpenWorld/Runtime/{OpenWorldContent,OpenWorldInput,WorldEditSession,WorldGridGeometry,WorldGridView}.cs`; 새 표현 후보 `BuildAreaHologramView.cs`.

- 가동 영역 제공자 필터를 실제 가동 판정과 공유한다. 시작 씬의 AnyNormalBaseCoverage에서는 일반 메인/서브 기지 필터를 보존한다. 특수 제공자를 지원하는 기존 규칙 모드도 별도 회귀한다.
- 셀 키는 기존 2m/45° 격자, 영역 포함은 `IBuildArea.Contains`를 사용한다. 1셀 전체 포함 검사는 halfSize `(1,1)`과 격자 회전으로 판정한다. 중심점 포함만 검사해 121셀을 맞추지 않는다.
- `MapCoverage.Covered`는 지형/에너지용 배열이며 중심점 판정과 일반 기지 필터를 사용한다. 이를 그대로 범용 홀로그램이나 건물 설치 가능 마스크로 재사용하지 않는다.
- 등록·활성·확정된 제공자의 셀을 합집합으로 만들고 중복 면과 내부 외곽을 제거한다. 임시 설치는 제공자가 아니며 회수 예약 중인 확정 제공자는 유지된다. 파괴·확정 회수·강화·씬 종료에서 갱신/해제한다.
- 기본 셀의 지형 차단과 선택 건물 전체 footprint의 설치 결과를 분리한다. 후자는 실제 PlacementSession.Validate/Resolve/CheckGround의 결과를 사용한다. 파랑=가동 영역, 주황=설치 가능·비작동, 빨강+패턴=설치 불가를 문장과 함께 표시한다.
- 기존 WorldGridView의 9청크 중립 격자는 유지한다. 홀로그램은 보이는 청크와 변경된 영역의 재사용 메시로 구현하고 소유한 메시/머티리얼만 정리한다. Collider/그림자 없음.
- 수정 중 영역 외곽은 새 렌더러가 소유하고 `ContentBuildingView.ShowBuildArea` 중복 출력을 막는다. 일반 선택의 공격 사거리 표시와 분리한다.
- 완료 조건: 정면 정확히 121셀, 중첩 면 중복 없음, 취소/확정/패배 후 잔상 없음, 카메라·상태가 정지한 동안 재생성 횟수 증가 없음. 실제 메모리/GC 측정은 구현 후 수행한다.

## 5. 검증 도구의 선행 정리

| 도구/검사 | 현재 전제 또는 문제 | 준비된 수정·실행 기준 |
|---|---|---|
| `Tools/VerifyHudFolds.cs` | 상단 clock/resource/test 세 패널만 검사 | 지도·플레이 메뉴·선택 상세·개발자 접기와 임시 예약 보존을 추가. 접은 지도 갱신 생략 및 개발자 접기와 지도 독립성 검사. |
| `Tools/VerifyMinimap.cs` Main/Input | 현재 요소 이름/표시 상태와 실제 InputSystem 경로 사용 | 새 위치에서 다시 실행. 캡처 중 접기/해제와 지도 밖 휠 정상 동작 추가. |
| `Tools/VerifyBuildingInventoryUI.cs` | cancel이 없어야 한다는 단정, 재펼침 때 방어 분류 초기화 단정, 기타 분류가 비어 있다는 단정, 한 줄 8개 고정 검사 | 명시 취소 버튼을 검사하고 현재의 분류 보존으로 수정. 실제 카탈로그에서 기대 목록 계산. 반응형 변경에 맞춰 항목 접근 가능/잘림/스크롤을 검사. 예약 해제·회수 취소 검사는 유지. |
| `Tools/VerifyWorldGridVisibility.cs` | 중립 격자의 9메시·이동 시 3개 재생성 검사 | 기존 기준 유지. 별도 홀로그램 검사로 표시 수명·중첩·변경 갱신·정지 중 캐시를 확인. |
| `Tools/VerifyStartLoop.cs` | 신규 Play에서 자동 메인 1개를 전제로 하며 건물 설치·밤·보스·오브까지 진행 | 다른 변경성 검사의 뒤에서 같은 상태로 실행하지 않는다. 새 씬/Play 세션에서 전용 실행. 기존 121셀 판정과 렌더된 셀 집합 일치 검사 추가. |
| 새 강화 조회 EditMode 검사 | 아직 없음 | 세 구현, 최대/활성/레벨 제한, 비용 미설정/중복/부족, 반복 조회 무변경, 클릭 시 재검증 및 결제 원자성 검사. |
| 기존 저장·입력·기능 회귀 | 도구마다 씬/초기 상태가 다름 | `VerifySingleMapSave`, `VerifyQuantityFocus`, `VerifyDragMouseInput`, `VerifyBasePowerIntegration`, `VerifyResourceStockUI`의 해당 전제에 맞게 격리 실행. |

실행 순서:

1. 컴파일 및 UXML/USS import 오류 확인, 관련 EditMode/Architecture 검사.
2. 별도 임시 저장 루트에서 StartRegionSandbox 신규 세션 시작. [저장 검증 지침](Validation/2026-09-24-single-map-save.md)의 `ETERNAL_STEAM_SAVE_TEST_ROOT` 설정을 적용하고 제품 저장으로 검증하지 않는다.
3. 초기 HUD, 접기, 지도 실제 입력, 카탈로그·편집·취소/확정 검사. 상태를 변경하는 도구 사이에는 초기 세션을 복원한다.
4. 강화/가동 영역/전력 및 저장·복원·패배 검사. StartLoop는 독립 신규 세션에서 실행한다.
5. 1366×768, 1920×1080, 2560×1440, 3440×1440에서 일반/선택/수정/접힘/메뉴 상태를 촬영한다. 문자 잘림·지도와 상세의 하단 침범·접힌 빈 영역 입력 차단을 확인한다.
6. 홀로그램 정지/이동/영역 변경 재생성 횟수와 GC 측정 결과를 별도 기록한다. 게임 빌드는 이번 준비 및 1차 Editor 검증 범위에 포함하지 않는다.

## 6. 착수 판단

**P1부터 착수 가능한 상태다.** 주요 자원 우선순위, 최종 아이콘·비용·성장 정책은 미정이지만 전체 자원 접근, 실제 모듈 값, 현재 검증용 무료 정책으로 1차 구현할 수 있다. 시안에만 있는 숫자·아이콘·배속·미니맵 줌을 채워 넣지 않는다.

첫 구현 단위는 강화/진행의 읽기 상태와 그 회귀 검사다. 이어 UXML/USS·입력 연결을 함께 바꾸고, 마지막으로 동일 가동 판정을 쓰는 홀로그램을 붙인다. 각 단계의 실제 결과가 나온 뒤에만 적용결과 문서와 현재 구현 목록을 갱신한다.
