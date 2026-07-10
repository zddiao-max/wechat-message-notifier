using System;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal sealed class AnimationClock : IDisposable
    {
        private readonly Timer timer;

        internal AnimationClock(EventHandler tick)
        {
            timer = new Timer
            {
                Interval = MotionSettings.FrameIntervalMilliseconds
            };
            timer.Tick += tick;
        }

        internal bool IsRunning
        {
            get { return timer.Enabled; }
        }

        internal void Start()
        {
            if (!timer.Enabled)
            {
                timer.Start();
            }
        }

        internal void Stop()
        {
            timer.Stop();
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
