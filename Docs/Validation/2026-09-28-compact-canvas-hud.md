# Canvas HUD 밀도 개선 적용 결과

2026-09-28. [실제 적용 계획](../Canvas_HUD_밀도개선_실제적용계획.md)에 따라 Unity 6000.3.14f1의 실제 에디터에서 두 씬을 수정하고 저장했다.

## 적용 내용

- StartRegionSandbox·OpenWorldSandbox의 기존 Canvas 계층을 수정했다. 시작 씬 59개 버튼/25개 카탈로그, OpenWorld 57개 버튼/23개 카탈로그와 persistent callback을 보존했다.
- CanvasScaler를 1920×1080, Match 0.5로 전환했다. 1080p 기준 상단 높이 60, 좌측 체력/시계 폭 232, 자원 폭 208, 지도 폭 272, 하단 펼침 204/접힘 44를 적용했다.
- TopBar·LeftStatus·RightStatus의 큰 배경과 Raycast Target을 제거했다. 기능별 배경은 저장된 자식 패널로 분리했다. 선택 상세를 숨기면 빈 배경도 사라진다.
- 자원 이름과 수량을 실제 TMP 두 열로 저장했다. 전체 자원 접근과 0 재고 표시를 유지했다. 조작 안내는 좌우 패널 사이 전용 영역으로 옮겼다.
- 카탈로그의 매 프레임 6열·높이60 덮어쓰기를 제거했다. 저장된 CanvasHudLayout 설정으로 화면 크기/접기 상태가 바뀔 때만 배치를 갱신한다. 런타임 UI 오브젝트 생성은 없다.
- 실제 1024×768 검증에서 긴 이름과 선택 제목 겹침을 발견해 추가 보정했다. 카탈로그 셀은 최소 표시 폭 152px·높이44px를 보장하며 이 크기에서는 6열, 나머지 검증 해상도에서는 8열이다. 선택 제목 높이60, 내용 시작72, 진행/저장 버튼 폭144를 적용했다.
- 기본 본문18·보조16·제목22를 적용하고 작은 화면에서는 실제 본문14px·보조12px 하한을 유지한다. 시계 자체는 일괄 축소하지 않았다.

주요 코드: `CanvasHudLayout.cs`, `CanvasWorldHud.cs`. `Tools/ApplyCompactCanvasHud.cs`는 기존 오브젝트/참조를 확인하고 수정하는 Editor 도구이며 런타임에 실행하지 않는다. 최초 YAML 마이그레이션 도구를 다시 실행하지 않았다.

## 실제 검증

- 최종 EditMode **89/89 통과**, 실패·건너뜀 0. 두 씬의 배율/레이아웃 바인딩/투명 부모/자원 수량 열 검사 포함. [결과](CompactHUD/editmode-results.json)
- 7개 씬 저장 참조 정적 검사 통과. [결과](CompactHUD/saved-references.json)
- 시작 씬: VerifyCanvasHud, VerifyBuildAreaHologram, VerifyWorldGridVisibility, VerifyStartLoop 통과. 후속 작은 화면 보정 뒤 VerifyCompactCanvasHud 재통과.
- 두 씬: 패널 접기 높이, 자원 재배치, 변화 없는 배치 캐시, 미니맵 레이캐스트/드래그, 버튼 연결 및 런타임 계층 개수 유지 검사 통과. OpenWorld 검사는 작은 화면 최종 치수 보정 전 실행했으며, 최종 보정 후에는 두 씬 저장 계층 검사를 재실행했다.
- EventSystem의 pointerClickHandler를 통한 수정/취소, ScrollRect 스크롤, persistent 강화 콜백 통과. 명령 문자열 실행과 구분해 실제 Button/ScrollRect 이벤트 경로를 검사했다.
- 임시 저장 경로에서 저장/중복 데이터 거부/편집·야간 저장 차단/백업 손상 복구/패배 저장 차단 검사 통과. 새 Canvas의 `load` 명령으로 실제 씬을 재로드하고 정확한 스냅샷 왕복을 확인했다.
- 최종 Play Console 오류 0개. [Play 검증 상세](CompactHUD/play-results.json)

## 화면 증거

일반 상태 5개 해상도를 실제 Game View로 캡처했다. 해상도 변경 직후에는 오래된 Canvas 기하가 남거나 재로드 후 첫 프레임에 HUD Start가 아직 실행되지 않는 경우가 있어, 에디터 프레임을 진행하고 초기화·크기 갱신이 끝난 최종 캡처만 아래에 보관했다. 카메라 전용 CLI screenshot 출력으로 HUD를 판정하지 않았다.

| 해상도 | 결과 |
|---|---|
| 1024×768 | [6열, 최소 글자/셀 크기](CompactHUD/normal-1024.png) |
| 1366×768 | [8열](CompactHUD/normal-1366.png) |
| 1920×1080 | [기준 배치](CompactHUD/normal-1920.png) |
| 2560×1440 | [QHD 배치](CompactHUD/normal-2560.png) |
| 3440×1440 | [울트라와이드 배치](CompactHUD/normal-3440.png) |

1024×768에서 [선택](CompactHUD/selection-1024.png), [접힘](CompactHUD/folded-1024.png), [수정](CompactHUD/edit-1024.png), [저장 메뉴](CompactHUD/menu-1024.png)를 추가 확인했다. 목록의 viewport 밖 항목은 의도적으로 마스킹되며 스크롤로 접근한다.

![1080p 적용 화면](CompactHUD/normal-1920.png)

## 범위와 남은 확인

모든 해상도×모든 상태의 전체 조합을 실행한 것은 아니다. 입력 검사는 이벤트와 핸들러를 프로그램으로 실행한 결과이며 물리 마우스/키보드로 모든 흐름을 조작한 수동 QA, 개발자 수량 입력 전체 회귀, 극단적으로 큰 자원 수량의 문자열 폭, 전체 프레임/GC 프로파일링은 별도다. 계획의 기능·데이터 정책은 유지했으며 새로운 아이콘 아트나 장식은 추가하지 않았다.

검증 Play는 종료했고 시작 씬을 편집 상태로 남겼다. 테스트용 저장 경로 환경변수와 Game View 해상도 선택을 복원했다. 사용자가 이미 보유한 씬 변경 및 이전 테스트 수정은 유지했다. Unity 저장/동적 폰트 직렬화 변경도 포함되며, 이번 적용 변경은 아직 커밋·업로드하지 않았다.

## 후속 수정 및 시안 적용 범위

[기지 레벨별 목록 표시 수정](2026-09-28-catalog-base-level.md)을 적용했다. 이 문서의 크기·밀도 개선 완료는 시안 전체의 시각 표현 완료를 뜻하지 않는다. 금속 테두리, 자원 아이콘, 건물 이미지 카드 표현은 아직 남아 있다.

후속: [시안 시각 요소 적용](2026-09-28-reference-hud-visuals.md)에서 당시 남았던 테두리·자원 아이콘·이미지 카드 및 상세 표현을 적용했다. 최신 시각 상태와 한 줄 카탈로그 치수는 후속 기록을 따른다.
