# 사용법 (How to Use)

자주 쓰는 작업을 레시피 형태로 정리합니다. 처음이라면 먼저 [GettingStart](GettingStart.md)를 보세요.
모든 비동기 API는 `UniTask`입니다.

> 네임스페이스 주의: `emiteat.NexUI.*` 내부 코드에서는 파사드를 `Core.NexUI.X`로 호출합니다.
> 외부 게임 코드(예: `MyGame`)에서는 `using emiteat.NexUI.Core;` 후 `NexUI.X`로 바로 씁니다.

## 화면 열기 / 닫기 / 뒤로

```csharp
await NexUI.OpenAsync("Inventory");
await NexUI.CloseAsync("Inventory");
await NexUI.ToggleAsync("Inventory");
await NexUI.BackAsync();          // 백 스택 pop, 없으면 최상위 모달 닫기
bool open = NexUI.IsOpen("Inventory");
```

- `openPolicy`로 동작을 제어: `Single`(중복 방지), `StackPush`(백 스택), `ReplaceLayer`(같은
  레이어 교체), `Queue`(토스트식 순차), `Additive`.

## 모달 + 포커스 트랩

`UIScreenDefinition`에서 `layer.layerType = Modal`, `focus.trapFocus = true`,
`focus.defaultFocusElementId = "confirmButton"`, `policy.blockInputBehind = true`로 설정합니다.
열릴 때 기본 요소에 포커스가 잡히고 뒤 입력이 차단됩니다.

## 토스트 (순차 표시)

`layer.layerType = Toast`, `openPolicy = Queue`로 두면 매니저가 한 번에 하나씩 큐로 표시합니다.

## 상태 바인딩

```csharp
var store = new UIStateStore();
store.Set("score.current", 0);

var surface = NexUI.Manager.GetSurface("HUD");
new UITextBinder(v => $"Score: {v}").Bind(surface.FindRequired("scoreLabel"), "score.current", store);

store.Set("score.current", 1200);   // 라벨 자동 갱신
```

바인더 종류: `UITextBinder`(텍스트), `UIValueBinder`(슬라이더/프로그레스), `UIVisibilityBinder`(표시),
`UIClassBinder`(클래스 토글), `UICommandBinder`(클릭→액션). Capability가 없으면 경고 로그를 남깁니다.

## 버튼 액션 연결

```csharp
UIActionResolver.Instance.Register("inventory.close", () => NexUI.Close("Inventory"));
new UICommandBinder(UIActionResolver.Instance)
    .Bind(surface.FindRequired("closeButton"), "inventory.close", store);
```

## 커맨드 파이프라인

```csharp
var dispatcher = new UICommandDispatcher();
dispatcher.UseMiddleware(new LoggingMiddleware());
dispatcher.RegisterHandler(new SetValueCommandHandler(store));   // State 소유 커맨드
await dispatcher.DispatchAsync(new SetValueCommand("score.current", 999, previousValue: 1200));
```

- 커맨드 기록/재생: `dispatcher.Log = new CommandLog();` 후 `new CommandReplay(dispatcher).ReplayAsync(log)`.
- Undo: `IUndoableCommand.CreateInverse()`로 역커맨드를 얻어 다시 디스패치.

## 모션

에셋으로: `UIScreenDefinition.motion`에 open/close `UIMotionPreset`을 할당하면 열고 닫을 때 자동 재생.
코드로:

```csharp
var timeline = MotionCompiler.Compile(popupPreset);   // 또는 MotionCompilerCache 사용
await motionPlayer.PlayAsync(handle, timeline, ct);
```

- 제스처: `new GestureMotionController(handle).Attach();` (hover/press/focus 반응)
- 프로덕션 이징: DOTween 설치 후 `manager.MotionPlayer = new DOTweenMotionPlayer();`

## 테마 전환 / 토큰 오버라이드

```csharp
NexUITheme.Registry.Register(darkTheme);
NexUITheme.Registry.Register(lightTheme);
NexUITheme.Use("dark");
NexUITheme.SetToken("radius.md", "12");           // 런타임 오버라이드
ThemeEvents.ThemeChanged += id => Debug.Log($"theme = {id}");
```

## Query (선택) — 로딩/에러 바운더리

```csharp
var query = new UIQuery<Data>(new QueryKey("profile"), ct => LoadProfileAsync(ct), cache);
new LoadingBoundary<Data>(query.State, surface.FindRequired("spinner"));
new ErrorBoundary<Data>(query.State, surface.FindRequired("errorPanel"));
await query.RunAsync();
```

## VContainer로 DI 등록 (선택)

```csharp
builder.RegisterNexUI(settings);   // UIManager, StateStore, Dispatcher, ThemeRegistry 등 등록
```

## MessagePipe로 이벤트 발행 (선택)

`NexUIMessagePublisher`를 살려두면 `UIOpenedMessage`/`UIClosedMessage`/`UICommandExecutedMessage`/
`MotionStarted·CompletedMessage`가 자동 발행됩니다.

## Input System 액션맵 전환 (선택)

```csharp
manager.RegisterInputSystem(inputActions, gameplayMap: "Gameplay", uiMap: "UI");
// 모달/입력차단 화면이 열리면 Gameplay 비활성, UI 활성 → 닫히면 복구
```

## 디버그 오버레이

```csharp
NexUIDebug.Configure(NexUI.Manager, store, UIActionResolver.Instance, commandLog, queryCache);
NexUIDebug.ToggleOverlay();   // 기본 F9
```

## 검증

```csharp
var ctx = new UIValidationContext(definitions, new HashSet<UIRenderBackend>{ UIRenderBackend.UGUI });
var report = new ProjectValidator().Validate(ctx);
Debug.Log(report.ToSummaryString());
```

## 자주 겪는 문제

- **바인딩이 안 먹힘:** 요소 id가 맞는지(uGUI는 `NxUGuiBindingTag.elementId`, UI Toolkit은 `name`),
  요소가 필요한 Capability를 제공하는지 확인 (경고 로그 확인).
- **모션이 안 보임:** 대상이 `IUITransformCapability`를 가지는지, 프리셋이 컴파일 후 트랙이 있는지 확인.
- **`FindRequired` 예외:** 해당 id 요소가 실제 UXML/프리팹에 없음 → id 수정 또는 `TryFind` 사용.
