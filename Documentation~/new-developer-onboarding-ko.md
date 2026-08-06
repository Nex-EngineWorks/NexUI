# NexUI 신입 개발자 온보딩 가이드

이 문서는 C#과 Unity의 기본 사용법은 알지만 NexUI 코드베이스를 처음 접한 개발자를 위한
소스 중심 안내서다. 문서의 목적은 API 목록을 외우게 하는 것이 아니라 다음 질문에 답할 수 있게
하는 것이다.

- 플레이를 누르면 무엇이 가장 먼저 실행되는가?
- `OpenAsync` 한 번이 어느 객체들을 거쳐 실제 UI를 만드는가?
- Runtime 패키지와 Studio 패키지는 어디에서 만나고 어디에서 분리되는가?
- 화면, 상태, 명령, 모션, 테마의 소유자는 누구인가?
- 기능을 추가하거나 버그를 고칠 때 어느 어셈블리를 수정해야 하는가?
- 현재 구현에서 이름만 보고 추측하면 틀리기 쉬운 부분은 무엇인가?

이 문서는 현재 소스 코드만을 기준으로 작성되었다. 기준 버전은 Unity `6000.4.2f1`, Runtime
패키지 `com.nexengineworks.nexui` `0.1.0`이다.

## 0. 첫날에 먼저 기억할 다섯 가지

1. 이 저장소에는 Runtime과 Studio라는 두 로컬 패키지가 있다.
2. 일반 화면 실행 경로와 Studio 컴파일 화면 실행 경로는 현재 서로 다른 경로다.
3. `UIManager`는 백엔드 타입을 직접 다루지 않고 `IUISurface`와 Capability만 다룬다.
4. 전역 편의 진입점 이름은 `NexUIApp`이며, 실제 상태는 내부 `UIManager`가 가진다.
5. 문자열 ID는 화면, 요소, 상태, 명령을 연결하는 계약이므로 변경 영향이 크다.

저장소를 처음 읽을 때 모든 폴더를 순서대로 읽지 않는다. 다음 파일부터 시작한다.

1. [`NexUI.cs`](../Runtime/Core/NexUI.cs)
2. [`UIManager.cs`](../Runtime/Core/UIManager.cs)
3. [`UIScreenDefinition.cs`](../Runtime/Core/UIScreenDefinition.cs)
4. [`UGUIIntegrationBootstrap.cs`](../Integrations/UGUI/UGUIIntegrationBootstrap.cs) 또는
   [`UIToolkitIntegrationBootstrap.cs`](../Integrations/UIToolkit/UIToolkitIntegrationBootstrap.cs)
5. [`UIStateStore.cs`](../Runtime/State/UIStateStore.cs)
6. [`NexScreenCompiler.cs`](../../com.nexengineworks.nexui.studio/Editor/Compiler/NexScreenCompiler.cs)
7. [`NexUGuiScreenBuilder.cs`](../Integrations/UGUI/Compiled/NexUGuiScreenBuilder.cs)

## 1. 저장소 구성

프로젝트 루트의 핵심 구성은 다음과 같다.

```text
E:/UnityProjects/NexUI/
├─ Assets/
├─ Packages/
│  ├─ com.nexengineworks.nexui/          Runtime 패키지
│  ├─ com.nexengineworks.nexui.studio/   Studio/Designer 패키지
│  └─ com.cysharp.unitask/                UniTask
├─ ProjectSettings/
├─ NexUI.sln
└─ emiteat.NexUI.*.csproj
```

### 1.1 Runtime 패키지

`com.nexengineworks.nexui`는 Player에서 실행될 UI 프레임워크와 Unity Editor 보조 도구를 가진다.

```text
Runtime/
├─ Abstractions   백엔드 독립 계약
├─ Core           화면, 레이어, 포커스, 정책, 기본 커맨드
├─ State          상태 저장소와 바인더
├─ Motion         단일 대상 모션
├─ MotionClip     다중 대상 키프레임 모션
├─ MotionGraph    순서/분기/병렬 모션 그래프
├─ Theme          테마와 토큰
├─ Components     공통 컴포넌트 계약과 컬렉션 로직
├─ Query          비동기 데이터 상태
├─ Settings       데이터 기반 초기화
├─ Debug          런타임 스냅샷과 오버레이
├─ Diagnostics    코드화된 진단 모델
├─ Compiled       Studio 컴파일 결과 포맷
├─ Interaction    컴파일된 상호작용 실행기
├─ Flow           실행 경로 추적
├─ Scenario       자동화 시나리오 실행기
├─ Time           실제/수동 시간 공급원
└─ Overrides      런타임 속성 변경 출처 추적

Integrations/
├─ UGUI
├─ UIToolkit
├─ DOTween
├─ VContainer
├─ MessagePipe
├─ Addressables
└─ InputSystem
```

### 1.2 Studio 패키지

`com.nexengineworks.nexui.studio`는 UI를 편집하고 메타데이터를 저장하며 컴파일 결과를 만드는
Editor 중심 패키지다.

```text
Runtime/Metadata/   Designer가 저장하는 데이터 모델
Editor/Core/        창, 컨텍스트, 선택, 저장, Undo
Editor/Backend/     uGUI/UI Toolkit 미리보기 백엔드
Editor/Serialization/ 백엔드 에셋과 JSON 출력
Editor/Compiler/    DesignerMetadata → NexScreenProgram
Editor/Validation/  저장 전 검증
Editor/Advanced/    Variant, Responsive, Motion, Scenario 등
Tests/              Studio EditMode/PlayMode 테스트
```

### 1.3 asmdef가 설계 경계다

폴더만 보고 의존성을 추측하지 말고 각 `.asmdef`의 `references`를 본다. 중요한 방향은 다음과 같다.

```text
Abstractions
  ↑
  ├─ Core
  ├─ State
  ├─ Motion
  ├─ Theme
  └─ Components

Diagnostics ← Compiled ← Interaction
                         ↑
                   Integrations.UGUI

Designer.Runtime ← Designer.Editor
Runtime modules  ← Designer.Editor
```

핵심 규칙은 Core가 Integration을 참조하지 않는다는 것이다. 화면을 만드는 실제 코드는 Integration이
Core의 인터페이스를 구현하는 방향으로 연결된다.

## 2. 반드시 구분해야 하는 두 실행 경로

이 프로젝트에는 “화면을 실행한다”는 말로 묶이지만 실제로는 다른 두 경로가 있다.

### 2.1 경로 A: UIScreenDefinition + UIManager

이 경로는 uGUI 프리팹 또는 UI Toolkit VisualTreeAsset을 연다.

```text
UIScreenDefinition
        ↓ RegisterScreen
UIScreenRegistry
        ↓ OpenAsync(screenId)
UIManager
        ↓ backend 선택
IUIScreenFactory
        ↓
UGUIScreenFactory 또는 UIToolkitScreenFactory
        ↓
IUISurface
```

