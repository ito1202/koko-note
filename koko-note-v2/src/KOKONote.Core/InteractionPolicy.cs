namespace KOKONote.Core;

public enum ToggleAction { Show, Hide, Defer }

public static class InteractionPolicy
{
    public static ToggleAction Toggle(bool visible, bool foreground, bool composing) =>
        composing ? ToggleAction.Defer : visible && foreground ? ToggleAction.Hide : ToggleAction.Show;

    public static int NextTab(int current, int count, bool reverse) =>
        count <= 0 ? -1 : (current + (reverse ? count - 1 : 1)) % count;
}
