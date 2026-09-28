# 시안 후속 HUD 시각 요소 적용

2026-09-28. StartRegionSandbox와 OpenWorldSandbox의 기존 Canvas 계층에 적용하고 저장했다. 이전 밀도 개선에서 남았던 패널·아이콘·카드·선택 표현을 대상으로 한다.

## 적용한 표현

- 어두운 패널에 얇은 금속색 이중 테두리와 금색 모서리 장식을 적용했다. 크기가 변해도 선 굵기를 유지하는 9-slice Sprite를 사용한다.
- 메인 기지 심볼, 일곱 자원별 색상 아이콘, 전력 아이콘, 범주 아이콘을 저장된 Image로 추가했다.
- 낮·밤 시계에 외곽 링, 눈금, 해·달 표시를 추가했다. 기존 시계 바늘은 실제 시간에 따라 움직인다.
- 실제 프로젝트 프리팹 29개를 에디터에서 렌더링해 건물 썸네일 에셋으로 저장했다. 시작 씬 25개, 오픈월드 씬 23개 기존 카드에 연결했다. 선택 상세에는 정의 ID별 33개 이미지 매핑을 저장했다.
- 하단 카탈로그를 한 줄 카드와 가로 스크롤바로 바꿨다. 기본 펼침 높이는 168, 접힘은 44이며 작은 화면에서는 최소 카드 높이에 맞춰 보정한다. 선택 범주는 금색, 선택한 배치 카드는 청록색으로 표시한다.
- 기지 레벨 미충족 건물 숨김과 직접 선택 차단을 유지했다. 레벨 조건 충족 후 다시 표시한다.
- 상세 패널에는 건물 이미지와 실제 체력 막대를 추가했다. 강화 버튼·불가 사유를 스크롤 영역 밖 하단에 고정했다.
- 상단 전력은 실제 저장량/용량, 생산-실제 소비 속도, 충전 막대로 표시한다. 소속 기지 내부 ID는 표시 이름으로 바꿨다.
- 미니맵 지형 색상을 갈색 계열로 맞췄다. 클릭·드래그 및 실제 지형/개체 데이터는 유지한다.
- 월드 격자가 공유하던 노란 Tracer 재질을 전용 중립색 반투명 재질로 분리했다. TileWorld의 플레이 가능 지면 밖에는 격자를 생성하지 않는다. 기존 푸른 가동 영역 홀로그램 및 막힌 셀 표시를 유지한다.

정적 패널·버튼·아이콘·카드·스크롤바는 씬에 저장되며, Play 초기화에서 재생성하지 않는다. 런타임은 표시 상태·수치·선택 이미지·기존 RectTransform·메시 데이터만 갱신한다. 버튼의 기존 persistent callback을 유지했다.

## 실제 검증

Unity 6000.3.14f1의 연결된 에디터에서 실행했다. 테스트는 사용자 저장 데이터와 분리한 임시 경로를 사용했으며 Play 종료 후 환경 설정 및 Game View 해상도를 복원했다.

- **EditMode 89/89 통과**. 두 씬의 저장된 썸네일, 자원 아이콘, 프레임, 상세/전력 막대, 스크롤바, 강화 버튼 위치, 중립 격자 재질 참조 검사를 포함한다.
- 두 씬에서 새 HUD 시각 참조·범주/건물 선택 강조·가로 스크롤·접기·persistent callback 검증 통과. 메인 기지 이미지 및 고정 강화 조작은 메인 기지가 있는 시작 씬에서 확인했다.
- 시작 씬에서 기존 Canvas 명령, 실제 EventSystem 버튼 이벤트, 강화, 레벨별 목록 노출, 홀로그램 121칸·9청크 재사용, 격자의 편집 전용 표시와 정지 시 캐시 검증 통과.
- CLI 평가 컨텍스트의 Screen 크기가 Game View와 달라 미니맵 raycast 검사가 처음 실패했다. 실제 Game 프레임에서 다시 실행해 raycast, 드래그 이동, 접기, 레이아웃 캐시 및 기존 계층 유지 검사 통과를 확인했다. 테스트용 임시 GameObject는 검사 후 제거되며 씬에 저장하지 않는다.
- [Play 결과](ReferenceHUD/play-results.json), [EditMode 결과](ReferenceHUD/editmode-results.json).

| 해상도 | 직접 확인한 실제 캡처 |
|---|---|
| 1920×1080 | [일반](ReferenceHUD/normal-1920.png), [선택](ReferenceHUD/selection-1920.png), [수정](ReferenceHUD/edit-1920.png) |
| 1024×768 | [일반](ReferenceHUD/normal-1024.png), [선택](ReferenceHUD/selection-1024.png), [수정](ReferenceHUD/edit-1024.png), [접힘](ReferenceHUD/folded-1024.png) |
| 3440×1440 | [일반](ReferenceHUD/normal-3440.png) |

화면 크기는 실제 Game View 캡처 기준이다. 입력 검증은 프로그램으로 호출한 EventSystem/Pointer 이벤트 및 Game 프레임 raycast이며, 모든 상태의 수동 마우스 QA나 빌드 검증을 의미하지 않는다.

## 저작 도구와 범위

- `Tools/CreateHudVisualAssets.cs`: 원본 UI 도형 에셋 및 실제 프리팹 썸네일 생성. Retina에서는 실제 RenderTexture 전체 크기를 읽는다.
- `Tools/ApplyReferenceCanvasHud.cs`: 기존 씬에 에셋·카드·스크롤바·참조를 연결하고 저장. `ApplyCompactCanvasHud` 이후에 실행하는 후속 저작 단계다. 일반 실행 때는 저작 도구를 다시 실행할 필요가 없다.
- `Tools/VerifyReferenceCanvasHud.cs`: Play 시 참조·상태·스크롤 검사.
- `Tools/VerifyHudFrame.cs`: 실제 Game 프레임 raycast 검사와 이미 로드한 `VerifyCompactCanvasHud` 실행. 결과는 `/tmp/reference-hud-frame-result.txt`에 기록한다.

시안의 UI 구성과 시각적 구분을 현재 게임 데이터에 맞춰 옮겼다. 시안 속 고해상도 건물 일러스트와 3D 월드 아트 자체를 복제한 작업은 아니다. 카드에는 현재 프로젝트의 실제 단순 모델이 보인다. 새 해금 시스템, 건설 비용, 미니맵 줌·핑, 배속 기능은 추가하지 않았다.
