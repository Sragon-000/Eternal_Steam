# 완성 편성·통합 HUD·해상도 앵커 적용 및 검증

작성: 2026-10-02. 대상: Unity 6000.3.14f1, StartRegionSandbox / OpenWorldSandbox. 이 문서는 제품 구현과 회귀 검증 기록이다. **연구 실험은 시작하지 않았다.**

## 적용 결과

완성 기관차·화물칸·토대칸과 바퀴/로드 연동 애니메이션을 기존 Train 프리팹에 적용했다. 원본 Blender 파일은 유지하고 복제한 내보내기 씬에서 정적 메시를 병합했다. `Assets/EternalSteam/Art/Train/TrainConsist.fbx`와 URP/Lit 머티리얼을 저장했고 기존 프리팹 GUID와 전투 컴포넌트를 유지했다. 이동 거리/방향에 따라 2초 반복 클립을 샘플링하며 정차 시 위상을 유지한다. 포탑과 전투 발사 기준점은 토대칸 소켓에 맞춘다.

기존 전용 RailwayPanel / RailwayButton / RailwayIcon 스프라이트를 전체 제품 HUD에 확장 적용했다. 기존 자원·건물 아이콘은 유지했다. 건설과 철도 관리 탭은 같은 하단 도크에서 전환하고, 역 선택 정보에서 해당 철도로 이동한다. 기차역·선로·석탄 생성기를 건설 카드에 통합했다. 철도 초안이 남으면 건설 전환을 막아 유실을 방지한다. 정적 계층·스프라이트 참조·클릭 콜백은 두 씬에 저장되며 런타임은 표시 상태와 데이터만 갱신한다. 개발/테스트 조작 UI는 기본 비활성이다. 노출되는 검증비·검증용 문구도 정리했다.

## 해상도 대응

- 체력/시계/자원은 좌측 상단, 시간/전력/진행은 상단, 미니맵/선택 정보는 우측 상단, 건설 메뉴는 하단 가로 확장 앵커로 저장했다.
- 남은 세로 공간에서 미니맵과 선택 정보 높이를 함께 계산하고 하단 건설 메뉴를 침범하지 않게 한다. 미니맵은 정사각형을 유지한다.
- 철도 작업 공간은 좌측 정보 열과 우측 선택 열 사이 폭, 하단 메뉴와 목표 영역을 제외한 높이에 맞춘다. 긴 정차역 편집 패널도 같은 계산에 포함한다.
- 건설 메뉴의 카테고리·카드·스크롤바 높이를 모두 반영하고 저장/전력 팝업도 남은 공간에 맞춰 조정한다. 작은 창에서 글자만 과도하게 커져 버튼 밖으로 넘치지 않게 상한을 둔다.
- 800×600, 1024×768, 1280×720, 1920×1080, 2560×1080, 3840×1080, 3840×2160의 실제 GameView 렌더에서 제품 UI 경계·상호작용을 검증했다. 이보다 작은 창, 모바일 노치/safe area, 다른 OS 글꼴 배율은 이번 검증 범위가 아니다. 작은 해상도는 UI가 축소되므로 동일한 가독성을 보장한다는 의미도 아니다.

## 검증 결과와 증거

증거 루트: [Measurements/2026-10-02-train-hud-integration](../Measurements/2026-10-02-train-hud-integration/).

| 검증 | 결과 | 기록 |
|---|---|---|
| 전체 EditMode 회귀 | **266/266 통과**, 실패·생략 0, 20.890초 | [product-tests.xml](../Measurements/2026-10-02-train-hud-integration/product-tests.xml) |
| 저장된 두 씬 | 콜백·참조·카드·스크롤 계층·개발 UI 숨김·편성 참조 통과 | saved-references.json, saved-final-command.json |
| 앵커와 좁은 화면 | 6개 회귀 사례 + 실제 7개 해상도 통과 | responsive-anchors.json, responsive-matrix-status.txt, play-ui-WIDTHxHEIGHT.json |
| 실제 UI 입력 | GraphicRaycaster/PointerClick로 탭·건설 카드·역 선택·초안 보호·상세/편집 패널·팝업 검증 | play-ui-WIDTHxHEIGHT.json |
| 철도 기능 | 유한 재고로 신규/재사용 선로 견적·비용·실패 원자성·미배정 연결 저장/재로드·노선 배정·급탄·운행·귀환·재로드 | play-measurements.json 및 snapshot 파일 |
| 편성 애니메이션 | 2초 루프 오차 0, 이동 시 변화, 정지 위상 유지, 역방향 변화, 연결 소켓 간격 최대 약 4.77e−7 | train-validation.json |
| 런타임 편성 연동 | 실제 노선 이동으로 애니메이션 위상 변화, 토대칸 포탑과 전투 기준점 일치 | play-ui-WIDTHxHEIGHT.json |
| 최종 문구 정리 | 카드의 검증용 문구 제거 및 선택 텍스트 정리 집중 확인, 컴파일 오류 0 | editor-final-state.json, final-recompile.json |
| 연구 문서 요청 항목 | 7개 항목 확인 | research-document-check.json |

