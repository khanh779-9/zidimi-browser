using System;
using CefSharp;
using CefSharp.Structs;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Tracks the tab's audio playback state (spec 10.4 — audio indicator).
/// Counts active audio streams; if any are running, reports that audio is playing.
/// </summary>
public sealed class AudioHandler : CefSharp.Handler.AudioHandler
{
    private int _activeStreams;

    public event Action<bool>? PlaybackStateChanged;

    protected override bool GetAudioParameters(IWebBrowser browserControl, IBrowser browser,
        ref AudioParameters parameters)
    {
        // Allow receiving audio data (so we know when playback starts/stops).
        return true;
    }

    protected override void OnAudioStreamStarted(IWebBrowser browserControl, IBrowser browser,
        AudioParameters parameters, int channels)
    {
        System.Threading.Interlocked.Increment(ref _activeStreams);
        PlaybackStateChanged?.Invoke(true);
    }

    protected override void OnAudioStreamPacket(IWebBrowser browserControl, IBrowser browser,
        IntPtr data, int noOfFrames, long pts)
    {
        // No need to process raw data — we only need the state.
    }

    protected override void OnAudioStreamStopped(IWebBrowser browserControl, IBrowser browser)
    {
        if (System.Threading.Interlocked.Decrement(ref _activeStreams) <= 0)
            PlaybackStateChanged?.Invoke(false);
    }

    protected override void OnAudioStreamError(IWebBrowser browserControl, IBrowser browser, string errorMessage)
    {
        _activeStreams = 0;
        PlaybackStateChanged?.Invoke(false);
    }
}
