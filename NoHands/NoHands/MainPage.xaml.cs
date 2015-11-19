using Lumia.Imaging;
using Lumia.Imaging.Adjustments;
using Lumia.Imaging.Artistic;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NoHands
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        bool frontCam;
        MediaCapture mediaCapture;
        InMemoryRandomAccessStream fPhotoStream = new InMemoryRandomAccessStream();

        private GrayscaleEffect _grayscaleEffect;
        private ColorBoostEffect _colorboostEffect;
        private LensBlurEffect _lensblurEffect;
        private HueSaturationEffect _hueSaturationEffect;
        private AntiqueEffect _antiqueEffect;

        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Unregister the old background task
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == "Nagger")
                {
                    cur.Value.Unregister(true);
                }
            }

            // Build the new background task
            await BackgroundExecutionManager.RequestAccessAsync();

            var builder = new BackgroundTaskBuilder();

            builder.Name = "Nagger";
            builder.TaskEntryPoint = "Tiler.Nag"; // namespace.class of the background task
            builder.SetTrigger(new TimeTrigger(15, false)); // set the trigger to launch every 15 minutes, time can't be smaller, can be bigger

            BackgroundTaskRegistration task = builder.Register();

            BackgroundAccessStatus status = BackgroundAccessStatus.Unspecified;
            try
            {
                status = await BackgroundExecutionManager.RequestAccessAsync();
            }
            catch (UnauthorizedAccessException)
            {
                // An access denied exception may be thrown if two requests are issued at the same time
                // For this specific sample, that could be if the user double clicks "Request access"
            }

            // Clear all old notifications when app is started
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();

            mediaCapture = new MediaCapture();
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Use the front camera if found one
            if (devices == null) return;
            DeviceInformation info = devices[0];

            foreach (var devInfo in devices)
            {
                if (devInfo.Name.ToLowerInvariant().Contains("front"))
                {
                    info = devInfo;
                    frontCam = true;
                    continue;
                }
            }

            await mediaCapture.InitializeAsync(
                new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = info.Id
                });

            captureElement.Source = mediaCapture;
            captureElement.FlowDirection = frontCam ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            await mediaCapture.StartPreviewAsync();

            DisplayInformation displayInfo = DisplayInformation.GetForCurrentView();
            displayInfo.OrientationChanged += DisplayInfo_OrientationChanged;

            DisplayInfo_OrientationChanged(displayInfo, null);
        }

        private void DisplayInfo_OrientationChanged(DisplayInformation sender, object args)
        {
            if (mediaCapture != null)
            {
                mediaCapture.SetPreviewRotation(frontCam
                ? VideoRotationLookup(sender.CurrentOrientation, true)
                : VideoRotationLookup(sender.CurrentOrientation, false));
                var rotation = VideoRotationLookup(sender.CurrentOrientation, false);
                mediaCapture.SetRecordRotation(rotation);
            }
        }

        private VideoRotation VideoRotationLookup(DisplayOrientations displayOrientation, bool counterclockwise)
        {
            switch (displayOrientation)
            {
                case DisplayOrientations.Landscape:
                    return VideoRotation.None;

                case DisplayOrientations.Portrait:
                    return (counterclockwise) ? VideoRotation.Clockwise270Degrees : VideoRotation.Clockwise90Degrees;

                case DisplayOrientations.LandscapeFlipped:
                    return VideoRotation.Clockwise180Degrees;

                case DisplayOrientations.PortraitFlipped:
                    return (counterclockwise) ? VideoRotation.Clockwise90Degrees :
                    VideoRotation.Clockwise270Degrees;

                default:
                    return VideoRotation.None;
            }
        }

        private async void OnTap(object sender, TappedRoutedEventArgs e)
        {
            inking_initialization();

            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
            //var fPhotoStream = new InMemoryRandomAccessStream();

            mediaCapture.CapturePhotoToStreamAsync(imageProperties, fPhotoStream).AsTask().Wait();
            fPhotoStream.FlushAsync().AsTask().Wait();
            fPhotoStream.Seek(0);
            await mediaCapture.StopPreviewAsync();
            captureElement.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Visible;

            var _bmp = new BitmapImage();
            _bmp.SetSource(fPhotoStream);
            PreviewImage.Source = _bmp;
            NormalThumb.Source = _bmp;

            using (_grayscaleEffect = new GrayscaleEffect())
                await ApplyEffectAsync(fPhotoStream, _grayscaleEffect, GreyScaleThumb);

            using (_colorboostEffect = new ColorBoostEffect())
            {
                _colorboostEffect.Gain = 0.75;
                await ApplyEffectAsync(fPhotoStream, _colorboostEffect, ColorBoostThumb);
            }

            using (_hueSaturationEffect = new HueSaturationEffect())
                await ApplyEffectAsync(fPhotoStream, _hueSaturationEffect, HueSaturationThumb);

            using (_lensblurEffect = new LensBlurEffect())
                await ApplyEffectAsync(fPhotoStream, _lensblurEffect, LensBlurThumb);

            using (_antiqueEffect = new AntiqueEffect())
                await ApplyEffectAsync(fPhotoStream, _antiqueEffect, SepiaThumb);
        }

        /// <summary>
        /// Apply filter to image
        /// </summary>
        /// <param name="fileStream"></param>
        private async Task ApplyEffectAsync(IRandomAccessStream fileStream, IImageProvider provider, SwapChainPanel target)
        {
            using (var _renderer = new SwapChainPanelRenderer(provider, target))
            {
                try
                {
                    // Rewind the stream to start.
                    fileStream.Seek(0);

                    // Set the imageSource on the effect and render.
                    ((IImageConsumer)provider).Source = new RandomAccessStreamImageSource(fileStream);
                    await _renderer.RenderAsync();
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine(exception.Message);
                }
            }
            
        }

        private async void GreyScaleThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FilteredImage.Width = PreviewImage.ActualWidth;
            FilteredImage.Height = PreviewImage.ActualHeight;

            PreviewImage.Visibility = Visibility.Collapsed;
            FilteredImage.Visibility = Visibility.Visible;
            using (_grayscaleEffect = new GrayscaleEffect())
                await ApplyEffectAsync(fPhotoStream, _grayscaleEffect, FilteredImage);

        }

        private async void ColorBoostThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FilteredImage.Width = PreviewImage.ActualWidth;
            FilteredImage.Height = PreviewImage.ActualHeight;

            PreviewImage.Visibility = Visibility.Collapsed;
            FilteredImage.Visibility = Visibility.Visible;
            using (_colorboostEffect = new ColorBoostEffect())
            {
                _colorboostEffect.Gain = 0.75;
                await ApplyEffectAsync(fPhotoStream, _colorboostEffect, FilteredImage);
            }

        }

        private async void SepiaThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FilteredImage.Width = PreviewImage.ActualWidth;
            FilteredImage.Height = PreviewImage.ActualHeight;

            PreviewImage.Visibility = Visibility.Collapsed;
            FilteredImage.Visibility = Visibility.Visible;
            using (_antiqueEffect = new AntiqueEffect())
                await ApplyEffectAsync(fPhotoStream, _antiqueEffect, FilteredImage);


        }

        private async void LensBlurThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FilteredImage.Width = PreviewImage.ActualWidth;
            FilteredImage.Height = PreviewImage.ActualHeight;

            PreviewImage.Visibility = Visibility.Collapsed;
            FilteredImage.Visibility = Visibility.Visible;
            using (_lensblurEffect = new LensBlurEffect())
                await ApplyEffectAsync(fPhotoStream, _lensblurEffect, FilteredImage);


        }

        private async void HueSaturationThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FilteredImage.Width = PreviewImage.ActualWidth;
            FilteredImage.Height = PreviewImage.ActualHeight;

            PreviewImage.Visibility = Visibility.Collapsed;
            FilteredImage.Visibility = Visibility.Visible;
            using (_hueSaturationEffect = new HueSaturationEffect())
                await ApplyEffectAsync(fPhotoStream, _hueSaturationEffect, FilteredImage);
        }

        private void NormalThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }
    }
}
