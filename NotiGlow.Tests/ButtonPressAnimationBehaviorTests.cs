using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.UI.Animations;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ButtonPressAnimationBehaviorTests
    {
        [TestMethod]
        public void TestButtonPressAnimationBehavior_PropertiesRegistered()
        {
            Assert.IsNotNull(ButtonPressAnimationBehavior.IsEnabledProperty);
            Assert.IsNotNull(ButtonPressAnimationBehavior.PressedScaleProperty);
            Assert.IsNotNull(ButtonPressAnimationBehavior.PressDurationMsProperty);
            Assert.IsNotNull(ButtonPressAnimationBehavior.ReleaseDurationMsProperty);

            Assert.IsNotNull(PressAnimation.IsEnabledProperty);
            Assert.IsNotNull(PressAnimation.PressedScaleProperty);
            Assert.IsNotNull(PressAnimation.PressDurationMsProperty);
            Assert.IsNotNull(PressAnimation.ReleaseDurationMsProperty);
        }

        [TestMethod]
        public void TestMotionPolicy_ReducesDurationsAndDetectsSettings()
        {
            MotionPolicy.Update(reduceMotion: false, reduceAnimations: false);
            Assert.IsFalse(MotionPolicy.IsReduceMotion);
            Assert.IsFalse(MotionPolicy.IsReduceAnimations);
            Assert.AreEqual(200, MotionPolicy.GetDurationMs(200));

            MotionPolicy.Update(reduceMotion: true, reduceAnimations: false);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);
            Assert.AreEqual(0, MotionPolicy.GetDurationMs(200));

            MotionPolicy.Update(reduceMotion: false, reduceAnimations: true);
            Assert.IsTrue(MotionPolicy.IsReduceAnimations);
            Assert.AreEqual(0, MotionPolicy.GetDurationMs(200));

            MotionPolicy.Update(reduceMotion: false, reduceAnimations: false);
        }
    }
}
