using Lumia.Imaging;
using Lumia.Imaging.Adjustments;
using Lumia.Imaging.Artistic;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
            var fPhotoStream = new InMemoryRandomAccessStream();

            await mediaCapture.CapturePhotoToStreamAsync(imageProperties, fPhotoStream);
            await fPhotoStream.FlushAsync();
            fPhotoStream.Seek(0);
            await mediaCapture.StopPreviewAsync();
            captureElement.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Visible;

            var _bmp = new BitmapImage();
            _bmp.SetSource(fPhotoStream);
            PreviewImage.Source = _bmp;
            NormalThumb.Source = _bmp;

            using (_grayscaleEffect = new GrayscaleEffect())
                ApplyEffectAsync(fPhotoStream, _grayscaleEffect, GreyScaleThumb);

            using (_colorboostEffect = new ColorBoostEffect())
            {
                _colorboostEffect.Gain = 0.75;
                ApplyEffectAsync(fPhotoStream, _colorboostEffect, ColorBoostThumb);
            }

            using (_hueSaturationEffect = new HueSaturationEffect())
                ApplyEffectAsync(fPhotoStream, _hueSaturationEffect, HueSaturationThumb);

            using (_lensblurEffect = new LensBlurEffect())
                ApplyEffectAsync(fPhotoStream, _lensblurEffect, LensBlurThumb);

            using (_antiqueEffect = new AntiqueEffect())
                ApplyEffectAsync(fPhotoStream, _antiqueEffect, SepiaThumb);
        }

        /// <summary>
        /// TODO: Apply filter to image
        /// </summary>
        /// <param name="fileStream"></param>
        private async void ApplyEffectAsync(IRandomAccessStream fileStream, IImageProvider provider, SwapChainPanel target)
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

        private void GreyScaleThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            return;
            // TODO
            PreviewImage.Visibility = Visibility.Collapsed;
            GreyScaleThumb.SetValue(Grid.RowProperty, 0);
            
            GreyScaleThumb.Width = this.ActualWidth;
            GreyScaleThumb.Height = this.ActualHeight;
            GreyScaleThumb.UpdateLayout();
        }

        private void ColorBoostThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }
    }
}
