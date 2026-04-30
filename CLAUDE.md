# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

**비대칭 협력 멀티플레이어 VR 방탈출 게임** — Meta Quest 3 전용 캡스톤 프로젝트.

- Player A와 Player B는 각자 분리된 VR 환경에 위치
- 각 플레이어는 자기 환경의 오브젝트는 직접 조작 가능, 상대방 환경 오브젝트는 직접 조작 불가
- 퍼즐은 두 플레이어가 서로 소통하고 협력해야만 풀 수 있도록 설계 (비대칭 협력)
- 각 스테이지마다 통신 채널이 변화하며 퍼즐 복잡도 상승 (유선전화 → 음성전용 → 시각전용 → 워키토키)
- **Product name:** Capstone
- **Target platform:** Android (ARM64), Meta Quest 3
- **Render pipeline:** URP (Universal Render Pipeline), Mobile/PC 렌더러 분리 구성
- **Meta XR SDK:** 85.0.0, Horizon OS SDK target 85 / min 60

> **Note:** 초기 구상은 MR(Passthrough + Scene API로 실제 방 스캔/공유) 기반이었으나, 안정성 문제로 2026-04-28 순수 VR로 전환. 두 플레이어의 비대칭 환경을 어떤 방식(프리셋 / 절차적 생성 등)으로 구현할지는 미결정.

## 핵심 패키지

- `com.meta.xr.sdk.core` 85.0.0 — Meta XR Core SDK (OVR 카메라 리그, 핸드트래킹 등). VR만 사용 — Passthrough/Scene Understanding 기능은 사용 안 함.
- `com.meta.xr.sdk.interaction.ovr` 85.0.0 — Meta Interaction SDK (손 추적, 컨트롤러, Ray Interactor)
- `com.unity.xr.openxr` 1.16.1 — OpenXR 백엔드
- `com.unity.inputsystem` 1.19.0 — New Input System
- `com.unity.ai.navigation` 2.0.11 — AI NavMesh
- `com.unity.render-pipelines.universal` 17.3.0 — URP
- `Meta.XR.MultiplayerBlocks.Fusion` — **Photon Fusion 2** 기반 Building Blocks (채택)
- **Photon Voice 2** — 비대칭 통신 채널(음성 스테이지) 구현에 사용

## 프로젝트 구조

```
Assets/
  Scripts/          # 커스텀 C# 스크립트 (UI/RoomMatchingUI.cs, UI/VRKeyboard.cs)
  Scenes/           # Unity 씬 (SampleScene.unity)
  Oculus/           # OculusProjectConfig.asset — Meta 프로젝트 설정
  Plugins/Android/  # AndroidManifest.xml — Oculus VR 인텐트 필터, 디바이스 지원 플래그
  Resources/        # 런타임 로드 에셋 (InputActions.asset, OculusRuntimeSettings.asset)
  Settings/         # URP 렌더러/파이프라인 에셋 (Mobile_RPAsset, PC_RPAsset)
  XR/               # XR 관리 설정 및 로더 구성
  StreamingAssets/  # 런타임 스트리밍 에셋
  Photon/           # Photon Fusion 2 SDK
```

## 개발 워크플로우

빌드/테스트/배포는 모두 **Unity Editor**에서 수행 (별도 CLI 빌드 스크립트 없음).

- **프로젝트 열기:** Unity Hub에서 루트 폴더 열기
- **Quest 빌드:** File > Build Settings > Android > Build or Build and Run
- **XR 설정:** Edit > Project Settings > XR Plug-in Management (OpenXR + Oculus 로더)
- **Meta XR 기능 설정:** `Assets/Oculus/OculusProjectConfig.asset`에서 관리 — 코드에서 직접 변경하지 말 것
- **Android Manifest:** `Assets/Plugins/Android/AndroidManifest.xml` — `com.oculus.intent.category.VR` 인텐트와 `focusaware` 메타데이터 제거 금지

## 스크립팅 규칙

- 스크립트는 `Assets/Scripts/`에 작성
- **New Input System** 사용 — `Input.GetKey` 등 레거시 API 사용 금지
- **URP** 사용 중 — Built-in 렌더 파이프라인 셰이더 사용 금지
- Meta Interaction SDK의 Hand/Controller 프리팹과 Ray Interactor를 우선 활용, 커스텀 입력 처리 최소화
- **퍼즐/스테이지는 모듈화**하여 설계 — 추가 콘텐츠 삽입이 용이한 구조 유지

