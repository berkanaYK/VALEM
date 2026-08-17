using Microsoft.UI.Xaml.Data;
using VALE.Contracts;

namespace VALE.Client.Converters;

public sealed class TicketStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        TicketStatus.Received => "Teslim alındı",
        TicketStatus.Parked => "Park edildi",
        TicketStatus.Requested => "Araç isteniyor",
        TicketStatus.Delivered => "Teslim edildi",
        TicketStatus.Cancelled => "İptal edildi",
        _ => "Bilinmiyor"
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

