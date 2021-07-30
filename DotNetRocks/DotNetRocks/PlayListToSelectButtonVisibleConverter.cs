using DotNetRocks.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Xamarin.Forms;

namespace DotNetRocks
{
    public class PlayListToSelectButtonVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) 
                // No Playlist
                return false;

            var playlist = (PlayList)value;

            // We're going to pass a label with the Text set to the Show.Id
            var label = (Label)parameter;

            if (label.Text == null) 
                // No value here. Unexpected, for sure
                return false;

            // Convert the Text property to an int
            int Id = System.Convert.ToInt32(label.Text);

            // Check to see if the show is in the playlist Shows list
            var match = (from x in playlist.Shows where x.Id == Id select x).FirstOrDefault();
            if (match == null)
                // Not there, so we can show the SELECT button
                return true;
            else
                // It's there. DON'T show the SELECT button
                return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
