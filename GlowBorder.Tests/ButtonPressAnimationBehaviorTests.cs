using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GlowBorder.UI.Animations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wpf.Ui.Controls;

namespace GlowBorder.Tests
{
    [TestClass]
    public class ButtonPressAnimationBehaviorTests
    {
        [TestMethod]
        public void TestButtonPressAnimationAttachmentAndDefaults()
        {
            Exception? caughtEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application();
                    }

                    var button = new System.Windows.Controls.Button();
                    ButtonPressAnimationBehavior.SetIsEnabled(button, true);

                    Assert.IsTrue(ButtonPressAnimationBehavior.GetIsEnabled(button));
                    Assert.AreEqual(0.97, ButtonPressAnimationBehavior.GetPressedScale(button), 0.001);
                    Assert.AreEqual(80, ButtonPressAnimationBehavior.GetPressDurationMs(button));
                    Assert.AreEqual(115, ButtonPressAnimationBehavior.GetReleaseDurationMs(button));

                    // Verify RenderTransformOrigin is centered (0.5, 0.5)
                    Assert.AreEqual(0.5, button.RenderTransformOrigin.X, 0.001);
                    Assert.AreEqual(0.5, button.RenderTransformOrigin.Y, 0.001);

                    // Verify ScaleTransform is assigned
                    Assert.IsNotNull(button.RenderTransform);
                    Assert.IsTrue(button.RenderTransform is ScaleTransform || button.RenderTransform is TransformGroup);

                    // Detach
                    ButtonPressAnimationBehavior.SetIsEnabled(button, false);
                    Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(button));
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caughtEx != null)
            {
                Assert.Fail($"Test failed with exception: {caughtEx}");
            }
        }

        [TestMethod]
        public void TestPressAnimationAliasAndCustomProperties()
        {
            Exception? caughtEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var toggleBtn = new ToggleButton();
                    PressAnimation.SetIsEnabled(toggleBtn, true);
                    PressAnimation.SetPressedScale(toggleBtn, 0.95);
                    PressAnimation.SetPressDurationMs(toggleBtn, 80);
                    PressAnimation.SetReleaseDurationMs(toggleBtn, 100);

                    Assert.IsTrue(PressAnimation.GetIsEnabled(toggleBtn));
                    Assert.AreEqual(0.95, PressAnimation.GetPressedScale(toggleBtn), 0.001);
                    Assert.AreEqual(80, PressAnimation.GetPressDurationMs(toggleBtn));
                    Assert.AreEqual(100, PressAnimation.GetReleaseDurationMs(toggleBtn));
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caughtEx != null)
            {
                Assert.Fail($"Test failed with exception: {caughtEx}");
            }
        }

        [TestMethod]
        public void TestNavigationViewItemAttachment()
        {
            Exception? caughtEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var navItem = new NavigationViewItem
                    {
                        Content = "General",
                        Tag = "General"
                    };

                    ButtonPressAnimationBehavior.Attach(navItem);

                    Assert.AreEqual(0.5, navItem.RenderTransformOrigin.X, 0.001);
                    Assert.AreEqual(0.5, navItem.RenderTransformOrigin.Y, 0.001);
                    Assert.IsNotNull(navItem.RenderTransform);

                    ButtonPressAnimationBehavior.Detach(navItem);
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caughtEx != null)
            {
                Assert.Fail($"Test failed with exception: {caughtEx}");
            }
        }

        [TestMethod]
        public void TestTransformGroupPreservation()
        {
            Exception? caughtEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var border = new Border();
                    var existingGroup = new TransformGroup();
                    existingGroup.Children.Add(new TranslateTransform(10, 20));
                    border.RenderTransform = existingGroup;

                    ButtonPressAnimationBehavior.Attach(border);

                    Assert.IsInstanceOfType(border.RenderTransform, typeof(TransformGroup));
                    var tg = (TransformGroup)border.RenderTransform;
                    Assert.AreEqual(2, tg.Children.Count);
                    Assert.IsInstanceOfType(tg.Children[0], typeof(TranslateTransform));
                    Assert.IsInstanceOfType(tg.Children[1], typeof(ScaleTransform));

                    ButtonPressAnimationBehavior.Detach(border);
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caughtEx != null)
            {
                Assert.Fail($"Test failed with exception: {caughtEx}");
            }
        }
    }
}