두 백엔드 모두 지원한다. 화면 스택, 레이어, 모션, 포커스, 시간/커서 정책은 이 경로에서
`UIManager`가 관리한다.

### 2.2 경로 B: DesignerMetadataAsset → NexScreenProgram → uGUI Builder

Studio의 컴파일 경로는 메타데이터를 Player용 평탄화 데이터로 변환한다.

```text
DesignerMetadataAsset
        ↓ NexScreenCompiler
NexScreenProgram
        ↓ NexScreenPublisher
Assets/NexUI/Compiled/<ScreenId>.asset
        ↓ NexUGuiScreenBuilder.Build
NexScreenRuntime + 동적 uGUI hierarchy
```

현재 소스에서 컴파일된 프로그램의 실제 Builder는 `Integrations/UGUI/Compiled`에 있다. 동일한
`NexScreenProgram`을 직접 만드는 UI Toolkit compiled builder는 현재 보이지 않는다.

### 2.3 두 경로는 자동으로 합쳐지지 않는다

`UIManager.OpenAsync`는 `UIScreenDefinition`과 등록된 `IUIScreenFactory`를 사용한다.
`NexUGuiScreenBuilder.Build`는 `NexScreenProgram`을 받아 별도의 `NexScreenRuntime`을 반환한다.

따라서 다음을 동일한 객체라고 생각하면 안 된다.

| 일반 경로 | 컴파일 경로 |
|---|---|
| `UIScreenDefinition` | `NexScreenProgram` |
| `UIScreenInstance` | `NexScreenRuntime` |
| `UIManager`가 생명주기 소유 | 호출자가 `Dispose` 책임 |
| `IUISurface` + Capability | `NexRuntimeSourceMap` + node index |
| `UIActionResolver`/`UICommandDispatcher` | `NexCommandRouter`/compiled interaction |

신규 기능을 시작하기 전에 어느 경로의 기능인지 먼저 결정한다. 두 경로 모두에 필요한 기능이면
각 경로의 어댑터와 테스트를 따로 고려해야 한다.

## 3. 플레이 시작 시 초기화 흐름

### 3.1 공유 관리자

[`NexUIApp`](../Runtime/Core/NexUI.cs)은 정적 편의 API다.

```csharp
public static UIManager Manager => _manager ??= new UIManager();
```

최초 접근 시 `UIManager`가 만들어진다. DI를 사용하는 프로젝트는 `SetManager`로 다른 인스턴스를
주입할 수 있다. 정적 API를 사용하지 않고 `UIManager`를 직접 소유해도 된다.

### 3.2 Settings 자동 초기화

[`NexUIRuntimeSettings`](../Runtime/Settings/NexUIRuntimeSettings.cs)의
`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`가 먼저 실행될 수 있다.

조건은 다음과 같다.

1. `NexUISettingsProvider.Current`가 설정 에셋을 찾는다.
2. 명시적으로 `Set`하지 않았다면 `Resources.Load<NexUISettings>("NexUISettings")`를 호출한다.
3. `bootstrapMode == RuntimeInitializeOnLoad`일 때 `Apply`한다.

현재 `Apply`가 실제로 하는 작업은 다음뿐이다.

- settings의 화면을 Manager에 등록
- 테마를 `NexUITheme.Registry`에 등록
- 내장 MotionPlayer와 MotionResolver 설정
- 옵션이 켜졌으면 정적 `CommandLog` 생성

`enableDebugOverlay`, `enableQuery`, `enableValidationOnStart` 필드는 설정 에셋에 존재하지만 현재
`NexUIRuntimeSettings.Apply`에서는 소비되지 않는다. 이름만 보고 자동 기능이 켜진다고 가정하지 않는다.

### 3.3 씬 백엔드 Bootstrap

씬이 로드되면 다음 중 하나의 `Awake`가 실행된다.

- [`UGUIIntegrationBootstrap`](../Integrations/UGUI/UGUIIntegrationBootstrap.cs)
- [`UIToolkitIntegrationBootstrap`](../Integrations/UIToolkit/UIToolkitIntegrationBootstrap.cs)

각 Bootstrap은 공유 Manager에 다음을 등록한다.

1. 해당 백엔드 `IUIScreenFactory`
2. 해당 백엔드 `IUIFocusAdapter`
3. MotionPlayer와 MotionResolver가 없다면 기본 구현
4. 해당 백엔드 ThemeApplier
5. 지정된 `UILayerType`별 LayerRoot

Settings 자동 등록은 화면 정의를 알려 줄 뿐이다. 실제 프리팹/UXML을 만드는 Factory와 Layer는 씬
Bootstrap이 제공해야 한다.

### 3.4 권장 시작 순서

일반적으로 Bootstrap은 `Awake`, 게임 측 최초 화면 열기는 `Start`에 둔다.

```text
BeforeSceneLoad: NexUIRuntimeSettings.Apply
Scene Awake:     UGUI/UIToolkitIntegrationBootstrap.Register
Game Start:      NexUIApp.OpenAsync("HUD")
```

수동 등록 프로젝트라면 게임 Bootstrap의 `Start` 전에 ScreenDefinition 등록이 끝나야 한다.

## 4. 일반 화면의 OpenAsync 내부 플로우

소스 기준 실제 순서는 다음과 같다.

### 4.1 등록 확인과 관계 처리

1. Registry에서 `screenId`를 찾는다.
2. 없으면 오류 로그를 남기고 반환한다.
3. `parentScreenId`가 있으면 부모 화면을 먼저 연다.
4. 관계 순환을 `HashSet<string>`으로 차단한다.

Registry는 같은 Screen ID가 다시 등록되면 경고 후 새 정의로 덮어쓴다.

### 4.2 Queue와 전환 충돌 처리

`openPolicy == Queue`이면 ToastQueue의 활성 슬롯을 얻는다. 이미 다른 화면이 활성 상태면 요청을
큐에 넣고 즉시 반환한다.

같은 화면에서 Open/Close 전환이 겹치면 `conflictPolicy`를 적용한다.

- `Wait`: 진행 중 전환이 끝난 다음 새 요청 실행
- `Cancel`: 기존 토큰 취소 후 종료를 기다리고 새 요청 실행
- `Ignore`: 새 요청을 버림

화면별 `_transitions` Dictionary가 직렬화 단위다.

### 4.3 기존 인스턴스와 생성

이미 `_open`에 같은 Screen ID가 있으면 새 인스턴스를 만들지 않는다. 기존 화면에 포커스를 다시
요청하고 반환한다. `Additive`도 같은 ID의 복제본을 뜻하지 않는다. 서로 다른 Screen ID들이 같은
레이어에서 공존한다는 뜻이다.

