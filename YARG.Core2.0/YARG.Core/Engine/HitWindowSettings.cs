using System;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Engine
{
    public class HitWindowSettings
    {
        /// <summary>
        /// The scale factor of the hit window. This should be used to scale the window
        /// up/down during speed ups and slow downs.
        /// </summary>
        /// <remarks>
        /// This value is <b>NOT</b> serialized as it should be set when first creating the
        /// engine based on the song speed.
        /// </remarks>
        public double Scale;

        /// <summary>
        /// The maximum window size. If the hit window is not dynamic, this value will be used.
        /// </summary>
        public readonly double MaxWindow;

        /// <summary>
        /// The minimum window size. This value will only be used if the window is dynamic.
        /// </summary>
        public readonly double MinWindow;

        /// <summary>
        /// Whether the hit window size can change over time.
        /// This is usually done by looking at the time in between notes.
        /// </summary>
        public readonly bool IsDynamic;

        public readonly double DynamicWindowSlope;

        public readonly double DynamicWindowScale;

        public readonly double DynamicWindowGamma;

        /// <summary>
        /// The front to back ratio of the hit window.
        /// </summary>
        public readonly double FrontToBackRatio;

        private readonly double _minMaxWindowRatio;

        public HitWindowSettings(double maxWindow, double minWindow, double frontToBackRatio, bool isDynamic,
            double dwSlope, double dwScale, double dwGamma)
        {
            // Swap max and min if necessary to ensure that max is always larger than min
            if (maxWindow < minWindow)
            {
                (maxWindow, minWindow) = (minWindow, maxWindow);
            }

            Scale = 1.0;
            MaxWindow = maxWindow;
            MinWindow = minWindow;
            FrontToBackRatio = frontToBackRatio;

            IsDynamic = isDynamic;
            DynamicWindowSlope = Math.Clamp(dwSlope, 0, 1);
            DynamicWindowScale = Math.Clamp(dwScale, 0.3, 3);
            DynamicWindowGamma = Math.Clamp(dwGamma, 0.1, 10);

            _minMaxWindowRatio = MinWindow / MaxWindow;
        }

        public HitWindowSettings(UnmanagedMemoryStream stream, int version)
        {
            Scale = 1;
            MaxWindow = stream.Read<double>(Endianness.Little);
            MinWindow = stream.Read<double>(Endianness.Little);
            FrontToBackRatio = stream.Read<double>(Endianness.Little);
            IsDynamic = stream.ReadBoolean();

            DynamicWindowSlope = stream.Read<double>(Endianness.Little);
            DynamicWindowScale = stream.Read<double>(Endianness.Little);
            DynamicWindowGamma = stream.Read<double>(Endianness.Little);

            _minMaxWindowRatio = MinWindow / MaxWindow;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(MaxWindow);
            writer.Write(MinWindow);
            writer.Write(FrontToBackRatio);
            writer.Write(IsDynamic);

            writer.Write(DynamicWindowSlope);
            writer.Write(DynamicWindowScale);
            writer.Write(DynamicWindowGamma);
        }

        /// <summary>
        /// Calculates the size of the front end of the hit window.
        /// The <see cref="Scale"/> is taken into account.
        /// </summary>
        /// <param name="fullWindow">
        /// The full hit window size. This should be passed in from <see cref="CalculateHitWindow"/>.
        /// </param>
        public double GetFrontEnd(double fullWindow)
        {
            return -(Math.Abs(fullWindow / 2) * FrontToBackRatio) * Scale;
        }

        /// <summary>
        /// Calculates the size of the back end of the hit window.
        /// The <see cref="Scale"/> is taken into account.
        /// </summary>
        /// <param name="fullWindow">
        /// The full hit window size. This should be passed in from <see cref="CalculateHitWindow"/>.
        /// </param>
        public double GetBackEnd(double fullWindow)
        {
            return Math.Abs(fullWindow / 2) * (2 - FrontToBackRatio) * Scale;
        }

        /// <summary>
        /// This method should be used to determine the full hit window size.
        /// This value can then be passed into the <see cref="GetFrontEnd"/>
        /// and <see cref="GetBackEnd"/> methods.
        /// </summary>
        /// <param name="averageTimeDistance">
        /// The average time distance between the notes at this time.
        /// </param>
        /// <returns>
        /// The size of the full hit window.
        /// </returns>
        public double CalculateHitWindow(double averageTimeDistance)
        {
            if (!IsDynamic)
            {
                return MaxWindow;
            }

            return Dark_Yarg_Impl(averageTimeDistance);
        }

        private double Original_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double sqrt = Math.Sqrt(averageTimeDistance + _minMaxWindowRatio);
            double tenth = 0.1 * averageTimeDistance;
            double realSize = tenth * sqrt + MinWindow * 1000;

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);
        }

        private double Second_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double minOverFive = MinWindow / 5 * 1000;

            double sqrt = minOverFive * Math.Sqrt(averageTimeDistance * _minMaxWindowRatio);
            double eighthAverage = 0.125 * averageTimeDistance;
            double realSize = eighthAverage + sqrt + MinWindow * 1000;

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);
        }

        private double Third_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double realSize = Curve(Math.Sqrt(MinWindow * 1000 / 40) * averageTimeDistance) + MinWindow * 1000;

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);

            static double Curve(double x)
            {
                return 0.2 * x + Math.Sqrt(17 * x);
            }
        }

        private double Dark_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double realSize = Curve(averageTimeDistance);

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);

            double Curve(double x)
            {
                double minWindowMillis = MinWindow * 1000;
                double maxWindowMillis = MaxWindow * 1000;

                double maxMultiScale = maxWindowMillis * DynamicWindowScale;

                double gammaPow = Math.Pow(x / maxMultiScale, DynamicWindowGamma);

                double minMultiSlope = minWindowMillis * DynamicWindowSlope;
                double result = gammaPow * (maxWindowMillis - minMultiSlope) + minMultiSlope;

                return result;
            }
        }
    }
}