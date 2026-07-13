using System;
using NUnit.Framework;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.MotionClip;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>Motion Editor UX Phase 3: the Penner easing set added for the Easing Browser must be well-behaved for every value the browser will ever render (0/1 boundaries, no NaN across the range).</summary>
    public sealed class UIMotionEasingTests
    {
        private static readonly UIMotionEasing[] AllEasings =
            (UIMotionEasing[])Enum.GetValues(typeof(UIMotionEasing));

        [Test]
        public void EveryEasing_At0_Returns0()
        {
            foreach (var easing in AllEasings)
                Assert.AreEqual(0f, UIMotionClipEvaluator.Ease(easing, 0f), 0.0001f, easing.ToString());
        }

        [Test]
        public void EveryEasing_At1_Returns1()
        {
            foreach (var easing in AllEasings)
                Assert.AreEqual(1f, UIMotionClipEvaluator.Ease(easing, 1f), 0.0001f, easing.ToString());
        }

        [Test]
        public void EveryEasing_AcrossRange_NeverReturnsNaNOrInfinity()
        {
            foreach (var easing in AllEasings)
            {
                for (var i = 0; i <= 20; i++)
                {
                    var t = i / 20f;
                    var value = UIMotionClipEvaluator.Ease(easing, t);
                    Assert.IsFalse(float.IsNaN(value), $"{easing} at t={t} was NaN");
                    Assert.IsFalse(float.IsInfinity(value), $"{easing} at t={t} was Infinity");
                }
            }
        }

        [Test]
        public void EaseInOutQuad_IsSymmetric()
        {
            var mid = UIMotionClipEvaluator.Ease(UIMotionEasing.EaseInOutQuad, 0.5f);
            Assert.AreEqual(0.5f, mid, 0.0001f);
        }
    }
}