닫힐 때 보관된 `_retained` 인스턴스가 있으면 재사용한다. 없으면 다음 순서다.

1. Definition의 `backendAsset.backend`로 Factory 선택
2. 부모 화면 또는 해당 LayerRoot를 mount parent로 선택
3. Factory의 `CreateAsync` 호출
4. null이면 실패 처리

### 4.4 로딩 전략

일반 로딩은 Definition의 `backendAsset.asset`을 Factory에 넘긴다.

`loadStrategy == Addressable`이면 다음 조건이 필요하다.

- `UIManager.ResourceProvider` 등록
- `backendAsset.resourceKey` 설정

Manager가 리소스를 로드하고, Definition을 런타임 복제한 뒤 로드된 asset을 넣어 Factory를 호출한다.
생성 후 Provider의 `Release(key)`를 호출한다.

### 4.5 화면 배치와 override

생성된 Surface를 먼저 비활성화한다. `ReplaceLayer`이면 같은 레이어의 다른 화면을 immediate close한
뒤 계속한다. `relations.closes`에 적힌 화면도 immediate close한다.

그다음 순서:

1. `UIScreenInstance` 생성 또는 재사용
2. 상태를 `Opening`으로 변경
3. `_open` Dictionary에 추가
4. Surface 활성화
5. Layer 기본 sorting order + 화면 priority 적용
6. input blocking 적용
7. Variant override 적용
8. 현재 해상도와 입력 모드에 맞는 Responsive override 적용

공통 override 경로는 text, visibility, position, scale, color, interactable, value, min/max,
opacity, rotation, width/height, fontSize, class, token 등을 Capability로 적용한다. 백엔드 전용 경로는
등록된 `UIScreenPropertyOverrideApplier`가 먼저 처리한다.

Variant나 Responsive Rule이 있는 화면은 닫을 때 retain하지 않는다. 이전 open에서 적용한 동적 값이
다음 open에 새어 들어오는 것을 막기 위해 항상 다시 만든다.

### 4.6 정책, 포커스, 스택, 모션

계속해서 다음 순서로 실행한다.

1. `OnBeforeOpenAsync`
2. 시간/커서 정책 적용
3. 등록된 `IInputPolicy.Apply`
4. Modal이면 ModalStack에 push
5. 포커스 정책이 있으면 FocusAdapter에 trap 요청
6. `StackPush`이면 BackStack에 push
7. open motion resolve 및 play
8. 상태를 `Open`으로 변경
9. `OnAfterOpenAsync`
10. `ScreenOpened` 이벤트
11. `relations.opensWith` 화면 열기

도중 취소나 예외가 발생하면 `_open`, ModalStack, BackStack, 정책, 입력 정책, 포커스를 되돌리고
Surface를 제거한다. 일반 예외는 `ScreenFaulted(screenId, exception)`로 알린다.

### 4.7 Lifecycle의 현재 발견 방식

`UIScreenInstance`는 다음 두 대상을 `IUIScreenLifecycle`로 캐스팅한다.

- `surface.RootHandle.Native`
- `surface` 자체

현재 uGUI RootHandle의 `Native`는 `GameObject`다. 루트 GameObject에 붙은 MonoBehaviour를
`GetComponent<IUIScreenLifecycle>()`로 찾는 코드는 현재 없다. 따라서 “프리팹 루트의 컴포넌트에
인터페이스를 구현하면 자동 호출된다”고 가정하면 안 된다. Lifecycle 기능을 수정할 때는 이 발견
경로부터 테스트해야 한다.

## 5. CloseAsync, BackAsync, 생명주기

### 5.1 CloseAsync 순서

1. 화면 전환 handle 획득
2. 상태를 `Closing`으로 변경
3. `OnBeforeCloseAsync`
4. immediate가 아니고 suppressMotion이 아니면 close motion 재생
5. 포커스 release 및 선택적으로 이전 포커스 복구
6. 시간/커서 정책 revert
7. 모든 InputPolicy release
8. ModalStack과 BackStack에서 제거
9. retain 대상이면 비활성화 후 `_retained`로 이동, 아니면 Destroy
10. `_open`에서 제거하고 상태를 `Closed`로 변경
11. `OnAfterCloseAsync`
12. `ScreenClosed` 이벤트
13. Toast였다면 다음 Queue 요청 실행

Close 중 예외가 발생해도 cleanup은 강제로 계속한다. 화면 하나의 실패가 전체 UI 스택을 막지 않게
하기 위한 구조다.

### 5.2 KeepAlive와 Pool의 현재 의미

현재 `ShouldRetain`은 `KeepAlive`와 `Pool`을 모두 `_retained[screenId]` 한 개에 보관하는 방식으로
처리한다. 여러 인스턴스를 관리하는 일반적인 Object Pool 구현은 아니다. 같은 Screen ID도 동시에
한 개만 열린다.

### 5.3 BackAsync

BackStack을 pop하면서 현재 열린 화면을 찾는다. 해당 화면이 `closeOnBack`이거나 `StackPush`이면
닫고 종료한다. 스택에서 닫을 화면을 못 찾으면 ModalStack 최상단을 확인하고 `closeOnBack`인
모달을 닫는다.

## 6. ScreenDefinition을 읽는 법

[`UIScreenDefinition`](../Runtime/Core/UIScreenDefinition.cs)은 일반 실행 경로의 단일 소스다.

| 블록 | 주요 필드 | 런타임 소비자 |
|---|---|---|
| `identity` | screenId, priority, accessibilityLabel | Registry, sorting |
| `backendAsset` | backend, asset, styleAssets, resourceKey | Factory/ResourceProvider |
| `layer` | layerType, openPolicy | LayerManager, Back/Toast |
| `motion` | openMotion, closeMotion | MotionResolver/Player |
| `policy` | input, time, cursor, focus, conflict, lifetime | UIManager/PolicyRunner |
| `focus` | default ID, trap, restore | FocusManager |
| `relations` | opensWith, closes, parent | UIManager |
| `validation` | modal focus, motion warning 등 | Validator |
| `contract` | 요구 요소와 Capability | Validator/도구 |
| `loadStrategy` | Preload, LazyLoad, Addressable, Pool, KeepAlive | UIManager |
| `variants` | 명명된 속성 override | Open 시 적용 |
| `responsiveRules` | 해상도/입력 모드 override | Open 시 적용 |

ID와 backend asset이 실제 실행을 결정한다. 파일 이름이나 GameObject 이름이 Screen ID를 대신하지
않는다.

## 7. Backend, Surface, Handle, Capability

### 7.1 Factory

- `UGUIScreenFactory`: Definition asset이 GameObject prefab인지 확인하고 Instantiate한다.
- `UIToolkitScreenFactory`: Definition asset이 VisualTreeAsset인지 확인하고 CloneTree한다.

