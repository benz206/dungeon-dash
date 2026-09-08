using System;

namespace DungeonDash
{
    [Flags]
    public enum TutorialAction { Move = 1, Attack = 2, Dash = 4, Clear = 8, Exit = 16, All = 31 }

    public static class FirstRunGuide
    {
        public static string Hint(int completed, bool inHub, bool touch)
        {
            bool Has(TutorialAction action) => (completed & (int)action) != 0;
            if ((completed & (int)TutorialAction.All) == (int)TutorialAction.All) return null;
            if (!Has(TutorialAction.Move)) return touch
                ? "MOVE · Drag the left pad to explore."
                : "MOVE · Use WASD to explore.";
            if (inHub) return touch
                ? "ENTER · Approach the north door, then tap DUNGEON."
                : "ENTER · Approach the north door, then press E.";
            if (!Has(TutorialAction.Attack)) return touch
                ? "ATTACK · Hold the right pad toward an enemy."
                : "ATTACK · Aim with the mouse. Hold left click.";
            if (!Has(TutorialAction.Dash)) return touch
                ? "EVADE · Move and tap DASH to burst out of danger."
                : "EVADE · Move and right click to dash out of danger.";
            if (!Has(TutorialAction.Clear)) return "CLEAR · Evade orange warnings and defeat every enemy.";
            return touch
                ? "DESCEND · Approach the open north door. Tap NEXT CHAMBER."
                : "DESCEND · Approach the open north door. Press E.";
        }
    }
}
