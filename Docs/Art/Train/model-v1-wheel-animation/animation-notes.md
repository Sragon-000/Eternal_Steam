# 바퀴 회전 애니메이션 v1

2026-10-02 KST · Blender 5.2.1 LTS / Blender MCP

사용자 요청: 현재 모델을 보존하고 바퀴 회전 애니메이션만 제작.

원본 model-v1/train-a-v1.blend와 열린 TRAIN_A_V1 씬을 보존했다. 별도 TRAIN_A_WHEEL_ROTATION_V1 씬을 복제하여 바퀴 회전 피벗 8개만 추가했다. 기존 메시, 머티리얼, 모디파이어는 변경하지 않았다. 바퀴 관련 216개 부품(타이어·스포크·허브·차축)을 축 중심의 Empty에 연결했다.

- 16개 바퀴 / 8개 차축, -Y축 회전으로 -X 전방 주행 방향.
- 24fps, 재생 구간 1–48, 프레임 1에서 0도, 49에서 -360도.
- Linear 키프레임 및 Cycles 반복. 타임라인에서 Space로 재생.
- 모든 바퀴가 2초당 1회전하는 회전 확인용 루프. 서로 다른 반지름에 따른 실제 주행 속도 동기화는 포함하지 않았다.
- 요청 범위에 맞춰 차체 이동·연결봉·크랭크 핀·피스톤은 고정. 기구학적 주행 애니메이션이 아니다.

검증: 프레임 1의 기존 형태 일치 오차 1.2e-7 이하. 7/13/25/37/49 프레임에서 비회전 부품 변환 변화 0. 바퀴 부품의 축 중심 거리 유지 확인. 49번 프레임 루프 일치 오차 2.4e-7 이하. 원본 씬 애니메이션 0개, 저장 파일 내 Action 8개 확인. 원본 .blend SHA-256 변경 없음. 세부 수치는 animation-validation.json.

산출물: train-a-v1-wheel-animation.blend, wheel-rotation-preview.mp4 (12fps 샘플 프리뷰), preview-frames/, animation-validation.json. Unity 자산 및 씬 변경 없음; Unity 임포트/Play 검증 없음.
