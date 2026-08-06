# Optional integrations

NexUI keeps optional adapters in separate assemblies for DOTween, Addressables, Input System,
MessagePipe and VContainer. Each is gated by a define constraint, so an adapter whose third-party
package is absent is not compiled at all.

The core runtime remains backend independent. UI Toolkit and uGUI implementations live in
`Integrations/UIToolkit` and `Integrations/UGUI` respectively.

## How an integration is enabled

| 통합 | 정의 심볼 | 자동으로 붙는 조건 |
| --- | --- | --- |
| DOTween | `DOTWEEN` | DOTween의 **Setup DOTween...** 실행 시(플러그인 설치) 또는 `com.demigiant.dotween` UPM 설치 시 |
| Addressables | `NEXUI_HAS_ADDRESSABLES` | `com.unity.addressables` UPM 설치 |
| Input System | `NEXUI_HAS_INPUTSYSTEM` | `com.unity.inputsystem` UPM 설치 |
| MessagePipe | `NEXUI_HAS_MESSAGEPIPE` | `com.cysharp.messagepipe` UPM 설치 |
| VContainer | `NEXUI_HAS_VCONTAINER` | `jp.hadashikick.vcontainer` UPM 설치 |

> [!NOTE]
> DOTween은 UPM이 아니라 Asset Store 플러그인(`Assets/Plugins/Demigiant/`)으로 설치하는 경우가
> 훨씬 흔합니다. 그래서 DOTween 통합만 UPM 패키지 이름이 아니라 **DOTween 자신이 정의하는
> `DOTWEEN` 심볼**을 기준으로 삼습니다. 두 설치 방식 모두에서 동작합니다.

## DOTween: 두 방향

두 어댑터는 서로 반대 방향이고, 어느 쪽도 상대를 요구하지 않습니다.

**NexUI 모션을 DOTween으로 재생** (`DOTweenMotionPlayer`)
NexUI에서 저작한 모션을 DOTween이 실행합니다. 애니메이션을 NexUI로 옮길 의사가 있을 때 씁니다.

**기존 DOTween 애니메이션을 NexUI가 관찰** (`DOTweenTracking`)
**이쪽이 이미 DOTween 코드가 있는 프로젝트를 위한 경로입니다.** Tween은 쓰던 그대로 두고,
그것이 무엇을 움직이는지만 NexUI에 알려줍니다. 그러면 Override Ledger가 "이 값이 왜 0.4인가"에
Tween 이름으로 답하고, Flow Trace에 애니메이션이 이를 시작한 상호작용과 함께 나타납니다.

```csharp
using emiteat.NexUI.Integrations.DOTween;
using emiteat.NexUI.Overrides;

// 기존 코드 그대로. 마지막 한 줄만 추가됩니다.
panel.DOFade(0.4f, 0.3f)
     .SetId("panel-dim")
     .TrackAs(runtime.Overrides, runtime.SourceMap, "InventoryPanel", NexOverrideProperty.Opacity);
```

Tween이 하는 일은 전혀 바뀌지 않습니다. 모든 메서드가 받은 Tween을 그대로 반환하므로 기존 체인에
끼워 넣을 수 있고, 호출을 지우면 애니메이션은 원래대로 돌아갑니다.

`TraceAs`는 Flow Trace가 꺼져 있으면 아무 일도 하지 않습니다. `TrackAndTrace`는 둘을 한 번에 겁니다.

기록 시점은 시작·완료·중단뿐입니다. 매 프레임 기록하면 Ledger가 로그가 되는데, "이 값을 누가
소유하는가"라는 질문의 답은 Tween이 도는 동안 내내 같습니다.

## 배포 전

프로젝트가 사용한 서드파티 의존성의 정확한 버전과 라이선스를 기록하세요. NexUI가 직접 요구하는
서드파티 의존성은 `Third Party Notices.txt`에 있습니다.
