using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace NoHands
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StorageCamera : Page
    {
        public StorageCamera()
        {
            this.InitializeComponent();
        }
        class BitmapItem
        {
            public ImageSource Source { get; set; }
        }
        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        ObservableCollection<BitmapItem> items = new ObservableCollection<BitmapItem>();
        private async void Grid_Drop(object sender, DragEventArgs e)
        {


            var files = await e.DataView.GetStorageItemsAsync();

            foreach (StorageFile file in files)
            {
                try
                {
                    BitmapImage bi = new BitmapImage();
                    bi.SetSource(await file.OpenAsync(FileAccessMode.Read));
                    items.Add(new BitmapItem() { Source = bi });
                }
                catch { }
            }
            listView.ItemsSource = items;
        }

    }

}
