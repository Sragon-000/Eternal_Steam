# Canvas HUD·건설 표시의 실제 Hierarchy 적용

2026-09-28 사용자 지시: 모든 적용 사항은 실제 계층에 저장하고, HUD도 Canvas Hierarchy로 전환한다. 이전 P2/P3의 런타임 표시 오브젝트 구성 방식을 수정했다. **씬 파일에 실제 계층·참조 저장 완료, C# 컴파일·정적 참조 검사 완료. Unity import·화면·NUnit/Play 실행 검증은 미완료**다.

## 저장된 씬과 계층

- `Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity`: `GameplayCanvas`와 `GameplayEventSystem`을 추가했다. 버튼 59개, 카탈로그 25개를 포함한 이번 변경의 새 GameObject 206개가 씬에 저장되어 있다.
- `Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity`: 같은 Canvas 구성, 버튼 57개·카탈로그 23개를 포함한 새 GameObject 202개를 저장했다.
- 기존 `Test Space HUD`와 `OpenWorldHud` 컴포넌트는 삭제하지 않고 비활성화했다. 현재 두 씬의 HUD는 UIDocument를 실행하지 않는다. 이전 UXML/USS와 UI Toolkit 표시 코드는 역사적 검증 씬용으로 보존했다.
- 두 현재 씬과 같은 `OpenWorldInput`을 사용하는 과거 스냅샷 5개에는 `ConstructionPresentation` 25개 오브젝트를 각각 저장했다. 공통 런타임 코드를 변경했으므로 과거 씬에도 누락 참조가 생기지 않게 연결했다. 과거 스냅샷의 HUD까지 새 레이아웃으로 바꾸지는 않았다.

```text
GameplayCanvas
├─ TopBar                         날짜·시간·모드·전력·진행
├─ LeftStatus                     메인 체력·낮밤 시계·자원 스크롤
├─ RightStatus                    미니맵·선택 상세·강화 스크롤
├─ ConstructionBar                수정·확정·취소·분류·카탈로그 스크롤
├─ Notifications                  작업 결과·배치 판정
├─ ProgressAndSave                저장·이어하기·새 게임 확인·오브
├─ PowerDetails                   상세 전력 스크롤
└─ Developer                      수량 입력·소환·공중 적·낮밤·초기화
GameplayEventSystem               InputSystemUIInputModule
ConstructionPresentation
├─ WorldGrid                      Chunk0 … Chunk8
├─ BuildAreaHologram               Chunk0 … Chunk8
├─ PlacementPreview
│  └─ InvalidFootprintPattern
├─ SelectedWeaponRange
└─ DragSelection
```

접힌 메뉴, 선택 패널, 배치 미리보기는 처음에는 비활성 상태이지만 Hierarchy에 저장되어 있다. 격자/홀로그램도 MeshFilter·MeshRenderer·머티리얼 참조가 존재한다. 시작 상태에서 표시하지 않는 오브젝트를 확인하려면 Hierarchy에서 선택하면 된다. 기존 지형/건물 오브젝트는 삭제하지 않았다.

## 코드와 에셋

`CanvasWorldHud`는 씬에 직렬화된 TMP 텍스트·Button·패널·카탈로그 참조를 갱신한다. 버튼의 Inspector `OnClick`에는 `Execute(string)` 명령과 인수가 저장된다. 실행 중 버튼/패널/카탈로그 GameObject를 생성하지 않는다. 분류 변경은 저장된 항목의 활성 상태를 바꾸고 GridLayoutGroup·ContentSizeFitter가 다시 배치한다. CanvasScaler는 1280×800 기준, 높이 기준 배율로 구성했다. 각 주요 영역은 화면 모서리와 가장자리에 고정한다.

`CanvasWorldMinimap`과 `CanvasClockDial`은 저장된 Graphic 컴포넌트다. 미니맵의 지형·적 밀도·기지/건물·보스·카메라 영역은 텍스처/메시 데이터로 갱신하고 클릭·드래그 이동을 연결했다. 접기/비활성화 시 상호작용 상태를 해제한다. 기존 강화 결제·저장·공략 명령은 기존 서비스로 전달한다. 자원 0 재고, 같은 출력 ID 중복 제거, 주요 자원 미설정 시 전체 노출을 유지한다.

`WorldGridView`·`BuildAreaHologramView`는 런타임 오브젝트 팩토리를 제거하고 저장된 9쌍의 렌더러/필터를 초기화한다. 메시 버퍼와 지형·영역 데이터 갱신은 플레이 상태에 따라 계속 수행한다. 배치 Cube·오류 패턴·공격 사거리·드래그 LineRenderer도 `OpenWorldInput`에 직렬화했고, 색상 머티리얼은 `.mat` 에셋으로 저장했다. 오브젝트/머티리얼을 런타임에 만들어 누락 참조를 보완하지 않는다.

