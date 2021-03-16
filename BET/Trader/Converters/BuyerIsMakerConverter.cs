using System;
using System.Globalization;

namespace Trader.Converters
{
    public class BuyerIsMakerConverter : SignToBrushConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if(value is bool buyerIsMaker) 
                {
                    return buyerIsMaker ?
                        Negative // taker sells - RED - DOWN
                        :
                        Positive; // taker buys - GREEN - UP
                }
                else 
                    return Neutral;


            }
            catch
            {
                return Neutral;
            }
        }
    }
}
