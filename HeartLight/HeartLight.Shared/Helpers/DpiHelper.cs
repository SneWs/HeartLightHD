namespace HeartLight.Helpers
{
    public static class DpiHelper
    {
	    public static float ToLogical(float value, float dpi)
	    {
		    return value * 96.0f / dpi;
	    }

	    public static float ToPhysical(float value, float dpi)
	    {
		    return value * dpi / 96.0f;
	    }

		public static double ToLogical(double value, float dpi)
		{
			return value * 96.0f / dpi;
		}

		public static double ToPhysical(double value, float dpi)
		{
			return value * dpi / 96.0f;
		}
    }
}


