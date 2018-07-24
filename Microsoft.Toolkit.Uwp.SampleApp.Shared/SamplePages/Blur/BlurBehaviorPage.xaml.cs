// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.SampleApp.Models;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.Toolkit.Uwp.SampleApp.SamplePages
{
    /// <summary>
    /// A page that shows how to use the blur behavior.
    /// </summary>
    public sealed partial class BlurBehaviorPage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlurBehaviorPage"/> class.
        /// </summary>
        public BlurBehaviorPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the Page is loaded and becomes the current source of a parent Frame.
        /// </summary>
        /// <param name="e">Event data that can be examined by overriding code. The event data is representative of the pending navigation that will load the current Page. Usually the most relevant property to examine is Parameter.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

#if NETFX_CORE // UNO TODO
            if (!AnimationExtensions.BlurEffect.IsSupported)
            {
                WarningText.Visibility = Visibility.Visible;
            }
#endif
		}
	}
}