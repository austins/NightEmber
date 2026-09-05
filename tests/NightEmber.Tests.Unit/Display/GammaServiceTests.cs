using NightEmber.Display;

namespace NightEmber.Tests.Unit.Display;

public sealed class GammaServiceTests
{
    [Fact]
    public void Apply_AllDisplays_UsesIndependentHandlesAndExpectedRamp()
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        var rgb = ColorTemperature.ToRgb(3400);
        var expected = GammaRampBuilder.Build(rgb.Red, rgb.Green, rgb.Blue, 0.8, true);

        // Act
        var result = service.Apply(3400, 80);

        // Assert
        result.Should().BeTrue();
        devices.Writes.Select(static write => write.Handle).Should().Equal(1, 2);
        devices.Writes.Should().AllSatisfy(write => write.Ramp.Should().Equal(expected));
    }

    [Fact]
    public void Apply_TransientWriteFailure_RetriesWithoutReopening()
    {
        // Arrange
        var devices = new FakeDevices();
        devices.WriteResults.Enqueue(false);
        using var service = new GammaService(devices);

        // Act
        var applied = service.Apply(3400, 80);

        // Assert
        applied.Should().BeTrue();
        devices.Writes.Select(static write => write.Handle).Should().Equal(1, 1, 2);
        devices.OpenCount.Should().Be(2);
        devices.Closed.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_PersistentPartialFailure_ReopensAndAttemptsEveryDisplay(bool retrySucceeds)
    {
        // Arrange
        var devices = new FakeDevices();
        foreach (var result in new[] { false, false, true, retrySucceeds, retrySucceeds, true })
        {
            devices.WriteResults.Enqueue(result);
        }

        using var service = new GammaService(devices);

        // Act
        var applied = service.Apply(3400, 80);

        // Assert
        applied.Should().Be(retrySucceeds);
        devices.Closed.Should().Equal(1, 2);
        devices.OpenCount.Should().Be(4);
        devices.Writes.Should().Contain(static write => write.Handle == 2);
        devices.Writes.Should().Contain(static write => write.Handle == 4);
    }

    [Fact]
    public void Apply_FailedReplacement_RepairUsesLastSuccessfulState()
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        var initialApplied = service.Apply(3400, 80);
        var expected = devices.Writes[0].Ramp;
        for (var index = 0; index < 8; index++)
        {
            devices.WriteResults.Enqueue(false);
        }

        devices.Reads.Enqueue((true, 0));

        // Act
        var replacementApplied = service.Apply(5000, 90);
        service.RepairDrift();

        // Assert
        initialApplied.Should().BeTrue();
        replacementApplied.Should().BeFalse();
        devices.Writes.TakeLast(2).Should().AllSatisfy(write => write.Ramp.Should().Equal(expected));
    }

    [Fact]
    public void RepairDrift_NoSuccessfulRamp_DoesNotReadOrOpenDisplays()
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);

        // Act
        service.RepairDrift();
        service.OpenDisplays();
        service.RepairDrift();

        // Assert
        devices.ReadHandles.Should().BeEmpty();
        devices.Writes.Should().BeEmpty();
        devices.OpenCount.Should().Be(2);
    }

    [Fact]
    public void RepairDrift_FailedInitialApply_DoesNotRememberRejectedRamp()
    {
        // Arrange
        var devices = new FakeDevices();
        for (var index = 0; index < 8; index++)
        {
            devices.WriteResults.Enqueue(false);
        }

        using var service = new GammaService(devices);
        var initialApplied = service.Apply(3400, 80);

        // Act
        service.RepairDrift();

        // Assert
        initialApplied.Should().BeFalse();
        devices.ReadHandles.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepairDrift_ReadFailureOrWithinTolerance_DoesNotWrite(bool readSucceeds)
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        var initialApplied = service.Apply(3400, 80);
        var midpoint = devices.Writes[0].Ramp[640];
        devices.Reads.Enqueue((readSucceeds, (ushort)(midpoint + 256)));
        devices.Reads.Enqueue((readSucceeds, midpoint));
        devices.Writes.Clear();

        // Act
        service.RepairDrift();

        // Assert
        initialApplied.Should().BeTrue();
        devices.ReadHandles.Should().Equal(1, 2);
        devices.Writes.Should().BeEmpty();
    }

    [Fact]
    public void RepairDrift_LaterDisplayDrift_ReappliesAllDisplaysOnce()
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        var initialApplied = service.Apply(3400, 80);
        devices.Reads.Enqueue((false, 0));
        devices.Reads.Enqueue((true, 0));
        devices.Writes.Clear();

        // Act
        service.RepairDrift();

        // Assert
        initialApplied.Should().BeTrue();
        devices.ReadHandles.Should().Equal(1, 2);
        devices.Writes.Select(static write => write.Handle).Should().Equal(1, 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reset_IdentityRamp_OnlySuccessfulResetChangesRememberedState(bool succeeds)
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        var initialApplied = service.Apply(3400, 80);
        var previous = devices.Writes[0].Ramp;
        var identity = GammaRampBuilder.Build(1, 1, 1, 1, false);
        var expectedRepair = succeeds ? GammaRampBuilder.Build(1, 1, 1, 1, true) : previous;
        devices.Writes.Clear();
        if (!succeeds)
        {
            devices.WriteResults.Enqueue(false);
            devices.WriteResults.Enqueue(false);
        }

        devices.Reads.Enqueue((true, 0));

        // Act
        var reset = service.Reset();
        var resetWrites = devices.Writes.ToArray();
        devices.Writes.Clear();
        service.RepairDrift();

        // Assert
        initialApplied.Should().BeTrue();
        reset.Should().Be(succeeds);
        resetWrites.Should().AllSatisfy(write => write.Ramp.Should().Equal(identity));
        devices.OpenCount.Should().Be(2);
        devices.Writes.Should().AllSatisfy(write => write.Ramp.Should().Equal(expectedRepair));
        devices.Writes.Should().HaveCount(2);
    }

    [Fact]
    public void OpenDisplays_UnusableDisplay_IsSkippedAndExistingHandlesAreClosed()
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        service.OpenDisplays();
        devices.OpenResults.Enqueue(0);
        devices.OpenResults.Enqueue(20);

        // Act
        service.OpenDisplays();
        var reset = service.Reset();

        // Assert
        reset.Should().BeTrue();
        devices.Closed.Should().Equal(1, 2);
        devices.Writes.Should().ContainSingle().Which.Handle.Should().Be(20);
    }

    [Fact]
    public void OpenDisplays_NoUsableDisplay_ReportsNativeError()
    {
        // Arrange
        var devices = new FakeDevices();
        devices.OpenResults.Enqueue(0);
        devices.OpenResults.Enqueue(0);
        using var service = new GammaService(devices);

        // Act
        var open = service.OpenDisplays;

        // Assert
        open.Should().Throw<System.ComponentModel.Win32Exception>().Which.NativeErrorCode.Should().Be(5);
    }

    [Fact]
    public void ResetAllDisplays_OpensIndependently_WritesIdentityTwiceAndClosesHandles()
    {
        // Arrange
        var devices = new FakeDevices { DeviceNames = ["unavailable", "first", "second"] };
        devices.OpenResults.Enqueue(0);
        devices.OpenResults.Enqueue(10);
        devices.OpenResults.Enqueue(20);
        devices.WriteResults.Enqueue(false);
        var identity = GammaRampBuilder.Build(1, 1, 1, 1, false);

        // Act
        GammaService.ResetAllDisplays(devices);

        // Assert
        devices.Writes.Select(static write => write.Handle).Should().Equal(10, 10, 20, 20);
        devices.Writes.Should().AllSatisfy(write => write.Ramp.Should().Equal(identity));
        devices.Closed.Should().Equal(10, 20);
    }

    [Fact]
    public void ResetAllDisplays_WriteThrows_ClosesHandleAndPropagates()
    {
        // Arrange
        var devices = new FakeDevices { WriteException = new IOException("write failed") };

        // Act
        var reset = () => GammaService.ResetAllDisplays(devices);

        // Assert
        reset.Should().Throw<IOException>().WithMessage("write failed");
        devices.Closed.Should().Equal(1);
    }

    [Fact]
    public void Dispose_RepeatedCalls_ClosesHandlesOnceAndRejectsFurtherUse()
    {
        // Arrange
        var devices = new FakeDevices();
        using var service = new GammaService(devices);
        service.OpenDisplays();
        Action[] operations =
        [
            service.OpenDisplays, () => service.Apply(3400, 80), () => service.Reset(), service.RepairDrift
        ];

        // Act
#pragma warning disable IDISP016, IDISP017 // Explicit repeated disposal exercises idempotence and disposed guards.
        service.Dispose();
        service.Dispose();
#pragma warning restore IDISP016, IDISP017

        // Assert
        devices.Closed.Should().Equal(1, 2);
        foreach (var operation in operations)
        {
            operation.Should().Throw<ObjectDisposedException>();
        }
    }

    [Theory]
    [InlineData(30000, 30000, false)]
    [InlineData(30000, 29745, false)]
    [InlineData(30000, 29744, false)]
    [InlineData(30000, 29743, true)]
    [InlineData(30000, 30255, false)]
    [InlineData(30000, 30256, false)]
    [InlineData(30000, 30257, true)]
    public void HasDrifted_ExpectedAndActualBlueValues_UsesExclusiveThreshold(int expected, int actual, bool hasDrifted)
    {
        // Act
        var result = GammaService.HasDrifted((ushort)expected, (ushort)actual);

        // Assert
        result.Should().Be(hasDrifted);
    }

    private sealed class FakeDevices : IGammaDeviceApi
    {
        public string[] DeviceNames { get; init; } = ["first", "second"];

        public Queue<nint> OpenResults { get; } = new();

        public Queue<bool> WriteResults { get; } = new();

        public Queue<(bool Success, ushort Blue)> Reads { get; } = new();

        public List<(nint Handle, ushort[] Ramp)> Writes { get; } = [];

        public List<nint> ReadHandles { get; } = [];

        public List<nint> Closed { get; } = [];

        public Exception? WriteException { get; init; }

        public int OpenCount { get; private set; }

        public IEnumerable<string> GetDeviceNames()
        {
            return DeviceNames;
        }

        public nint Open(string deviceName)
        {
            OpenCount++;
            return OpenResults.TryDequeue(out var handle) ? handle : OpenCount;
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

            return !WriteResults.TryDequeue(out var result) || result;
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
}
