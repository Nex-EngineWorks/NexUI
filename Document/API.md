# API 레퍼런스 (0.1.0)

모든 비동기 멤버는 `UniTask` / `UniTask<T>`를 반환합니다.

## 어셈블리

| 어셈블리 | 참조 | 목적 |
|---|---|---|
| `emiteat.NexUI.Abstractions` | UniTask | 인터페이스, Capability, 컴파일된 모션 데이터 |
| `emiteat.NexUI.Core` | Abstractions | 매니저, 화면, 레이어, 커맨드, 검증 |
| `emiteat.NexUI.State` | Abstractions | 상태 저장소, 시그널, 바인더 |
| `emiteat.NexUI.Motion` | Abstractions | 모션 오소링, 컴파일러, 플레이어 |
| `emiteat.NexUI.Theme` | Abstractions | 토큰, 테마, 어플라이어 |
| `emiteat.NexUI.Components` | Abstractions | 컴포넌트 계약 |
| `emiteat.NexUI.Query` | Abstractions, State | 선택적 데이터 쿼리 모듈 |
| `emiteat.NexUI.Debug` | Abstractions, Core, State, Motion, Theme, Query | 스냅샷 + 오버레이 |
| `emiteat.NexUI.Settings` | Abstractions, Core, State, Motion, Theme | 설정 에셋 + 부트스트랩 |
| `emiteat.NexUI.Integrations.*` | 런타임 모듈 (+ 외부 패키지) | 백엔드 및 선택 통합 |

## Abstractions

- `UIRenderBackend` — `UIToolkit`, `UGUI`.
- `IUIElementHandle` — `Id`, `Backend`, `Native`, `Has<T>()`, `As<T>()`.
- Capability: `IUITextCapability`, `IUIValueCapability`, `IUIVisibilityCapability`,
  `IUIInteractableCapability`, `IUIClickCapability`, `IUIStyleCapability`,
  `IUITransformCapability`, `IUIPointerCapability`, `IUIFocusCapability`.
- `IUISurface` — `TryFind(id)`(핸들 또는 null), `FindRequired(id)`(없으면
  `UIElementNotFoundException` 발생), `NativeRoot`, `SetActive/SetSortingOrder/SetInputBlocking/Destroy`.
- `IUIFocusAdapter`, `IUICommand`, `IUndoableCommand`, `IUICommandHandler<T>`,
  `IUICommandDispatcher`(`RegisterHandler<T>` 포함), `IUIMiddleware`, `IUIScreenLifecycle`,
  `IUIMotionPlayer`, `IUIMotionResolver`, `IUIResourceProvider`, `IUIThemeApplier`.
- 컴파일된 모션: `UIMotionProperty`, `UIMotionEasing`, `UIMotionKeyframe`, `UIMotionTrack`,
  `UIMotionTimeline`, 그리고 `UIMotionEvents` 버스.
- 컨텍스트: `UICommandContext`, `UIScreenContext`.

## Core

- `UIManager` — `OpenAsync/CloseAsync/ToggleAsync/BackAsync`, `IsOpen`, `GetSurface`,
  `Register…`, `MotionPlayer/MotionResolver`, `ScreenOpened/ScreenClosed` 이벤트, 디버그 조회 표면.
- `NexUI` — 공유 `UIManager`에 대한 정적 파사드 (트리 내부에서는 `Core.NexUI`).
- `UIScreenDefinition` + 설정 구조체들, `IUIScreenFactory`, `UIScreenInstance`, `UIScreenRegistry`.
- 레이어링: `UILayerType`, `UIOpenPolicy`, `IUILayerRoot`, `UILayerManager`.
- 내비게이션: `UIBackStack`, `UIModalStack`, `UIToastQueue`, `UIFocusManager`, `UIPolicyRunner`.
- 커맨드: `UICommandDispatcher`, 화면 커맨드, `LoggingMiddleware`, `ExceptionGuardMiddleware`;
  `Command/` — `CommandLog`, `CommandReplay`, `CommandLogEntry`.
- `IInputPolicy`.
- 레지스트리 에셋: `UIScreenRegistryAsset`, `UIIconRegistryAsset`, `UITemplateRegistryAsset`.
- 검증: `IUIValidator`, `UIValidationResult/Report/Context`, `ProjectValidator` + 8개 규칙.

## State

- `UIStateStore`(`Set/Get/TryGet/Watch/Keys`), `UISignal<T>`, `UIDerivedState<,>`,
  `UIActionResolver`.
- 바인더: `UIBinder` + `UITextBinder`, `UIValueBinder`, `UIVisibilityBinder`, `UIClassBinder`,
  `UICommandBinder`.
- `SetValueCommand`(undoable) + `SetValueCommandHandler`.

## Motion

- 오소링: `UIMotionPreset`, `UIMotionVariant`, `UIMotionStep`, `UIMotionGraph`,
  `UIMotionRegistryAsset`, `UIMotionProperties`.
- `MotionCompiler`, `MotionCompilerCache`, `MotionResolver`(`IUIMotionResolver`).
- 플레이어: `BuiltInMotionPlayer`(`IUIMotionPlayer`), + DOTween 통합.
- 헬퍼: `AnimatePresence`, `GestureMotion`, `GestureMotionController`, `LayoutMotion`,
  `SharedElementTransition`, `MotionConflictResolver`(`MotionConflictPolicy`),
  `MotionPlaybackHandle`, `MotionEvents`, `PlayMotionCommand`(+ 핸들러).

## Theme

- `ThemeToken`, `UITheme`, `ThemeRegistry`, `UIThemeRegistryAsset`, `RuntimeTokenOverride`,
  `ResponsiveRule`/`ResponsiveRuleSet`, `ThemeTransition`, `ThemeScope`, `ThemeVariant`,
  `ThemeEvents`, `NexUIThemeAPI`, `NexUITheme`(`Use/UseAsync/SetToken/GetToken`),
  `SetThemeCommand`(+ 핸들러).

## Components

- 계약: `INXButton`, `INXProgressBar`, `INXModal`, `INXToast`, `INXTooltip`, `INXPopover`,
  `INXList`, `INXGrid`, `INXSlot`, `INXChoiceList`, `INXSpinner`, `INXSkeleton`,
  `INXRadialFill`; `ComponentRegistry`.

## Query (선택)

- `QueryKey`, `QueryState<T>`/`QueryStatus`, `UIQuery<T>`, `QueryCache`, `RetryPolicy`,
  `Invalidation`; 바운더리 `LoadingBoundary<T>`, `ErrorBoundary<T>`, `EmptyBoundary<T>`,
  `FallbackScreen<T>`.

## Debug / Settings

- `NexUIDebug`(`Configure/Capture/Show/Hide/ToggleOverlay`), `NexUIDebugSnapshot`,
  `NexUIDebugService`, `NexUIDebugOverlay`, `NexUIDebugOptions`.
- `NexUISettings`, `NexUIBootstrapMode`, `NexUISettingsProvider`, `NexUIRuntimeSettings`,
  `UILayerRootConfig`.

## Integrations

- **UIToolkit / UGUI:** 요소 핸들, 표면, 화면 팩토리, 포커스 어댑터, 테마 어플라이어, 샘플 컴포넌트, 부트스트래퍼.
- **DOTween:** `DOTweenMotionPlayer`. **VContainer:** `RegisterNexUI()` / 인스톨러.
  **MessagePipe:** `NexUIMessagePublisher` + 메시지. **Addressables:**
  `AddressablesUIResourceProvider`. **InputSystem:** `InputSystemPolicy` + 맵 스위처.
