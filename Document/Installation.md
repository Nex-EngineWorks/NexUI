# 설치

## 요구사항

- Unity **6000.4+**.
- **UniTask** (`com.cysharp.unitask`) — 필수. 모든 비동기 API가 사용합니다.
- uGUI 통합용: `com.unity.ugui` + TextMeshPro (Unity 기본 포함).

## 1. UniTask 설치

NexUI는 UniTask가 필요합니다. 아래 중 하나면 됩니다.

**A. 임베디드 패키지 (권장, 가장 안정적)**
UniTask 저장소의 `src/UniTask/Assets/Plugins/UniTask` 폴더를 프로젝트의
`Packages/com.cysharp.unitask/`로 복사합니다. Unity가 임베디드 패키지를 자동 인식하므로 매니페스트
항목이 필요 없습니다. (본 프로젝트는 이 방식으로 포함되어 있습니다.)

**B. Git URL** — `Packages/manifest.json`에 추가 (PATH에 git 필요):
```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10"
```

**C. OpenUPM**:
```json
"scopedRegistries": [
  { "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": ["com.cysharp.unitask"] }
],
"dependencies": { "com.cysharp.unitask": "2.5.10" }
```

## 2. NexUI 추가

`com.emiteat.nexui` 패키지를 프로젝트의 `Packages/` 폴더에 두거나(임베디드), Package Manager의
`+ ▸ Add package from disk…`로 해당 `package.json`을 선택합니다.

UniTask가 resolve되면 NexUI 어셈블리가 컴파일됩니다.

## 3. 선택 통합 활성화

선택 통합은 해당 외부 패키지가 있을 때만 컴파일됩니다. 패키지를 설치하면 `versionDefines`로 define
심볼이 자동 설정되어 코드가 활성화됩니다.

| 통합 | 외부 패키지 | Define 심볼 |
|------|------------|-------------|
| DOTween | DOTween (에셋/UPM) | `NEXUI_HAS_DOTWEEN` |
| VContainer | `jp.hadashikick.vcontainer` | `NEXUI_HAS_VCONTAINER` |
| MessagePipe | `com.cysharp.messagepipe` | `NEXUI_HAS_MESSAGEPIPE` |
| Addressables | `com.unity.addressables` | `NEXUI_HAS_ADDRESSABLES` |
| Input System | `com.unity.inputsystem` | `NEXUI_HAS_INPUTSYSTEM` |

> DOTween은 UPM 패키지가 없는 에셋인 경우가 많습니다. 자동 감지가 안 되면 **Project Settings ▸
> Player ▸ Scripting Define Symbols**에 `NEXUI_HAS_DOTWEEN`을 수동으로 추가하세요.

## 4. 샘플 임포트 (선택)

Package Manager에서 NexUI를 선택하고 Samples 탭에서 **Basic Runtime** 샘플(및 기타)을 임포트합니다.

## 확인

컴파일 후 씬에 백엔드 부트스트랩을 추가하고([GettingStart](GettingStart.md) 참조)
`NexUI.OpenAsync("HUD")`를 호출합니다. 콘솔 에러가 없으면 준비 완료입니다.
