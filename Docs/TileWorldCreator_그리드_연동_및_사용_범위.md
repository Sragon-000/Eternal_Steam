# TileWorldCreator 그리드 연동 및 사용 범위

작성일: 2026-09-17. 프로젝트에 설치된 TileWorldCreator 4.3.5 소스와 기존 게임 코드를 기준으로 작성했다.

## 1. 현재 적용 상태

**OpenWorldSandbox에 TWC 산·강 맵과 건설·소환 연동을 적용했다.** 실제 사용 범위는 아래 표와 [적용·검증 기록](Validation/TileWorldCreator_산과강_적용.md)을 기준으로 한다. 나머지 활용 가능 기능은 자동으로 적용된 것이 아니다.

| 구분 | 상태 | 근거 및 의미 |
| --- | --- | --- |
| TWC 소스·타일 리소스 | 설치됨 | `Assets/TileWorldCreator`에 C# 소스, URP 타일 프리셋 포함 |
| TWC 샘플 | 패키지 파일 있음 | `Samples_URP.unitypackage`가 있으며, 조사 시 샘플 씬은 미전개 상태 |
| TWC API 호출 | Editor 생성에 사용 | `Tools/ApplyTileWorldMountainRiver.cs`가 Blueprint와 Build Layer를 구성하고 생성 API 실행 |
| 저장된 씬의 TWC Manager | 사용 중 | OpenWorldSandbox의 `TWC · Mountain River`, 96×96 셀·2m·45°·13개 레이어 |
| 기존 건설 그리드 | 사용 중 | `BuildGrid`, `WorldGridGeometry`, `FoundationPlacement` |
| OpenWorld 지형 | TWC 메시·콜라이더 사용 | Terrain은 렌더링·충돌을 끈 높이 캐시로만 유지. `TileWorldGround`가 건설 가능 영역과 실제 지면 조회 담당 |
| TWC 생성·연동 실행 검증 | Edit/Play 검증 수행 | 씬 재로드, 평지·강·산지 판정, 적 512마리 소환·10초 이동, 건설 UI 검증 기록 참조 |

현재 상태는 저장된 씬과 Unity Editor 실행 검증 기준이다. 원본 Terrain 데이터와 교체 전 씬 백업을 보존했다. 초기 조사 시 연결되지 않던 Pipeline은 기존 서버 재시작 후 연결했다.

## 2. 현재 사용하는 부분과 유지할 역할

| 현재 시스템 | 실제 사용하는 부분 | TWC 연동 시 역할 |
| --- | --- | --- |
| `BuildGrid` | 셀 좌표 변환, 범위, 차단 셀, 건물 점유·예약 | 건설 규칙의 기준으로 유지 |
| `WorldGridGeometry` | XZ 평면의 Y축 45° 격자, 토대 위치·겹침·지형 판정 | 좌표 규칙은 유지하고 지형 조회를 연결 |
| `FoundationPlacement` | 토대별 `BuildingWorld`와 배치 세션 생성·관리 | 토대별 건설 칸과 건물 수명 관리 유지 |
| `BuildingWorld`·배치 세션 | 건설 요청·확정과 점유 처리 | TWC 오브젝트 배치 기능으로 대체하지 않음 |
| `Terrain.SampleHeight()` 등 | 적·카메라 높이와 지형 범위 조회 | TWC 메시에서 구운 숨김 Terrain 캐시 사용; 건설은 TWC 콜라이더 조회 |
| 기존 적 시스템 | 생성·이동·전투·렌더링 | 지형 및 통행 데이터만 연결; TWC가 자동 대체하지 않음 |

OpenWorld는 **8×8m 토대 하나에 2m 간격 4×4 건설 칸**을 사용한다. 각 토대는 별도 `BuildGrid`를 소유하므로, 서로 다른 토대의 로컬 `(0, 0)`은 서로 다른 월드 위치다. 위 수치는 OpenWorld 구성값이며 모든 데모의 공통 고정값은 아니다.

`Code/LegacyDemo/HordeBuildGrid.cs`는 레거시 데모의 격자 선을 그리는 컴포넌트다. 실제 점유·예약 로직을 가진 공통 `Code/Placement/BuildGrid.cs`와 구분한다.

