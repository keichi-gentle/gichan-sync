using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace GichanDiary.Views;

public partial class EventListView : UserControl
{
    public EventListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (EventGrid.ItemsSource is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (s, args) => RefreshColumnWidths();
        }
    }

    private void RefreshColumnWidths()
    {
        Dispatcher.InvokeAsync(() =>
        {
            // Force DataGrid to recalculate SizeToCells columns
            EventGrid.UpdateLayout();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
}
