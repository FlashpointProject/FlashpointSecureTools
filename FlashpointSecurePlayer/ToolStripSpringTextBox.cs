﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    [ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.ToolStrip)]
    public partial class ToolStripSpringTextBox : ToolStripTextBox {
        public ToolStripSpringTextBox() {
            InitializeComponent();
        }

        public override Size GetPreferredSize(Size constrainingSize) {
            // use the default size if the text box is on the overflow menu
            // or is on a vertical ToolStrip
            if (IsOnOverflow || Owner.Orientation == Orientation.Vertical) {
                return DefaultSize;
            }

            // declare a variable to store the total available width as
            // it is calculated, starting with the display width of the
            // owning ToolStrip
            int width = Owner.DisplayRectangle.Width;

            // subtract the width of the overflow button if it is displayed
            if (Owner.OverflowButton.Visible) {
                width -= Owner.OverflowButton.Width + Owner.OverflowButton.Margin.Horizontal;
            }

            // declare a variable to maintain a count of ToolStripSpringTextBox
            // items currently displayed in the owning ToolStrip
            int springBoxCount = 0;

            foreach (ToolStripItem item in Owner.Items) {
                // ignore items on the overflow menu
                if (!item.IsOnOverflow) {
                    if (item is ToolStripSpringTextBox) {
                        // for ToolStripSpringTextBox items, increment the count and
                        // subtract the margin width from the total available width
                        width -= item.Margin.Horizontal;
                        springBoxCount++;
                    } else {
                        // for all other items, subtract the full width from the total
                        // available width
                        width -= item.Width + item.Margin.Horizontal;
                    }
                }
            }

            // if there are multiple ToolStripSpringTextBox items in the owning
            // ToolStrip, divide the total available width between them
            if (springBoxCount > 1) {
                width /= springBoxCount;
            }

            // if the available width is less than the default width, use the
            // default width, forcing one or more items onto the overflow menu
            if (width < DefaultSize.Width) {
                width = DefaultSize.Width;
            }

            // retrieve the preferred size from the base class, but change the
            // width to the calculated width
            Size size = base.GetPreferredSize(constrainingSize);
            size.Width = width;
            return size;
        }
    }
}
