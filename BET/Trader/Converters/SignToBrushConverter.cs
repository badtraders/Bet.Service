using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Trader.Converters
{
    public class SignToBrushConverter : IValueConverter
    {
        // TODO: take from settings
        public static Brush Positive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0ecb81"));
        public static Brush Negative = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f6465d"));
        public static Brush Neutral = Brushes.Black;// new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f6465d"));
        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var val = System.Convert.ToDecimal(value);
                if (val > 0)
                    return Positive;
                else if (val < 0)
                    return Negative;
                else
                    return Neutral;

            }
            catch
            {
                return Neutral;
            }
        }

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
