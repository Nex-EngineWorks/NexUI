# NexUI Motion Clip Demo

Minimal demo of the Motion Clip Editor's runtime playback path:

- `MotionClipDemoController` builds a `UIMotionClip` in code (scale 0.9→1, alpha 0→1 over 0.25s
  against the screen's root element) so the sample runs without pre-authored assets, then plays
  it through `UIManager.PlayMotionClipAsync` (`emiteat.NexUI.MotionClip` extension method).
- Press `O` (or call `PlayOpenAnimation()` from a UI Button's OnClick) to open the panel screen
  and play the clip.

To author your own clips instead of building them in code: `Tools/NexUI/Designer/Motion Clip
Editor`. See `Packages/com.nexengineworks.nexui.studio/Documentation~/motion-clip-editor.md`.
