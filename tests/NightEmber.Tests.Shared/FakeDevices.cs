using NightEmber.Core.Display;
using System.Collections.ObjectModel;

namespace NightEmber.Tests.Shared;

public sealed class FakeDevices : IGammaDeviceApi
{
    private readonly Dictionary<nint, string> _deviceNamesByHandle = [];

    public IReadOnlyList<string> Names { get; init; } = ["first", "second"];

    public Dictionary<string, ushort[]> CurrentRamps { get; } = [];

    public Queue<nint> OpenResults { get; } = new();

    public Queue<bool> WriteResults { get; } = new();

    public Queue<(bool Success, ushort Blue)> Reads { get; } = new();

    public Collection<(nint Handle, ushort[] Ramp)> Writes { get; } = [];

    public Collection<nint> ReadHandles { get; } = [];

    public Collection<nint> Closed { get; } = [];

    public Exception? WriteException { get; init; }

    public int OpenCount { get; private set; }

    public IEnumerable<string> GetDeviceNames()
    {
        return Names;
    }

    public nint Open(string deviceName)
    {
        OpenCount++;
        var handle = OpenResults.TryDequeue(out var result) ? result : OpenCount;
        if (handle != 0)
        {
            _deviceNamesByHandle[handle] = deviceName;
        }

        return handle;
    }

    public int GetLastError()
    {
        return 5;
    }

    public bool Write(nint handle, ushort[] ramp)
    {
        Writes.Add((handle, (ushort[])ramp.Clone()));
        if (WriteException is not null)
        {
            throw WriteException;
        }

        var accepted = !WriteResults.TryDequeue(out var result) || result;
        if (accepted)
        {
            CurrentRamps[_deviceNamesByHandle[handle]] = (ushort[])ramp.Clone();
        }

        return accepted;
    }

    public bool Read(nint handle, ushort[] ramp)
    {
        ReadHandles.Add(handle);
        if (!Reads.TryDequeue(out var result))
        {
            return false;
        }

        ramp[640] = result.Blue;
        return result.Success;
    }

    public void Close(nint handle)
    {
        Closed.Add(handle);
    }
}
