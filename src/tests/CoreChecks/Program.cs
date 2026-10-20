using System;
using System.Linq;
using ItemMarkers.Core;

int passed = 0;
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
Test("Explicit supported tier mapping and unknown exclusion", () =>
{
    var tiers = new[] { "Tier1", "Tier2", "Tier3", "Boss", "Lunar", "VoidTier1", "VoidTier2", "VoidTier3", "VoidBoss", "Cursed", "Hex", "Page" };
    var expected = new[] { MarkerCategory.White, MarkerCategory.Green, MarkerCategory.Red, MarkerCategory.Boss, MarkerCategory.Lunar,
        MarkerCategory.Void, MarkerCategory.Void, MarkerCategory.Void, MarkerCategory.Void, MarkerCategory.Hallowed, MarkerCategory.Hex, MarkerCategory.Pages };
    for (int i = 0; i < tiers.Length; i++) Assert(MarkerRules.Classify(tiers[i], false) == expected[i], tiers[i]);
    Assert(MarkerRules.Classify("NoTier", false) == MarkerCategory.Unknown, "Untiered helper excluded");
    Assert(MarkerRules.Classify("UnknownModTier", false) == MarkerCategory.Unknown, "Unmapped tier excluded");
    Assert(MarkerRules.Classify("Lunar", true) == MarkerCategory.Equipment, "Lunar equipment uses equipment filter");
});
Test("Category and variant filters compose independently", () =>
{
    for (int flags = 0; flags < 32; flags++)
    {
        bool category = (flags & 1) != 0, temporary = (flags & 2) != 0, ethereal = (flags & 4) != 0;
        bool showTemporary = (flags & 8) != 0, showEthereal = (flags & 16) != 0;
        bool expected = category && (!temporary || showTemporary) && (!ethereal || showEthereal);
        Assert(MarkerRules.Allows(MarkerCategory.White, category, temporary, ethereal, showTemporary, showEthereal) == expected, "Filter combination " + flags);
    }
    Assert(!MarkerRules.Allows(MarkerCategory.Unknown, true, false, false, true, true), "Unknown cannot be enabled");
});
Test("Zero limit hides all candidates", () =>
{
    var nearest = new NearestMarkers<int>();
    for (int i = 0; i < 200; i++) nearest.Consider(i, i, i, 0);
    Assert(nearest.Count == 0, "No marker with limit zero");
});
Test("Nearest candidates win across 1000 discovered pickups", () =>
{
    var random = new Random(1042);
    var input = Enumerable.Range(0, 1000).Select(id => (id, distance: (float)random.Next(0, 10000))).ToArray();
    foreach (int limit in new[] { 1, 4, 32, 60 })
    {
        var nearest = new NearestMarkers<int>();
        foreach (var item in input) nearest.Consider(item.id, item.distance, item.id, limit);
        var expected = input.OrderBy(item => item.distance).ThenBy(item => item.id).Take(limit).ToArray();
        Assert(nearest.Count == limit, "Exact limit " + limit);
        for (int i = 0; i < limit; i++) Assert(nearest[i] == expected[i].id, "Nearest ordering " + i);
    }
});
Test("Equal distances have a stable identity ordering", () =>
{
    var nearest = new NearestMarkers<int>();
    for (int i = 99; i >= 0; i--) nearest.Consider(i, 1f, i, 60);
    for (int i = 0; i < 60; i++) Assert(nearest[i] == i, "Stable tie " + i);
});
Test("Invalid candidates excluded and selection reset releases old entries", () =>
{
    var nearest = new NearestMarkers<int>();
    nearest.Consider(1, float.NaN, 1, 60); nearest.Consider(2, float.PositiveInfinity, 2, 60); nearest.Consider(3, -1, 3, 60);
    Assert(nearest.Count == 0, "Invalid distances excluded");
    nearest.Consider(4, 0, 4, 60); nearest.Clear(); Assert(nearest.Count == 0, "No stale selection");
});
Test("Marker size clamps and snaps safely", () =>
{
    Assert(Math.Abs(MarkerRules.ScaleFromPercent(100) - 1) < 0.0001, "100 percent");
    Assert(Math.Abs(MarkerRules.ScaleFromPercent(52.5f) - 0.55f) < 0.0001, "5 percent steps");
    Assert(MarkerRules.ScaleFromPercent(float.NaN) == 1, "Invalid config value");
    Assert(MarkerRules.ScaleFromPercent(-10) == 0.5f && MarkerRules.ScaleFromPercent(250) == 2, "Bounds");
    Assert(MarkerRules.ClampLimit(-1) == 0 && MarkerRules.ClampLimit(100) == 60, "Limit bounds");
});
Test("Visible center stays centered", () =>
{
    Assert(MarkerProjection.TryPlace(.5f, .5f, 1, 1920, 1080, 45, 48, out var p), "Valid projection");
    Assert(p.X == 0 && p.Y == 0 && !p.AtEdge, "Center placement");
});
Test("Offscreen projection stays on a safe edge across aspect ratios", () =>
{
    foreach (var size in new[] { (1920f, 1080f), (3440f, 1440f), (960f, 1080f), (640f, 360f) })
    foreach (float x in new[] { -10f, -1f, 0.5f, 2f, 10f })
    foreach (float y in new[] { -10f, -1f, 0.5f, 2f, 10f })
    {
        Assert(MarkerProjection.TryPlace(x, y, 1, size.Item1, size.Item2, 90, 96, out var p), "Finite");
        Assert(Math.Abs(p.X) <= size.Item1 / 2 - 90 + .01f && Math.Abs(p.Y) <= size.Item2 / 2 - 96 + .01f, "Safe bounds");
        if (p.AtEdge) Assert(Math.Abs(Math.Abs(p.X) - (size.Item1 / 2 - 90)) < .01f
            || Math.Abs(Math.Abs(p.Y) - (size.Item2 / 2 - 96)) < .01f, "On edge");
    }
});
Test("Behind-camera direction is mirrored and remains visible", () =>
{
    Assert(MarkerProjection.TryPlace(.75f, .5f, -1, 1920, 1080, 45, 48, out var p) && p.X < 0 && p.AtEdge, "Behind left");
    Assert(MarkerProjection.TryPlace(.5f, .5f, -1, 1920, 1080, 45, 48, out p) && p.Y < 0 && p.AtEdge, "Directly behind fallback");
});
Test("Invalid projection input cannot reach the UI", () =>
{
    Assert(!MarkerProjection.TryPlace(float.NaN, .5f, 1, 1920, 1080, 45, 48, out _), "NaN");
    Assert(!MarkerProjection.TryPlace(.5f, .5f, float.PositiveInfinity, 1920, 1080, 45, 48, out _), "Infinite depth");
    Assert(!MarkerProjection.TryPlace(.5f, .5f, 1, 0, 1080, 45, 48, out _), "Zero area");
});
Test("Proximity hiding uses the exact distance and preserves the boundary", () =>
{
    Assert(!MarkerRules.ShouldShowAtDistance(4.99f * 4.99f, 5), "Inside 5 meters hidden");
    Assert(MarkerRules.ShouldShowAtDistance(25, 5), "Exactly 5 meters visible");
    Assert(MarkerRules.ShouldShowAtDistance(5.01f * 5.01f, 5), "Outside 5 meters visible");
    Assert(!MarkerRules.ShouldShowAtDistance(0, 1), "One meter setting hides coincident pickup");
    Assert(!MarkerRules.ShouldShowAtDistance(2499, 50) && MarkerRules.ShouldShowAtDistance(2500, 50), "Maximum radius");
});
Test("Zero proximity radius disables hiding while invalid positions stay excluded", () =>
{
    Assert(MarkerRules.ShouldShowAtDistance(0, 0), "Zero meters also shows coincident pickups");
    Assert(MarkerRules.ShouldShowAtDistance(1, 0), "Near pickup visible");
    Assert(MarkerRules.ShouldShowAtDistance(10000, 0), "Far pickup visible");
    Assert(!MarkerRules.ShouldShowAtDistance(float.NaN, 0), "Invalid distance excluded");
    Assert(!MarkerRules.ShouldShowAtDistance(float.PositiveInfinity, 5), "Infinite position excluded");
    Assert(!MarkerRules.ShouldShowAtDistance(-1, 0), "Negative squared distance excluded");
    Assert(MarkerRules.ShouldShowAtDistance(0, -1) && MarkerRules.ShouldShowAtDistance(2500, 51), "Config bounds clamp");
});
Test("Nearby hidden pickups leave the display limit available for farther markers", () =>
{
    var nearest = new NearestMarkers<int>();
    foreach (int meters in new[] { 1, 2, 3, 4, 8, 6, 10 })
    {
        float squared = meters * meters;
        if (MarkerRules.ShouldShowAtDistance(squared, 5)) nearest.Consider(meters, squared, meters, 2);
    }
    Assert(nearest.Count == 2 && nearest[0] == 6 && nearest[1] == 8, "Two nearest visible markers selected");
});
Test("Pending icons share one request and become available without restarting", () =>
{
    var request = new FakeAssetRequest();
    int loads = 0, errors = 0;
    var cache = new AsyncAssetCache<string, object>(_ => { loads++; return request; }, (_, _) => errors++);
    Assert(cache.Get("white") == null && cache.Get("white") == null, "No sprite before completion");
    Assert(loads == 1, "One request for repeated bindings/multiple HUDs");
    var sprite = new object(); request.Result = sprite; request.Done = true;
    Assert(ReferenceEquals(cache.Get("white"), sprite), "Late loaded sprite available on rebind");
    Assert(loads == 1 && errors == 0, "No reloading or spurious error");
    cache.Clear();
});
Test("Failed icons do not reload or log on every sweep", () =>
{
    int loads = 0, errors = 0;
    var request = new FakeAssetRequest { Done = true, Failure = new InvalidOperationException("Invalid address") };
    var cache = new AsyncAssetCache<string, object>(_ => { loads++; return request; }, (_, _) => errors++);
    for (int i = 0; i < 100; i++) Assert(cache.Get("missing") == null, "Failed icon stays unavailable");
    Assert(loads == 1 && errors == 1, "One request and one contextual error");
    cache.Clear(); Assert(request.ReleaseCount == 1, "Failed load handle released");
});
Test("Synchronous load failures remain isolated from other icons", () =>
{
    int loads = 0, errors = 0;
    var good = new object();
    var cache = new AsyncAssetCache<string, object>(key =>
    {
        loads++;
        if (key == "bad") throw new InvalidOperationException("No key");
        return new FakeAssetRequest { Done = true, Result = good };
    }, (_, _) => errors++);
    cache.Get("bad"); cache.Get("bad");
    Assert(ReferenceEquals(cache.Get("good"), good), "Other icons still resolve");
    Assert(loads == 2 && errors == 1, "Thrown failure remembered");
    cache.Clear();
});
Test("Completed loads without sprites produce a bounded diagnostic", () =>
{
    int errors = 0;
    var cache = new AsyncAssetCache<string, object>(_ => new FakeAssetRequest { Done = true }, (_, _) => errors++);
    cache.Get("empty"); cache.Get("empty"); Assert(errors == 1, "Missing result reported once");
    cache.Clear();
});
Test("Run cleanup releases pending and completed icon handles exactly once", () =>
{
    var pending = new FakeAssetRequest();
    var complete = new FakeAssetRequest { Done = true, Result = new object() };
    var cache = new AsyncAssetCache<string, object>(key => key == "pending" ? pending : complete, (_, _) => { });
    cache.Get("pending"); cache.Get("complete"); cache.Clear(); cache.Clear();
    Assert(pending.ReleaseCount == 1 && complete.ReleaseCount == 1, "No pending leak or double release");
});
Test("A late old-run completion cannot replace a new-run icon", () =>
{
    var oldRequest = new FakeAssetRequest();
    var newRequest = new FakeAssetRequest { Done = true, Result = new object() };
    int loads = 0;
    var cache = new AsyncAssetCache<string, object>(_ => ++loads == 1 ? oldRequest : newRequest, (_, _) => { });
    cache.Get("item"); cache.Clear();
    oldRequest.Result = new object(); oldRequest.Done = true;
    Assert(ReferenceEquals(cache.Get("item"), newRequest.Result), "New run owns a new request");
    Assert(loads == 2 && oldRequest.ReleaseCount == 1, "Old handle was released");
    cache.Clear();
});
Console.WriteLine($"{passed} test groups passed.");

sealed class FakeAssetRequest : IAssetRequest<object>
{
    public bool Done;
    public object? Result;
    public Exception? Failure;
    public int ReleaseCount;
    public bool IsDone => Done;
    public object? Asset => Result;
    public Exception? Error => Failure;
    public void Dispose() => ReleaseCount++;
}
