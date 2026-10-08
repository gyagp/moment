using Shike.Core;

var assertions = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    assertions++;
}
var rng = new Random(2817);
var data = new byte[180 * 1400 * 4];
rng.NextBytes(data);
var document = new PixelFrame(180, 1400, data);
var stitcher = new ScrollStitcher();
Check(stitcher.Add(document.Crop(0, 0, 180, 400)).Status == StitchStatus.Added, "First frame");
Check(stitcher.Add(document.Crop(0, 0, 180, 400)).Status == StitchStatus.Unchanged, "Duplicate frame");
foreach (var y in new[] { 117, 294, 501, 740, 1000 })
    Check(stitcher.Add(document.Crop(0, y, 180, 400)).Status == StitchStatus.Added, $"Offset {y}");
Check(stitcher.Build().Pixels.SequenceEqual(data), "Pixel-exact reconstruction across multiple scrolls");
Check(stitcher.Add(document.Crop(0, 850, 180, 400)).Status == StitchStatus.NoMatch, "Reject upward scroll");
var unrelated = new byte[180 * 400 * 4];
rng.NextBytes(unrelated);
Check(stitcher.Add(new(180, 400, unrelated)).Status == StitchStatus.NoMatch, "Reject unrelated frame");
Check(stitcher.Height == 1400, "Rejected frame must not mutate result");
var blank = new ScrollStitcher();
Check(blank.Add(new(100, 200, new byte[100 * 200 * 4])).Status == StitchStatus.Added, "Blank first frame");
Check(blank.Add(new(100, 200, new byte[100 * 200 * 4])).Status == StitchStatus.Unchanged, "Blank duplicate");
var tooTall = new ScrollStitcher();
Check(tooTall.Add(new(1, ScrollStitcher.MaxHeight + 1, new byte[(ScrollStitcher.MaxHeight + 1) * 4])).Status == StitchStatus.LimitReached, "Height guard");
var badSize = false;
try { _ = new PixelFrame(10, 10, new byte[3]); } catch (ArgumentException) { badSize = true; }
Check(badSize, "Invalid image buffer rejected");
Console.WriteLine($"PASS: {assertions} assertions (scroll reconstruction, rejection, bounds).");
