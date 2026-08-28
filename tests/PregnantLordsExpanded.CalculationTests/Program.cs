using System;
using PregnantLordsExpanded.Pregnancy;

namespace PregnantLordsExpanded.CalculationTests
{
    internal static class Program
    {
        private static int Main()
        {
            AssertKnown(100.0, 100.0, 190.0, 0.0, 1, "start of pregnancy");
            AssertKnown(145.0, 100.0, 190.0, 0.5, 5, "exactly fifty percent");
            AssertKnown(190.0, 100.0, 190.0, 1.0, 9, "due time");
            AssertKnown(90.0, 100.0, 190.0, 0.0, 1, "before conception clamps");
            AssertKnown(200.0, 100.0, 190.0, 1.0, 9, "after due time clamps");

            AssertKnown(18.0, 0.0, 36.0, 0.5, 5, "36-day normalized duration");
            AssertKnown(36.0, 0.0, 72.0, 0.5, 5, "72-day normalized duration");

            AssertInvalid(10.0, 20.0, 20.0, "zero duration");
            AssertInvalid(10.0, 20.0, 19.0, "reversed duration");
            AssertInvalid(double.NaN, 0.0, 10.0, "non-finite value");

            Console.WriteLine("All normalized pregnancy progress tests passed.");
            return 0;
        }

        private static void AssertKnown(
            double currentDay,
            double conceptionDay,
            double dueDay,
            double expectedProgress,
            int expectedMonth,
            string name)
        {
            double progress;
            int month;
            string failure;
            bool success = PregnancyProgressCalculator.TryCalculate(
                currentDay,
                conceptionDay,
                dueDay,
                out progress,
                out month,
                out failure);

            if (!success
                || Math.Abs(progress - expectedProgress) > 0.000001
                || month != expectedMonth)
            {
                throw new InvalidOperationException(
                    name + " failed. Success=" + success
                    + ", progress=" + progress
                    + ", month=" + month
                    + ", reason=" + failure);
            }
        }

        private static void AssertInvalid(
            double currentDay,
            double conceptionDay,
            double dueDay,
            string name)
        {
            double progress;
            int month;
            string failure;
            bool success = PregnancyProgressCalculator.TryCalculate(
                currentDay,
                conceptionDay,
                dueDay,
                out progress,
                out month,
                out failure);

            if (success || string.IsNullOrEmpty(failure))
            {
                throw new InvalidOperationException(name + " should fail safely.");
            }
        }
    }
}

