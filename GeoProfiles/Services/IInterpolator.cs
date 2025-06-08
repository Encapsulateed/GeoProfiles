namespace GeoProfiles.Services;

public interface IInterpolator
{
    double Interpolate(double x);
}

public static class CubicSpline
{
    public static IInterpolator CreatePchip(double[] xs, double[] ys)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);

        int n = xs.Length;
        if (ys.Length != n) throw new ArgumentException("xs.Length != ys.Length");
        if (n < 2) throw new ArgumentException("Нужно как минимум 2 точки для сплайна");

        var del = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            double dx = xs[i + 1] - xs[i];
            if (dx <= 0) throw new ArgumentException("Массив xs должен быть строго возрастающим");
            del[i] = (ys[i + 1] - ys[i]) / dx;
        }

        var m = new double[n];
        m[0] = del[0];
        m[n - 1] = del[n - 2];

        for (int i = 1; i < n - 1; i++)
        {
            if (del[i - 1] * del[i] <= 0)
            {
                m[i] = 0;
            }
            else
            {
                double h1 = 2 * (xs[i + 1] - xs[i]);
                double h2 = 2 * (xs[i] - xs[i - 1]);
                m[i] = (h1 + h2) / (h1 / del[i - 1] + h2 / del[i]);
            }
        }

        return new PchipInterpolator(xs, ys, m);
    }

    private class PchipInterpolator(double[] xs, double[] ys, double[] m) : IInterpolator
    {
        public double Interpolate(double x)
        {
            int idx = Array.BinarySearch(xs, x);
            if (idx < 0) idx = ~idx - 1;
            if (idx < 0) idx = 0;
            if (idx >= xs.Length - 1) idx = xs.Length - 2;

            double x0 = xs[idx], x1 = xs[idx + 1];
            double y0 = ys[idx], y1 = ys[idx + 1];
            double m0 = m[idx], m1 = m[idx + 1];
            double h = x1 - x0;
            double t = (x - x0) / h;
            double t2 = t * t;
            double t3 = t2 * t;

            double h00 = 2 * t3 - 3 * t2 + 1;
            double h10 = t3 - 2 * t2 + t;
            double h01 = -2 * t3 + 3 * t2;
            double h11 = t3 - t2;

            return h00 * y0
                   + h10 * h * m0
                   + h01 * y1
                   + h11 * h * m1;
        }
    }
}