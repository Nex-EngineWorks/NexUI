# Motions

Author `UIMotionPreset` assets here (*Create → NexUI → Motion Preset*).

A preset holds **variants** (named lists of `UIMotionStep`s) and/or a **graph**. Steps
animate a single `UIMotionProperty` (Opacity / PositionX / PositionY / ScaleX / ScaleY /
Rotation) from `from` to `to` over `duration` with `Linear` or `EaseInOut` easing.

At runtime the `MotionResolver` compiles the preset to a `UIMotionTimeline`, which the
`BuiltInMotionPlayer` plays through `IUITransformCapability`. Assign presets to a screen
definition's open/close motion, or compile and play manually via `MotionCompiler.Compile`.