## VR / Meta SDK 핵심 사항

- **Hand Tracking:** 활성화됨 (`handTrackingSupport: 1`)
- **Passthrough / Scene Understanding:** 사용 안 함 — `OculusProjectConfig.asset`에서 비활성화 유지 (`insightPassthroughEnabled: 0`, `_insightPassthroughSupport: 0`, `sceneSupport: 0`)
- MR 관련 기능을 다시 활성화하지 말 것 (안정성 문제로 VR 전환 결정)

## 게임 아키텍처 방향 (설계 기준)

```
[Player A Device]                          [Player B Device]
  VR 환경 A                                  VR 환경 B
  (자기 방 오브젝트 직접 조작 가능)           (자기 방 오브젝트 직접 조작 가능)
        ↓                                         ↑
        └──────── Photon Fusion 2 네트워크 동기화 ─┘

퍼즐 오브젝트 상태, 플레이어 위치/애니메이션 → NetworkObject / RPC 동기화
통신 채널 (Photon Voice 2) → 스테이지별 제약 적용
```

- 두 플레이어의 VR 환경 구성 방식(프리셋 씬 / 절차적 생성 등)은 미결정
- 퍼즐 오브젝트 상태(위치, 활성화 여부 등)는 NetworkObject로 동기화
- 플레이어가 상대방 환경 오브젝트와 간접적으로 영향을 주는 인터랙션은 **네트워크 RPC**로 처리
- 각 환경에는 서로 다른 퍼즐 조각/정보가 배치되어 반드시 협동이 필요한 구조로 설계

## 기능적 요구사항 (구현 목표)

### VR 환경 및 네트워킹

| 기능 | 상세 설명 | 목표 지표 |
|------|-----------|-----------|
| VR 환경 구성 | 각 플레이어에게 비대칭으로 다른 VR 환경 제공 (프리셋/절차적 방식 미정) | — |
| 퍼즐 오브젝트 배치 | 환경 내 퍼즐 오브젝트 배치 — 비대칭 협력 구조 보장 | 배치 성공률 ≥ 90% |
| 실시간 상태 동기화 | 플레이어 위치, 애니메이션, 퍼즐 상태를 Photon Fusion 2로 동기화 | 동기화 지연 < 100ms |
| 매칭 및 세션 관리 | 2인 매칭, 방 생성/참가, 세션 상태(대기/진행/완료) 관리 | 매칭 대기 < 10초 |

### 비대칭 통신 채널 (스테이지별 진화)

| 스테이지 | 채널 | 제약 |
|----------|------|------|
| 1 | 유선 전화 | 텍스트 타이핑 소통, 실시간성 제한 |
| 2 | 음성 전용 | Photon Voice 2, 시각 정보 차단 |
| 3 | 시각 전용 | 스케치/이모지 공유만 가능, 언어 소통 차단 |
| 4 | 워키토키 | Push-to-Talk 방식, 단방향 음성 제한 |

### 퍼즐 시스템

- **비대칭 퍼즐 배치:** 각 방에 서로 다른 퍼즐 조각/정보 배치 → 반드시 협동 필요
- **물리 기반 상호작용:** XR Interaction Toolkit으로 VR 환경 내 물체 조작 및 퍼즐 풀기
- **힌트 시스템:** 스테이지별 최대 3회 힌트 제공, 힌트 사용 시 클리어 시간에 페널티
- **타이머 및 진행 상태:** 실시간 카운트다운, 퍼즐 진행률 UI, 스테이지 완료 판정

## 비기능적 요구사항 (성능 기준)

- **프레임 레이트:** Quest 3 기준 72fps 이상 안정적 유지
- **네트워크 지연:** RTT ≤ 100ms, 동기화 손실 < 0.1%
- **VR Comfort:** IPD 조정 지원, 로코모션 옵션(텔레포트 / 스무스 이동) 제공
- **튜토리얼:** 첫 플레이 온보딩 ≤ 5분, 조작 숙련도 확보
- **확장성:** 퍼즐/스테이지 모듈화 → 추가 콘텐츠 삽입이 용이한 구조

## UX 화면 흐름

```
앱 실행(Splash) → 메인 메뉴 → 매칭 로비 → 스테이지 시작 → 게임 플레이 → 결과 화면
```

- **메인 메뉴:** 서버 생성 / 서버 찾기
- **게임 플레이 HUD:** 타이머, 퍼즐 진행률 표시
- **결과 화면:** 클리어 시간 표시, 게임 종료
