# NexUI 소개

## 무엇인가

NexUI는 UI 로직을 렌더링 백엔드로부터 분리하는 Unity용 **런타임 UI 프레임워크**입니다. 화면,
상태 바인딩, 커맨드, 모션, 테마를 한 번 오소링하면, 얇은 Integration 계층이 이를 **UI Toolkit**
또는 **uGUI**(혹은 화면별 혼합)로 실현합니다.

이 패키지는 런타임 기반입니다. 비주얼 오소링(Unity EditorWindow)은 별도 패키지
`com.nexengineworks.nexui.studio`가 제공합니다.

## 왜 필요한가

- **백엔드 자유 / 마이그레이션.** 게임 측 UI 로직을 다시 쓰지 않고 uGUI ↔ UI Toolkit 사이를 오갈 수 있습니다.
- **테스트 용이성.** 핵심 로직(내비게이션, 스택, 상태, 커맨드 파이프라인, 모션 컴파일)은 순수
  C#이라 백엔드 없이 단위 테스트가 가능합니다.
- **조합성.** 각 모듈은 단독으로 사용 가능하며, 누락된 선택 모듈은 우아하게 비활성화됩니다.

## 아키텍처 원칙

1. **허브 & 스포크 어셈블리.** `Abstractions`는 의존성이 없습니다. `Core`, `State`, `Motion`,
   `Theme`, `Components`는 *오직* `Abstractions`만 참조하고 서로를 참조하지 않습니다.
   `Integrations.*`가 전체를 조합합니다.
2. **구체 타입 대신 Capability.** 요소는 Capability 인터페이스(`IUITextCapability`,
   `IUIValueCapability`, `IUITransformCapability` 등)로 동작을 노출하며, `IUIElementHandle.As<T>()`로
   조회합니다. Core/State/Motion은 `Native`를 캐스팅하지 않습니다.
3. **커맨드 소유권 분산.** 각 도메인이 자기 커맨드와 핸들러를 소유합니다. 화면 커맨드는 Core,
   `SetValueCommand`는 State, `PlayMotionCommand`는 Motion, `SetThemeCommand`는 Theme.
   컴포지션 루트가 디스패처에 핸들러를 등록합니다.
4. **모션의 오소링 vs 런타임 분리.** 프리셋/베리언트/그래프는 오소링 데이터이고,
   `MotionCompiler`가 런타임용 `UIMotionTimeline`을 생성하면 플레이어가 이를 재생합니다.
5. **선택 통합은 진짜 선택.** DOTween/VContainer/MessagePipe/Addressables/InputSystem은
   `defineConstraints` + `versionDefines`로 게이팅되며, 절대 필수 패키지 의존성이 되지 않습니다.

## 의존성

- **UniTask** (`Cysharp.Threading.Tasks`) — 모든 비동기 API가 `UniTask` / `UniTask<T>`를 반환합니다.
- **uGUI 통합**은 추가로 `UnityEngine.UI` + TextMeshPro를 사용합니다.

## 버저닝

세미버 유사 방식. `1.0` 전까지 `0.x`에서는 마이너 버전 간에도 파괴적 API 변경이 있을 수 있습니다.

## 이 패키지의 비목표

- 비주얼 에디터 / EditorWindow 오소링 없음 (Designer 패키지 참조).
- UniTask 외 서드파티 런타임 라이브러리에 대한 필수 의존성 없음.
