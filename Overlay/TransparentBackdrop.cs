using System;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NotiGlow.Overlay
{
    public sealed class TransparentBackdrop : SystemBackdrop
    {
        private Windows.UI.Composition.CompositionColorBrush? _brush;
        private Windows.UI.Composition.Compositor? _compositor;

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            _compositor = new Windows.UI.Composition.Compositor();
            _brush = _compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
            connectedTarget.SystemBackdrop = _brush;
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop connectedTarget)
        {
            base.OnTargetDisconnected(connectedTarget);
            connectedTarget.SystemBackdrop = null;
            _brush?.Dispose();
            _brush = null;
            _compositor?.Dispose();
            _compositor = null;
        }
    }
}
