# Train A — Blender MCP 모델 v1

작성: 2026-10-02 KST

## 실행 및 보존
- Blender 5.2.1 LTS, Blender MCP addon 1.6, protocol 5. 초기 연결 실패 후 실행된 Blender에 MCP 연결 성공. 텔레메트리 off 확인.
- 모델링과 렌더는 모두 MCP execute_blender_code를 통해 실행. CLI 모델링 대체 없음.
- 원래 미저장 Scene(기본 Cube/Camera/Light)은 삭제·덮어쓰기하지 않음. 별도 TRAIN_A_V1 씬 제작 후 bpy.data.libraries.write로 해당 씬과 종속 데이터만 독립 .blend 저장. 기존 Blender 파일을 열어 교체하지 않음.
- 저장소 AGENTS.md 확인. 더 깊은 AGENTS 및 별도 기차 아트 지침은 검색 결과 없음. Document BELTFED game design의 최신 사용자 수정과 v3 프롬프트·실제 이미지 픽셀을 기준으로 제작.
- 다른 작업의 다수 변경이 이미 있었음. 본 작업은 이 model-v1 폴더만 생성. Unity Assets/씬/프리팹/철도 코드/공통 문서 수정 없음. 커밋·푸시 없음.

## 참조와 해석
참조: ../train-a-three-view-v3.png 및 ../train-a-three-view-v3.prompts.txt. 원본 변경 없음.

측면·상면의 가까운 길이방향 쌍연통, 보일러 3개 밴드, 4개 대형 구동축, 07 운전실, 긴 개방형 석탄 적재부, 소형 지지륜, 전면 쐐기를 관찰하여 반영했다. 청회색 철판·검은 기계부·황동 배관·황토색 번호판으로 구분한다.

이미지는 공학 도면이 아니며 세 투영 사이 축척과 형상 차이가 있다. 측면 및 상면의 길이 배치를 우선했다. 정면도의 램프는 보일러 하단 중앙인데 측면은 더 높게 표현되어 하단 중앙으로 통일했다. 정면에서 넓고 경사진 지붕은 v1에서 평판으로 단순화했다. 절대 치수 없음: -X 전방 / Z 위, 총 길이 약 12 단위의 임시 모델링 스케일이며 Unity 치수 확정 아님.

## 모델 구조
- 01 Body and frame: 보일러, 밴드, 문, 쐐기, 프레임
- 02 Fittings and detail: 황동 배관, 밸브, 램프, 루버, 리벳
- 03 Wheels and motion: 구동 4축, 스포크, 연결봉, 전방/운전실/석탄부 지지륜과 베어링
- 04 Cab and tender: 운전실, 창, 편집 가능한 07 텍스트, 사다리, 석탄벽/보강대
- 05 Coal: 개별 low-poly 석탄 및 하부 채움
- 90 Review cameras: SIDE / TOP / FRONT / PERSPECTIVE (모두 orthographic, 마지막은 사선 검토 시점)
- 605개 편집 부품, 기본 메시 32,752 vertices / 26,214 polygons. Bevel/Weighted Normal modifier 미적용 유지. 게임용 최적화 완료 수치 아님.

## 비교 검증
comparison.html에서 원본과 결과를 함께 볼 수 있다. side.png / top.png / front.png는 동일 모델의 정투영 렌더이고 perspective.png는 사선 정투영이다. Workbench material-color 렌더이므로 실제 PBR/발광 렌더 검증 아님.

실제 렌더 픽셀 확인: 측·상면에 연통 2개, 정면은 1개 가림; 구동축 4개; 긴 석탄부 및 지지륜; 양측 기계부. 첫 렌더에서 07 글자 매몰, 석탄 하부 빈틈, 지지륜 프레임 연결 부족, 사선 렌더 잘림을 발견해 수정 후 재렌더했다.

저장 검증: train-a-v1.blend를 Blender library reader로 읽어 TRAIN_A_V1 씬, 609개 오브젝트(부품 605 + 카메라 4) 확인. 상세 결과는 train-a-v1-validation.json. 기존 씬 보존 확인. 새 파일 재오픈 UI와 Unity 임포트/Play 검증은 수행하지 않음.

## 남은 차이 / 미완료
- 참조 대비 전면 상판 돌출, 평평한 지붕, 매끈한 보일러·석탄벽, 단순한 현가장치/배관/연결봉 형태.
- 마모·그을음·패널 이음/촘촘한 볼트, 지붕 경사, 핸들 및 추가 서비스 부품 미완료.
- 석탄은 의도적으로 각진 단순 메시. UV, 텍스처 베이크, LOD, 메시 병합, 콜라이더, 리깅/차륜 애니메이션, 게임용 축/스케일 조정 미완료.
- 실제 주행 기구학/간섭 또는 공학적 제작 가능성 검증 없음.
- Unity 기존 기차 자산 교체와 씬 연결은 별도 조율 대상. 이 결과를 게임 통합 완료로 취급하지 않음.

## 산출물
- train-a-v1.blend: 편집 원본
- side.png, top.png, front.png, perspective.png: 검토 렌더
- comparison.html: 원본/결과 비교
- train-a-v1-validation.json: Blender 내부/저장 검사
- checksums.json: 참조 및 산출물 SHA-256