두 Factory 모두 `IUIScreenFactory`를 구현하므로 Core는 구체 타입을 모른다.

### 7.2 Surface

`IUISurface`는 화면 전체의 백엔드 독립 손잡이다.

- 요소 검색
- 활성/비활성
- 정렬 순서
- 입력 차단
- 제거

uGUI Surface는 먼저 `NxUGuiBindingTag.ResolveId`로 요소를 찾고, 없으면 자식 GameObject 이름을
재귀 검색한다. 명시적 태그가 변경과 중복 검증에 더 안전하다.

UI Toolkit Surface는 `root.Q<VisualElement>(elementId)`로 name을 찾는다.

### 7.3 Handle과 Capability 생성

각 ElementHandle 생성자는 Native 요소에 붙은 컴포넌트/타입을 검사해 Capability Dictionary를
만든다.

예:

```text
uGUI Button      → Click + Interactable
uGUI Slider      → Value + ValueInput
TMP_InputField   → Text + TextInput
TMP_Text         → Text + Typography + Color
RectTransform    → Transform + Size

UIToolkit Button → Click + Interactable
Label            → Text
Slider           → Value + ValueInput
VisualElement    → Visibility + Transform + Style 등
```

Runtime/State와 Runtime/Motion은 Native 타입을 캐스팅하지 않고 `handle.As<TCapability>()`만
사용한다.

### 7.4 input blocking 이름에 주의

`UIManager`는 `blockInputBehind || Modal` 값을 `surface.SetInputBlocking`에 전달한다. uGUI 구현은
이 값을 root `CanvasGroup.blocksRaycasts`에 직접 넣는다. 따라서 현재 구현에서는 false가 해당 화면
자체의 raycast도 끌 수 있다. 상호작용 가능한 uGUI 화면의 입력 문제를 조사할 때 이 경로를 먼저
확인한다. 필드 이름만 보고 “뒤 화면만 막는다”고 가정하지 않는다.

## 8. State와 Binding 플로우

### 8.1 UIStateStore

`UIStateStore`는 `Dictionary<string, object>`와 key별 watcher 목록을 가진다.

`Set<T>` 흐름:

```text
값 저장
  ↓
해당 key watcher 목록 복사
  ↓
각 callback 호출
  ↓
callback 예외는 로그하고 다음 watcher 계속
```

watcher가 콜백 중 구독을 해제해도 안전하도록 배열 snapshot을 사용한다. 타입이 일치하는 watcher만
호출된다. `int`로 저장하고 `Watch<float>`로 구독하면 알림을 받지 못한다.

### 8.2 Binder

Binder는 다음 세 요소를 연결한다.

```text
UIStateStore key ↔ UIBinder ↔ IUIElementHandle Capability
```

`UITextBinder`와 `UIValueBinder`는 OneWay, TwoWay, OneWayToSource를 지원한다. TwoWay는
`IUITextInputCapability` 또는 `IUIValueInputCapability`가 있어야 한다. `_syncing` 플래그로 UI와
State가 서로 무한히 갱신하는 것을 막는다.

`UIVisibilityBinder`, `UIClassBinder`는 bool state를 사용한다. `UICommandBinder`는 State 값 대신
클릭 Capability와 ActionResolver를 연결한다.

### 8.3 소유권

일반 Binder는 자동으로 화면 생명주기에 등록되지 않는다. 만든 코드가 Binder 인스턴스를 보관하고
화면 종료 또는 소유자 파괴 시 `Unbind`해야 한다.

컴파일 경로의 `NexUGuiScreenBuilder`는 다르게 동작한다. State watcher와 클릭 구독을
`NexScreenRuntime`에 `Track`하고 `Dispose`에서 모두 정리한다.

## 9. 세 종류의 명령 시스템

이 코드베이스에는 목적이 다른 명령 연결 방식이 세 개 있다. 이름이 비슷하므로 반드시 구분한다.

### 9.1 UIActionResolver

State 모듈의 간단한 `string → Action/Func<UniTask>` Registry다.

- 주 사용처: 일반 화면의 `UICommandBinder`
- 인스턴스 기반이며 Singleton이 아님
- 미등록 키는 경고
- 구독 정리는 Binder가 담당

### 9.2 UICommandDispatcher

Core의 타입 기반 커맨드 파이프라인이다.

```text
IUICommand
  ↓ exact runtime type lookup
IUIMiddleware 0
  ↓
IUIMiddleware 1
  ↓
IUICommandHandler<T>
  ↓
CommandLog + CommandExecuted
```

Handler 검색은 command의 정확한 runtime type을 key로 사용한다. 등록된 Handler가 없으면 경고 후
종료하며 Log와 `CommandExecuted`는 실행되지 않는다.

현재 제공되는 Handler는 State의 `SetValueCommandHandler`, Theme의 `SetThemeCommandHandler`,
Motion의 `PlayMotionCommandHandler`다. `OpenScreenCommand`, `CloseScreenCommand`,
`ToggleScreenCommand`, `BackCommand` 타입은 Core에 있지만 이 타입들의 기본 Handler 구현은 현재
검색되지 않는다. Dispatcher에서 사용하려면 프로젝트 측 Handler를 등록해야 한다.

### 9.3 NexCommandRouter

컴파일된 Studio 화면의 `CommandId`를 게임 코드에 연결한다.

- `string commandId → Action<NexCommandContext>`
- 등록은 IDisposable handle을 반환
- 미등록/Handler 예외를 `NexDiagnostic`으로 반환
- 예외를 밖으로 던져 전체 입력을 깨지 않음
- `NexFlowTrace`와 함께 “클릭 → 명령 → Handler”를 기록

세 시스템을 임의로 섞지 않는다. 화면이 어느 실행 경로인지 보고 선택한다.

## 10. Motion 플로우

일반 화면 open/close motion은 다음 순서다.

```text
UIScreenDefinition.motion의 UnityEngine.Object
  ↓ MotionResolver.Resolve
UIMotionPreset
  ↓ MotionCompiler.Compile + cache
UIMotionTimeline
  ↓ BuiltInMotionPlayer.PlayAsync
RootHandle.As<IUITransformCapability>()
  ↓ 매 프레임
Opacity / Position / Scale / Rotation
```

BuiltInMotionPlayer는 unscaled delta time을 사용하므로 게임이 pause 상태여도 UI 모션이 진행된다.
같은 target에 새 모션이 오면 기존 CancellationTokenSource를 취소한다.

`MotionCompiler`는 variant steps가 있으면 각 step을 track으로 바꾸고, graph가 있으면 dependency의
최대 종료 시간을 기준으로 delay를 평탄화한다. `MotionResolver`는 preset별 Timeline을 cache하므로
런타임에서 preset을 수정했다면 `Invalidate`가 필요하다.

