﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace MicroserviceExplorer.Utils
{
    /// <summary>
    /// Adorner for the watermark
    /// </summary>
    internal class WatermarkAdorner : Adorner
    {
        #region Private Fields

        /// <summary>
        /// <see cref="System.Windows.Controls.ContentPresenter"/> that holds the watermark
        /// </summary>
        readonly ContentPresenter ContentPresenter;

        #endregion

        #region Constructor

        public WatermarkAdorner(UIElement adornedElement, object watermark) :
            base(adornedElement)
        {
            this.IsHitTestVisible = false;

            this.ContentPresenter = new ContentPresenter();
            this.ContentPresenter.Content = watermark;
            this.ContentPresenter.Opacity = 0.5;
            this.ContentPresenter.Margin = new Thickness(Control.Margin.Left + Control.Padding.Left,
                Control.Margin.Top + Control.Padding.Top, 0, 0);

            if (this.Control is ItemsControl && !(this.Control is ComboBox))
            {
                this.ContentPresenter.VerticalAlignment = VerticalAlignment.Center;
                this.ContentPresenter.HorizontalAlignment = HorizontalAlignment.Center;
            }

            // Hide the control adorner when the adorned element is hidden
            var binding = new Binding("IsVisible")
            {
                Source = adornedElement,
                Converter = new BooleanToVisibilityConverter()
            };
            this.SetBinding(VisibilityProperty, binding);
        }

        #endregion

        #region Protected Properties

        /// <summary>
        /// Gets the number of children for the <see cref="ContainerVisual"/>.
        /// </summary>
        protected override int VisualChildrenCount => 1;

        #endregion

        #region Private Properties

        /// <summary>
        /// Gets the control that is being adorned
        /// </summary>
        private Control Control => (Control)this.AdornedElement;

        #endregion

        #region Protected Overrides

        /// <summary>
        /// Returns a specified child <see cref="Visual"/> for the parent <see cref="ContainerVisual"/>.
        /// </summary>
        /// <param name="index">A 32-bit signed integer that represents the index value of the child <see cref="Visual"/>. The value of index must be between 0 and <see cref="VisualChildrenCount"/> - 1.</param>
        /// <returns>The child <see cref="Visual"/>.</returns>
        protected override Visual GetVisualChild(int index) => this.ContentPresenter;

        /// <summary>
        /// Implements any custom measuring behavior for the adorner.
        /// </summary>
        /// <param name="constraint">A size to constrain the adorner to.</param>
        /// <returns>A <see cref="Size"/> object representing the amount of layout space needed by the adorner.</returns>
        protected override Size MeasureOverride(Size constraint)
        {
            // Here's the secret to getting the adorner to cover the whole control
            this.ContentPresenter.Measure(Control.RenderSize);
            return Control.RenderSize;
        }

        /// <summary>
        /// When overridden in a derived class, positions child elements and determines a size for a <see cref="FrameworkElement"/> derived class. 
        /// </summary>
        /// <param name="finalSize">The final area within the parent that this element should use to arrange itself and its children.</param>
        /// <returns>The actual size used.</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            this.ContentPresenter.Arrange(new Rect(finalSize));
            return finalSize;
        }

        #endregion
    }
}
