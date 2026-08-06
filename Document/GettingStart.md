# 시작하기

첫 화면 열기, 상태 바인딩, 모션 재생, 테마 전환 과정을 다룹니다. 모든 비동기 API는 `UniTask`이므로
`await`하거나 동기 래퍼로 fire-and-forget 하면 됩니다.

## 1. 씬에 백엔드 부트스트랩 추가

원하는 백엔드를 골라 해당 부트스트랩 컴포넌트를 씬에 배치합니다.

- **UI Toolkit:** `UIDocument`(+ `PanelSettings`)와 `UIToolkitIntegrationBootstrap`를 가진 GameObject.
- **uGUI:** `Canvas`(+ `GraphicRaycaster`, 씬에 `EventSystem`)와 `UGUIIntegrationBootstrap`.

부트스트랩은 화면 팩토리, 포커스 어댑터, 테마 어플라이어, 내장 모션 플레이어를 등록하고
`UILayerType`별 레이어 컨테이너를 생성합니다.

## 2. 화면 오소링

**UIScreenDefinition**을 만들고(`Create ▸ NexUI ▸ Screen Definition`) 다음을 설정합니다.

- `identity.screenId` — 열 때 사용할 문자열 (예: `"HUD"`).
- `backendAsset.backend` + `backendAsset.asset` — `VisualTreeAsset`(UI Toolkit) 또는 프리팹(uGUI).
- `layer` — `layerType` + `openPolicy` (예: HUD/Single, Modal/StackPush, Toast/Queue).
- `motion` — 선택적인 open/close `UIMotionPreset` 에셋.

> uGUI: 바인딩/애니메이션/포커스 대상 요소에 **`NxUGuiBindingTag`**를 붙이고 `elementId`를 설정하세요.
> UI Toolkit: 요소의 `name`을 id로 사용합니다.

## 3. 화면 등록 및 제어

```csharp
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Core;

NexUIApp.RegisterScreen(hudDefinition);
NexUIApp.RegisterScreen(pauseDefinition);

await NexUIApp.OpenAsync("HUD");
await NexUIApp.ToggleAsync("Inventory");
NexUIApp.Open("PauseMenu");   // fire-and-forget
await NexUIApp.BackAsync();    // 백 스택 pop / 최상위 모달 닫기
```

> `emiteat.NexUI.*` 네임스페이스 트리 내부에서는 파사드를 `Core.NexUIApp.X`로 호출하세요.
> 외부 게임 코드는 `using emiteat.NexUI.Core;` 후 `NexUIApp.X`를 바로 쓸 수 있습니다.

## 4. 상태와 바인딩

```csharp
using emiteat.NexUI.State;

var store = new UIStateStore();
store.Set("player.hp", 1f);
store.Set("player.name", "Hero");

var surface = NexUIApp.Manager.GetSurface("HUD");
var nameEl = surface.TryFind("nameLabel");     // 없으면 null
if (nameEl != null) new UITextBinder().Bind(nameEl, "player.name", store);
new UIValueBinder().Bind(surface.FindRequired("hpBar"), "player.hp", store);

store.Set("player.hp", 0.4f); // 바인딩된 UI가 자동 갱신됨
```

바인더는 `IUIElementHandle.As<T>()`로 Capability를 조회하며, Capability가 없으면 조용히 실패하지
않고 **경고 로그**를 남깁니다. `TryFind`는 핸들 또는 `null`을 반환하고, `FindRequired`는
`UIElementNotFoundException`을 던집니다.

## 5. 모션

```csharp
using emiteat.NexUI.Motion;

var timeline = MotionCompiler.Compile(fadePreset);
await motionPlayer.PlayAsync(handle, timeline, ct);
```

`BuiltInMotionPlayer`는 `IUITransformCapability`를 통해 Opacity/Position/Scale/Rotation을
애니메이션합니다. 프로덕션급 이징이 필요하면 DOTween을 설치하고 `DOTweenMotionPlayer`로 교체하세요.

## 6. 테마

```csharp
using emiteat.NexUI.Theme;

NexUITheme.Registry.Register(darkTheme);
NexUITheme.Use("dark");
NexUITheme.SetToken("color.primary", "#3B82F6");
```

## 7. 데이터 기반 부트스트랩 (선택)

**NexUISettings** 에셋을 만들고(`Create ▸ NexUI ▸ Settings`) 화면/테마/모션을 할당한 뒤
`bootstrapMode = RuntimeInitializeOnLoad`로 설정하고 `Resources` 폴더에 두면, NexUI가 시작 시
자동으로 전부 등록합니다.

## 8. 디버그 오버레이

```csharp
using emiteat.NexUI.Debugging;
NexUIDebug.ToggleOverlay();   // 화면/스택/상태/커맨드/테마를 보여주는 IMGUI 오버레이
```

전체 타입 목록은 [API](API.md)를 참고하세요.
