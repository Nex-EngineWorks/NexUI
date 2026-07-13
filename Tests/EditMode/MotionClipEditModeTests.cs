using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.MotionClip;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class MotionClipEditModeTests
    {
        [Test]
        public void Evaluator_NoKeyframes_ReturnsNull()
        {
            var track = new UIMotionClipPropertyTrack { propertyType = UIMotionClipPropertyType.CanvasGroupAlpha };
            Assert.IsNull(UIMotionClipEvaluator.Evaluate(track, 0.5f));
        }

        [Test]
        public void Evaluator_SingleKeyframe_ReturnsThatValue()
        {
            var track = new UIMotionClipPropertyTrack
            {
                propertyType = UIMotionClipPropertyType.CanvasGroupAlpha,
                keyframes = new[] { new UIMotionClipKeyframe(0.4f, UIMotionClipValue.Float(0.75f)) }
            };

            var value = UIMotionClipEvaluator.Evaluate(track, 10f);
            Assert.IsTrue(value.HasValue);
            Assert.AreEqual(0.75f, value.Value.floatValue);
        }

        [Test]
        public void Evaluator_BeforeFirstKeyframe_ClampsToFirst()
        {
            var track = new UIMotionClipPropertyTrack
            {
                propertyType = UIMotionClipPropertyType.CanvasGroupAlpha,
                keyframes = new[]
                {
                    new UIMotionClipKeyframe(0.5f, UIMotionClipValue.Float(0.2f)),
                    new UIMotionClipKeyframe(1f, UIMotionClipValue.Float(1f))
                }
            };

            var value = UIMotionClipEvaluator.Evaluate(track, 0f);
            Assert.AreEqual(0.2f, value.Value.floatValue);
        }

        [Test]
        public void Evaluator_AfterLastKeyframe_ClampsToLast()
        {
            var track = new UIMotionClipPropertyTrack
            {
                propertyType = UIMotionClipPropertyType.CanvasGroupAlpha,
                keyframes = new[]
                {
                    new UIMotionClipKeyframe(0f, UIMotionClipValue.Float(0f)),
                    new UIMotionClipKeyframe(0.5f, UIMotionClipValue.Float(1f))
                }
            };

            var value = UIMotionClipEvaluator.Evaluate(track, 5f);
            Assert.AreEqual(1f, value.Value.floatValue);
        }

        [Test]
        public void Evaluator_MidSegment_LinearlyInterpolates()
        {
            var track = new UIMotionClipPropertyTrack
            {
                propertyType = UIMotionClipPropertyType.CanvasGroupAlpha,
                keyframes = new[]
                {
                    new UIMotionClipKeyframe(0f, UIMotionClipValue.Float(0f), UIMotionEasing.Linear),
                    new UIMotionClipKeyframe(1f, UIMotionClipValue.Float(1f), UIMotionEasing.Linear)
                }
            };

            var value = UIMotionClipEvaluator.Evaluate(track, 0.5f);
            Assert.AreEqual(0.5f, value.Value.floatValue, 0.0001f);
        }

        [Test]
        public void Evaluator_Vector2Track_Interpolates()
        {
            var track = new UIMotionClipPropertyTrack
            {
                propertyType = UIMotionClipPropertyType.AnchoredPosition,
                keyframes = new[]
                {
                    new UIMotionClipKeyframe(0f, UIMotionClipValue.FromVector2(Vector2.zero)),
                    new UIMotionClipKeyframe(1f, UIMotionClipValue.FromVector2(new Vector2(100f, 0f)))
                }
            };

            var value = UIMotionClipEvaluator.Evaluate(track, 0.5f);
            Assert.AreEqual(50f, value.Value.vector2Value.x, 0.0001f);
        }

        [Test]
        public void Clip_Defaults_WorkAreaOffAndMarkersEmpty()
        {
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            Assert.IsFalse(clip.useWorkArea);
            Assert.AreEqual(0f, clip.workAreaStart);
            Assert.AreEqual(1f, clip.workAreaEnd);
            Assert.AreEqual(0, clip.markers.Length);
        }

        [Test]
        public void Clip_Markers_RoundTrip()
        {
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.markers = new[] { new UIMotionClipMarker("Impact", 0.4f), new UIMotionClipMarker("Loop Point", 0.8f) };

            Assert.AreEqual(2, clip.markers.Length);
            Assert.AreEqual("Impact", clip.markers[0].name);
            Assert.AreEqual(0.4f, clip.markers[0].time, 0.0001f);
        }

        [Test]
        public void Clip_CreateAddTrackAddKeyframe_RoundTrips()
        {
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.clipName = "Test";
            clip.duration = 1f;

            var track = new UIMotionClipTrack { targetElementId = "panel" };
            var propertyTrack = new UIMotionClipPropertyTrack { propertyType = UIMotionClipPropertyType.SizeDelta };
            propertyTrack.keyframes = new[] { new UIMotionClipKeyframe(0f, UIMotionClipValue.FromVector2(new Vector2(200f, 80f))) };
            track.propertyTracks = new[] { propertyTrack };
            clip.tracks = new[] { track };

            Assert.AreEqual(1, clip.tracks.Length);
            Assert.AreEqual("panel", clip.tracks[0].targetElementId);
            Assert.AreEqual(1, clip.tracks[0].propertyTracks.Length);
            Assert.AreEqual(1, clip.tracks[0].propertyTracks[0].keyframes.Length);
        }
    }
}
