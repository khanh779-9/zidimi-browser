using System;
using CefSharp;
using CefSharp.Structs;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Theo dõi trạng thái phát âm thanh của tab (spec 10.4 — chỉ báo âm thanh).
/// Đếm số luồng audio đang chạy; đang chạy &gt; 0 thì báo đang phát.
/// </summary>
public sealed class AudioHandler : CefSharp.Handler.AudioHandler
{
    private int _activeStreams;

    public event Action<bool>? PlaybackStateChanged;

    protected override bool GetAudioParameters(IWebBrowser browserControl, IBrowser browser,
        ref AudioParameters parameters)
    {
        // Cho phép nhận dữ liệu audio (để biết khi nào bắt đầu/dừng).
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
        // Không cần xử lý dữ liệu thô — chỉ cần biết trạng thái.
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
