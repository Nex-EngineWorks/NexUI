# NexUI Documentation

Runtime UI framework for Unity. Same game code drives either **UI Toolkit** or **uGUI**.

## Start here

| Document | What it covers |
|---|---|
| [Beginner Handbook (한국어)](beginner-handbook-ko.md) | 부트스트랩 → 화면 열기 → 바인딩 → 모션까지 15분 입문 |
| [Integrations](integrations.md) | UI Toolkit / uGUI 백엔드, DOTween·VContainer·MessagePipe·Addressables·Input System |
| [Command Pipeline](command-pipeline.md) | 클릭 → 커맨드 디스패치 → 미들웨어 → Undo/리플레이 |
| [Validation](validation.md) | 프로젝트 검증기, 런타임 계약 검사, Studio Issues 패널과의 관계 |
| [Project Setup](project-setup.md) | `Tools/NexUI/Project Setup`이 만들어 주는 설정·폴더·에셋 |

## 60-second tour

```csharp
// 1. Bootstrap once (scene): UIToolkitIntegrationBootstrap or UGUIIntegrationBootstrap
// 2. Register screens
NexUIApp.RegisterScreen(shopScreen);        // UIScreenDefinition asset

// 3. Open with data, await the close result
await NexUIApp.OpenAsync("Shop", new UIOpenArgs { payload = new Dictionary<string, object> { { "tab", "gear" } } });
var bought = await NexUIApp.WaitForCloseAsync("Shop");

// 4. Bulk navigation
await NexUIApp.CloseLayerAsync(UILayerType.Popup);
await NexUIApp.CloseOthersAsync("HUD");
```

Rules of thumb: Core never touches `VisualElement`/`GameObject`; backends live only in
`Integrations/*`; motion animates through capabilities only.
