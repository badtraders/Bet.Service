using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace TradingTerminal.Extensions
{
    public class GridViewColumnExtension : GridViewColumn
    {
        public HorizontalAlignment HorizontalAlignment
        {
            get { return (HorizontalAlignment)GetValue(HorizontalAlignmentProperty); }
            set { SetValue(HorizontalAlignmentProperty, value); }
        }

        public static readonly DependencyProperty HorizontalAlignmentProperty =
            DependencyProperty.Register("HorizontalAlignment", typeof(HorizontalAlignment), typeof(GridViewColumnExtension), new PropertyMetadata(HorizontalAlignment.Right));


        private BindingBase _binding;
        public BindingBase Binding
        {
            get
            {
                return _binding;
            }
            set
            {
                if (_binding != value)
                {
                    _binding = value;
                    UpdateTemplate();
                }
            }
        }

        private void UpdateTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment);
            factory.SetBinding(TextBlock.TextProperty, this._binding);

            this.CellTemplate = new DataTemplate
            {
                VisualTree = factory
            };
        }

    }
}
