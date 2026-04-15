using AkkornStudio.UI.Services.Canvas.AutoJoin;
using AkkornStudio.UI.Services.Explain;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.Tests.Unit.ViewModels.Canvas;

public class ExplainSimulationLatencyOptionsTests
{
    [Fact]
    public void ResolveDelayMs_UsesZero_WhenEnvironmentVariableMissing()
    {
        string? previous = Environment.GetEnvironmentVariable(
            ExplainSimulationLatencyOptions.DelayEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                null
            );

            var sut = new ExplainSimulationLatencyOptions();
            int result = sut.ResolveDelayMs();

            Assert.Equal(ExplainSimulationLatencyOptions.BuiltInDefaultDelayMs, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                previous
            );
        }
    }

    [Fact]
    public void ResolveDelayMs_UsesEnvironmentValue_WhenValid()
    {
        string? previous = Environment.GetEnvironmentVariable(
            ExplainSimulationLatencyOptions.DelayEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                "320"
            );

            var sut = new ExplainSimulationLatencyOptions();
            int result = sut.ResolveDelayMs();

            Assert.Equal(320, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                previous
            );
        }
    }

    [Fact]
    public void ResolveDelayMs_ClampsNegativeValues_ToZero()
    {
        string? previous = Environment.GetEnvironmentVariable(
            ExplainSimulationLatencyOptions.DelayEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                "-50"
            );

            var sut = new ExplainSimulationLatencyOptions();
            int result = sut.ResolveDelayMs();

            Assert.Equal(0, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                previous
            );
        }
    }

    [Fact]
    public void ResolveDelayMs_ClampsTooLargeValues_ToMaximum()
    {
        string? previous = Environment.GetEnvironmentVariable(
            ExplainSimulationLatencyOptions.DelayEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                "99999"
            );

            var sut = new ExplainSimulationLatencyOptions();
            int result = sut.ResolveDelayMs();

            Assert.Equal(ExplainSimulationLatencyOptions.MaximumDelayMs, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                previous
            );
        }
    }

    [Fact]
    public void ResolveDelayMs_UsesDefault_WhenEnvironmentIsInvalid()
    {
        string? previous = Environment.GetEnvironmentVariable(
            ExplainSimulationLatencyOptions.DelayEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                "invalid"
            );

            var sut = new ExplainSimulationLatencyOptions();
            int result = sut.ResolveDelayMs();

            Assert.Equal(ExplainSimulationLatencyOptions.BuiltInDefaultDelayMs, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ExplainSimulationLatencyOptions.DelayEnvironmentVariable,
                previous
            );
        }
    }
}


