using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Dynamo.Applications.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Boolean boolean))
            {
                return Visibility.Collapsed;
            }

            if (Inverse)
            {
                boolean = !boolean;
            }

            return boolean
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Visibility visibility))
            {
                return false;
            }

            var visible = visibility.Equals(Visibility.Visible);

            return Inverse ? !visible : visible;
        }
    }
}
