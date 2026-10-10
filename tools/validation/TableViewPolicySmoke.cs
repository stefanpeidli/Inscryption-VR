using System;
using InscryptionVR;
internal static class TableViewPolicySmoke
{
    private static int _checks;
    private static void Check(bool result, string label) { _checks++; if (!result) throw new Exception(label); }
    private static void Main()
    {
        var policy = new TableViewPolicy();
        Check(!policy.Update(false, true, null) && policy.Current == TableViewMode.Seated, "Battle retains seated baseline");
        Check(policy.Update(true, true, null) && policy.Current == TableViewMode.MapOverview, "Map enters raised overview");
        Check(!policy.Update(true, true, null), "Map viewpoint must not recapture every frame");
        Check(policy.Update(true, true, TableViewMode.TopDown), "Manual top-down");
        Check(!policy.Update(true, true, null) && policy.Current == TableViewMode.TopDown, "Top-down held during map selection");
        Check(policy.Update(false, true, null) && policy.Current == TableViewMode.Seated, "Event entry restores seated mode");
        Check(!policy.Update(false, true, null), "No repeated seat switch after event");
        Check(!policy.Update(true, false, null) && policy.Current == TableViewMode.Seated, "Auto disabled retains seated view");
        Check(policy.Update(true, false, TableViewMode.TopDown), "Manual top-down works with auto disabled");
        Check(policy.Update(true, false, TableViewMode.Seated), "Manual seated override");
        Check(!policy.Update(true, true, null) && policy.Current == TableViewMode.Seated, "Manual seated persists during map");
        policy.Update(false, true, null);
        Check(policy.Update(true, true, null) && policy.Current == TableViewMode.MapOverview, "Next map clears stale manual override");
        Console.WriteLine("PASS: " + _checks + " stable map/viewpoint policy assertions.");
    }
}