MotionClip은 여러 target/property track과 keyframe을 위한 별도 시스템이다. MotionGraph는 sequence,
parallel, race, repeat, branch, timeout, command dispatch 같은 실행 노드를 위한 시스템이다.

## 11. Theme 플로우

```text
UITheme 등록
  ↓ NexUITheme.Registry
NexUITheme.Use(themeId)
  ↓ ActiveTheme + RuntimeTokenOverride.BaseTheme
ThemeEvents.ThemeChanged
  ↓ 구독자 갱신
NexUIThemeAPI.ApplyTo(handle)
  ↓ backend별 IUIThemeApplier
IUIStyleCapability.ApplyToken
```

Backend Bootstrap은 ThemeApplier를 등록한다. 하지만 `Use`는 ActiveTheme을 바꾸고 이벤트를 발생시키는
작업이며 현재 열린 모든 요소를 자동 순회해 `ApplyTo`하지 않는다. 실제 화면/컴포넌트가
ThemeEvents를 구독하거나 적절한 시점에 ApplyTo를 호출하는지 확인해야 한다.

Runtime token override는 원본 UITheme asset을 바꾸지 않는다. `ResolveToken`은 override를 우선하고
없으면 ActiveTheme 값을 사용한다.

## 12. Query 플로우

`UIQuery<T>`는 Core를 참조하지 않고 State의 `UISignal<QueryState<T>>`를 사용한다.

```text
RunAsync
  ├─ 유효한 cache hit → Success/Empty publish
  └─ cache miss
       ↓ Loading
       fetch(ct)
       ├─ 성공 → cache 저장 → Success/Empty
       ├─ 취소 → 조용히 반환
       └─ 예외 → RetryPolicy → 재시도 또는 Failure
```

LoadingBoundary, ErrorBoundary, EmptyBoundary, FallbackScreen은 QueryState와 UI Capability를 연결하는
도우미다. Retry delay 이후 continuation이 Unity main thread가 아닐 수 있다는 주석이 있으므로
UnityEngine.Object 접근은 호출 측에서 thread를 확인한다.

## 13. Studio 편집과 저장 플로우

### 13.1 Studio 진입점

메뉴는 `Tools/Nex/NexUI Studio/Open NexUI Studio`다. 창은 `NexUIDesignerWindow`, 작업 상태는
`NexUIDesignerContext`가 소유한다.

Context는 현재 ScreenDefinition, Metadata, backend preview surface, selection, variant context,
dirty state를 묶는다. 여러 패널은 이 Context 이벤트를 구독한다.

### 13.2 Save

`NexUIDesignerContext.Save`의 실제 순서:

1. 열린 Screen이 있는지 확인
2. Metadata screenId와 ScreenDefinition ScreenId 일치 확인
3. `DesignerValidationService` preflight
4. 오류가 있으면 저장 중단
5. 현재 Screen JSON snapshot 보관
6. backend별 serializer 선택
7. Component instance를 임시 평탄화
8. expansion 오류/경고 수집
9. screen motion reference 동기화
10. Variant/Responsive metadata를 runtime 구조로 compile해 ScreenDefinition에 반영
11. uGUI 또는 UI Toolkit backend asset 저장
12. review 가능한 companion JSON 저장
13. 실패 시 ScreenDefinition snapshot 복구
14. baseline 갱신, dirty 해제, validation 재실행

Save와 `NexScreenProgram` Compile/Publish는 별도 경로다. Save 코드 안에서
`NexScreenBuildPipeline.CompileAndPublish`를 호출하지 않는다.

## 14. Studio 컴파일과 Publish 플로우

컴파일 메뉴는 다음 경로다.

- `Tools/NexUI/Compile Selected Screen`
- `Tools/NexUI/Compile All Screens`

`NexScreenBuildPipeline`이 Compiler, Publisher, Build Report를 묶는다.

### 14.1 Compiler 네 단계

1. **Normalize**: parent-first DFS, siblingIndex와 elementId로 결정적 정렬
2. **Validate**: ID 누락/중복, parent 누락/순환, automation ID 중복, binding/interaction 유효성
3. **Lower**: Designer element를 제한된 `NexNodeKind`와 resolved node index로 변환
4. **Hash**: canonical string의 content hash 계산

컴파일 오류가 있어도 진단과 preview를 위해 partial Program은 만들 수 있지만 Publisher는 오류가 있는
결과를 출판하지 않는다.

### 14.2 출력

출력 위치:

```text
Assets/NexUI/Compiled/<ScreenId>.asset
```

Compiler version은 현재 `NexScreenProgram.CurrentCompilerVersion == 5`다. 런타임 Builder는 버전이
다르면 `ProgramSchemaMismatch` 진단을 남기고 Build를 거부한다.

content hash가 기존 asset과 같으면 Publisher가 파일을 다시 쓰지 않는다. Compile All은 변경된
화면만 publish한다.

### 14.3 컴파일 결과가 포함하는 것

- parents-first `NexNodeProgram[]`
- source map: stable node ID, element ID, node index, authoring path
- feature manifest
- interaction program
- reference resolution
- compiler version과 content hash

Preview 전용 값, Editor 메모, 원본 도구 상태는 Player 포맷에 넣지 않는 것이 설계 의도다.

## 15. 컴파일 화면의 런타임 Build

`NexUGuiScreenBuilder.Build(program, options, diagnostics)`가 실행 진입점이다.

### 15.1 BuildOptions

- `Store`: text binding과 interaction state에 사용할 UIStateStore
- `Router`: authored CommandId를 받을 NexCommandRouter
- `Parent`: 화면 root의 부모 Transform. 없으면 scene 첫 Canvas 검색

### 15.2 Build 순서

1. null과 compiler version 검증
2. parent 결정
3. stretch root 생성
4. `NexRuntimeSourceMap`과 `NexScreenRuntime` 생성
5. `NexUGuiScreenSurface`와 `NexOverrideLedger` 생성
6. `NexInteractionRuntime` 생성
7. compiled node array를 단일 forward pass로 순회
8. Panel/Image/Label/Button GameObject 생성
9. node index와 live object를 SourceMap에 등록
10. text binding 연결
11. CommandId Button listener 연결
12. compiled interaction trigger listener 연결
13. authored visibility 적용
14. delay action이 있는 화면에만 `NexScreenTicker` 추가
15. 전체 hierarchy 생성 후 OnShow interaction 실행

부모가 자식보다 먼저 배열에 있다는 조건은 Compiler가 보장한다. Builder는 Player에서 다시 tree
validation을 하지 않는다.

### 15.3 명령 listener와 interaction listener

node에 `CommandId`가 있으면 Router dispatch listener가 붙는다. 별도의 authored OnClick interaction
rule도 있으면 InteractionRuntime listener가 추가된다. 둘은 서로 다른 경로이므로 버튼 하나에서 둘 다
실행될 수 있다.

