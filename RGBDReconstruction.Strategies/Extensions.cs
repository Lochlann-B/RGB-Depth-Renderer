namespace RGBDReconstruction.Strategies;

static class Extensions
{
    public static float RoundToInterval(this float x, float interval)
    {
        if (interval == 0f)
        {
            throw new ArgumentException("Can't round to nearest 0.");
        }

        return (float) Math.Round(x / interval) * interval;
    }

    public static float FloorToInterval(this float x, float interval)
    {
        if (interval == 0f)
        {
            throw new ArgumentException("Can't round to nearest 0.");
        }

        return (float) Math.Floor(x / interval) * interval;
    }
    
    public static float CeilToInterval(this float x, float interval)
    {
        if (interval == 0f)
        {
            throw new ArgumentException("Can't round to nearest 0.");
        }

        return (float) Math.Ceiling(x / interval) * interval;
    }
}