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
        // Auto-resize columns when items change (page navigation)
        if (EventGrid.ItemsSource is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (s, args) => AutoResizeColumns();
        }
        AutoResizeColumns();
    }

    private void AutoResizeColumns()
    {
        Dispatcher.InvokeAsync(() =>
        {
            foreach (var col in EventGrid.Columns)
            {
                // Skip star-width columns (세부내용, 비고)
                if (col.Width.IsStar) continue;

                // Reset to auto to force recalculation
                col.Width = 0;
                col.Width = DataGridLength.Auto;
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
}