## 3. TWC에서 활용 가능한 부분

다음은 설치된 소스에서 확인한 기능 목록이다. 현재 사용하는 것은 맵 설정·Blueprint 셀 배치·TilesBuildLayer·전체 맵 생성·메시 병합·콜라이더다. 내장 던전 생성기, 장식물, 런타임 지형 수정은 아직 사용하지 않는다.

| 활용 가능한 기능 | 확인한 기능·API | 사용 용도 및 조건 |
| --- | --- | --- |
| 맵 설정 | `Configuration.width`, `height`, `cellSize`, 시드 설정 | 크기·셀 간격·시드를 지정한 맵 제작 |
| 절차적 생성 | BSPDungeon, RandomWalkDungeon, CellularAutomata, Maze, RandomNoise 등 | 방·통로·섬·지형 셀 생성 |
| 지형 영역 가공 | Add, Subtract, Boolean, Expand, Shrink, Smooth 등 | 건설 부지, 물, 절벽, 통로 레이어 분리 |
| 셀 조회 | `GetBlueprintLayer()`, `allPositions`, `CellPositionExists()` | 지형 종류·존재 여부를 게임 판정에 전달 |
| 타일 외형 생성 | `TilesBuildLayer`, `TilePreset` | 지면·절벽·강·계단·경사로 외형 구성 |
| 장식물 생성 | `ObjectBuildLayer` | 가중치·회전·크기 변화를 적용한 환경 프리팹 배치 |
| 전체 맵 생성 | `GenerateCompleteMap()` | 준비된 Blueprint와 Build Layer 실행 |
| 지형 수정 | `AddCellsToLayer()`, `RemoveCellsFromLayer()`, `ExecuteBuildLayers()` | 셀 변경 후 생성물 갱신. 게임 판정 동기화는 별도 구현 |
| 생성 알림 | `OnMapReady`, `OnBlueprintLayersReady`, `OnBuildLayersReady` | 연동 데이터 갱신 시점 연결. 실제 메시·콜라이더 완료 시점 검증 필요 |
| 전용 생성 규칙 | `BlueprintModifier.Execute()` 확장 | 기지 안전 구역·건설 부지·진입로 등 프로젝트 전용 규칙 |
| 메시 병합·콜라이더 | 클러스터, mergeTiles, colliderType | 지형 생성물 구성·성능 조정. 실제 성능 측정 필요 |
| 생성물 저장 | `SaveMeshesAndPrefab()` | Editor에서 메시·프리팹 저장. 기본 구현은 저장 경로 대화상자를 사용 |

TWC의 `Pathfinding`은 Blueprint에 경로 셀을 생성하는 기능이다. 이를 사용해도 적의 장애물 회피나 런타임 이동 시스템이 자동으로 구현되지는 않는다.

## 4. 적용 범위와 후속 연동안

맵 설정·지형 레이어·타일 생성·건설 지형 판정은 이번에 적용했다. 나머지는 아래와 같이 분리한다.

| 분류 | 연동안 |
| --- | --- |
| 현재 사용하는 TWC 부분 | Configuration, Blueprint 지형 레이어, TilesBuildLayer, 지형 셀 조회 |
| 현재 연결한 부분 | 토대 설치 가능한 지형·높이·경사·맵 경계 조회 |
| 필요할 때 추가할 부분 | 환경 장식물, 절차적 생성, 시드별 맵 후보, 지형 변경 |
| 기존 게임에 남길 부분 | 토대·포탑 건설, 예약·점유, 확정·취소·회수, 공격·경제·적 관리 |
| 별도 설계가 필요한 부분 | 이동 가능 영역·장애물 회피, 지형 변경 시 건물 처리, 저장·복원 |

연결 구조는 **TWC 지형 생성 → 지형·좌표 변환 어댑터 → 기존 토대 설치·건설 판정**으로 구성한다. 어댑터는 `TileWorldGround`로 구현했다. TWC 배포 소스는 수정하지 않고 프로젝트 소유 코드에서 연결한다. 적 이동은 기존 직선 방식이며, 평지를 벗어나는 직선 경로는 소환 후보에서 제외한다.

