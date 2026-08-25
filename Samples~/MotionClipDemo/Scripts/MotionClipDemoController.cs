using System.Threading.Tasks;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.MotionClip;

namespace emiteat.NexUI.Samples.MotionClipDemo
{
    /// <summary>
    /// Minimal Motion Clip Editor demo: press O to open the panel screen and play a
    /// scale+alpha "open" <see cref="UIMotionClip"/> against its root element, built in code so
    /// the sample runs without pre-authored assets (same convention as MotionDemo).
    /// </summary>
    public sealed class MotionClipDemoController : MonoBehaviour
    {
        [SerializeField] private UIScreenDefinition _panel;

        private UIMotionClip _openClip;

        private void Awake()
        {
            if (_panel == null) return;
            NexUIApp.RegisterScreen(_panel);
            _openClip = BuildOpenClip();
        }

        private void Update()
        {
            if (_panel == null) return;
            if (Input.GetKeyDown(KeyCode.O))
                PlayOpenAnimation();
        }

        /// <summary>Hook this up to a UI Button's OnClick as well as the O key above.</summary>
        public void PlayOpenAnimation()
        {
            if (_panel == null || _openClip == null) return;
            _ = OpenAndAnimateAsync();
        }

        private async Task OpenAndAnimateAsync()
        {
            await NexUIApp.OpenAsync(_panel.ScreenId, new UIOpenArgs { suppressMotion = true });
            await NexUIApp.Manager.PlayMotionClipAsync(_panel.ScreenId, _openClip);
        }

        private static UIMotionClip BuildOpenClip()
        {
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.clipName = "demo.panel.open";
            clip.duration = 0.25f;
            clip.loop = false;
            clip.tracks = new[]
            {
                new UIMotionClipTrack
                {
                    // Empty id -> the screen's root element (see UIMotionClipTargetResolver).
                    targetElementId = string.Empty,
                    propertyTracks = new[]
                    {
                        new UIMotionClipPropertyTrack
                        {
                            propertyType = UIMotionClipPropertyType.CanvasGroupAlpha,
                            keyframes = new[]
                            {
                                new UIMotionClipKeyframe(0f, UIMotionClipValue.Float(0f)),
                                new UIMotionClipKeyframe(0.25f, UIMotionClipValue.Float(1f))
                            }
                        },
                        new UIMotionClipPropertyTrack
                        {
                            propertyType = UIMotionClipPropertyType.LocalScale,
                            keyframes = new[]
                            {
                                new UIMotionClipKeyframe(0f, UIMotionClipValue.FromVector3(Vector3.one * 0.9f)),
                                new UIMotionClipKeyframe(0.25f, UIMotionClipValue.FromVector3(Vector3.one))
                            }
                        }
                    }
                }
            };
            return clip;
        }
    }
}
