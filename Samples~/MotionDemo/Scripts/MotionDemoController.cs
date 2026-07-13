using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Motion;

namespace emiteat.NexUI.Samples.MotionDemo
{
    public sealed class MotionDemoController : MonoBehaviour
    {
        [SerializeField] private UIScreenDefinition _popup;
        [SerializeField] private UIScreenDefinition _toast;

        private void Awake()
        {
            if (_popup != null)
            {
                _popup.motion.openMotion = BuildPopupMotion();
                NexUIApp.RegisterScreen(_popup);
            }

            if (_toast != null)
            {
                _toast.motion.openMotion = BuildToastMotion();
                NexUIApp.RegisterScreen(_toast);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P) && _popup != null)
                NexUIApp.Toggle(_popup.ScreenId);

            if (Input.GetKeyDown(KeyCode.Y) && _toast != null)
                NexUIApp.Open(_toast.ScreenId);
        }

        private static UIMotionPreset BuildPopupMotion()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "demo.popup.in";
            preset.variants = new[]
            {
                new UIMotionVariant
                {
                    name = "default",
                    steps = new[]
                    {
                        UIMotionStep.Fade(0f, 1f, 0.18f),
                        new UIMotionStep { property = UIMotionProperty.ScaleX, from = 0.92f, to = 1f, duration = 0.18f },
                        new UIMotionStep { property = UIMotionProperty.ScaleY, from = 0.92f, to = 1f, duration = 0.18f }
                    }
                }
            };
            return preset;
        }

        private static UIMotionPreset BuildToastMotion()
        {
            var preset = ScriptableObject.CreateInstance<UIMotionPreset>();
            preset.motionId = "demo.toast.in";
            preset.variants = new[]
            {
                new UIMotionVariant
                {
                    name = "default",
                    steps = new[]
                    {
                        UIMotionStep.Fade(0f, 1f, 0.12f),
                        new UIMotionStep { property = UIMotionProperty.PositionY, from = -24f, to = 0f, duration = 0.18f }
                    }
                }
            };
            return preset;
        }
    }
}