## 5. 그리드 좌표 연결 규칙

### 셀 크기·회전·원점

- 기존 `BuildGrid`: 역회전한 로컬 좌표를 셀 크기로 나눈 뒤 `FloorToInt`를 사용한다. 셀 중심은 `(인덱스 + 0.5) × 셀 크기`다.
- TWC: Manager의 `InverseTransformPoint`로 변환한 좌표를 `cellSize`로 나눈 뒤 `RoundToInt`를 사용한다.
- OpenWorld 건설 칸과 정렬하려면 TWC 셀 크기 2m, Y축 회전 45°를 기본 후보로 삼고 원점·반 셀 오프셋을 맞춘다. Manager와 부모 Transform의 스케일은 1을 기본 조건으로 한다.
- 두 시스템의 같은 숫자 좌표가 같은 위치라는 가정을 하지 않는다. 토대의 로컬 셀 중심을 월드 좌표로 바꾼 후 TWC 논리 셀로 변환한다.
- TWC의 유한 맵 인덱스와 기존 월드의 음수 좌표를 연결할 때는 맵 원점 오프셋을 명시한다.

같은 간격·회전의 논리 셀 중심을 맞추는 초기 관계식은 다음과 같다. 실제 배치는 선택한 프리셋과 콜라이더로 검증한다.

```text
건설 셀 중심 = 건설 원점 + 회전 × ((x + 0.5) × s, 0, (z + 0.5) × s)
TWC 셀 중심  = TWC 원점  + 회전 × (u × s, 0, v × s)

동일 인덱스의 중심 정렬 후보:
TWC 원점 = 건설 원점 + 회전 × (0.5 × s, 0, 0.5 × s)
```

경계에서 `RoundToInt`와 `FloorToInt`의 결과가 다를 수 있으므로, 클릭·설치 셀 소유권은 기존 `BuildGrid` 판정을 기준으로 한다. TWC 조회는 그 셀의 중심·면적을 변환해서 수행한다. 맵 경계는 변환된 논리 인덱스와 `width`·`height`로 검증하며, 셀 인덱스와 월드 길이 단위를 혼용하지 않는다.

### Standard Grid와 Dual Grid

TWC는 Standard와 Dual Grid를 지원한다. Dual Grid에서는 논리 셀 하나가 주변 반 셀 위치의 시각 타일 네 개에 영향을 줄 수 있다. 따라서 **생성된 GameObject 하나를 건설 칸 하나로 취급하지 않는다.**

건설 판정의 데이터 기준은 Blueprint 논리 셀로 삼는다. 외형 조회가 필요하면 `GetTilesAtBlueprintCell()`처럼 논리 셀과 시각 타일을 연결하는 API를 사용한다. Dual Grid 외곽의 곡선·모서리에는 실제 지지 면적 검사가 추가로 필요하다.

### 지형과 토대 위 건설 칸 구분

토대 아래 지형의 물·빈 공간·경사 정보는 우선 **토대 설치 판정**에 사용한다. 포탑은 토대 윗면의 건설 칸을 사용하므로, 아래 지형이 물이라는 이유만으로 모든 포탑 칸을 차단하는 정책은 자동 적용하지 않는다. 수상 토대·교량을 허용할지는 게임 규칙으로 정한다.

`BuildGrid.SetBlocked()`는 필요 시 사용할 연결 지점이다. 여러 차단 사유를 한 불리언에 직접 덮어쓰지 않도록 어댑터에서 지형·기타 차단 사유를 합산한 결과를 반영한다. 지형 갱신으로 기존 예약·점유를 임의로 삭제하지 않는다.

## 6. 지형 조회와 동적 변경

기존 Terrain 경로는 25개 지점을 검사한다. 현재 TWC 경로는 `TileWorldGround.CheckFoundation()`에서 토대 영역 81개 지점의 허용 셀과 실제 콜라이더 높이를 확인하고 높이 차이 1.2m 초과를 거부한다. 다음 원칙을 적용하며 동적 변경은 후속 범위다.