### 15.4 Dispose

`NexScreenRuntime.Dispose`는 다음을 수행한다.

1. OnHide interaction 실행
2. 지연 중 interaction 취소
3. 모든 State/Click subscription Dispose
4. SourceMap clear
5. root GameObject Destroy

Builder를 직접 호출한 코드가 `NexScreenRuntime`을 소유하고 반드시 Dispose해야 한다.

## 16. Compiled Interaction 내부 플로우

`NexInteractionRuntime`은 Compiler가 미리 해석한 rule을 실행한다. 런타임 click path에서 문자열
element 검색이나 값 parsing을 다시 하지 않는다.

### 16.1 Trigger 전파

현재 hierarchy propagation을 하는 trigger는 OnClick이다.

```text
Capture: 바깥 ancestor → 안쪽 ancestor
Target:  클릭된 node
Bubble:  안쪽 ancestor → 바깥 ancestor
```

rule의 `StopsPropagation`이 true면 이후 전파를 중단한다. OnShow/OnHide 같은 lifecycle trigger는
전파하지 않고 해당 node의 Target rule만 실행한다.

### 16.2 Condition과 Action

Condition은 `INexStateAccess`를 통해 현재 값을 읽고 compiled comparison으로 평가한다. Action은
Router command dispatch, state set, visibility/text 변경, delay 등으로 lowering되어 있다.

Port가 없거나 Action이 실패해도 전체 engine이 throw하지 않는다. Diagnostic을 발생시키고 남은 rule을
계속 처리한다.

### 16.3 Delay

Delay Action은 continuation을 `_pending`에 저장한다. Program에 delay가 있을 때만 screen root에
Ticker가 붙고 `Update → interactions.Tick()`으로 재개한다. 테스트는 `NexManualTime`을 넣어 프레임
시간에 의존하지 않고 진행할 수 있다.

## 17. Diagnostics, Flow, Overrides

이 세 시스템은 “무엇이 실패했는가”, “어떤 경로로 실행됐는가”, “누가 마지막으로 값을
바꿨는가”를 각각 담당한다.

### 17.1 Diagnostics

- `NexDiagnosticCodes`: 안정적인 코드와 기본 메시지
- `NexDiagnostic`: severity, code, location, message, detail
- `NexDiagnosticBag`: compile 단위 수집
- `NexDiagnosticLog`: runtime 진단 query
- `NexSourceLocation`: screen/node/path/member 위치

새 진단을 단순 문자열 로그로만 만들지 말고 기존 진단 코드 체계를 따를지 먼저 검토한다.

### 17.2 Flow

`NexFlowTrace.Begin(origin)`으로 scope를 열고 step을 기록한다. Console sink나 Memory sink를 붙일 수
있다. 컴파일 화면의 click, rule phase, condition, command, binding 변경을 하나의 chain으로 본다.

### 17.3 Overrides

`NexOverrideLedger`는 node property의 마지막 변경을 기록한다.

변경 출처 예:

- Binding
- Interaction
- GameCode
- 그 외 compiled/runtime source

`NexScreenRuntime.SetText/SetVisible`를 사용하면 GameCode 변경 이유가 기록된다. Native TMP_Text를 직접
수정하면 화면은 바뀌지만 Ledger가 이유를 알 수 없다.

## 18. Scenario와 자동화

`NexScenario`는 fluent API로 다음 step을 구성한다.

- automation ID로 요소 찾기
- click
- state set
- 조건 충족까지 poll
- 시간 대기
- visible/hidden/text/state assertion
- error 없음 assertion

`NexScenarioRunner`는 매 `MoveNext`에서 한 단계 또는 한 poll을 진행한다. `INexScenarioWorld`가 실제
화면, state, click, diagnostics에 대한 port다. uGUI compiled runtime에는
`NexUGuiScenarioWorld` 어댑터가 있다.

테스트는 element 이름이나 hierarchy path 대신 automation ID를 우선 사용한다. SourceMap은 이름과
부모가 바뀌어도 automation ID 계약이 유지되면 동일 요소를 찾는다.

## 19. 선택 Integration

선택 통합 asmdef에는 define constraint가 있다.

| 모듈 | define | 연결 지점 |
|---|---|---|
| Addressables | `NEXUI_HAS_ADDRESSABLES` | `IUIResourceProvider` |
| DOTween | `NEXUI_HAS_DOTWEEN` | `IUIMotionPlayer` |
| Input System | `NEXUI_HAS_INPUTSYSTEM` | `IInputPolicy`, device prompt |
| MessagePipe | `NEXUI_HAS_MESSAGEPIPE` | Manager/command/motion event 발행 |
| VContainer | `NEXUI_HAS_VCONTAINER` | Manager, Store, Theme, Query DI 등록 |

패키지만 설치하고 define이 없거나, define만 있고 패키지 reference가 없으면 Assembly가 기대대로
활성화되지 않는다. 관련 asmdef와 define 설정을 함께 확인한다.

## 20. 기능별 수정 위치

| 변경 요구 | 먼저 볼 위치 | 같이 확인할 곳 |
|---|---|---|
| 화면 Open/Close 순서 | `Runtime/Core/UIManager.cs` | Core/PlayMode tests |
| 새 화면 정책 | Core config + PolicyRunner | Validator, Studio inspector |
| 새 Capability | Abstractions | UGUI/UIToolkit handles, binders |
| uGUI 요소 검색 | `UGUISurface.cs` | binding tags, duplicate ID test |
| UI Toolkit 요소 검색 | `UIToolkitSurface.cs` | UXML name |
| 새 Binder | Runtime/State | 양쪽 backend capability |
| 새 일반 Command | command type + handler | dispatcher tests/log/replay |
| 새 compiled Action | Compiled enum/data | Studio lowerer, Interaction runtime, tests |
| 새 Motion property | Abstractions timeline | compiler, player, DOTween |
| 새 Theme token 동작 | Theme | 두 backend applier |
| Studio 저장 형식 | Runtime/Metadata | serializer, JSON, migration |
| Studio compiled format | Compiled + Compiler | compiler version, builder, determinism test |
| compiled uGUI 생성 | NexUGuiScreenBuilder | SourceMap, Dispose, PlayMode tests |
| 진단 추가 | Diagnostics | compiler/runtime caller, report UI |

한 층에서 public enum이나 serialized 구조를 바꾸면 해당 데이터를 쓰는 Editor와 읽는 Runtime을 같이
검색한다. compiled format의 의미가 바뀌면 compiler version 갱신 여부를 검토한다.

## 21. 테스트 구조

### 21.1 Runtime EditMode

`Packages/com.nexengineworks.nexui/Tests/EditMode`:

- Core screen/registry/validation
- State와 binding
- Motion, Theme, Query
- MotionClip/StateMachine
- MotionGraph
- Focus navigation
- Collection controller

