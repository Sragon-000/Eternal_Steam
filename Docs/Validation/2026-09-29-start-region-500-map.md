# 시작 지역 500×500 맵 교체 기록

기준: 2026-09-29. 원본 프로젝트 `StartRegionSandbox`를 대상으로 한다. 게임 빌드는 수행하지 않는다.

## 제공 에셋과 기존 씬

- 새 원본: `Assets/Map/TileWorldCreatorConfiguration.asset` (TWC 500×500셀, 셀 1m, Floor1/2/3). Floor1은 250,000셀을 덮고, Floor2의 저장된 월드 셀은 8,102개다. 원본 타일 프리셋 참조는 프로젝트에 있다.
- 교체 전 시작 씬: `Assets/Prefab_Map/MapCreater .prefab`의 50×50셀 인스턴스, 별도 숨김 Terrain 높이 캐시와 `TileWorldGround` 통행 셀. 원본 프리팹과 교체 전 씬 사본 `Assets/EternalSteam/Scene/Tests/StartRegionSandbox_Before500Map.unity`은 보존했다. 과거 96×96 산·강 TWC 루트도 비활성으로 남아 있다.
- 건설 논리 격자는 계속 2m·Y축 45°다. 새 맵의 1m 타일과 건설 셀을 같은 크기로 해석하지 않는다.

## 적용 경로

`Assets/EternalSteam/Tests/OpenWorld/Editor/StartRegion500MapAuthoring.cs`의 **Eternal Steam → Open World → Apply 500x500 Start Region Map**을 원본 Editor에서 실행했다. 적용 전 시작 씬 사본을 만들고 TWC 원본으로 저장된 메시·콜라이더를 생성한다. 5셀 클러스터를 25셀로 다시 구워 생성 객체 수를 낮췄다. 메인 기지는 새 맵의 중앙 부근 평지 (248,257)에 정렬하고, 높이 캐시·통행 셀·연결 가능한 8개 공세 지점·지역 경계·카메라 초점을 갱신했다. 기존 50×50 인스턴스는 시작 씬에서 제거하고 원본 프리팹은 보존했다. 이미 교체된 씬에서 도구를 다시 실행하면 중복 생성 없이 종료한다.

카메라는 회전된 실제 맵의 로컬 경계 안으로 이동을 제한한다. 미니맵은 `TileWorldGround`와 Terrain 크기에서 외곽과 지면 이미지를 읽으므로 별도 고정 50×50 수치를 사용하지 않는다. 지형 교체 전 저장은 기존 `SingleMap` 경로에 보존하고, 새 구성은 `SingleMap/maps/<구성 지문>`에 별도 저장한다. 이전 지형의 건물을 새 지형에 부분 복원하지 않는다.

## 검증 상태

- Unity 6000.3.14f1 Editor가 전체 스크립트를 컴파일했고, `Temp/ApplyStartRegion500.status`는 `complete`를 기록했다.
- 저장된 씬에 `TWC · Start Region 500` 루트, **1,136개**의 영구 메시/콜라이더, 500×500 통행 셀 중 **224,622개**의 설치·통행 가능 셀, 새 Terrain 높이 캐시를 확인했다.
- Play에서 새 지형 렌더링, 고정 메인 기지 **100/100** 자동 설치, 미니맵의 전체 45° 맵 표시와 카메라 영역을 확인했다. 이전 지형 저장 파일은 변경하지 않고 새 구성의 별도 저장 파일이 생성됐다.
- 게임 빌드는 수행하지 않았다. 실제 설치/회수 조작, 야간 8방향 공세와 장시간 적 이동, 기존 데모 3맵 회귀는 이번 확인 범위에 포함되지 않았다.

후속 인수 항목: 정면 11×11 구역의 실제 배치/회수, 8개 야간 스폰의 메인 접근, WASD/휠·미니맵 클릭, 저장·새 게임, 기존 데모 3맵 비침범. 원본 설정 에셋은 약 147MiB, 생성 메시와 높이 캐시는 약 277MiB다. GitHub 업로드 전 LFS 또는 경량화를 결정해야 한다.
