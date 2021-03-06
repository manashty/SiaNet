﻿namespace SiaNet
{
    using CNTK;

    internal class Utility
    {
        internal static Variable CreateParamVar(float value)
        {
            return new Parameter(new int[] { 1 }, DataType.Float, value);
        }
    }

    public class Shape
    {
        public static int[] Create(params int[] values)
        {
            return values;
        }
    }
}
