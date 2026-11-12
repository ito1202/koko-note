using KOKONote.Core;

namespace KOKONote.Tests;

public class InteractionTests
{
    [Theory]
    [InlineData(false, false, false, ToggleAction.Show)]
    [InlineData(true, false, false, ToggleAction.Show)]
    [InlineData(true, true, false, ToggleAction.Hide)]
    [InlineData(true, true, true, ToggleAction.Defer)]
    public void HotkeyTransitions(bool visible, bool foreground, bool composing, ToggleAction expected) =>
        Assert.Equal(expected, InteractionPolicy.Toggle(visible, foreground, composing));

    [Fact]
    public void TabsWrapInBothDirections()
    {
        Assert.Equal(0, InteractionPolicy.NextTab(2, 3, false));
        Assert.Equal(2, InteractionPolicy.NextTab(0, 3, true));
    }

    [Fact]
    public void InstanceLeaseRejectsDuplicateAndReleasesOnDispose()
    {
        var name = "KOKONote-test-" + Guid.NewGuid().ToString("N");
        using (var first = new InstanceLease(name))
        using (var duplicate = new InstanceLease(name))
        { Assert.True(first.IsFirst); Assert.False(duplicate.IsFirst); }
        using var next = new InstanceLease(name); Assert.True(next.IsFirst);
    }
}
