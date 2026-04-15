using AkkornStudio.UI.Services.Canvas.AutoJoin;
using AkkornStudio.UI.Services.Explain;
using System.Reflection;
using AkkornStudio.UI.ViewModels;
using Xunit;

namespace AkkornStudio.Tests.Unit.ViewModels.Canvas;

public class ValidationManagerSafeExecutionTests
{
    [Fact]
    public void ValidationManager_HasSafeWrapperForPostedValidation()
    {
        Type validationType = typeof(CanvasViewModel)
            .Assembly
            .GetType("AkkornStudio.UI.ViewModels.Canvas.ValidationManager")!;

        Assert.NotNull(validationType.GetMethod("RunValidationSafely", BindingFlags.Instance | BindingFlags.NonPublic));
    }
}


