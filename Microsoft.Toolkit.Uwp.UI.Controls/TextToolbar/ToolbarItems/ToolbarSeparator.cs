// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI.Xaml.Controls;

namespace Microsoft.Toolkit.Uwp.UI.Controls.TextToolbarButtons
{
    /// <summary>
    /// Seperates a collection of <see cref="IToolbarItem"/>
    /// </summary>
    public partial class ToolbarSeparator : AppBarSeparator, IToolbarItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolbarSeparator"/> class.
        /// </summary>
        public ToolbarSeparator()
        {
#if !__WASM__
			this.DefaultStyleKey = typeof(ToolbarSeparator);
#endif
        }

        /// <inheritdoc/>
        public int Position { get; set; } = -1;
    }
}