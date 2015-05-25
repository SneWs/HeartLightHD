using Windows.Foundation;

namespace HeartLight.Helpers
{
    public static class PointExtensions
    {
	    public static Point ToLogical(this Point pt, float dpi)
	    {
			return new Point(DpiHelper.ToLogical(pt.X, dpi), DpiHelper.ToLogical(pt.Y, dpi));
	    }

	    public static Point ToPhysical(this Point pt, float dpi)
	    {
			return new Point(DpiHelper.ToPhysical(pt.X, dpi), DpiHelper.ToPhysical(pt.Y, dpi));
	    }
    }
}
