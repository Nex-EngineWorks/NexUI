# NexUI 초보자 종합 안내서

이 문서는 NexUI를 처음 접하는 사람이 프로젝트를 직접 열어 보고, 작은 화면부터 코딩하고,
문제가 생겼을 때 스스로 원인을 찾을 수 있도록 만든 입문서다. 특정 버그 하나만 설명하는 문서가
아니라 현재 `com.nexengineworks.nexui` 런타임 패키지의 전체 구조와 주요 기능을 한 흐름으로
정리한다.

현재 패키지 기준은 다음과 같다.

- Unity: `6000.4.2f1`
- NexUI 패키지: `com.nexengineworks.nexui` `0.1.0`
- 루트 네임스페이스: `emiteat.NexUI`
- 비동기 라이브러리: UniTask
- 화면 백엔드: uGUI 또는 UI Toolkit

> 처음부터 모든 모듈을 외우지 않아도 된다. 1부부터 7부까지 따라 해서 화면 하나를 띄운 뒤,
> 필요한 기능이 생겼을 때 뒤쪽 장을 찾아보는 방식이 가장 편하다.

## 목차

1. [NexUI가 무엇인가](#1-nexui가-무엇인가)
2. [먼저 알아야 할 Unity와 C# 용어](#2-먼저-알아야-할-unity와-c-용어)
3. [프로젝트 구조](#3-프로젝트-구조)
4. [NexUI가 화면을 여는 과정](#4-nexui가-화면을-여는-과정)
5. [첫 화면 만들기](#5-첫-화면-만들기)
6. [화면을 코드로 열고 닫기](#6-화면을-코드로-열고-닫기)
7. [상태와 바인딩](#7-상태와-바인딩)
8. [버튼과 액션](#8-버튼과-액션)
9. [레이어와 화면 정책](#9-레이어와-화면-정책)
10. [모션과 애니메이션](#10-모션과-애니메이션)
11. [테마](#11-테마)
12. [컴포넌트와 컬렉션](#12-컴포넌트와-컬렉션)
13. [비동기 데이터 Query](#13-비동기-데이터-query)
14. [나머지 런타임 모듈 지도](#14-나머지-런타임-모듈-지도)
15. [선택 통합 모듈](#15-선택-통합-모듈)
16. [디버깅과 검증](#16-디버깅과-검증)
17. [컴파일과 테스트](#17-컴파일과-테스트)
18. [이번 Time 이름 충돌에서 배우는 디버깅](#18-이번-time-이름-충돌에서-배우는-디버깅)
19. [소스를 읽고 기능을 추가하는 방법](#19-소스를-읽고-기능을-추가하는-방법)
20. [추천 실습 순서](#20-추천-실습-순서)
21. [용어 사전](#21-용어-사전)

## 1. NexUI가 무엇인가

Unity에는 대표적인 UI 방식이 두 개 있다.

- **uGUI**: `Canvas`, `RectTransform`, `Button`, `Image`, TextMeshPro를 사용한다.
- **UI Toolkit**: `UIDocument`, UXML, USS, `VisualElement`를 사용한다.

두 방식은 API가 서로 다르다. 게임 코드가 `Button`, `RectTransform`, `VisualElement`를 직접
사용하기 시작하면 나중에 UI 방식을 바꾸기 어렵다. NexUI는 이 차이를 중간 계층으로 감싼다.

예를 들어 게임 코드는 “이 요소의 텍스트를 바꿔라”라고 요청한다. uGUI에서는 TextMeshPro의
텍스트를 바꾸고, UI Toolkit에서는 Label의 텍스트를 바꾸는 구체적인 작업을 각 Integration이
맡는다.

핵심 생각은 다음 한 문장으로 정리할 수 있다.

> 게임 코드는 UI가 무엇을 해야 하는지만 말하고, uGUI/UI Toolkit의 구체적인 조작은 백엔드가 맡는다.

NexUI가 담당하는 주요 기능은 다음과 같다.

- 화면 등록, 열기, 닫기, 토글, 뒤로 가기
- HUD, 창, 모달, 토스트 같은 레이어 순서 관리
- 게임 상태를 UI 텍스트나 게이지에 자동 반영하는 바인딩
- 버튼 액션과 커맨드 실행
- 화면 열기/닫기 모션
- 색상, 간격, 반지름 같은 테마 토큰
- 비동기 데이터의 로딩/성공/빈 결과/실패 처리
- 디버그 오버레이, 검증기, 테스트 도우미

## 2. 먼저 알아야 할 Unity와 C# 용어

### 2.1 클래스와 인스턴스

클래스는 설계도이고 인스턴스는 실제로 만들어진 물건이다.

```csharp
UIStateStore store = new UIStateStore();
```

- `UIStateStore`는 클래스 이름이다.
- `new UIStateStore()`는 실제 저장소 하나를 만든다.
- `store`는 그 저장소를 가리키는 변수다.

### 2.2 네임스페이스와 using

네임스페이스는 같은 이름끼리 충돌하지 않도록 코드를 분류하는 주소다.

```csharp
using emiteat.NexUI.Core;
using emiteat.NexUI.State;
```

`using`을 적으면 이후 코드에서 긴 전체 이름 대신 `UIManager`, `UIStateStore`처럼 짧게 쓸 수
있다. 단, 서로 다른 네임스페이스에 같은 이름이 있으면 컴파일러가 어느 것을 뜻하는지 알지
못하거나 의도와 다르게 해석할 수 있다. 18장에서 실제 사례를 설명한다.

### 2.3 MonoBehaviour

`MonoBehaviour`를 상속한 클래스는 GameObject에 컴포넌트로 붙일 수 있다.

```csharp
public sealed class InventoryController : MonoBehaviour
{
    private void Start() { }
    private void Update() { }
}
```

- `Awake`: 오브젝트가 준비될 때 한 번 호출된다.
- `Start`: 첫 프레임 직전에 한 번 호출된다.
- `Update`: 활성화된 동안 매 프레임 호출된다.
- `OnDestroy`: 오브젝트가 제거될 때 호출된다.

NexUI 백엔드 부트스트랩은 `Awake`에서 등록된다. 따라서 일반적인 화면 열기 코드는 `Start`에서
실행하는 것이 이해하기 쉽다.

### 2.4 ScriptableObject

`UIScreenDefinition`, `UITheme`, `UIMotionPreset`은 `ScriptableObject`다. 씬 오브젝트가 아니라
프로젝트 창에 저장되는 데이터 에셋이다. 같은 화면 설정을 여러 씬에서 재사용할 수 있다.

### 2.5 async, await, UniTask

화면 모션이나 리소스 생성은 한 프레임 안에 끝나지 않을 수 있다. 그래서 NexUI의 주요 API는
UniTask를 반환한다.

```csharp
await NexUIApp.OpenAsync("Inventory");
```

`await`는 “화면 열기 작업이 완료될 때까지 이 메서드의 다음 줄을 잠시 기다린다”는 뜻이다.
Unity 이벤트 메서드에서는 다음처럼 시작할 수 있다.

```csharp
private async void Start()
{
    await NexUIApp.OpenAsync("HUD");
    Debug.Log("HUD 열기 완료");
}
```

`NexUIApp.Open("HUD")`처럼 반환값을 기다리지 않는 편의 함수도 있지만, 오류 위치와 실행 순서를
파악하기 쉬운 `OpenAsync` + `await`를 먼저 익히는 것을 권장한다.

### 2.6 인터페이스와 Capability

인터페이스는 “이 기능을 제공한다”는 약속이다. NexUI 요소는 구체적인 uGUI 타입 대신 Capability를
제공한다.

- `IUITextCapability`: 텍스트를 읽고 쓸 수 있음
- `IUIValueCapability`: 숫자 값을 읽고 쓸 수 있음
- `IUIVisibilityCapability`: 보이기/숨기기 가능
- `IUIClickCapability`: 클릭 이벤트 제공
- `IUITransformCapability`: 위치, 크기, 회전, 투명도 변경 가능

따라서 `UITextBinder`는 대상이 TextMeshPro인지 UI Toolkit Label인지 몰라도 된다. 대상이
`IUITextCapability`만 제공하면 작동한다.

## 3. 프로젝트 구조

패키지의 실제 위치는 다음과 같다.

```text
Packages/com.nexengineworks.nexui/
├─ Runtime/          게임 실행 중 사용하는 백엔드 독립 코드
├─ Integrations/     uGUI, UI Toolkit, 외부 패키지 연결 코드
├─ Editor/           Unity Editor 전용 도구
├─ Samples~/         Package Manager에서 가져올 수 있는 샘플
├─ Tests/            EditMode와 PlayMode 테스트
├─ Documentation~/   패키지 문서
└─ package.json      버전, 의존성, 샘플 목록
```

### 3.1 가장 중요한 의존성 규칙

```text
게임 코드
   ↓
Core / State / Motion / Theme / Components
   ↓
Abstractions

Integrations.UGUI 또는 Integrations.UIToolkit
   ↓
Unity의 실제 UI 타입
```

Core는 `Button`, `RectTransform`, `VisualElement` 같은 구체적인 UI 타입을 알면 안 된다. 이 규칙을
지키면 같은 게임 로직을 두 백엔드에서 사용할 수 있다.

### 3.2 자주 보는 폴더

| 폴더 | 하는 일 | 처음 배울 중요도 |
|---|---|---:|
| `Runtime/Abstractions` | 인터페이스와 Capability 계약 | 높음 |
| `Runtime/Core` | 화면, 레이어, 포커스, 정책, 커맨드 | 매우 높음 |
| `Runtime/State` | 상태 저장소와 바인딩 | 매우 높음 |
| `Runtime/Motion` | 기본 화면 애니메이션 | 중간 |
| `Runtime/Theme` | 테마와 디자인 토큰 | 중간 |
| `Runtime/Components` | 공통 UI 컴포넌트 계약과 컬렉션 | 중간 |
| `Integrations/UGUI` | uGUI 구현 | uGUI 사용 시 높음 |
| `Integrations/UIToolkit` | UI Toolkit 구현 | UI Toolkit 사용 시 높음 |
| `Runtime/Query` | 비동기 데이터 상태 | 나중 |
| `Runtime/Debug` | 실행 상태 확인 | 높음 |

## 4. NexUI가 화면을 여는 과정

`await NexUIApp.OpenAsync("Inventory")`를 호출하면 내부에서는 대략 다음 순서로 처리된다.

1. `UIScreenRegistry`에서 `Inventory` 정의를 찾는다.
2. 정의에 적힌 백엔드가 uGUI인지 UI Toolkit인지 확인한다.
3. 해당 `IUIScreenFactory`로 프리팹 또는 VisualTreeAsset을 인스턴스화한다.
4. 화면을 HUD, Window, Modal 같은 레이어 컨테이너 아래에 붙인다.
5. 입력 차단, 커서, 시간 정지, 포커스 정책을 적용한다.
6. 열기 모션이 있으면 재생한다.
7. 화면 상태를 `Open`으로 바꾸고 `ScreenOpened` 이벤트를 발생시킨다.

닫을 때는 닫기 모션, 포커스 복구, 정책 해제, 화면 제거 또는 보관이 반대 순서로 진행된다.

여기에서 꼭 기억할 객체는 세 개다.

- `UIScreenDefinition`: 화면 하나의 설정 데이터
- `UIManager`: 등록된 화면과 현재 열린 화면을 관리하는 실제 관리자
- `NexUIApp`: 공유 `UIManager`를 편하게 호출하는 정적 입구

## 5. 첫 화면 만들기

처음에는 uGUI와 UI Toolkit 중 하나만 선택한다. 둘을 동시에 배우면 어떤 부분이 NexUI이고 어떤
부분이 Unity 백엔드인지 헷갈리기 쉽다.

### 5.1 uGUI 방식

#### 씬 준비

1. Hierarchy에서 `Canvas`를 만든다.
2. 씬에 `EventSystem`이 있는지 확인한다.
3. Canvas GameObject에 `UGUIIntegrationBootstrap`을 추가한다.
4. 화면으로 사용할 UI를 별도 프리팹으로 만든다.

부트스트랩은 실행 시 Canvas 아래에 다음 레이어 컨테이너들을 만든다.

```text
NexUI.Layer.Background
NexUI.Layer.HUD
NexUI.Layer.Window
NexUI.Layer.Modal
NexUI.Layer.Toast
NexUI.Layer.Overlay
```

#### 요소 ID 지정

NexUI가 프리팹 내부 요소를 찾으려면 요소에 ID가 필요하다. 대상 GameObject에
`NxUGuiBindingTag`를 붙이고 `elementId`를 지정한다.

예시:

| GameObject | elementId |
|---|---|
| 이름 텍스트 | `nameLabel` |
| HP 슬라이더 | `hpBar` |
| 닫기 버튼 | `closeButton` |

ID는 대소문자를 구분하므로 `closeButton`과 `CloseButton`은 다른 값이다.

### 5.2 UI Toolkit 방식

1. Hierarchy에서 `UIDocument`를 만든다.
2. `PanelSettings`를 연결한다.
3. 같은 GameObject에 `UIToolkitIntegrationBootstrap`을 추가한다.
4. 화면으로 사용할 UXML `VisualTreeAsset`을 만든다.
5. USS가 필요하면 별도 StyleSheet를 만든다.

UI Toolkit은 요소의 `name`을 NexUI ID로 사용한다.

```xml
<ui:Label name="nameLabel" text="Hero" />
<ui:ProgressBar name="hpBar" value="1" />
<ui:Button name="closeButton" text="Close" />
```

### 5.3 UIScreenDefinition 만들기

Project 창에서 `Create > NexUI > Screen Definition`을 선택한다. 예를 들어
`HUDScreenDefinition.asset`을 만든 뒤 다음처럼 설정한다.

- `identity.screenId`: `HUD`
- `backendAsset.backend`: `UGUI` 또는 `UIToolkit`
- `backendAsset.asset`: uGUI 프리팹 또는 VisualTreeAsset
- `layer.layerType`: `HUD`
- `layer.openPolicy`: `Single`
- `policy.closeOnBack`: HUD라면 보통 끔

`screenId`는 파일 이름이 아니라 코드에서 사용하는 고유 키다. 같은 ID를 가진 정의를 두 개
등록하지 않는다.

## 6. 화면을 코드로 열고 닫기

다음 스크립트를 빈 GameObject에 붙이고 Inspector에서 화면 정의를 연결한다.

```csharp
using UnityEngine;
using emiteat.NexUI.Core;

namespace MyGame.UI
{
    public sealed class GameUIBootstrap : MonoBehaviour
    {
        [SerializeField] private UIScreenDefinition hud;
        [SerializeField] private UIScreenDefinition inventory;

        private async void Start()
        {
            NexUIApp.RegisterScreen(hud);
            NexUIApp.RegisterScreen(inventory);

            await NexUIApp.OpenAsync("HUD");
        }
    }
}
```

한 줄씩 읽어 보면 다음과 같다.

- `[SerializeField]`: private 필드를 Inspector에 표시한다.
- `RegisterScreen`: 관리자가 화면 ID와 정의를 알 수 있게 등록한다.
- `OpenAsync`: 등록된 ID의 화면을 연다.
- `await`: 열기 모션까지 끝날 때 기다린다.

자주 쓰는 API는 다음과 같다.

```csharp
await NexUIApp.OpenAsync("Inventory");
await NexUIApp.CloseAsync("Inventory");
await NexUIApp.ToggleAsync("Inventory");
await NexUIApp.BackAsync();

bool isOpen = NexUIApp.IsOpen("Inventory");
```

입력 예제:

```csharp
private void Update()
{
    if (Input.GetKeyDown(KeyCode.I))
        NexUIApp.Toggle("Inventory");

    if (Input.GetKeyDown(KeyCode.Escape))
        NexUIApp.Back();
}
```

`Toggle`과 `Back`은 기다리지 않는 편의 API다. 새 코드를 학습하거나 실행 순서가 중요한 로직은
가능하면 Async 버전을 사용한다.

### 6.1 자동 설정 등록

화면마다 `RegisterScreen`을 직접 쓰는 대신 `NexUISettings` 에셋에 화면, 테마, 모션을 모을 수
있다. 자동 부트스트랩을 사용하려면 에셋 파일 이름을 `NexUISettings`로 하고 Unity의 정확한
`Resources` 폴더 안에 둔다.

```text
Assets/Resources/NexUISettings.asset
```

에셋의 `bootstrapMode`를 `RuntimeInitializeOnLoad`로 설정한다. 백엔드 팩토리와 레이어는 여전히
씬의 `UGUIIntegrationBootstrap` 또는 `UIToolkitIntegrationBootstrap`이 제공한다.

## 7. 상태와 바인딩

UI를 직접 갱신하는 가장 단순한 방식은 점수 변경 때마다 텍스트를 찾아 값을 넣는 것이다. 하지만
화면이 많아지면 게임 로직과 UI 코드가 강하게 엉킨다. NexUI에서는 상태 저장소에 값을 넣고,
바인더가 UI를 자동으로 갱신하게 할 수 있다.

### 7.1 UIStateStore

```csharp
UIStateStore store = new UIStateStore();

store.Set("player.name", "Hero");
store.Set("player.hp", 1f);

string playerName = store.Get<string>("player.name");
float hp = store.Get<float>("player.hp");
```

상태 키는 문자열이다. 프로젝트 전체에서 같은 규칙을 정하는 것이 좋다.

```text
player.name
player.hp
inventory.gold
settings.masterVolume
quest.activeCount
```

`Get<T>`의 `T`는 저장한 타입과 같아야 한다. `1f`는 float이고 `1`은 int이므로 서로 다르다.

### 7.2 Watch

```csharp
IDisposable subscription = store.Watch<float>("player.hp", value =>
{
    Debug.Log($"HP가 {value}로 변경됨");
});

store.Set("player.hp", 0.5f);

subscription.Dispose();
```

`Dispose`하지 않으면 더 이상 필요하지 않은 콜백이 계속 남을 수 있다. 바인더도 사용이 끝날 때
`Unbind`해야 한다.

### 7.3 화면 요소 찾기

```csharp
IUISurface surface = NexUIApp.Manager.GetSurface("HUD");
IUIElementHandle nameHandle = surface.TryFind("nameLabel");
```

- `GetSurface`: 현재 열린 화면의 표면을 가져온다. 닫혀 있으면 `null`이다.
- `TryFind`: 요소가 없으면 `null`을 반환한다.
- `FindRequired`: 요소가 없으면 `UIElementNotFoundException`을 발생시킨다.

초기 실습에는 `TryFind`가 편하고, 반드시 존재해야 하는 요소를 빠르게 검증하려면
`FindRequired`가 좋다.

### 7.4 텍스트와 게이지 바인딩

```csharp
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.State;

private readonly UIStateStore store = new UIStateStore();
private UITextBinder nameBinder;
private UIValueBinder hpBinder;

private async void Start()
{
    store.Set("player.name", "Hero");
    store.Set("player.hp", 1f);

    await NexUIApp.OpenAsync("HUD");

    IUISurface surface = NexUIApp.Manager.GetSurface("HUD");

    nameBinder = new UITextBinder();
    nameBinder.Bind(surface.FindRequired("nameLabel"), "player.name", store);

    hpBinder = new UIValueBinder();
    hpBinder.Bind(surface.FindRequired("hpBar"), "player.hp", store);

    store.Set("player.hp", 0.75f);
}

private void OnDestroy()
{
    nameBinder?.Unbind();
    hpBinder?.Unbind();
}
```

바인더 종류:

| 바인더 | 용도 |
|---|---|
| `UITextBinder` | 문자열이나 숫자를 텍스트로 표시 |
| `UIValueBinder` | Slider, ProgressBar 같은 float 값 |
| `UIVisibilityBinder` | 상태에 따라 보이기/숨기기 |
| `UIClassBinder` | 스타일 클래스 토글 |
| `UICommandBinder` | 클릭을 액션 키에 연결 |

`UIBindingMode`에는 세 가지가 있다.

- `OneWay`: 상태 → UI
- `TwoWay`: 상태 ↔ 편집 가능한 UI
- `OneWayToSource`: UI → 상태

입력 필드나 슬라이더를 양방향으로 연결할 때만 `TwoWay`부터 사용한다. 처음에는 `OneWay`가 가장
안전하다.

## 8. 버튼과 액션

`UIActionResolver`는 문자열 키와 실제 C# 동작을 연결한다. Singleton이 아니므로 원하는 생명주기에
맞춰 인스턴스를 직접 만든다.

```csharp
private readonly UIStateStore store = new UIStateStore();
private readonly UIActionResolver actions = new UIActionResolver();
private UICommandBinder closeBinder;

private async void Start()
{
    actions.Register("inventory.close", async () =>
    {
        await NexUIApp.CloseAsync("Inventory");
    });

    await NexUIApp.OpenAsync("Inventory");
    IUISurface surface = NexUIApp.Manager.GetSurface("Inventory");

    closeBinder = new UICommandBinder(actions);
    closeBinder.Bind(
        surface.FindRequired("closeButton"),
        "inventory.close",
        store);
}

private void OnDestroy()
{
    closeBinder?.Unbind();
}
```

버튼이 작동하지 않을 때는 다음 순서로 확인한다.

1. `closeButton` ID가 실제 요소와 정확히 같은가?
2. 대상이 클릭 Capability를 제공하는 Button인가?
3. `inventory.close` 액션을 먼저 등록했는가?
4. 바인더가 화면이 열린 뒤 연결됐는가?
5. Console에 Capability 경고가 있는가?

### 8.1 Command Pipeline과 차이

`UIActionResolver`는 문자열 기반의 간단한 버튼 동작 연결에 적합하다. `UICommandDispatcher`는
로깅, 미들웨어, 재생, Undo 같은 구조가 필요한 큰 프로젝트에 적합하다.

```csharp
UICommandDispatcher dispatcher = new UICommandDispatcher();
dispatcher.UseMiddleware(new LoggingMiddleware());
dispatcher.RegisterHandler(new SetValueCommandHandler(store));

await dispatcher.DispatchAsync(
    new SetValueCommand("inventory.gold", 999, previousValue: 100));
```

처음에는 ActionResolver로 시작하고, 실행 기록이나 Undo가 필요해질 때 Command Pipeline으로
옮겨 가면 된다.

## 9. 레이어와 화면 정책

### 9.1 레이어

낮은 레이어에서 높은 레이어 순으로 렌더링된다.

| 레이어 | 대표 용도 |
|---|---|
| `Background` | UI 배경 |
| `Scene` | 씬에 붙는 UI |
| `HUD` | 체력, 미니맵, 퀘스트 |
| `Window` | 인벤토리, 상점, 설정 |
| `Modal` | 확인창, 중요한 선택창 |
| `Toast` | 잠깐 표시되는 알림 |
| `Overlay` | 화면 전체 효과, 로딩막 |
| `System` | 최상위 시스템 UI |

기본 부트스트랩 배열에는 `Scene`과 `System`이 포함되지 않는다. 이 레이어를 사용하려면
부트스트랩 Inspector의 `_layers` 목록에 추가해야 한다.

### 9.2 Open Policy

| 정책 | 동작 |
|---|---|
| `Additive` | 같은 레이어의 다른 화면과 함께 표시 |
| `ReplaceLayer` | 같은 레이어의 다른 화면을 닫고 표시 |
| `Single` | 같은 화면 ID의 중복 열기를 막음 |
| `StackPush` | 뒤로 가기 스택에 추가 |
| `Queue` | 토스트처럼 하나씩 순서대로 표시 |

예시 추천:

- HUD: `HUD + Single`
- 인벤토리: `Window + StackPush` 또는 `Single`
- 설정 확인창: `Modal + StackPush`
- 획득 알림: `Toast + Queue`

### 9.3 Policy 필드

- `blockInputBehind`: 뒤 화면의 입력을 막는다.
- `pauseGameBehind`: 화면이 열린 동안 게임을 정지한다.
- `closeOnBack`: Back 요청으로 닫힐 수 있다.
- `cursorPolicy`: 커서 표시와 잠금 정책이다.
- `timePolicy`: 정상, 정지, 느리게 재생 중 하나다.
- `focusPolicy`: 기본 포커스 또는 포커스 트랩 정책이다.
- `conflictPolicy`: 열기/닫기 전환 요청이 겹칠 때 Wait, Cancel, Ignore 중 무엇을 할지 정한다.
- `lifetimePolicy`: 닫을 때 제거할지 보관할지 정한다.

모달은 보통 `blockInputBehind = true`, `trapFocus = true`, 기본 포커스 버튼 ID 설정을 함께 사용한다.

## 10. 모션과 애니메이션

### 10.1 기본 모션 흐름

1. `UIMotionPreset` 에셋에 사람이 편집하기 좋은 단계를 저장한다.
2. `MotionCompiler`가 런타임용 `UIMotionTimeline`으로 변환한다.
3. `BuiltInMotionPlayer`가 Capability를 통해 값을 적용한다.

`UIScreenDefinition.motion.openMotion`과 `closeMotion`에 프리셋을 연결하면 화면 열기와 닫기에 자동
재생된다.

### 10.2 코드로 프리셋 만들기

```csharp
UIMotionPreset preset = ScriptableObject.CreateInstance<UIMotionPreset>();
preset.motionId = "inventory.open";
preset.defaultVariant = "default";
preset.variants = new[]
{
    new UIMotionVariant
    {
        name = "default",
        steps = new[]
        {
            UIMotionStep.Fade(0f, 1f, 0.2f),
            new UIMotionStep
            {
                property = UIMotionProperty.ScaleX,
                from = 0.9f,
                to = 1f,
                duration = 0.2f,
                easing = UIMotionEasing.EaseInOut
            }
        }
    }
};
```

처음에는 에셋을 Inspector에서 만드는 편이 쉽다. 코드 생성은 테스트나 동적 프리셋이 필요할 때
사용한다.

### 10.3 Motion과 MotionClip의 차이

- `Motion`: 요소 하나의 Fade, Position, Scale, Rotation 같은 간단한 전환에 적합하다.
- `MotionClip`: 여러 요소와 여러 트랙을 키프레임으로 함께 움직이는 타임라인에 적합하다.
- `MotionGraph`: 순서, 병렬, 조건, 반복, 지연, 서브그래프 같은 실행 흐름에 적합하다.

처음에는 `UIMotionPreset`만 사용한다. 여러 요소의 연출이 꼭 필요해졌을 때 MotionClip으로 넘어간다.

### 10.4 시간 정지와 애니메이션

내장 모션 플레이어는 `UnityEngine.Time.unscaledDeltaTime`을 사용한다. 게임의 `timeScale`이 0이어도
메뉴 모션이 계속 재생되도록 하기 위한 선택이다.

## 11. 테마

테마는 색상이나 간격을 하드코딩하지 않고 문자열 키로 관리한다.

대표 토큰:

```text
color.bg
color.surface
color.primary
color.danger
color.text
space.sm
space.md
radius.md
motion.fast
```

테마 등록과 전환:

```csharp
NexUITheme.Registry.Register(darkTheme);
NexUITheme.Registry.Register(lightTheme);

NexUITheme.Use("dark");
string primary = NexUITheme.GetToken("color.primary");
```

런타임 오버라이드:

```csharp
NexUITheme.SetToken("color.primary", "#3B82F6");
```

`SetToken`은 원본 에셋 파일을 수정하는 것이 아니라 실행 중 오버라이드를 적용한다. 테마 적용은
각 백엔드의 `UGUIThemeApplier` 또는 `UIToolkitThemeApplier`가 맡는다.

## 12. 컴포넌트와 컬렉션

`Runtime/Components`에는 백엔드 독립적인 컴포넌트 계약이 있다.

- 버튼, 프로그레스 바
- 모달, 토스트, 툴팁, 팝오버
- 리스트, 그리드, 슬롯, 선택 목록
- 스피너, 스켈레톤, 원형 게이지
- 컬렉션 가상화와 선택 처리

uGUI와 UI Toolkit Integration에는 실제 구현이 있다. 예를 들면 다음과 같다.

- 텍스트 마키, 타이프라이터, 숫자 카운터
- 길게 눌러 실행하는 버튼
- 스와이프 영역
- 가상 리스트, 캐러셀, 탭 그룹
- 그라디언트, 그림자, 쿨다운 표시

대량 목록에는 모든 행을 한 번에 만들지 않고 보이는 행만 만드는 가상화가 중요하다.
`NXCollectionController`가 표시 범위, 선택, 스크롤 위치 계산을 백엔드와 분리해 담당한다.

처음 실습에서는 `Samples~/CollectionDemo`를 Import해 10,000개 행이 어떻게 재사용되는지 확인하는
것이 가장 빠르다.

## 13. 비동기 데이터 Query

Query는 서버나 저장 파일에서 데이터를 가져올 때 상태를 표준화한다.

```text
Idle → Loading → Success
               ├─ Empty
               └─ Error
```

기본 예제:

```csharp
QueryCache cache = new QueryCache();

UIQuery<PlayerProfile> query = new UIQuery<PlayerProfile>(
    new QueryKey("player.profile"),
    cancellationToken => LoadProfileAsync(cancellationToken),
    cache);

query.State.Subscribe(state =>
{
    Debug.Log($"현재 쿼리 상태: {state.Status}");
});

await query.RunAsync();
```

`LoadingBoundary`, `ErrorBoundary`, `EmptyBoundary`, `FallbackScreen`을 사용하면 상태에 따라 관련 UI
요소를 자동으로 표시할 수 있다. Query는 재시도와 캐시도 지원한다.

주의할 점은 재시도 지연 후 코드가 Unity 메인 스레드가 아닌 곳에서 이어질 가능성이 있다는 것이다.
Unity 오브젝트를 직접 만질 때는 필요하면 메인 스레드로 전환한다.

## 14. 나머지 런타임 모듈 지도

아래 모듈은 모두 알아야 NexUI를 시작할 수 있는 필수 요소는 아니다. 프로젝트가 커질 때 해당 문제를
해결하기 위해 존재한다.

| 모듈 | 역할 | 언제 보는가 |
|---|---|---|
| `Accessibility` | 접근성 역할과 사용자 선호 | 키보드/스크린리더/감소된 모션 대응 |
| `Localization` | 게임용 현지화 테이블 | 언어 변경 기능을 만들 때 |
| `Prompt` | 현재 입력 장치와 버튼 글리프 | 키보드·패드 아이콘을 바꿀 때 |
| `Templates` | 재사용 가능한 UI Recipe | 화면 구조 템플릿이 필요할 때 |
| `MotionClip` | 다중 요소 키프레임 타임라인 | 복잡한 연출을 만들 때 |
| `MotionGraph` | 조건·병렬·반복 모션 흐름 | 연출 로직이 분기될 때 |
| `Diagnostics` | 진단 코드, 심각도, 소스 위치, 로그 | 에디터/컴파일 진단을 표준화할 때 |
| `Compiled` | Designer가 만든 런타임 프로그램 데이터 | 비주얼 저작 결과를 실행할 때 |
| `Flow` | 실행 단계 추적과 메모리/콘솔 Sink | 복잡한 실행 경로를 관찰할 때 |
| `Interaction` | 트리거를 커맨드와 상태에 연결 | 컴파일된 상호작용 규칙 실행 |
| `Scenario` | 자동 시나리오 실행과 결과 | UI 흐름 테스트/데모 자동화 |
| `Time` | 실제 시간과 수동 테스트 시간 공급원 | 결정적인 테스트나 타임라인 제어 |
| `Overrides` | 속성 변경 출처와 우선순위 기록 | Designer/런타임/반응형 덮어쓰기 추적 |
| `Settings` | 화면·테마·모션 일괄 등록 | 코드 없는 프로젝트 초기화 |
| `Debug` | 실행 상태 스냅샷과 오버레이 | 문제를 재현하고 내부 상태 확인 |

### 14.1 Core의 고급 기능

Core에는 기본 화면 열기 외에도 다음 기능이 있다.

- 화면 관계: 부모 화면, 같이 열 화면, 열 때 닫을 화면
- Variant와 해상도/입력 모드 Responsive Rule
- 화면 생명주기와 전환 충돌 정책
- 세션의 열린 화면을 PlayerPrefs에 저장/복구
- Deep Link 라우팅
- 포커스 내비게이션 그래프
- 화면별 오류 격리와 `ScreenFaulted` 이벤트
- `UITestHarness` 기반 통합 테스트

처음부터 이 기능들을 모두 켜지 말고, 실제 요구사항이 생길 때 하나씩 추가한다.

## 15. 선택 통합 모듈

`Integrations` 아래 선택 모듈은 해당 외부 패키지가 설치된 경우에만 사용한다.

| 통합 | 역할 |
|---|---|
| `DOTween` | 내장 모션 대신 DOTween 플레이어 사용 |
| `VContainer` | UIManager와 서비스의 DI 등록 |
| `MessagePipe` | 화면/커맨드/모션 이벤트를 메시지로 발행 |
| `Addressables` | Addressables 리소스 공급자 |
| `InputSystem` | 모달에 따라 Gameplay/UI 액션맵 전환 |

외부 패키지가 없는 상태에서 통합 asmdef를 무리하게 활성화하면 컴파일 오류가 날 수 있다. 먼저
패키지 버전과 define 설정을 확인한다.

## 16. 디버깅과 검증

### 16.1 Console 오류 읽기

Unity 오류는 보통 다음 형식이다.

```text
파일경로(줄,열): error CS번호: 설명
```

처음 볼 순서:

1. 가장 위의 첫 번째 C# 오류를 본다.
2. 파일과 줄 번호를 연다.
3. `CS0234`, `CS0246`, `CS0104` 같은 오류 코드를 확인한다.
4. 같은 원인으로 반복된 오류는 하나를 고친 뒤 다시 컴파일한다.

뒤쪽 오류는 첫 오류 때문에 연쇄적으로 생기는 경우가 많다.

### 16.2 런타임 디버그 오버레이

```csharp
NexUIDebug.Configure(
    manager: NexUIApp.Manager,
    stateStore: store,
    actions: actions);

NexUIDebug.ToggleOverlay();
```

오버레이와 스냅샷으로 열린 화면, 레이어, 뒤로 가기 스택, 상태 키, 액션 키, 커맨드 로그, Query
캐시를 확인할 수 있다.

### 16.3 Validator

Unity 메뉴의 `Tools > NexUI > Validator`를 사용하면 다음 문제를 찾을 수 있다.

- 중복 Screen ID
- 백엔드와 에셋 타입 불일치
- 화면 에셋 누락
- 모달 기본 포커스 누락
- 레이어 정책 오류
- Variant/Responsive Rule 오류
- 사용할 수 없는 화면 Factory

### 16.4 흔한 문제 체크리스트

#### 화면이 열리지 않는다

- 화면 정의를 등록했는가?
- 문자열 Screen ID가 정확한가?
- 씬에 올바른 백엔드 Bootstrap이 있는가?
- uGUI 정의에 GameObject 프리팹을 넣었는가?
- UI Toolkit 정의에 VisualTreeAsset을 넣었는가?
- 해당 Layer가 Bootstrap 배열에 있는가?

#### 바인딩이 작동하지 않는다

- 화면이 열린 뒤 Surface를 가져왔는가?
- uGUI에 `NxUGuiBindingTag`와 `elementId`가 있는가?
- UI Toolkit 요소의 `name`이 맞는가?
- `Set`에 저장한 타입과 Binder가 기대하는 타입이 같은가?
- Capability 경고가 있는가?
- Binder 인스턴스를 지역 변수로만 만들고 잃어버리지 않았는가?

#### 버튼이 두 번 실행된다

- 같은 Binder를 중복 연결하지 않았는가?
- 닫거나 파괴할 때 `Unbind`했는가?
- Unity Inspector의 Button OnClick과 NexUI Binder가 같은 동작을 모두 호출하지 않는가?

#### 모션이 안 보인다

- 화면 정의에 open/close motion이 연결됐는가?
- `MotionPlayer`와 `MotionResolver`가 등록됐는가?
- 대상이 Transform Capability를 제공하는가?
- 호출 인자의 `suppressMotion`이 true가 아닌가?

## 17. 컴파일과 테스트

### 17.1 Unity에서 확인

1. 스크립트 저장
2. Unity로 돌아가 Asset Refresh 대기
3. Console의 첫 오류 확인
4. Play Mode 실행
5. 실제 클릭, 포커스, 화면 전환 확인

### 17.2 PowerShell에서 어셈블리 빌드

프로젝트 루트 `E:\UnityProjects\NexUI`에서 실행한다.

```powershell
dotnet build emiteat.NexUI.Core.csproj --no-restore --nologo
dotnet build emiteat.NexUI.Integrations.UGUI.csproj --no-restore --nologo
dotnet build emiteat.NexUI.Integrations.UIToolkit.csproj --no-restore --nologo
```

전체 솔루션은 프로젝트 수가 많아 오래 걸릴 수 있다.

```powershell
dotnet build NexUI.sln --no-restore --nologo
```

`dotnet build` 성공은 C# 컴파일 성공을 뜻한다. Unity Play Mode에서 레이아웃, 입력, 모션이 실제로
올바른지는 별도로 확인해야 한다.

### 17.3 테스트 종류

- EditMode: 순수 로직, 레지스트리, 상태, 컴파일러 같은 빠른 테스트
- PlayMode: GameObject, 프레임, 실제 백엔드 동작이 필요한 테스트
- `UITestHarness`: Screen ID와 Command를 기준으로 화면 흐름을 검사
- `Scenario`: 여러 단계의 UI 시나리오를 데이터처럼 실행

버그를 고칠 때는 가능하면 “수정 전 실패, 수정 후 성공”하는 작은 회귀 테스트를 함께 만든다.

## 18. 이번 Time 이름 충돌에서 배우는 디버깅

### 18.1 증상

다음과 같은 오류가 여러 파일에서 발생했다.

```text
error CS0234: 'unscaledDeltaTime' does not exist in the namespace 'emiteat.NexUI.Time'
```

코드는 겉으로 보면 정상처럼 보인다.

```csharp
_elapsed += Time.unscaledDeltaTime;
```

하지만 NexUI에는 `emiteat.NexUI.Time`이라는 자체 네임스페이스가 있다. 현재 코드가
`emiteat.NexUI.*` 아래에 있기 때문에 컴파일러가 `Time`을 `UnityEngine.Time` 클래스가 아니라
`emiteat.NexUI.Time` 네임스페이스로 먼저 해석했다.

### 18.2 해결 방법

Unity의 Time 클래스에 별칭을 붙였다.

```csharp
using UnityTime = UnityEngine.Time;
```

사용하는 곳도 명확하게 바꿨다.

```csharp
_elapsed += UnityTime.unscaledDeltaTime;
```

이제 사람과 컴파일러 모두 어떤 Time인지 바로 알 수 있다.

### 18.3 완전한 이름을 쓰는 방법

별칭 대신 다음처럼 전체 이름을 써도 된다.

```csharp
_elapsed += UnityEngine.Time.unscaledDeltaTime;
```

사용 횟수가 적으면 전체 이름이 간단하다. 같은 파일에서 여러 번 쓰면 `UnityTime` 별칭이 읽기 좋다.

### 18.4 이 사례의 일반적인 교훈

- 오류 메시지에 나온 실제 해석 결과를 믿고 읽는다.
- `using UnityEngine;`이 있어도 더 가까운 네임스페이스 이름이 우선될 수 있다.
- 이름 충돌은 별칭이나 완전한 이름으로 의도를 명시한다.
- 한 파일만 고치지 말고 같은 패턴을 전체 패키지에서 검색한다.
- 수정 후 영향받는 어셈블리를 각각 빌드한다.

PowerShell 검색 예:

```powershell
rg -n "\bTime\.(deltaTime|unscaledDeltaTime|timeScale)" Packages/com.nexengineworks.nexui -g "*.cs"
```

## 19. 소스를 읽고 기능을 추가하는 방법

### 19.1 기능 하나를 세 층으로 나눈다

예를 들어 “체크박스 컴포넌트”를 추가한다고 가정한다.

1. `Abstractions` 또는 `Components`에 백엔드 독립 계약을 만든다.
2. `Integrations/UGUI`에 Toggle 기반 구현을 만든다.
3. `Integrations/UIToolkit`에 Toggle 기반 구현을 만든다.
4. 게임 코드는 계약만 사용한다.
5. 두 백엔드 테스트를 각각 작성한다.

Core에 uGUI의 `Toggle`을 직접 넣으면 백엔드 분리 규칙이 깨진다.

### 19.2 새 화면 기능을 만드는 순서

1. 요구사항을 한 문장으로 쓴다.
2. 화면 ID와 요소 ID를 정한다.
3. ScreenDefinition과 백엔드 에셋을 만든다.
4. 화면을 등록하고 열기만 먼저 성공시킨다.
5. 상태 키와 바인딩을 하나씩 연결한다.
6. 버튼 액션을 연결한다.
7. 마지막에 모션과 테마를 붙인다.
8. 오류/빈 데이터/뒤로 가기 경로를 테스트한다.

한 번에 모든 기능을 붙이면 어느 단계에서 문제가 생겼는지 찾기 어렵다.

### 19.3 좋은 ID 규칙

```text
Screen ID: HUD, Inventory, PauseMenu, ConfirmQuit
Element ID: titleLabel, hpBar, itemList, closeButton
State key: player.hp, inventory.items, settings.volume
Action key: inventory.close, settings.save, quit.confirm
Motion ID: inventory.open, modal.close, toast.enter
Theme token: color.primary, space.md, radius.lg
```

ID 문자열은 가능하면 상수로 모아 오타를 줄인다.

```csharp
public static class UIIds
{
    public const string InventoryScreen = "Inventory";
    public const string CloseButton = "closeButton";
    public const string InventoryCloseAction = "inventory.close";
}
```

### 19.4 수정 전 확인할 것

- 수정하려는 파일이 Runtime, Integration, Editor 중 어느 층인가?
- asmdef가 필요한 의존성을 참조하는가?
- public API를 바꾸면 샘플과 문서도 바뀌는가?
- 두 백엔드에 동일한 기능이 필요한가?
- 화면이 닫힐 때 이벤트와 구독이 해제되는가?
- `timeScale = 0`에서도 돌아야 하는 UI인가?

## 20. 추천 실습 순서

### 실습 1: 화면 하나 열기

- HUD 프리팹 또는 UXML 만들기
- `UIScreenDefinition` 만들기
- Bootstrap 배치
- `NexUIApp.RegisterScreen`
- `NexUIApp.OpenAsync`

완료 조건: Play Mode에서 HUD가 보이고 Console 오류가 없다.

### 실습 2: 인벤토리 토글

- `Inventory` 화면 추가
- I 키로 `Toggle`
- Escape로 `Back`
- `Window + StackPush` 비교

완료 조건: 중복 화면이 생기지 않고 뒤로 가기가 작동한다.

### 실습 3: 체력 바인딩

- `player.hp` float 상태 만들기
- `hpBar`에 `UIValueBinder` 연결
- 키 입력으로 체력 감소
- 파괴 시 `Unbind`

완료 조건: 상태만 변경해도 게이지가 자동 갱신된다.

### 실습 4: 닫기 버튼

- `UIActionResolver` 생성
- `inventory.close` 등록
- `UICommandBinder` 연결

완료 조건: Inspector OnClick 없이 NexUI 액션으로 화면이 닫힌다.

### 실습 5: 모달과 포커스

- 확인창을 `Modal + StackPush`로 생성
- 뒤 입력 차단
- 기본 버튼 ID 설정
- 포커스 트랩 활성화

완료 조건: 모달 뒤의 버튼이 눌리지 않고 Back으로 정상 복귀한다.

### 실습 6: 모션과 테마

- Fade open/close preset 연결
- dark/light 테마 만들기
- 버튼으로 테마 전환

완료 조건: 게임 일시정지 중에도 UI 모션이 재생되고 테마가 바뀐다.

### 실습 7: 직접 버그 재현과 수정

- 별도 연습 네임스페이스 `MyGame.Time` 만들기
- `Time.deltaTime` 이름 충돌 재현
- `UnityTime` 별칭으로 수정
- 관련 csproj 빌드

완료 조건: 오류 메시지를 보고 이름 해석 문제를 스스로 설명할 수 있다.

## 21. 용어 사전

| 용어 | 쉬운 뜻 |
|---|---|
| Backend | 실제 UI를 그리는 방식. uGUI 또는 UI Toolkit |
| Bootstrap | 시작할 때 필요한 서비스를 등록하는 컴포넌트 |
| Screen | HUD, 인벤토리처럼 독립적으로 열고 닫는 UI 단위 |
| Surface | 백엔드와 무관하게 화면 전체를 다루는 손잡이 |
| Element Handle | 백엔드와 무관하게 UI 요소 하나를 가리키는 손잡이 |
| Capability | 요소가 제공하는 텍스트, 클릭, 값 같은 기능 계약 |
| State | UI가 표시할 현재 데이터 |
| Binding | 상태와 UI 요소를 자동 연결하는 것 |
| Action | 문자열 키에 연결된 실행 동작 |
| Command | 기록, 미들웨어, Undo가 가능한 구조화된 동작 |
| Layer | 화면이 앞뒤로 쌓이는 논리적 높이 |
| Policy | 화면이 열리고 닫힐 때 적용할 규칙 |
| Motion | 요소나 화면의 애니메이션 |
| Theme Token | 색상과 간격을 가리키는 재사용 키 |
| Query | 비동기 데이터 요청과 그 상태 |
| asmdef | Unity 스크립트 어셈블리의 경계와 참조 설정 |
| Namespace | 타입 이름 충돌을 막는 코드 주소 |
| UniTask | Unity에 맞춘 비동기 작업 타입 |

## 마지막 체크리스트

직접 만든 화면이 완성됐다고 판단하기 전에 다음을 확인한다.

- [ ] Screen ID가 유일하다.
- [ ] 올바른 백엔드 에셋을 연결했다.
- [ ] 필요한 Layer가 Bootstrap에 등록돼 있다.
- [ ] 요소 ID가 uGUI 태그 또는 UI Toolkit name과 일치한다.
- [ ] 상태 타입과 Binder 타입이 일치한다.
- [ ] 이벤트와 Binder를 `Unbind` 또는 `Dispose`한다.
- [ ] Back, 중복 열기, 빠른 연속 클릭을 테스트했다.
- [ ] 모달 뒤 입력과 포커스를 테스트했다.
- [ ] `timeScale = 0` 상황을 테스트했다.
- [ ] Console 경고와 오류가 없다.
- [ ] 관련 어셈블리가 빌드된다.
- [ ] Play Mode에서 실제 입력과 화면 배치를 확인했다.

## 다음에 읽을 문서와 샘플

- [설치](installation.md)
- [빠른 시작](quick-start.md)
- [프로젝트 설정](project-setup.md)
- [통합 모듈](integrations.md)
- [검증](validation.md)
- [커맨드 파이프라인](command-pipeline.md)
- `Samples~/BasicRuntime`
- `Samples~/UGUIRuntime`
- `Samples~/UIToolkitRuntime`
- `Samples~/CollectionDemo`
- `Samples~/MotionDemo`
- `Samples~/ThemeDemo`

