using System;

namespace PregnantLordsExpanded.Pregnancy
{
    /// <summary>
    /// Pure normalized-progress calculation. It intentionally has no Bannerlord dependency
    /// so the mathematical contract can be tested independently.
    /// </summary>
    public static class PregnancyProgressCalculator
    {
        public static bool TryCalculate(
            double currentDay,
            double conceptionDay,
            double dueDay,
            out double progress,
            out int approximateMonth,
            out string failureReason)
        {
            progress = 0.0;
            approximateMonth = 0;
            failureReason = string.Empty;

            if (!IsFinite(currentDay) || !IsFinite(conceptionDay) || !IsFinite(dueDay))
            {
                failureReason = "Pregnancy timing contains a non-finite value.";
                return false;
            }

            double totalDuration = dueDay - conceptionDay;
            if (totalDuration <= 0.0)
            {
                failureReason = "Pregnancy timing has a zero, negative, or reversed duration.";
                return false;
            }

            double rawProgress = (currentDay - conceptionDay) / totalDuration;
            progress = Clamp(rawProgress, 0.0, 1.0);

            int month = (int)Math.Floor(progress * 9.0) + 1;
            approximateMonth = Clamp(month, 1, 9);
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
