using CSCore;
using CSCore.SoundOut;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdamS2T2Docs
{
    public class SilenceGenerator : IWaveSource
    {
        private readonly WaveFormat _waveFormat = new WaveFormat(16000, 16, 1);       


        public int Read(byte[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        public WaveFormat WaveFormat
        {
            get { return _waveFormat; }
        }

        public long Position
        {
            get { return -1; }
            set
            {
                throw new InvalidOperationException();
            }
        }

        public long Length
        {
            get { return -1; }
        }

        public bool CanSeek => throw new NotImplementedException();

        public void Dispose()
        {
            //do nothing
        }
    }
}