스크린샷은 `responsive-railway-WIDTHxHEIGHT.png`, `responsive-construction-WIDTHxHEIGHT.png`로 모두 보관했다. [1920×1080 철도 화면](../Measurements/2026-10-02-train-hud-integration/responsive-railway-1920x1080.png), [3840×1080 화면](../Measurements/2026-10-02-train-hud-integration/responsive-railway-3840x1080.png), [편성 프리뷰](../Measurements/2026-10-02-train-hud-integration/train-unity-preview.png). 스크린샷/전체 회귀 이후의 마지막 변경은 석탄 카드와 선택 정보의 검증용 표시 문구 제거이며, 레이아웃 변경 없이 재컴파일·저장 참조·문구 집중 확인을 수행했다.

실제 해상도 행렬은 StartRegionSandbox에서 수행했다. OpenWorldSandbox는 저장 계층 검사와 EditMode 울트라와이드 경계 검증을 수행했다. 자동 입력은 Unity EventSystem을 이용한 제품 UI 검증이며 OS 마우스 수동 인수나 빌드된 Player 전수 인수는 아니다. Pipeline 요약의 일부 테스트 콜백 누락 때문에 최종 판정은 전체 NUnit XML을 기준으로 했다.

초기 계층 테스트 실패는 작업 공간 전환 후 콜백 계약과 PreviewScene의 Canvas 좌표 정규화를 반영해 수정했다. Play 검증 도구는 새로 표시한 Graphic의 raycast depth 갱신을 기다리도록 프레임 경계를 넣고 TMP 자동 하위 메시를 동적 UI 생성 판정에서 제외했다. 실패 원본과 수정 후 결과를 모두 증거 폴더에 보존했다.

## 연구 문서 보완

[표적탐색 최적화 연구 확장 검토](../Presentations/표적탐색_최적화_연구_확장_검토.md)에 재현 가능한 성능 손익분기 구간/한계 규명이라는 논문 목표 후보를 명시하고 확장안 선택을 미정으로 남겼다. 표적 유지 비율·검증 비용·재탐색 원인, 빈 탐색과 무호출 구분 및 분모, 공유 격자의 회피 불가능한 유지비와 이중 인덱스 비용을 추가했다. 정확성 실패·측정 불안정·희소 질의·다른 병목·고정 방식 우세·사전 기준 미달·자원 상한에 따른 예비실험 중단/전환 판단을 보완했다. 수치 기준은 사전 확정 전 미정이다. 기존 논문 출처를 새로 검증하거나 실험 결과를 추가한 작업이 아니다.

## 보존과 재현

작업 전 파일 사본은 증거 폴더의 `baseline/`, 해시는 `baseline.json`, 이번 범위의 변화/최종 해시는 `final-artifact-manifest.json`에 남긴다. 기존 무관한 변경을 되돌리거나 커밋하지 않았다.

저장 검증은 `ETERNAL_STEAM_SAVE_TEST_ROOT`로 프로젝트 안의 `play-save/`를 사용했다. 유한 재고 fixture는 코드로 준비했으며 별도 테스트 UI를 노출하지 않았다. 정상 사용자 저장 슬롯은 건드리지 않았다. 종료 시 Play를 중단하고 저장 경로 오버라이드를 해제했으며 GameView를 원래 3840×2160으로 복원했다.

재실행 도구는 프로젝트 `Tools/`에 보관했다: ExportTrainConsist.py, AuthorTrainConsist.cs, AuthorIntegratedHud.cs, PolishIntegratedHud.cs, IntegrateRailwayCatalog.cs, AuthorResponsiveHudAnchors.cs, VerifyTrainConsist.cs, VerifyIntegratedTrainPlay.cs, VerifyIntegratedPresentation.cs, VerifyResponsiveMatrix.cs, ExportProductTestResults.cs, FinishTrainHudIntegration.cs. 작성 도구는 깨끗한 편집 모드에서, Play 도구는 프로젝트 안의 격리된 저장 fixture를 준비한 뒤 실행한다. 연구 벤치마크 도구는 실행하지 않는다.

## 남은 범위와 한계

편성의 세 차량은 기존 열차의 단일 자세를 따른다. 곡선에서 차량별 관절/물리 연결 추종을 새로 구현하지 않았다. 바퀴 지름 차이에 대한 개별 회전율 보정도 이번 범위가 아니다. 내보낸 모델은 27개 MeshRenderer, 203,712 삼각형이며 LOD나 배포 빌드 성능 인수는 수행하지 않았다. 고급 철도 경유점, 모든 구형 미배정 연결 이행, 전체 경제 회차 및 RX 전체 완료를 주장하지 않는다.