- 논리 셀의 지형 종류·유효 범위·지면 존재 여부.
- 생성된 지형 콜라이더 등을 통한 실제 높이·경사 조회. 토대나 장식물에 잘못 맞지 않도록 조회 레이어 구분.
- 토대 전체 면적의 지지 여부. 중앙 한 점만 확인하면 절벽·물·빈 공간 위로 걸칠 수 있음.
- 적 생성 범위·높이 조회와 포인터의 지면 선택도 Terrain 의존 지점을 함께 교체.

TWC의 `SampleLayerHeight()`는 레이어 높이 조회 API이며 경사로 메시의 정확한 표면 높이와 동일하다고 가정하지 않는다.

동적 지형 변경을 채택하면 셀 데이터 변경, 메시·콜라이더 갱신, 건설·이동 데이터 갱신의 순서를 맞춘다. 이미 건물이 있는 셀의 지형을 제거할 수 있는지, 제거 시 건물을 유지·회수·파괴할지는 별도 규칙이 필요하다.

## 7. 적용 완료로 표시하기 위한 검증

실행한 검증과 결과는 [적용·검증 기록](Validation/TileWorldCreator_산과강_적용.md)에 구분했다. 아래는 전체 연동의 검증 범위이며, 임의 원점 변경·Standard Grid·경사로·동적 변경·대규모 성능 전체를 검증했다는 의미는 아니다.

1. 별도 테스트 씬에서 TWC 지형 생성 및 콘솔 오류 확인.
2. 2m 간격·45° 회전·원점 이동에서 건설 칸 중심과 지형 셀 정렬 확인.
3. 셀 경계 직전·정확한 경계·직후, 음수 좌표, 맵 외곽 좌표 판정 확인.
4. 서로 다른 토대의 같은 로컬 셀 인덱스가 올바른 TWC 위치로 연결되는지 확인.
5. Standard·Dual Grid에서 논리 셀과 외형·콜라이더 관계 확인.
6. 평지·물·절벽·경사로·지형 구멍에 걸친 토대 설치 판정 확인.
7. 기존 건설 예약·확정·취소·회수 및 점유 판정 유지 확인.
8. 적 생성·높이 조회와, 채택할 경우 이동 경로 연동 확인.
9. 채택할 경우 지형 변경 후 콜라이더·건설 판정 동기화와 기존 건물 정책 확인.
10. 생성·재생성 시간, 메시·콜라이더 수, 플레이 성능 측정.

## 8. 근거 코드

- [BuildGrid](../Assets/EternalSteam/Code/Placement/BuildGrid.cs): 건설 셀, 점유·예약·차단.
- [WorldGridGeometry](../Assets/EternalSteam/Tests/OpenWorld/Runtime/WorldGridGeometry.cs): 45° 회전, 토대별 4×4 격자, Terrain 판정.
- [FoundationPlacement](../Assets/EternalSteam/Tests/OpenWorld/Runtime/FoundationPlacement.cs): 토대별 건설 시스템.
- [OpenWorldSandbox](../Assets/EternalSteam/Tests/OpenWorld/Runtime/OpenWorldSandbox.cs): Terrain과 적·건설 시스템 조립.
- [TileWorldCreatorManager](../Assets/TileWorldCreator/Code/Components/TileWorldCreatorManager.cs): 생성·셀 조회·좌표 변환 API.
- [Configuration](../Assets/TileWorldCreator/Code/Data/Configuration.cs): 맵 크기·셀 크기·시드·레이어 설정.
- [TilesBuildLayer](../Assets/TileWorldCreator/Code/Data/Layers/TilesBuildLayer.cs): 타일 생성과 Dual Grid 셀 매핑.
- [ObjectBuildLayer](../Assets/TileWorldCreator/Code/Data/Layers/ObjectBuildLayer.cs): 환경 프리팹 배치.
- [BlueprintModifier](../Assets/TileWorldCreator/Code/Data/BlueprintModifier.cs): 사용자 정의 규칙 확장 지점.
- [기존 오픈월드 사용 안내](오픈월드_테스트_공간.md).
