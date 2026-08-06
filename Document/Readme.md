# NexUI

**백엔드 독립적인 Unity 런타임 UI 프레임워크.**

NexUI는 UI가 *무엇을 하는지*(화면, 상태, 바인딩, 커맨드, 모션, 테마)와 *어떻게 렌더링되는지*
(UI Toolkit 또는 uGUI)를 분리합니다. 덕분에 동일한 게임 측 코드로 두 백엔드를 모두 구동할 수
있습니다. 비동기 API는 **UniTask**를 사용합니다.

- **패키지:** `com.nexengineworks.nexui`
- **버전:** 0.1.0
- **Unity:** 2022.3 LTS+
- **루트 네임스페이스:** `emiteat.NexUI`

## 문서

| 파일 | 목적 |
|------|------|
| [About](About.md) | NexUI 소개, 아키텍처 원칙, 설계 규칙 |
| [Installation](Installation.md) | 패키지 + UniTask + 선택 통합 설치 |
| [GettingStart](GettingStart.md) | 첫 화면: 부트스트랩, 열기, 바인딩, 모션, 테마 |
| [HowToUse](HowToUse.md) | 자주 쓰는 작업 레시피 모음 |
| [API](API.md) | 모듈별 어셈블리와 주요 public 타입 |

## 모듈 구성

```
Abstractions   인터페이스 + Capability + 컴파일된 모션 타임라인 (백엔드 타입 없음)
   ^
   ├── Core        UIManager, 화면, 레이어, 스택, 커맨드 파이프라인, 검증, 설정
   ├── State       상태 저장소, 시그널, Capability 기반 바인더
   ├── Motion      오소링 에셋, 컴파일러, 내장 플레이어, 제스처/레이아웃/공유요소
   ├── Theme       토큰, 레지스트리, 런타임 오버라이드, 반응형 규칙
   ├── Components   백엔드 무관 컴포넌트 계약
   ├── Debug       런타임 스냅샷 + IMGUI 오버레이
   └── Settings     NexUISettings 에셋 + 부트스트랩

Query        (선택) 데이터 쿼리 모듈 — Core 없이도 컴파일됨
Integrations  UIToolkit, UGUI, DOTween, VContainer, MessagePipe, Addressables, InputSystem
```

## 핵심 규칙 (골든 룰)

- **Core**는 `VisualElement`, `GameObject`, `RectTransform`, `Canvas`, UI Toolkit, uGUI를
  참조하지 않습니다. 오직 `IUISurface`, `IUIElementHandle`, Capability만 다룹니다.
- **Motion**은 `IUITransformCapability`로만 애니메이션합니다. UI 백엔드나 DOTween을 몰라야 합니다.
- **Query**는 Core를 참조하지 않습니다.
- 백엔드는 오직 `emiteat.NexUI.Integrations.*` 안에만 존재합니다.
- `IUIElementHandle.Native` / `IUISurface.NativeRoot`는 **`Integrations.*` 내부에서만** 캐스팅합니다.

## 함께 쓰는 패키지

비주얼 에디터는 별도 패키지에 있습니다: **`com.nexengineworks.nexui.studio`** (NexUI Studio).
