﻿using System;
using System.Threading;
using System.Windows.Threading;
using Xunit;
using Vim.EditorHost;
using Vim.UI.Wpf.Implementation.Misc;
using Vim.UnitTest;

namespace Vim.UI.Wpf.UnitTest
{
    public sealed class KeyMappingTimeoutHandlerTest : VimTestBase
    {
        private readonly KeyMappingTimeoutHandler.TimerData _timerData;
        private readonly IVimBuffer _vimBuffer;

        public KeyMappingTimeoutHandlerTest()
        {
            Vim.GlobalSettings.Timeout = true;
            Vim.GlobalSettings.TimeoutLength = 100;

            _vimBuffer = CreateVimBuffer("");
            if (!KeyMappingTimeoutHandler.TryGetTimerData(_vimBuffer, out _timerData))
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Central place for waiting for the timer to expire.  The timer fires at Input priority so
        /// wait the timeout and queue at a lower priority event
        /// </summary>
        private void WaitForTimer()
        {
            var happened = false;
            var count = 0;
            EventHandler handler = delegate { happened = true; };
            _timerData.Tick += handler;

            while (!happened && count < 20)
            {
                Thread.Sleep(Vim.GlobalSettings.TimeoutLength);
                Dispatcher.CurrentDispatcher.DoEvents();
                count++;
            }

            _timerData.Tick -= handler;
            Assert.True(happened);
        }

        /// <summary>
        /// A timeout after a single key stroke should cause the keystroke to 
        /// be processed
        /// </summary>
        [WpfFact]
        public void Timeout_Single()
        {
            _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping("cat", "chase the cat", allowRemap: false, KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            WaitForTimer();
            Assert.Equal("c", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// A timeout after a double key stroke should cause the buffered keystrokes to 
        /// be processed
        /// </summary>
        [WpfFact]
        public void Timeout_Double()
        {
            _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping("cat", "chase the cat", allowRemap: false, KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('a');
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            WaitForTimer();
            Assert.Equal("ca", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        [WpfFact]
        public void NoTimeout()
        {
            _vimBuffer.Vim.GlobalSettings.TimeoutLength = 1000;
            _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping("cat", "chase the cat", allowRemap: false, KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            _vimBuffer.Process('a');
            Thread.Sleep(50);
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('t');
            Assert.Equal("chase the cat", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Setting notimeout should prevent commands from timing out.
        /// </summary>
        [WpfFact]
        public void NoTimeoutSetting()
        {
            _vimBuffer.Vim.GlobalSettings.TimeoutLength = 5;
            _vimBuffer.Vim.GlobalSettings.Timeout = false;
            _vimBuffer.Vim.GlobalKeyMap.AddKeyMapping("cat", "chase the cat", allowRemap: false, KeyRemapMode.Insert);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _vimBuffer.Process('c');
            _vimBuffer.Process('a');
            Thread.Sleep(10);
            Dispatcher.CurrentDispatcher.DoEvents();
            Assert.Equal("", _vimBuffer.TextBuffer.GetLine(0).GetText());
            _vimBuffer.Process('t');
            Assert.Empty(_vimBuffer.BufferedKeyInputs);
            Assert.Equal("chase the cat", _vimBuffer.TextBuffer.GetLine(0).GetText());
        }
    }
}
