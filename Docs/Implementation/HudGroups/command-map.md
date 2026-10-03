# 현재 명령의 적용 위치 대응표

원본: 두 씬의 저장 계층에서 추출한 inventory.json. 기존 콜백을 유지하며 표시 위치를 단계적으로 변경한다.

| 씬 | 명령 | 적용 위치 |
|---|---|---|
| StartRegionSandbox | `open` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `build:station` | 건설 진입 |
| StartRegionSandbox | `build:track` | 건설 진입 |
| StartRegionSandbox | `build:coal` | 건설 진입 |
| StartRegionSandbox | `rotate` | 건설 진입 |
| StartRegionSandbox | `close` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `row:0` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `row:1` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `row:2` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `row:3` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `page-prev` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `page-next` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `new` | 노선 편집 |
| StartRegionSandbox | `connect` | 노선 편집 |
| StartRegionSandbox | `start` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `stop` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `fuel` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `next-stop` | 화물 |
| StartRegionSandbox | `edit` | 노선 편집 |
| StartRegionSandbox | `list` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `configure` | 화물 |
| StartRegionSandbox | `cancel-pending` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `defense` | 방어 상세 |
| StartRegionSandbox | `add` | 노선 편집 |
| StartRegionSandbox | `next-stop` | 화물 |
| StartRegionSandbox | `remove` | 노선 편집 |
| StartRegionSandbox | `up` | 노선 편집 |
| StartRegionSandbox | `arrival` | 노선 편집 |
| StartRegionSandbox | `departure` | 노선 편집 |
| StartRegionSandbox | `validate` | 노선 편집 |
| StartRegionSandbox | `cancel` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `weapon-next` | 방어 상세 |
| StartRegionSandbox | `weapon-install` | 방어 상세 |
| StartRegionSandbox | `weapon-charge` | 방어 상세 |
| StartRegionSandbox | `weapon-shape` | 방어 상세 |
| StartRegionSandbox | `weapon-upgrade` | 방어 상세 |
| StartRegionSandbox | `weapon-remove` | 방어 상세 |
| StartRegionSandbox | `weapon-back` | 방어 상세 |
| StartRegionSandbox | `commit` | 노선 편집 |
| StartRegionSandbox | `back-edit` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `cancel` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `keep` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `discard` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `draft-row:0` | 노선 편집 |
| StartRegionSandbox | `draft-row:1` | 노선 편집 |
| StartRegionSandbox | `draft-row:2` | 노선 편집 |
| StartRegionSandbox | `draft-row:3` | 노선 편집 |
| StartRegionSandbox | `draft-prev` | 노선 편집 |
| StartRegionSandbox | `draft-next` | 노선 편집 |
| StartRegionSandbox | `down` | 노선 편집 |
| StartRegionSandbox | `first` | 노선 편집 |
| StartRegionSandbox | `open-loop` | 노선 편집 |
| StartRegionSandbox | `close-loop` | 노선 편집 |
| StartRegionSandbox | `map-arrival` | 노선 편집 |
| StartRegionSandbox | `map-departure` | 노선 편집 |
| StartRegionSandbox | `map-cancel` | 노선 편집 |
| StartRegionSandbox | `issue:0` | 노선 편집 |
| StartRegionSandbox | `issue:1` | 노선 편집 |
| StartRegionSandbox | `issue:2` | 노선 편집 |
| StartRegionSandbox | `issue-prev` | 노선 편집 |
| StartRegionSandbox | `issue-next` | 노선 편집 |
| StartRegionSandbox | `station-row:0` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station-row:1` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station-row:2` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station-row:3` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station-prev` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station-next` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `station-context` | 철도 목록/운행·문맥 |
| StartRegionSandbox | `connection-from` | 노선 편집 |
| StartRegionSandbox | `connection-to` | 노선 편집 |
| StartRegionSandbox | `connection-from-port` | 노선 편집 |
| StartRegionSandbox | `connection-to-port` | 노선 편집 |
| StartRegionSandbox | `connection-preview` | 노선 편집 |
| StartRegionSandbox | `connection-confirm` | 노선 편집 |
| StartRegionSandbox | `connection-cancel` | 노선 편집 |
| StartRegionSandbox | `pause` | 공통 저장/진행 |
| StartRegionSandbox | `clock-fold` | 전투 |
| StartRegionSandbox | `resource-fold` | 건설/선택/자원 |
| StartRegionSandbox | `resources-all` | 건설/선택/자원 |
| StartRegionSandbox | `minimap-fold` | 전투 |
| StartRegionSandbox | `selection-close` | 건설/선택/자원 |
| StartRegionSandbox | `upgrade` | 건설/선택/자원 |
| StartRegionSandbox | `edit` | 건설/선택/자원 |
| StartRegionSandbox | `confirm` | 건설/선택/자원 |
| StartRegionSandbox | `cancel` | 건설/선택/자원 |
| StartRegionSandbox | `inventory-fold` | 건설/선택/자원 |
| StartRegionSandbox | `power-fold` | 건설/선택/자원 |
| StartRegionSandbox | `menu-fold` | 공통 저장/진행 |
| StartRegionSandbox | `category--1` | 건설/선택/자원 |
| StartRegionSandbox | `category-0` | 건설/선택/자원 |
| StartRegionSandbox | `category-1` | 건설/선택/자원 |
| StartRegionSandbox | `category-2` | 건설/선택/자원 |
| StartRegionSandbox | `category-3` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-0` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-1` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-2` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-3` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-4` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-5` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-6` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-7` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-8` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-9` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-10` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-11` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-12` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-13` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-14` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-15` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-16` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-17` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-18` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-19` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-20` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-21` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-22` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-23` | 건설/선택/자원 |
| StartRegionSandbox | `catalog-24` | 건설/선택/자원 |
| StartRegionSandbox | `menu-close` | 공통 저장/진행 |
| StartRegionSandbox | `save` | 공통 저장/진행 |
| StartRegionSandbox | `load` | 공통 저장/진행 |
| StartRegionSandbox | `new-game` | 공통 저장/진행 |
| StartRegionSandbox | `new-game-confirm` | 공통 저장/진행 |
| StartRegionSandbox | `new-game-cancel` | 공통 저장/진행 |
| StartRegionSandbox | `orb-craft` | 건설/선택/자원 |
| StartRegionSandbox | `developer-fold` | 공통 테스트 도구 |
| StartRegionSandbox | `power-close` | 건설/선택/자원 |
| StartRegionSandbox | `developer-close` | 공통 테스트 도구 |
| StartRegionSandbox | `spawn` | 공통 테스트 도구 |
| StartRegionSandbox | `spawn-air` | 공통 테스트 도구 |
| StartRegionSandbox | `reset` | 공통 테스트 도구 |
| StartRegionSandbox | `run` | 공통 테스트 도구 |
| StartRegionSandbox | `day` | 공통 테스트 도구 |
| StartRegionSandbox | `night` | 공통 테스트 도구 |
| StartRegionSandbox | `infinite-resources` | 공통 테스트 도구 |
| StartRegionSandbox | `quick-day` | 공통 테스트 도구 |
| StartRegionSandbox | `quick-night` | 공통 테스트 도구 |
| StartRegionSandbox | `station-railway` | 건설/선택/자원 |
| OpenWorldSandbox | `open` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `build:station` | 건설 진입 |
| OpenWorldSandbox | `build:track` | 건설 진입 |
| OpenWorldSandbox | `build:coal` | 건설 진입 |
| OpenWorldSandbox | `rotate` | 건설 진입 |
| OpenWorldSandbox | `close` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `row:0` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `row:1` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `row:2` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `row:3` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `page-prev` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `page-next` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `new` | 노선 편집 |
| OpenWorldSandbox | `connect` | 노선 편집 |
| OpenWorldSandbox | `start` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `stop` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `fuel` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `next-stop` | 화물 |
| OpenWorldSandbox | `edit` | 노선 편집 |
| OpenWorldSandbox | `list` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `configure` | 화물 |
| OpenWorldSandbox | `cancel-pending` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `defense` | 방어 상세 |
| OpenWorldSandbox | `add` | 노선 편집 |
| OpenWorldSandbox | `next-stop` | 화물 |
| OpenWorldSandbox | `remove` | 노선 편집 |
| OpenWorldSandbox | `up` | 노선 편집 |
| OpenWorldSandbox | `arrival` | 노선 편집 |
| OpenWorldSandbox | `departure` | 노선 편집 |
| OpenWorldSandbox | `validate` | 노선 편집 |
| OpenWorldSandbox | `cancel` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `weapon-next` | 방어 상세 |
| OpenWorldSandbox | `weapon-install` | 방어 상세 |
| OpenWorldSandbox | `weapon-charge` | 방어 상세 |
| OpenWorldSandbox | `weapon-shape` | 방어 상세 |
| OpenWorldSandbox | `weapon-upgrade` | 방어 상세 |
| OpenWorldSandbox | `weapon-remove` | 방어 상세 |
| OpenWorldSandbox | `weapon-back` | 방어 상세 |
| OpenWorldSandbox | `commit` | 노선 편집 |
| OpenWorldSandbox | `back-edit` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `cancel` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `keep` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `discard` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `draft-row:0` | 노선 편집 |
| OpenWorldSandbox | `draft-row:1` | 노선 편집 |
| OpenWorldSandbox | `draft-row:2` | 노선 편집 |
| OpenWorldSandbox | `draft-row:3` | 노선 편집 |
| OpenWorldSandbox | `draft-prev` | 노선 편집 |
| OpenWorldSandbox | `draft-next` | 노선 편집 |
| OpenWorldSandbox | `down` | 노선 편집 |
| OpenWorldSandbox | `first` | 노선 편집 |
| OpenWorldSandbox | `open-loop` | 노선 편집 |
| OpenWorldSandbox | `close-loop` | 노선 편집 |
| OpenWorldSandbox | `map-arrival` | 노선 편집 |
| OpenWorldSandbox | `map-departure` | 노선 편집 |
| OpenWorldSandbox | `map-cancel` | 노선 편집 |
| OpenWorldSandbox | `issue:0` | 노선 편집 |
| OpenWorldSandbox | `issue:1` | 노선 편집 |
| OpenWorldSandbox | `issue:2` | 노선 편집 |
| OpenWorldSandbox | `issue-prev` | 노선 편집 |
| OpenWorldSandbox | `issue-next` | 노선 편집 |
| OpenWorldSandbox | `station-row:0` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station-row:1` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station-row:2` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station-row:3` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station-prev` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station-next` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `station-context` | 철도 목록/운행·문맥 |
| OpenWorldSandbox | `connection-from` | 노선 편집 |
| OpenWorldSandbox | `connection-to` | 노선 편집 |
| OpenWorldSandbox | `connection-from-port` | 노선 편집 |
| OpenWorldSandbox | `connection-to-port` | 노선 편집 |
| OpenWorldSandbox | `connection-preview` | 노선 편집 |
| OpenWorldSandbox | `connection-confirm` | 노선 편집 |
| OpenWorldSandbox | `connection-cancel` | 노선 편집 |
| OpenWorldSandbox | `pause` | 공통 저장/진행 |
| OpenWorldSandbox | `clock-fold` | 전투 |
| OpenWorldSandbox | `resource-fold` | 건설/선택/자원 |
| OpenWorldSandbox | `resources-all` | 건설/선택/자원 |
| OpenWorldSandbox | `minimap-fold` | 전투 |
| OpenWorldSandbox | `selection-close` | 건설/선택/자원 |
| OpenWorldSandbox | `upgrade` | 건설/선택/자원 |
| OpenWorldSandbox | `edit` | 건설/선택/자원 |
| OpenWorldSandbox | `confirm` | 건설/선택/자원 |
| OpenWorldSandbox | `cancel` | 건설/선택/자원 |
| OpenWorldSandbox | `inventory-fold` | 건설/선택/자원 |
| OpenWorldSandbox | `power-fold` | 건설/선택/자원 |
| OpenWorldSandbox | `menu-fold` | 공통 저장/진행 |
| OpenWorldSandbox | `category--1` | 건설/선택/자원 |
| OpenWorldSandbox | `category-0` | 건설/선택/자원 |
| OpenWorldSandbox | `category-1` | 건설/선택/자원 |
| OpenWorldSandbox | `category-2` | 건설/선택/자원 |
| OpenWorldSandbox | `category-3` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-0` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-1` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-2` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-3` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-4` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-5` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-6` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-7` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-8` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-9` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-10` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-11` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-12` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-13` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-14` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-15` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-16` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-17` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-18` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-19` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-20` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-21` | 건설/선택/자원 |
| OpenWorldSandbox | `catalog-22` | 건설/선택/자원 |
| OpenWorldSandbox | `menu-close` | 공통 저장/진행 |
| OpenWorldSandbox | `save` | 공통 저장/진행 |
| OpenWorldSandbox | `load` | 공통 저장/진행 |
| OpenWorldSandbox | `new-game` | 공통 저장/진행 |
| OpenWorldSandbox | `new-game-confirm` | 공통 저장/진행 |
| OpenWorldSandbox | `new-game-cancel` | 공통 저장/진행 |
| OpenWorldSandbox | `orb-craft` | 건설/선택/자원 |
| OpenWorldSandbox | `developer-fold` | 공통 테스트 도구 |
| OpenWorldSandbox | `power-close` | 건설/선택/자원 |
| OpenWorldSandbox | `developer-close` | 공통 테스트 도구 |
| OpenWorldSandbox | `spawn` | 공통 테스트 도구 |
| OpenWorldSandbox | `spawn-air` | 공통 테스트 도구 |
| OpenWorldSandbox | `reset` | 공통 테스트 도구 |
| OpenWorldSandbox | `run` | 공통 테스트 도구 |
| OpenWorldSandbox | `day` | 공통 테스트 도구 |
| OpenWorldSandbox | `night` | 공통 테스트 도구 |
| OpenWorldSandbox | `infinite-resources` | 공통 테스트 도구 |
| OpenWorldSandbox | `quick-day` | 공통 테스트 도구 |
| OpenWorldSandbox | `quick-night` | 공통 테스트 도구 |
| OpenWorldSandbox | `station-railway` | 건설/선택/자원 |