백엔드가 필요 없는 로직은 EditMode에 둔다.

### 21.2 Runtime PlayMode

`Packages/com.nexengineworks.nexui/Tests/PlayMode`:

- 일반 runtime flow
- compiled screen build
- interaction, propagation, delay
- scenario runner
- manual time
- override ledger
- diagnostic log

GameObject, Button, frame update, uGUI Builder가 필요한 검증은 PlayMode에 둔다.

### 21.3 Studio 테스트

Studio 패키지에는 별도의 EditMode/PlayMode 테스트와 Fixtures Assembly가 있다. Compiler, serializer,
designer component, coordinate migration, session, Figma importer처럼 Editor 기능을 여기서 검증한다.

### 21.4 회귀 테스트 선택 기준

- 순수 계산/Dictionary/Compiler lowering: EditMode
- 실제 Button listener/Destroy/Ticker: PlayMode
- Studio AssetDatabase/Serializer/Window service: Studio EditMode
- compiled format 변경: compiler test + runtime builder test 둘 다

## 22. 빌드와 검증 명령

프로젝트 루트에서 개별 어셈블리를 먼저 빌드한다.

```powershell
dotnet build emiteat.NexUI.Core.csproj --no-restore --nologo
dotnet build emiteat.NexUI.Integrations.UGUI.csproj --no-restore --nologo
dotnet build emiteat.NexUI.Integrations.UIToolkit.csproj --no-restore --nologo
dotnet build emiteat.NexUI.Designer.Editor.csproj --no-restore --nologo
```

변경 범위별 예:

```text
Core 변경        → Core + EditMode + PlayMode
Capability 변경  → Abstractions + UGUI + UIToolkit + tests
Compiler 변경    → Designer.Editor + Designer.Tests.EditMode + UGUI PlayMode
Interaction 변경 → Interaction + UGUI + PlayMode
```

전체 솔루션:

```powershell
dotnet build NexUI.sln --no-restore --nologo
```

솔루션은 Unity/Package 프로젝트 수가 많아 오래 걸릴 수 있다. C# build 성공은 PlayMode 동작,
AssetDatabase serialization, 시각적 레이아웃 성공을 보장하지 않는다.

최종 확인은 Unity Test Runner의 EditMode와 PlayMode, 실제 sample scene, Console warning/error를 함께
본다.

## 23. 자주 빠지는 함정

### 23.1 `NexUI`가 아니라 `NexUIApp`

전역 화면 API 타입은 `emiteat.NexUI.Core.NexUIApp`이다. `NexUI`는 상위 namespace와 혼동된다.

### 23.2 ActionResolver는 Singleton이 아니다

`UIActionResolver.Instance`는 없다. Bootstrap/DI container/화면 소유자가 인스턴스를 만들고
Binder와 Debug 서비스에 같은 인스턴스를 전달한다.

### 23.3 이름이 같은 Time

프로젝트에는 `emiteat.NexUI.Time` namespace가 있다. `emiteat.NexUI.*` 내부에서 Unity Time을
사용할 때는 다음처럼 명시한다.

```csharp
using UnityTime = UnityEngine.Time;
```

### 23.4 Store 타입 불일치

object 저장소지만 watcher는 typed check를 한다. `Set("hp", 1)`과 `Watch<float>("hp", ...)`는
연결되지 않는다.

### 23.5 일반 화면과 compiled 화면의 명령을 혼동

- 일반 Binder: `UIActionResolver`
- 타입 커맨드 파이프라인: `UICommandDispatcher`
- compiled Studio screen: `NexCommandRouter`

### 23.6 Studio Save와 Compile을 혼동

Save는 backend asset, ScreenDefinition의 variant/responsive, companion JSON을 갱신한다. Player용
`NexScreenProgram` publish는 Compiler 메뉴의 별도 작업이다.

### 23.7 Dispose 누락

- 일반 Binder: `Unbind`
- `Watch`/Router registration: `Dispose`
- compiled screen: `NexScreenRuntime.Dispose`
- UIManager 종료: `Shutdown`

### 23.8 Layer 누락

기본 Bootstrap 배열은 Background, HUD, Window, Modal, Toast, Overlay다. `Scene`이나 `System`을
사용하는 Definition은 Inspector에서 Layer 배열에 추가하지 않으면 parent layer warning이 발생한다.

## 24. 버그 조사 표준 절차

1. 문제가 일반 화면 경로인지 compiled 화면 경로인지 판별한다.
2. 첫 Console error 또는 첫 Diagnostic code를 확보한다.
3. Screen ID, backend, runtime instance 타입을 기록한다.
4. 일반 화면이면 `Manager.OpenScreens`, BackStack, ModalStack, Transition을 본다.
5. compiled 화면이면 Program version/hash, SourceMap, Router 등록, FlowTrace, OverrideLedger를 본다.
6. State 문제면 key와 실제 저장 타입, watcher 수명부터 본다.
7. 이름 충돌/중복 API는 `rg`로 전체 패턴을 검색한다.
8. 가장 작은 관련 Assembly를 빌드한다.
9. 실패를 재현하는 EditMode 또는 PlayMode test를 추가한다.
10. 두 backend 공통 계약 변경이면 양쪽 Integration을 모두 검사한다.

유용한 검색 예:

```powershell
rg -n "OpenAsync|CloseAsync" Packages/com.nexengineworks.nexui/Runtime/Core -g "*.cs"
rg -n "NexScreenProgram" Packages/com.nexengineworks.nexui* -g "*.cs"
rg -n "As<IUI.*Capability>" Packages/com.nexengineworks.nexui -g "*.cs"
rg -n "NexDiagnosticCodes\." Packages/com.nexengineworks.nexui* -g "*.cs"
```

## 25. 신입 개발자의 첫 주 권장 과제

### 1일차: 구조 추적

- 두 패키지의 asmdef 참조 방향 그리기
- `NexUIApp.OpenAsync`에서 uGUI prefab Instantiate까지 breakpoint로 추적
- 열린 화면의 `UIScreenInstance.State` 변화 기록

완료 기준: 일반 화면 Open 순서를 코드 없이 설명할 수 있다.

### 2일차: State와 Capability

- BasicRuntime sample import
- UIStateStore key 하나 추가
- Text/Value Binder 하나씩 연결
- uGUI handle의 capability build를 breakpoint로 확인
- 의도적으로 타입을 틀려 watcher가 실행되지 않는 상황 재현

완료 기준: Native UI 타입을 Core/State에서 직접 쓰지 않는 이유를 설명할 수 있다.

### 3일차: 정책과 오류 경로

- Modal + StackPush 화면 구성
- Wait/Cancel/Ignore 전환 충돌 비교
- open lifecycle 또는 Factory 실패를 fake로 만들어 rollback test 읽기
- KeepAlive 화면 재사용 확인

