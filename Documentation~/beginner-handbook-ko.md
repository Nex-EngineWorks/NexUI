# NexUI 입문 핸드북 (15분)

대상: Unity UI는 만들어 봤지만 NexUI가 처음인 프로그래머/테크니컬 디자이너.

## 0. mental model 한 장 요약

```
게임 코드  ──▶  UIManager (Core)          ← 화면 수명·정책·네비게이션. 백엔드를 모름
                  │  IUIScreenFactory
        ┌─────────┴─────────┐
   UI Toolkit 백엔드      uGUI 백엔드      ← Integations/* 만이 실제 그래픽을 다룸
```

- **화면** = `UIScreenDefinition` 에셋(아이디/레이어/정책/모션) + 백엔드 자산(UXML 또는 프리팹).
- **요소 접근** = 문자열 id로 `IUIElementHandle`을 받아 **capability**(텍스트/값/색상…)만 사용.
- 백엔드 전환은 정의의 `backend` 필드 하나. 게임 코드는 무수정.

## 1. 설치 & 부트스트랩

1. 패키지 2개 임포트: `com.nexengineworks.nexui` (+ 선택) `com.nexengineworks.nexui.studio`.
2. 씬에 배치:
   - UI Toolkit: `UIDocument` + `UIToolkitIntegrationBootstrap`
   - uGUI: `Canvas` + `UGUIIntegrationBootstrap`
3. 끝. 부트스트랩이 팩토리/포커스/테마/모션 플레이어를 `NexUIApp.Manager`에 등록합니다.

## 2. 첫 화면 열기

```csharp
NexUIApp.RegisterScreen(myScreenDefinition);   // 보통 Project Setup이 자동 등록
await NexUIApp.OpenAsync("HUD");
```

열림 흐름: 부모 관계 → 충돌 정책(Wait/Cancel/Ignore) → 인스턴스 확용(KeepAlive/Pool) →
레이어 정렬 → OnBeforeOpen → 정책(커서/timeScale) → 오픈 모션 → OnAfterOpen → `ScreenOpened`.

## 3. 데이터 넣고 결과 받기 (다이얼로그 패턴)

```csharp
// 열면서 데이터 전달
await NexUIApp.OpenAsync("ItemPicker", new UIOpenArgs
{
    payload   = new Dictionary<string, object> { { "rarity", "epic" } },
    variantId = "compact"
});

// 닫힘 대기 + 결과 수신
var picked = await NexUIApp.WaitForCloseAsync("ItemPicker");
```

닫는 쪽:

```csharp
await NexUIApp.CloseAsync("ItemPicker", new UICloseArgs { result = "sword-042" });
```

백 제스처도 결과를 운반할 수 있습니다: `await NexUIApp.BackAsync<MyResult>(result);`

## 4. 상태 바인딩

런타임 값은 `UIStateStore` 키 하나로 연결됩니다.

```csharp
NexUIApp.Manager.State.Set("player.hp", 80f);     // State 모듈
```

Studio에서 요소의 Binding 섹션에 `valueKey = player.hp` 처럼 기입하면 바인더가
capability(`IUIValueCapability`)로 흘려 넣습니다. 텍스트/가시성/클래스/커맨드/인터랙터블 채널 동일.

## 5. 모션

- 간단: `UIScreenDefinition.motion.openMotion/closeMotion`에 프리셋 지정.
- 클립: Motion Clip Editor(`Tools > Nex/NexUI Studio`)에서 멀티 트랙 타임라인 작성 →
  `UIManager.PlayMotionClipAsync(screen, clip)`.

취소 안전: 진행 중 취소 시 마지막 포즈로 스냅되므로 반쯤 사라진 화면이 남지 않습니다.

## 6. 자주 쓰는 내비게이션

| API | 용도 |
|---|---|
| `OpenAsync / CloseAsync / ToggleAsync` | 기본 |
| `BackAsync()` / `BackAsync(result)` | 스택 팝 (StackPush 화면) |
| `CloseLayerAsync(layer)` | 레이어 전체 닫기 |
| `CloseOthersAsync(keepId)` | 하나만 남기고 전부 닫기 |
| `CloseAllAsync()` | 초기화 |
| `WaitForCloseAsync(id)` | 닫힘+결과 대기 |

## 7. 디버깅

- 런타임: `NexUIDebug.ShowOverlay()` — 열린 화면/스택/스테이트 키/커맨드 로그.
- 에디터: Studio 하단 Issues 패널, `Tools/NexUI/Validator`.
- 저장 실패 대비: Studio가 주기적으로 **오토세이브** 스냅샷을 기록 — More(⋮) 메뉴에서 복원.

## 8. 다음 단계

- [Integrations](integrations.md): DOTween/VContainer/MessagePipe/Addressables 연결.
- [Command Pipeline](command-pipeline.md): 커맨드·미들웨어·Undo.
- Studio(디자이너 툴)는 한국어/영어 메뉴를 모두 지원합니다 (`Language` 토글).
