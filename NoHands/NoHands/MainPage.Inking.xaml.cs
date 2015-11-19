using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Input.Inking;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace NoHands
{
    partial class  MainPage : Page
    {

        InkRecognizerContainer inkRecognizerContainer = null;
        private IReadOnlyList<InkRecognizer> recoView = null;
        String result;

        public void inking_initialization()
        {

            
            myInkCanvas.Width = rootGrid.ActualWidth;
            myInkCanvas.Height = rootGrid.ActualHeight;

            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = Windows.UI.Colors.Black;
            drawingAttributes.Size = new Size(4, 4);
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;


            try
            {
                inkRecognizerContainer = new InkRecognizerContainer();
                recoView = inkRecognizerContainer.GetRecognizers();
                if (recoView.Count > 0)
                {
                    inkRecognizerContainer.SetDefaultRecognizer(recoView.First());
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("Couldn't initialize InkRecognizer");
            }

            myInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(
                drawingAttributes);
            myInkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen |
                Windows.UI.Core.CoreInputDeviceTypes.Touch;


        }

        void Clear_Click(object sender, RoutedEventArgs e)
        {
            myInkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        async void Recognize_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<InkStroke> currentStrokes =
                myInkCanvas.InkPresenter.StrokeContainer.GetStrokes();
            if (currentStrokes.Count > 0)
            {
                

                var recognitionResults = await inkRecognizerContainer.RecognizeAsync(
                    myInkCanvas.InkPresenter.StrokeContainer,
                    InkRecognitionTarget.All);

                if (recognitionResults.Count > 0)
                {
                    
                    // Display recognition result
                    string str = "Recognition result:";
                    foreach (var r in recognitionResults)
                    {
                        str += " " + r.GetTextCandidates()[0];
                    }
                    //Status.Text = str;
                    result = str;
                }
                else
                {
                    //Status.Text = "No text recognized.";
                    result = "No Text Recognized";
                }
                

            }
            
        }

    }
}
