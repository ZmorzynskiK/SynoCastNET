using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SynoCastNET
{
    // from YoutubeExplode
    internal class ConsolePercentProgress(TextWriter writer) : IProgress<double>, IDisposable
    {
        private readonly int _posX = Console.CursorLeft;
        private readonly int _posY = Console.CursorTop;

        private int _lastLength;

        public ConsolePercentProgress()
            : this(Console.Out) { }

        private void EraseLast()
        {
            if (_lastLength > 0)
            {
                Console.SetCursorPosition(_posX, _posY);
                writer.Write(new string(' ', _lastLength));
                Console.SetCursorPosition(_posX, _posY);
            }
        }

        private void Write(string text)
        {
            EraseLast();
            writer.Write(text);
            _lastLength = text.Length;
        }

        public void Report(double progress) => Write($"{progress:P1}");

        public void Dispose() => EraseLast();
    }

    internal class ConsoleStarProgress(TextWriter writer) : IProgress<double>, IDisposable
    {
        private int _lastProgress;

        public ConsoleStarProgress()
            : this(Console.Out) { }

        public void Report(double progress)
        {
            // just add star after each 1 percent
            int thisProgress = (int)Math.Round(progress * 100.0, MidpointRounding.AwayFromZero);
            if(thisProgress != _lastProgress)
            {
                writer.Write("*");
                _lastProgress = thisProgress;
            }
        }

        public void Dispose()
        {
        }
    }
}
