namespace AkkornStudio.Nodes.Pins;

public sealed record ClearComparisonScalarMutation(
    string NodeId) : IPinMutation
{
    public PinConnectionReasonCode ReasonCode => PinConnectionReasonCode.None;
}
