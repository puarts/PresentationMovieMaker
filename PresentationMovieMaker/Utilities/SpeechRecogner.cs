using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.Utilities
{
    public class SpeechRecognizer : IDisposable
    {
        public SpeechRecognizer()
        {
            // Create and load a dictation grammar.  
            SpeechRecognitionEngine.LoadGrammar(new DictationGrammar());
        }


        public void Dispose()
        {
            SpeechRecognitionEngine.Dispose();
        }

        public SpeechRecognitionEngine SpeechRecognitionEngine { get; } = new SpeechRecognitionEngine(
                new System.Globalization.CultureInfo("ja-JP"));


        public void RecognizeAsync(Stream waveStream)
        {
            var recognizer = SpeechRecognitionEngine;
            recognizer.SetInputToWaveStream(waveStream);

            // Start asynchronous, continuous speech recognition.  
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }
    }
}