완료 기준: Open 실패 후 어떤 상태가 rollback되는지 설명할 수 있다.

### 4일차: Studio Compile

- DesignerMetadataAsset 하나를 Compiler로 compile
- canonical string과 content hash 확인
- element ID 중복으로 Diagnostic 재현
- 생성된 `Assets/NexUI/Compiled/*.asset` 구조 확인

완료 기준: Save와 Compile/Publish의 차이를 설명할 수 있다.

### 5일차: Compiled Runtime

- `NexUGuiScreenBuilder.Build`로 Program 실행
- NexCommandRouter handler 등록
- State binding, interaction condition, delay 실행
- FlowTrace와 OverrideLedger 확인
- Dispose 후 watcher/listener가 남지 않는지 테스트 읽기

완료 기준: compiled click이 Router와 InteractionRuntime을 거치는 경로를 설명할 수 있다.

## 26. 코드 리뷰 체크리스트

### 아키텍처

- [ ] 변경이 올바른 asmdef/레이어에 있는가?
- [ ] Core 또는 독립 모듈에 backend concrete type을 넣지 않았는가?
- [ ] 일반 경로와 compiled 경로 중 영향받는 범위를 명시했는가?
- [ ] serialized/compiled 포맷 변경 시 Editor와 Runtime을 함께 수정했는가?

### 수명과 오류

- [ ] CancellationToken을 전달하고 취소 후 cleanup하는가?
- [ ] 이벤트, watcher, Router registration을 해제하는가?
- [ ] 화면 하나의 예외가 다른 화면/입력을 멈추지 않는가?
- [ ] 실패 시 stack, policy, focus, surface 상태가 rollback되는가?

### 데이터와 ID

- [ ] Screen/element/state/command/automation ID가 안정적인가?
- [ ] 중복과 누락을 Validator/Compiler가 진단하는가?
- [ ] State value 타입이 watcher/Binder와 일치하는가?
- [ ] Diagnostic에 screen/node/path가 포함되는가?

### 검증

- [ ] 관련 개별 csproj가 빌드되는가?
- [ ] EditMode/PlayMode 중 맞는 회귀 테스트가 있는가?
- [ ] uGUI/UI Toolkit 공통 기능이면 두 backend를 확인했는가?
- [ ] Studio 저장/compile 변경이면 실제 AssetDatabase 결과를 확인했는가?

## 27. 최종 정신 모델

일반 런타임은 `UIManager`가 ScreenDefinition을 읽고 backend Factory를 통해 Surface를 만든 뒤,
Capability로 정책·모션·바인딩을 적용하는 시스템이다.

Studio 런타임은 Designer metadata를 Editor Compiler가 검증·평탄화해 `NexScreenProgram`으로 publish하고,
uGUI Builder가 이를 단일 pass로 live hierarchy로 만든 뒤 Router, Interaction, Flow, Override, Scenario
시스템을 연결하는 별도 시스템이다.

새 코드를 작성할 때는 먼저 다음 문장을 완성한다.

> 이 변경은 ______ 실행 경로의 ______ 어셈블리가 소유하며, ______ 어댑터와 ______ 테스트까지
> 영향을 준다.

이 문장을 명확하게 채울 수 있으면 대부분 올바른 파일에서 작업을 시작할 수 있다.

## 부록 A. 일반 화면 경로의 최소 Host

다음 코드는 일반 `UIScreenDefinition + UIManager` 경로에서 화면, State, Action, Binder의 소유권을
한 컴포넌트에 모은 예다. 실제 프로젝트에서는 DI container나 feature 단위 controller로 나눌 수
있지만, 처음 흐름을 추적할 때는 이 정도가 가장 명확하다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.State;

namespace MyGame.UI
{
    public sealed class InventoryUIHost : MonoBehaviour
    {
        [SerializeField] private UIScreenDefinition inventoryDefinition;

        private readonly UIStateStore _store = new UIStateStore();
        private readonly UIActionResolver _actions = new UIActionResolver();
        private readonly List<UIBinder> _binders = new List<UIBinder>();

        private async void Start()
        {
            NexUIApp.RegisterScreen(inventoryDefinition);

            _store.Set("inventory.title", "Inventory");
            _actions.Register("inventory.close", () => NexUIApp.Close("Inventory"));

            await NexUIApp.OpenAsync("Inventory");

            IUISurface surface = NexUIApp.Manager.GetSurface("Inventory");
            if (surface == null) return;

            var title = new UITextBinder();
            title.Bind(surface.FindRequired("titleLabel"), "inventory.title", _store);
            _binders.Add(title);

            var close = new UICommandBinder(_actions);
            close.Bind(surface.FindRequired("closeButton"), "inventory.close", _store);
            _binders.Add(close);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _binders.Count; i++)
                _binders[i].Unbind();
            _binders.Clear();
        }
    }
}
```

Breakpoint 추천 위치:

1. `NexUIApp.RegisterScreen`
2. `UIManager.OpenInternalAsync`
3. 선택한 Backend Factory의 `CreateAsync`
4. Surface의 `TryFind`
5. Binder의 `Bind`
6. `UIStateStore.Set`

## 부록 B. 컴파일 화면 경로의 최소 Host

이 코드는 Studio가 publish한 `NexScreenProgram`을 uGUI Builder로 직접 실행한다. 이 경로에는
`UIManager`가 자동으로 `NexScreenRuntime`을 소유해 주지 않으므로 Host가 Dispose한다.

```csharp
using System;
using UnityEngine;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Integrations.UGUI;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.State;

namespace MyGame.UI
{
    public sealed class CompiledScreenHost : MonoBehaviour
    {
        [SerializeField] private NexScreenProgram program;
        [SerializeField] private RectTransform parent;

        private readonly UIStateStore _store = new UIStateStore();
        private readonly NexCommandRouter _router = new NexCommandRouter();
        private IDisposable _startRegistration;
        private NexScreenRuntime _runtime;

        private void Start()
        {
            _store.Set("menu.playerName", "Player");

            _startRegistration = _router.Register("Game.Start", context =>
            {
                Debug.Log($"Start requested by {context.ScreenId}/{context.SenderPath}");
            });

            _runtime = NexUGuiScreenBuilder.Build(program, new NexScreenBuildOptions
            {
                Store = _store,
                Router = _router,
                Parent = parent
            });
        }

        private void OnDestroy()
        {
            _startRegistration?.Dispose();
            _runtime?.Dispose();
        }
    }
}
```

Breakpoint 추천 위치:

1. `NexUGuiScreenBuilder.Build`
2. node 생성 loop
3. `WireText`, `WireCommand`, `WireInteractionTriggers`
4. `NexInteractionRuntime.Fire`
5. `NexCommandRouter.Dispatch`
6. `NexScreenRuntime.Dispose`