한글 TMP 표시를 위해 설치된 `com.unity.ugui` 패키지의 공식 TMP Essential Resources를 로컬 패키지에서 프로젝트로 복원했다. 기존 Noto Sans CJK KR 원본 폰트를 사용하는 동적 fallback 에셋을 추가하고, Latin 기본 폰트의 fallback 목록에 저장했다. 한글 fallback은 1024 atlas, Clear Dynamic Data On Build 설정을 사용한다. 폰트 face metrics는 원본 OTF의 head/hhea 테이블에서 읽었다. 실제 glyph 생성·줄바꿈·스타일 import는 Editor 확인이 남았다.

`AGENTS.md`에도 앞으로 정적 표현 계층과 버튼 콜백을 씬/프리팹에 저장하고, 런타임 생성만으로 적용 완료라 하지 않는 기준을 추가했다. 적 스폰·실제 건설 인스턴스와 게임 상태에 따라 달라지는 메시/텍스처 데이터는 게임 로직이며, 이 정적 HUD 계층 전환과 구분한다.

## 검증과 제한

| 검사 | 결과 |
|---|---|
| 저장된 계층 검사 | `Tools/verify_authored_hierarchy.py`: 7개 씬의 신규 fileID·GUID·부모/자식·9청크 배열·버튼 콜백·Canvas 참조 PASS. [검사 자료](2026-09-28-canvas-references.json) |
| 기존 오브젝트 보존 | 수정 직전 `/tmp/eternal-canvas-scene-backups`와 비교. 기존 fileID 전부 보존. 현재 씬은 기존 5블록, 과거 씬은 3블록만 변경하고 새 계층을 추가. [보존 자료](2026-09-28-canvas-preservation.json) |
| C# 컴파일 | Unity 번들 Roslyn으로 Runtime·Progression·Economy·OpenWorldSandbox·EditModeTests 5개 어셈블리 및 검증 도구 컴파일 성공. 임시 출력 `/tmp/eternal-canvas-compile` |
| 신규 Editor 테스트 | `CanvasHierarchyTests`: 두 저장 씬을 PreviewScene으로 읽어 Canvas/EventSystem·콜백·폰트·스크롤·미리보기·9청크 참조를 검사하도록 작성·컴파일. 실행은 대기 |
| 신규 Play 도구 | `VerifyCanvasHud.Main`: 수정/취소·맵 접기·분류·명령 실행 전후 계층 수·저장 콜백 검사. 작성·컴파일, 실행은 대기 |
| 정적 생성 호출 검사 | Canvas HUD·미니맵·시계·중립 격자·홀로그램·입력·드래그 표시 코드에 GameObject 생성/AddComponent 호출 없음 |
| Unity 실행 시도 | CLI 연결 없음. 목록에는 연결 불가 인스턴스가 남아 있고 Safe Mode는 보고되지 않음. Editor UI 조회도 timeout. 직접 batch 실행은 `Rosetta 2 isn't installed`를 출력하고 시작 전 종료 |
| 실제 import/Play | 미실행. 스크립트/씬/셰이더/TMP import·다양한 해상도·마우스/키보드·한글 폰트·메모리/GC·게임 저장 회귀는 아직 완료하지 않음 |

연결 가능한 Editor가 없음을 확인하고 씬 파일을 직접 수정했다. 정적 검사와 Roslyn 컴파일은 Unity의 씬 역직렬화·AssetDatabase import·실제 화면 검증을 대신하지 않는다. 게임 빌드는 실행하지 않았다.

## 이어서 확인할 순서

1. Editor 실행 환경 복구 후 두 현재 씬을 Edit Mode에서 열고 `GameplayCanvas`, `ConstructionPresentation` 및 Inspector 참조를 확인한다. 미저장 Editor 씬이 따로 있다면 덮어쓰지 말고 디스크 변경과 비교한다.
2. `CanvasHierarchyTests`, `BuildAreaCoverageTests`, `UpgradeQueryTests`와 기존 건설/회수/전력/저장 회귀를 실행한다.
3. 임시 저장 루트의 신규 세션에서 `VerifyCanvasHud`, `VerifyBuildAreaHologram`, `VerifyWorldGridVisibility`를 실행한다.
4. 1366×768, 1920×1080, 2560×1440, 3440×1440에서 한글·선택 상세 스크롤·분류·메뉴 겹침·입력 차단·미니맵 드래그를 검증한다. 저장/복원·강화·오브 제작도 별도 새 세션에서 확인한다.
5. 이전 `VerifyHudLayout`, `VerifyHudFolds`, `VerifyMinimap` 등 UI Toolkit 전용 HUD 도구는 현재 Canvas 씬 검증에 그대로 사용하지 않는다. 컴파일 가능하더라도 실제 Canvas 검사 PASS를 뜻하지 않는다.

`Tools/author_canvas_hierarchy.py`는 이번 오프라인 이관 도구이며 런타임에서 실행하지 않는다. 이미 이관된 씬에 재실행하면 중복 이관을 거부한다. 이후 수정은 가능하면 연결된 Editor에서 기존 오브젝트를 직접 변경하고 저장한다.
