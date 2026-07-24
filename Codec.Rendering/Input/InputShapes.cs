namespace Codec.Rendering.Input
{
    using System.Numerics;

    public static class InputShapes
    {
        public static T Clamp<T>(T min, T max, T value)
            where T : IFloatingPoint<T> =>
                value < min ? min :
                value > max ? max :
                value;

        public static T Signed<T>(T value)
            where T : IFloatingPoint<T> =>
                value + value - T.One;

        public static T DeadZone<T>(T deadZone, T max, T value)
            where T : IFloatingPoint<T>
        {
            value = T.Abs(value);
            return
                value < deadZone ? T.Zero :
                value >= max ? max / value :
                (value - deadZone) / ((max - deadZone) * value);
        }
    }
}
